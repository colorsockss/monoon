using System.Diagnostics;
using System.Globalization;

namespace Engine.Core;

/// <summary>
/// Call counts and inclusive/self times of engine work in each frame, nested by caller.
/// Only the editor's Scene Play and a development-profile player install one. Without it, a sample is a null check.
/// </summary>
public sealed class FrameProfiler
{
    public const int Frame = 0, UpdateUi = 1, Simulate = 2, Lifecycle = 3, UiInput = 4, UiLayout = 5, UiDraw = 6,
        Move = 7, Rigidbodies = 8, Contacts = 9, Overlap = 10, Raycast = 11, Sweep = 12, Animation = 13, Effects = 14,
        Cameras = 15, Audio = 16, TileStreaming = 17, SceneLoading = 18, Render = 19, RenderWorld = 20, Present = 21, Write = 22, Editor = 23;
    private static readonly List<string> names = ["Frame", "UpdateUi", "Simulate", "Lifecycle", "UI.Input", "UI.Layout", "UI.Draw",
        "Physics.Move", "Physics.Rigidbodies", "Physics.Contacts", "Physics.Overlap", "Physics.Raycast", "Physics.Sweep", "Animation", "Effects",
        "Cameras", "Audio", "Tiles.Streaming", "Scene.Loading", "Render", "Render.World", "Present", "Profiler.Write", "Editor"];
    private static readonly Dictionary<string, int> ids = names.Select((n, i) => (n, i)).ToDictionary(p => p.n, p => p.i, StringComparer.Ordinal);

    public static int Marker(string name)
    {
        lock (names)
        {
            if (ids.TryGetValue(name, out int id)) return id;
            ids[name] = id = names.Count;
            names.Add(name);
            return id;
        }
    }
    public static string NameOf(int marker) { lock (names) return names[marker]; }

    public const int MaxNodes = 256, MaxDepth = 32;
    public static FrameProfiler? Current { get; private set; }
    private readonly ProfiledFrame[] frames;
    private readonly int[] stack = new int[MaxDepth];
    private readonly long[] started = new long[MaxDepth];
    private readonly Dictionary<(Type, string), int> scriptMarkers = [];
    private ProfiledFrame open;
    private int head, depth, skipped, thread, frameGen0;
    private long frameStart, frameBytes;
    public int Capacity => frames.Length - 1;
    public int Count { get; private set; }
    /// <summary>Completed frames since the last <see cref="Clear"/>. The newest frame carries this number.</summary>
    public long Sequence { get; private set; }
    /// <summary>Runs after a frame completes, inside the next frame's Profiler.Write sample.</summary>
    public event Action<ProfiledFrame>? Completed;

    public FrameProfiler(int capacity = 300)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        frames = new ProfiledFrame[capacity + 1]; // One more slot for the frame in progress.
        for (int i = 0; i < frames.Length; i++) frames[i] = new();
        open = frames[0];
        thread = Environment.CurrentManagedThreadId;
        Restart();
    }

    /// <summary>Samples taken on the calling thread go to this profiler. Other threads are ignored.</summary>
    public void Install()
    {
        thread = Environment.CurrentManagedThreadId;
        Restart();
        Current = this;
    }
    public void Uninstall() { if (Current == this) Current = null; }

    /// <summary>Oldest first.</summary>
    public ProfiledFrame At(int index)
    {
        if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
        return frames[(head - Count + index + frames.Length) % frames.Length];
    }
    public ProfiledFrame? Find(long sequence) => sequence > Sequence - Count && sequence <= Sequence ? At(Count - 1 - (int)(Sequence - sequence)) : null;

    public void Clear() { Count = head = 0; Sequence = 0; Restart(); }

    /// <summary>Discards the frame in progress, e.g. after the window was inactive.</summary>
    public void Restart()
    {
        depth = skipped = 0;
        open = frames[head];
        open.Reset();
        frameStart = Stopwatch.GetTimestamp();
        frameBytes = GC.GetAllocatedBytesForCurrentThread();
        frameGen0 = GC.CollectionCount(0);
    }

    /// <summary>Closes the frame in progress at the top level of the loop and starts the next one.</summary>
    public void EndFrame()
    {
        if (depth != 0 || skipped != 0) throw new InvalidOperationException("A profiler sample is still open at the end of the frame.");
        long now = Stopwatch.GetTimestamp();
        var done = open;
        done.Ticks[0] = now - frameStart;
        done.Counts[0] = 1;
        done.AllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - frameBytes;
        done.Gen0 = GC.CollectionCount(0) - frameGen0;
        done.Sequence = ++Sequence;
        head = (head + 1) % frames.Length;
        Count = Math.Min(Count + 1, Capacity);
        open = frames[head];
        open.Reset();
        frameStart = now;
        frameBytes = GC.GetAllocatedBytesForCurrentThread();
        frameGen0 = GC.CollectionCount(0);
        if (Completed is {} completed)
            using (Begin(Write)) completed(done);
    }

    public static Scope Sample(int marker) => Current is {} profiler ? profiler.Begin(marker) : default;
    public static Scope Script(GameBehaviour behaviour, string method) =>
        Current is {} profiler ? profiler.Begin(profiler.ScriptMarker(behaviour.GetType(), method)) : default;

    private int ScriptMarker(Type type, string method)
    {
        if (!scriptMarkers.TryGetValue((type, method), out int id))
            scriptMarkers[(type, method)] = id = Marker(type.Name + "." + method);
        return id;
    }

    private Scope Begin(int marker)
    {
        if (Environment.CurrentManagedThreadId != thread) return default;
        var f = open;
        if (skipped > 0 || depth == MaxDepth) { skipped++; f.Dropped++; return new(this); }
        int parent = depth == 0 ? 0 : stack[depth - 1];
        int node = f.FirstChild[parent];
        while (node >= 0 && f.Markers[node] != marker) node = f.Next[node];
        if (node < 0)
        {
            if (f.NodeCount == MaxNodes) { skipped++; f.Dropped++; return new(this); }
            node = f.NodeCount++;
            f.Markers[node] = marker; f.Parents[node] = parent;
            f.FirstChild[node] = f.LastChild[node] = f.Next[node] = -1;
            f.Counts[node] = 0; f.Ticks[node] = 0;
            if (f.LastChild[parent] < 0) f.FirstChild[parent] = node; else f.Next[f.LastChild[parent]] = node;
            f.LastChild[parent] = node;
        }
        stack[depth] = node;
        started[depth++] = Stopwatch.GetTimestamp();
        return new(this);
    }

    private void End()
    {
        long now = Stopwatch.GetTimestamp();
        if (skipped > 0) { skipped--; return; }
        if (depth == 0) return; // Restarted while the sample was open.
        int node = stack[--depth];
        open.Ticks[node] += now - started[depth];
        open.Counts[node]++;
    }

    public readonly struct Scope : IDisposable
    {
        private readonly FrameProfiler? owner;
        internal Scope(FrameProfiler owner) => this.owner = owner;
        public void Dispose() => owner?.End();
    }
}

/// <summary>One frame's call tree. Node 0 is the whole frame; children keep their first-call order.</summary>
public sealed class ProfiledFrame
{
    internal readonly int[] Markers = new int[FrameProfiler.MaxNodes], Parents = new int[FrameProfiler.MaxNodes],
        FirstChild = new int[FrameProfiler.MaxNodes], LastChild = new int[FrameProfiler.MaxNodes], Next = new int[FrameProfiler.MaxNodes],
        Counts = new int[FrameProfiler.MaxNodes];
    internal readonly long[] Ticks = new long[FrameProfiler.MaxNodes];
    public long Sequence { get; internal set; }
    public int NodeCount { get; internal set; } = 1;
    public long AllocatedBytes { get; internal set; }
    public int Gen0 { get; internal set; }
    /// <summary>Samples not recorded because the tree was full or too deep.</summary>
    public int Dropped { get; internal set; }
    public double Milliseconds => InclusiveMilliseconds(0);

    internal void Reset()
    {
        NodeCount = 1; Dropped = 0; Sequence = 0; AllocatedBytes = 0; Gen0 = 0;
        Markers[0] = FrameProfiler.Frame; Parents[0] = -1; FirstChild[0] = LastChild[0] = Next[0] = -1; Counts[0] = 0; Ticks[0] = 0;
    }

    public int Marker(int node) => Markers[Check(node)];
    public string Name(int node) => FrameProfiler.NameOf(Markers[Check(node)]);
    public int Parent(int node) => Parents[Check(node)];
    public int FirstChildOf(int node) => FirstChild[Check(node)];
    public int NextSibling(int node) => Next[Check(node)];
    public int Calls(int node) => Counts[Check(node)];
    public double InclusiveMilliseconds(int node) => Ticks[Check(node)] * 1000d / Stopwatch.Frequency;
    public double SelfMilliseconds(int node)
    {
        long self = Ticks[Check(node)];
        for (int c = FirstChild[node]; c >= 0; c = Next[c]) self -= Ticks[c];
        return Math.Max(0, self) * 1000d / Stopwatch.Frequency;
    }
    public int Depth(int node)
    {
        int depth = 0;
        for (int p = Parents[Check(node)]; p >= 0; p = Parents[p]) depth++;
        return depth;
    }

    /// <summary>Calls and time of a marker anywhere in the tree. Time counts only the outermost of nested same-marker samples.</summary>
    public (int Calls, double Milliseconds) Total(int marker)
    {
        int calls = 0; long ticks = 0;
        for (int n = 0; n < NodeCount; n++)
        {
            if (Markers[n] != marker) continue;
            calls += Counts[n];
            bool nested = false;
            for (int p = Parents[n]; p >= 0 && !nested; p = Parents[p]) nested = Markers[p] == marker;
            if (!nested) ticks += Ticks[n];
        }
        return (calls, ticks * 1000d / Stopwatch.Frequency);
    }
    public (int Calls, double Milliseconds) Total(string name) => Total(FrameProfiler.Marker(name));

    private int Check(int node) => (uint)node < (uint)NodeCount ? node : throw new ArgumentOutOfRangeException(nameof(node));
}
