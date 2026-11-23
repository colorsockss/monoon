using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;

namespace Engine.Core;

// Marks a test method in a project's Tests/ folder: void Name(GameTestRun run) or IEnumerable<ScriptWait> Name(GameTestRun run).
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class GameTestAttribute(string scene) : Attribute
{
    public string Scene { get; } = scene;
    public int MaxFrames { get; init; } = 3600;
}

public sealed class GameTestFailure(string message) : Exception(message);

// One headless Play session driven at 60 frames per second. Input goes through the same router as a recorded replay.
public sealed class GameTestRun
{
    private readonly GameSession game;
    private readonly GameLoop loop;
    private readonly GameInputRouter router;
    private readonly Guid? project;
    private readonly string root;
    private readonly long origin = Stopwatch.GetTimestamp();
    private readonly List<(int Frame, ReplayInput Input)> queued = [];
    private readonly Dictionary<int, double> recordedFrames = [];

    internal GameTestRun(GameSession game, string root, Guid? project)
    {
        this.game = game;
        this.root = root;
        this.project = project;
        loop = new GameLoop(game, game.Actions);
        router = new GameInputRouter(game.Actions, () => game.InputUi, game.LogicalSize, () => game.InputUi == null ? null : game.Current);
    }

    /// <summary>Frames advanced so far. Each frame is 1/60 s of UI time and, unless paused, one game tick.</summary>
    public int Frame { get; private set; }
    public SessionState Session => game.State;
    public GameTime Time => game.Time;
    public SceneGame World => game.Current;
    public IGameObjects Objects => game.Current;
    public Guid? SceneId => game.SceneId;

    public Guid Find(string name) =>
        World.Objects.FirstOrDefault(o => o.Name == name && World.IsActive(o.Id))?.Id ??
        World.Objects.FirstOrDefault(o => o.Name == name)?.Id ?? throw new GameTestFailure($"No object named '{name}' in the current scene.");
    public T Script<T>(string objectName) where T : class =>
        Objects.GetComponent<T>(Find(objectName)) ?? throw new GameTestFailure($"'{objectName}' has no {typeof(T).Name}.");
    public Vector2 PositionOf(string name) =>
        Objects.TryGetPosition(Find(name), out var position) ? position : throw new GameTestFailure($"'{name}' has no position.");

    public void Press(string control) => Queue(Frame, Key(control, true));
    public void Release(string control) => Queue(Frame, Key(control, false));
    public void Tap(string control) { Press(control); Queue(Frame + 1, Key(control, false)); }
    public void Click(float x, float y)
    {
        Queue(Frame, new(ReplayInputKind.Pointer, X: x, Y: y));
        Queue(Frame, new(ReplayInputKind.Pointer, "Mouse.Left", true, X: x, Y: y));
        Queue(Frame + 1, new(ReplayInputKind.Pointer, "Mouse.Left", false, X: x, Y: y));
    }

    /// <summary>Plays a recorded .inputreplay (path relative to the project) from the next frame; yield the result to wait for its end.</summary>
    public ScriptWait Replay(string path)
    {
        var replay = InputReplay.Load(Path.Combine(root, path));
        if (project is Guid id && replay.Project != id) throw new GameTestFailure($"{path} was recorded for another project.");
        int start = Frame;
        foreach (var e in replay.Events) Queue(start + e.Frame, e.Input);
        for (int i = 0; i < replay.FrameTicks.Length; i++) recordedFrames[start + i] = TimeSpan.FromTicks(replay.FrameTicks[i]).TotalSeconds;
        int end = start + replay.FrameTicks.Length;
        return ScriptWait.Until(() => Frame >= end);
    }

    public void Assert(bool condition, string message)
    {
        if (!condition) throw new GameTestFailure(message);
    }

    public void AreEqual<T>(T expected, T actual, string what)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new GameTestFailure($"{what}: expected {expected}, got {actual}.");
    }

    private static ReplayInput Key(string control, bool down) => InputControls.Supported(control)
        ? new(ReplayInputKind.Key, control, down) : throw new ArgumentException($"Unknown control '{control}'.");

    private void Queue(int frame, ReplayInput input) => queued.Add((frame, input));

    internal void Start() => loop.Advance(0); // Runs OnCreate/OnEnable before the test body, without a game tick.

    // 1-based number of the frame being run, or of the last finished frame when the test body itself fails.
    internal int FailureFrame => advancing ? Frame + 1 : Frame;
    private bool advancing;

    internal void Advance()
    {
        advancing = true;
        long timestamp = origin + (long)(Frame * GameTime.FixedStep * Stopwatch.Frequency);
        foreach (var (_, input) in queued.Where(q => q.Frame == Frame)) input.Dispatch(router, loop, timestamp);
        queued.RemoveAll(q => q.Frame == Frame);
        game.WaitForSceneRead();
        loop.Advance(recordedFrames.Remove(Frame, out var seconds) ? seconds : GameTime.FixedStep);
        game.Current.SetPointerFrame(loop.Interpolation);
        Frame++;
        advancing = false;
    }
}

public static class ProjectTests
{
    private static readonly Regex ProjectFrame = new(@" in (?![A-Za-z]:[\\/]|/)", RegexOptions.CultureInvariant);

    /// <summary>Compiles Scripts/ with Tests/, runs every [GameTest] and returns the process exit code.</summary>
    public static int Run(string projectPath, TextWriter output)
    {
        var root = Path.GetFullPath(projectPath);
        if (!File.Exists(Path.Combine(root, GameSettings.FileName)))
            throw new InvalidDataException($"Expected a source project containing {GameSettings.FileName} at {root}.");
        var settings = GameSettings.Load(root);
        var build = ProjectScripts.ForTests(root).Current();
        var context = new AssemblyLoadContext("Project tests", isCollectible: true);
        try
        {
            using var bytes = new MemoryStream(build.Bytes, writable: false);
            using var symbols = new MemoryStream(build.Symbols, writable: false);
            var assembly = context.LoadFromStream(bytes, symbols);
            var factory = assembly.GetType(ScriptLibrary.FactoryName, throwOnError: true)!.GetMethod("Create")!
                .CreateDelegate<Func<ScriptComponent, GameBehaviour>>();
            var tests = assembly.GetTypes().Where(t => t.IsPublic && t.IsClass && !t.IsAbstract).OrderBy(t => t.FullName, StringComparer.Ordinal)
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => m.GetCustomAttribute<GameTestAttribute>() != null).OrderBy(m => m.MetadataToken))
                .ToArray();
            if (tests.Length == 0)
            {
                output.WriteLine($"No [GameTest] methods in {ProjectScripts.TestsFolder}/ of {root}.");
                return 1;
            }
            int failed = 0;
            foreach (var method in tests)
            {
                var attribute = method.GetCustomAttribute<GameTestAttribute>()!;
                string name = $"{method.DeclaringType!.Name}.{method.Name}";
                var failure = RunOne(root, settings, factory, method, attribute);
                if (failure == null) { output.WriteLine($"PASS {name}"); continue; }
                failed++;
                var (frame, tick, error, log) = failure.Value;
                output.WriteLine($"FAIL {name} ({attribute.Scene}) at frame {frame}, tick {tick}");
                output.WriteLine($"  {error.GetType().Name}: {error.Message}");
                foreach (var line in (error.GetBaseException().StackTrace ?? "").Split('\n').Where(l => ProjectFrame.IsMatch(l)).Take(4))
                    output.WriteLine("    " + line.Trim());
                foreach (var entry in log.TakeLast(5)) output.WriteLine($"  log: {entry.Message}");
            }
            output.WriteLine($"{tests.Length - failed} passed, {failed} failed.");
            return failed == 0 ? 0 : 1;
        }
        finally { context.Unload(); }
    }

    private static (int Frame, long Tick, Exception Error, List<GameLogEntry> Log)? RunOne(string root, GameSettings settings,
        Func<ScriptComponent, GameBehaviour> factory, MethodInfo method, GameTestAttribute attribute)
    {
        if (method.GetParameters() is not [{ ParameterType: var parameter }] || parameter != typeof(GameTestRun) ||
            method.ReturnType != typeof(void) && method.ReturnType != typeof(IEnumerable<ScriptWait>))
            return (0, 0, new GameTestFailure("Declare public void or IEnumerable<ScriptWait> Name(GameTestRun run)."), []);
        // Every test starts from an empty player profile so saved settings and slots never leak between runs.
        var profile = Directory.CreateTempSubdirectory("monoon-test-");
        var previous = Environment.GetEnvironmentVariable("MONOON_USER_DATA");
        Environment.SetEnvironmentVariable("MONOON_USER_DATA", profile.FullName);
        GameSession? game = null;
        GameTestRun? run = null;
        IEnumerator<ScriptWait>? steps = null;
        var log = new List<GameLogEntry>(); // Game logs are shown only when the test fails.
        try
        {
            game = new GameSession(SceneDocument.Load(Path.Combine(root, attribute.Scene)), null, factory);
            string? loadError = null;
            game.LoadFailed += message => loadError ??= message;
            game.State.Logged += log.Add;
            run = new GameTestRun(game, root, settings.ProjectId);
            var instance = Activator.CreateInstance(method.DeclaringType!)!;
            run.Start();
            object? body;
            try { body = method.Invoke(instance, [run]); }
            catch (TargetInvocationException ex) { throw ex.InnerException!; }
            steps = (body as IEnumerable<ScriptWait>)?.GetEnumerator();
            var pending = new PendingWait();
            bool more = steps?.MoveNext() == true;
            if (more) pending.Begin(steps!.Current, game.Time);
            while (more)
            {
                if (run.Frame >= attribute.MaxFrames)
                    throw new GameTestFailure($"Timed out after {attribute.MaxFrames} frames waiting for {pending.Wait}.");
                run.Advance();
                if (loadError != null) throw new GameTestFailure("Scene load failed: " + loadError);
                if (!pending.Ready(game.Time)) continue;
                more = steps!.MoveNext();
                if (more) pending.Begin(steps.Current, game.Time);
            }
            return null;
        }
        catch (Exception ex) { return (run?.FailureFrame ?? 0, game?.Time.TickCount ?? 0, ex, log); }
        finally
        {
            try { steps?.Dispose(); game?.Dispose(); }
            finally
            {
                Environment.SetEnvironmentVariable("MONOON_USER_DATA", previous);
                profile.Delete(recursive: true);
            }
        }
    }
}
