using System.Drawing;
using System.Numerics;

namespace Engine.Core;

public readonly record struct SceneReference(Guid Id);

public sealed partial class SessionState
{
    public event Action<GameLogEntry>? Logged;
    public void Log(string message,GameLogLevel level=GameLogLevel.Info,
        [System.Runtime.CompilerServices.CallerFilePath] string? source=null,[System.Runtime.CompilerServices.CallerLineNumber] int line=0)
    {
        if(Logged is {} receive)receive(new(level,message,source,line));
        else if(level==GameLogLevel.Error) Console.Error.WriteLine(message); else Console.WriteLine(message);
    }
    public PlayerStorage Storage { get; internal set; } = PlayerStorage.Unavailable;
    public Guid? SceneId { get; internal set; }
    public GameTime Time { get; } = new();
    internal UiPreferences? UiPreferences;
    internal AudioMixer Mixer { get; }=new();
    private InputActions? input;
    internal InputActions UseInput(InputBindings bindings) => input ??= new(bindings);
    public int InputRevision => input?.Revision ?? 0;
    public string? BindingFor(string action, InputDevice device) => input?.BindingFor(action,device);
    public void RebindInput(string action, params string[] controls) =>
        (input ?? throw new InvalidOperationException("Start a game before rebinding.")).Rebind(action,controls);
    private readonly HashSet<string> flags = new(StringComparer.Ordinal);
    public bool Has(string flag) => flags.Contains(flag);
    public bool Set(string flag) => flags.Add(flag);
    public bool Clear(string flag) => flags.Remove(flag);
    private readonly HashSet<Guid> addedScenes = [];
    public bool IsSceneLoaded(Guid id) => SceneId == id || addedScenes.Contains(id);
    internal void Added(Guid id) => addedScenes.Add(id);
    internal void Removed(Guid id) => addedScenes.Remove(id);
    internal void Replaced(Guid id) { SceneId = id; addedScenes.Clear(); }

    internal SceneRequest? Requested;
    public SceneOperation? PendingScene { get; internal set; }
    public void LoadScene(Guid assetId) => LoadScene(assetId, null);
    public void LoadScene(Guid assetId, Action<SessionState>? onEntered)
    {
        PendingScene?.Cancel();
        Requested = new(assetId, SceneChange.Replace, null, onEntered);
    }
    public SceneOperation LoadSceneAsync(Guid id, string? transitionText = "Loading…", double minimumSeconds = 0,
        Action<SessionState>? onEntered = null) => Request(id, SceneChange.Replace, transitionText, minimumSeconds, onEntered);
    public SceneOperation AddSceneAsync(Guid id) => Request(id, SceneChange.Add, null, 0, null);
    public SceneOperation UnloadScene(Guid id) => Request(id, SceneChange.Remove, null, 0, null);

    private SceneOperation Request(Guid id, SceneChange change, string? label, double minimum, Action<SessionState>? entered)
    {
        if (!double.IsFinite(minimum) || minimum < 0) throw new ArgumentOutOfRangeException(nameof(minimum));
        var op = new SceneOperation(id, change, label, minimum);
        if (PendingScene is { IsDone: false } || Requested is { Operation: null })
            op.Fail("Another scene change is in progress.");
        else
        {
            PendingScene = op;
            Requested = new(id, change, op, entered);
        }
        return op;
    }

}

public sealed partial class GameSession : IGame2D, IDisposable
{
    public SessionState State { get; } = new();
    private readonly string? project;
    private readonly ScriptRuntime? sharedScripts;
    private readonly Func<ScriptComponent, GameBehaviour>? compiledScripts;
    public SceneGame Current { get; private set; }
    public Guid? SceneId { get; private set; }
    private Action<SceneGame>? prepare;
    public void UseRenderer(Action<SceneGame> prepareAssets)
    {
        prepareAssets(Current);
        prepare = prepareAssets;
    }
    public void UseResources(SceneResources sceneResources)
    {
        if (resources != null) throw new InvalidOperationException("Scene resources are already attached.");
        resources = sceneResources;
        try { SyncResources(); } catch { resources.Dispose(); resources = null; throw; }
    }
    public event Action<string>? LoadFailed;
    public event Action<string>? SoundRequested;
    private void Sound(string path) => SoundRequested?.Invoke(path);
    public GameTime Time => State.Time;
    public InputActions Actions => Current.Actions;
    public InputBindings Bindings => Current.Bindings;
    public Size LogicalSize => Current.LogicalSize;
    public AudioMixer Mixer=>State.Mixer;

    public GameSession(SceneDocument scene, ProjectScripts? scripts = null) : this(scene, scripts, null) { }

    public GameSession(SceneDocument scene, ProjectScripts? scripts, Func<ScriptComponent, GameBehaviour>? compiledScripts, PlayerStorage? storage = null)
    {
        this.compiledScripts = compiledScripts;
        project = scene.ProjectDirectory;
        manifest = project == null ? null : ProjectAssets.Scan(project);
        scene.CheckReferences(manifest);
        var gameId = scene.Settings.ProjectId ?? scene.Settings.StartScene ?? scene.AssetId ?? Guid.Empty;
        bool editor = project == null || !File.Exists(Path.Combine(project, ".monoon-player"));
        State.Mixer.Levels=scene.Settings.Mixer;
        State.Storage = storage ?? PlayerStorage.ForGame(gameId, editor);
        State.SceneId = scene.AssetId;
        if (compiledScripts == null && System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported && project != null)
            sharedScripts = new ScriptRuntime(scripts ?? new ProjectScripts(project));
        try { Current = new SceneGame(scene, scripts, State, compiledScripts,sharedScripts,manifest); }
        catch { sharedScripts?.Dispose(); throw; }
        State.LiveWorld = Current;
        Current.SoundRequested += Sound;
        Current.AssetsChanged += OnAssetsChanged;
        Current.PreparingBodies += PrepareBodies;
        SceneId = scene.AssetId;
    }

    public void FixedUpdate(Vector2 move, float seconds) => FixedUpdate(new GameInput(move, false), seconds);
    public void FixedUpdate(GameInput input, float seconds)
    {
        // Preserve the combined update boundary for callers stepping the game directly.
        if (!Blocking) Current.FixedUpdate(input, seconds);
        else Time.AdvanceUi(seconds);
        PumpScenes(seconds);
    }

    public void UpdateUi(GameInput input, double seconds)
    {
        if (!Blocking) Current.UpdateUi(input, seconds);
        else Time.AdvanceUi(seconds);
        PumpScenes(seconds);
    }

    public void Simulate(GameInput input, float seconds)
    {
        if (!Blocking) Current.Simulate(input, seconds);
        if (State.Requested != null) PumpScenes(0);
    }

    public void Draw(ICanvas2D canvas, float interpolation)
    {
        if (loading?.Operation is { TransitionText: { } label, Change: SceneChange.Replace } op)
        {
            canvas.UseFonts(Current.Ui.Language.Fonts);
            canvas.Clear(SceneGame.Background);
            float width = Math.Min(240, LogicalSize.Width * .6f);
            float x = (LogicalSize.Width - width) / 2, y = LogicalSize.Height * .5f;
            canvas.DrawText(label, x, y - 28, 18, Color.White);
            canvas.FillRectangle(new(x,y,width,4), Color.FromArgb(59,83,107));
            canvas.FillRectangle(new(x,y,width * op.Progress,4), Color.LightSkyBlue);
        }
        else Current.Draw(canvas, interpolation);
    }

    public void Dispose()
    {
        State.PendingScene?.Cancel();
        State.PendingScene = null;
        State.Requested = null;
        DiscardLoading();
        try { Current.Dispose(); }
        finally { resources?.Dispose(); sharedScripts?.Dispose(); State.Mixer.Dispose(); }
    }
}
