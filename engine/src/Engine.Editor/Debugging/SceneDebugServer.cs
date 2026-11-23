using System.Collections.Frozen;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Engine.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Engine.Editor;

// A loopback Debug Adapter Protocol session scoped to one editor and project.
internal sealed class SceneDebugServer : IDisposable
{
    public string Root { get; }
    public string Workspace => Path.Combine(Root, ".monoon", "ide", $"ScenePlay.{Environment.ProcessId}.code-workspace");
    public string Token { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource lifetime = new();
    private readonly object gate = new();
    private readonly Dictionary<string, HashSet<int>> executable = [];
    private volatile FrozenSet<(string Path, int Line)> breaks = FrozenSet<(string,int)>.Empty;
    private readonly Dictionary<string, int[]> requested = [];
    private NetworkStream? client;
    private int seq;
    private bool attached;
    private long pausedTicks;
    public long TakePausedTicks() => Interlocked.Exchange(ref pausedTicks, 0);
    private (string File, int Line, string Method, SceneDebugValues Values)? stopped;

    public SceneDebugServer(string project)
    {
        Root = Path.GetFullPath(project);
        listener.Start();
        SceneScriptBreakpoint.Filter = IsSet;
        SceneScriptBreakpoint.Handler = Hit;
        _ = Accept(lifetime.Token);
    }

    public CSharpCompilation Instrument(CSharpCompilation compilation)
    {
        var result = compilation;
        var locations = new Dictionary<string, HashSet<int>>();
        foreach (var tree in compilation.SyntaxTrees)
        {
            var lines = new HashSet<int>();
            var rewritten = new SceneDebugInstrumentation(compilation.GetSemanticModel(tree), lines).Visit(tree.GetRoot())!;
            result = result.ReplaceSyntaxTree(tree, tree.WithRootAndOptions(rewritten,tree.Options));
            locations[tree.FilePath] = lines;
        }
        lock (gate)
        {
            executable.Clear();
            foreach (var entry in locations) executable.Add(entry.Key, entry.Value);
            if (attached)
                foreach (var (file, lines) in requested.ToArray())
                    foreach (int line in lines)
                        Event("breakpoint", new { reason = "changed", breakpoint = Breakpoint(file,line) });
        }
        return result;
    }

    private bool IsSet(string file, int line) => breaks.Contains((file,line));

    private void Hit(string file, int line, string method, string[] names, object?[] values)
    {
        lock (gate)
        {
            if (!attached || !IsSet(file,line) || stopped != null) return;
            stopped = (file, line, method, new(names,values));
            long before = System.Diagnostics.Stopwatch.GetTimestamp();
            Event("stopped", new { reason = "breakpoint", threadId = 1, allThreadsStopped = true });
            while (stopped != null && attached) Monitor.Wait(gate);
            Interlocked.Add(ref pausedTicks, System.Diagnostics.Stopwatch.GetTimestamp() - before);
        }
    }

    private async Task Accept(CancellationToken cancellation)
    {
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                using var socket = await listener.AcceptTcpClientAsync(cancellation).ConfigureAwait(false);
                using var stream = socket.GetStream();
                lock (gate) { client = stream; seq = 0; }
                try
                {
                    while (!cancellation.IsCancellationRequested)
                    {
                        // Drop a stalled, unauthenticated connection so the IDE can reconnect.
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                        lock (gate) { if (!attached) timeout.CancelAfter(TimeSpan.FromSeconds(10)); }
                        using var message = await Read(stream, timeout.Token).ConfigureAwait(false);
                        if (message == null || !Dispatch(message.RootElement)) break;
                    }
                }
                catch (Exception ex) when (ex is IOException or SocketException or JsonException or InvalidDataException or OperationCanceledException or ObjectDisposedException or KeyNotFoundException or InvalidOperationException or FormatException) { }
                finally { lock (gate) { Detach(); client = null; } }
            }
        }
        catch (Exception ex) when (cancellation.IsCancellationRequested && ex is OperationCanceledException or SocketException or ObjectDisposedException) { }
    }

    private static async Task<JsonDocument?> Read(NetworkStream stream, CancellationToken token)
    {
        var header = new List<byte>();
        byte[] b = new byte[1];
        while (header.Count < 1024)
        {
            if (await stream.ReadAsync(b,token).ConfigureAwait(false) == 0) return null;
            header.Add(b[0]);
            if (header.Count >= 4 && header[^4] == 13 && header[^3] == 10 && header[^2] == 13 && header[^1] == 10) break;
        }
        string? length = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n")
            .FirstOrDefault(h => h.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase));
        if (length == null || !int.TryParse(length[15..],out int size) || size is <= 0 or > 1024 * 1024)
            throw new InvalidDataException("Invalid debug message length.");
        byte[] body = new byte[size];
        await stream.ReadExactlyAsync(body,token).ConfigureAwait(false);
        return JsonDocument.Parse(body);
    }

    private void Send(object message)
    {
        if (client == null) return;
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(message);
            client.WriteTimeout = 2000;
            client.Write(Encoding.ASCII.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n"));
            client.Write(bytes);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException) { Detach(); client?.Close(); }
    }

    private void Event(string name, object body) => Send(new { seq = ++seq, type = "event", @event = name, body });

    private static int Integer(JsonElement args, string name) => args.GetProperty(name).GetInt32();
    private static string String(JsonElement args, string name) => args.GetProperty(name).GetString() ?? "";
    private object Breakpoint(string file, int line)
    {
        bool bound = executable.TryGetValue(file, out var valid) && valid.Contains(line);
        return new { id = BreakpointId(file,line), verified = bound, line, source = new { path = file, name = Path.GetFileName(file) },
            message = bound ? null : executable.ContainsKey(file) ? "Choose the start of a statement inside a method block." : "Waiting for Scene Play to compile this script." };
    }

    private readonly Dictionary<(string,int),int> ids = [];
    private int BreakpointId(string file,int line)
    {
        if (!ids.TryGetValue((file,line), out int id)) ids[(file,line)] = id = ids.Count + 1;
        return id;
    }

    private object SetBreakpoints(JsonElement args)
    {
        string path = Path.GetFullPath(String(args.GetProperty("source"u8),"path"));
        int[] lines = args.TryGetProperty("breakpoints"u8,out var points) ? points.EnumerateArray().Select(p => Integer(p,"line")).Distinct().ToArray() : [];
        requested[path] = lines;
        breaks = requested.SelectMany(entry => entry.Value.Select(line => (entry.Key,line))).ToFrozenSet();
        return new { breakpoints = lines.Select(line => Breakpoint(path,line)).ToArray() };
    }

    private bool Dispatch(JsonElement request)
    {
        lock (gate)
        {
            int id = Integer(request,"seq");
            string command = String(request,"command");
            request.TryGetProperty("arguments"u8, out var args);
            void Reply(object? body = null, string? error = null) => Send(new { seq = ++seq, type = "response",
                request_seq = id, command, success = error == null, body, message = error });
            try
            {
                if (!attached && command is not ("initialize" or "attach" or "disconnect"))
                { Reply(error:"Attach to the editor first."); return false; }
                switch (command)
                {
                    case "initialize":
                        Reply(new { supportsConfigurationDoneRequest = true, supportsEvaluateForHovers = true }); break;
                    case "attach":
                        if (attached || !args.TryGetProperty("monoonSession"u8, out var key) || key.GetString() != Token)
                        { Reply(error:"Open the C# project from the running MoNoon editor."); return false; }
                        attached = true; Reply(); Event("initialized", new {}); break;
                    case "setBreakpoints": Reply(SetBreakpoints(args)); break;
                    case "setExceptionBreakpoints": Reply(new { breakpoints = Array.Empty<object>() }); break;
                    case "configurationDone": Reply(); break;
                    case "threads": Reply(new { threads = new[] { new { id = 1, name = "Scene Play" } } }); break;
                    case "stackTrace" when stopped is {} frame:
                        Reply(new { stackFrames = new[] { new { id = 1, name = frame.Method, line = frame.Line, column = 1,
                            source = new { path = frame.File, name = Path.GetFileName(frame.File) } } }, totalFrames = 1 }); break;
                    case "scopes" when stopped != null: Reply(new { scopes = new[] { new { name = "Locals", variablesReference = 1, expensive = false } } }); break;
                    case "variables" when stopped is {} viewed: Reply(new { variables = viewed.Values.Variables(Integer(args,"variablesReference")) }); break;
                    case "evaluate" when stopped is {} inspected: Reply(inspected.Values.Evaluate(String(args,"expression"))); break;
                    case "continue" when stopped != null:
                        Reply(new { allThreadsContinued = true }); Event("continued", new { threadId = 1, allThreadsContinued = true });
                        stopped = null; Monitor.PulseAll(gate); break;
                    case "disconnect": Reply(); Event("terminated",new {}); return false;
                    default: Reply(error:"The current Scene Play session does not support this request."); break;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException or InvalidOperationException or InvalidDataException or FormatException)
            { Reply(error:ex.Message); }
            return true;
        }
    }

    public void EndPlay()
    {
        lock (gate)
        {
            executable.Clear();
            foreach (var (file, lines) in requested.ToArray())
                foreach (int line in lines)
                    Event("breakpoint", new { reason = "changed", breakpoint = Breakpoint(file,line) });
        }
    }

    private void Detach()
    {
        attached = false; stopped = null; requested.Clear(); breaks = FrozenSet<(string,int)>.Empty;
        ids.Clear(); Monitor.PulseAll(gate);
    }

    public void Dispose()
    {
        SceneScriptBreakpoint.Filter = null;
        SceneScriptBreakpoint.Handler = null;
        lifetime.Cancel();
        listener.Stop();
        lock (gate) { Detach(); client?.Close(); }
        // Only remove the connection file owned by this process.
        if (File.Exists(Workspace)) File.Delete(Workspace);
        lifetime.Dispose();
    }
}
