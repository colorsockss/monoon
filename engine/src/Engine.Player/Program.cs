using Engine.Core;
using Engine.Desktop;

long entryTime = System.Diagnostics.Stopwatch.GetTimestamp();
bool smoke = args.FirstOrDefault() == "--smoke-test";
bool record = args.FirstOrDefault() == "--record-input";
bool replay = args.FirstOrDefault() == "--replay-input";
if ((record || replay) && (args.Length != 3 || args.Skip(1).Any(string.IsNullOrWhiteSpace)))
{
    Console.Error.WriteLine("Usage: MoNoonPlayer <--record-input|--replay-input> <recording-file> <test-profile-directory>");
    return 2;
}
if (smoke && (args.Length < 2 || string.IsNullOrWhiteSpace(args[1])))
{
    Console.Error.WriteLine("Usage: MoNoonPlayer --smoke-test <test-profile-directory> [bound control ...]");
    return 2;
}

string run = smoke ? "smoke" : replay ? "replay" : record ? "record" : "play";
bool playing = args.FirstOrDefault() is not ("--mods" or "--content-update");
Guid? startScene = null;
GameSession? runningSession = null;
void Report(Exception error) => CrashDiagnostics.Report(error, run, runningSession?.SceneId ?? startScene);
if (playing) AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    if (e.ExceptionObject is Exception error) Report(error);
};
try
{
    using var trace = playing ? PlayerStartupTrace.Open(entryTime) : null;
    var installed = Path.Combine(AppContext.BaseDirectory, "Game");
    // A signed Mac app keeps Game in Contents/Resources behind a link; content checks reject links inside the tree.
    if (new DirectoryInfo(installed).ResolveLinkTarget(true) is { } signedLayout) installed = signedLayout.FullName;
    var settings = GameSettings.Load(installed);
    startScene = settings.StartScene;
    var mods = GameMods.ForPlayer(installed, settings);
    if (args.FirstOrDefault() == "--mods")
    {
        if (args is ["--mods", "list"])
        {
            Console.WriteLine($"User mods: {mods.UserDirectory}");
            foreach (string line in mods.List()) Console.WriteLine(line);
        }
        else if (args is ["--mods", "on" or "off", var id])
        {
            mods.SetEnabled(id, args[1] == "on");
            Console.WriteLine($"Mod {id}: {args[1]}");
        }
        else
        {
            Console.Error.WriteLine("Usage: MoNoonPlayer --mods list | --mods <on|off> <id>");
            return 2;
        }
        return 0;
    }
    Func<ScriptComponent, GameBehaviour>? factory = null;
#if MONOON_COMPILED_SCRIPTS
    factory = CompiledGameScripts.Create;
#endif
    GameSession Start(string root, PlayerStorage? storage, out string path)
    {
        var assets = ProjectAssets.Scan(root);
        path = assets.PathFor(settings.StartScene ?? Guid.Empty) ?? throw new InvalidDataException("The build has no start scene.");
        var scene = SceneDocument.Load(Path.Combine(root, path));
        return new GameSession(scene, null, factory, storage);
    }
    var updates = new ContentUpdates(installed, settings);
    if (args.FirstOrDefault() == "--content-update")
    {
        if (args is ["--content-update", "apply", var package, var sha256])
        {
            updates.Apply(package, sha256, root =>
            {
                var scratch = Directory.CreateTempSubdirectory("monoon-update-check-");
                try
                {
                    using var session = Start(root, new PlayerStorage(scratch.FullName), out _);
                    session.LoadFailed += message => throw new InvalidDataException(message);
                    session.FixedUpdate(session.Actions.Take(), 1f / 60);
                }
                finally { scratch.Delete(true); }
            });
            Console.WriteLine("Content update applied. It will be used on the next launch.");
        }
        else if (args is ["--content-update", "reset"])
        {
            updates.Reset();
            Console.WriteLine("Shipped content restored.");
        }
        else
        {
            Console.Error.WriteLine("Usage: MoNoonPlayer --content-update apply <bundle> <sha256> | --content-update reset");
            return 2;
        }
        return 0;
    }
    trace?.Mark("content_begin");
    using var content = updates.Open(mods, GameLog.Error, trace == null ? null : trace.BundleRead);
    trace?.Mounted(content);
    var storage = smoke ? new PlayerStorage(args[1]) : record || replay ? new PlayerStorage(args[2]) : null;
    using var game = Start(content.Root, storage, out string path);
    runningSession = game;
    GameLog.Attach(game.State);
    trace?.Mark("session_ready");
    // Development builds only: MONOON_FRAME_PROFILE=<file.csv> records every frame's call tree.
    using var frameProfile = FrameProfileWriter.Open(AppContext.BaseDirectory);
    if (FrameProfileWriter.IsDevelopmentBuild(AppContext.BaseDirectory) && SessionState.SceneLoadBudgetOverride() is {} budget)
        game.State.SceneLoadBudgetMilliseconds = budget;
    if (record || replay)
    {
        game.LoadFailed += message => throw new InvalidDataException(message);
        var project = settings.ProjectId ?? settings.StartScene!.Value;
        if (replay)
        {
            var recording = InputReplay.Load(args[1]);
            using var offscreen = OffscreenResources.Open(game);
            if (offscreen != null) Console.WriteLine($"Replay resources at start: {offscreen.Counts}");
            using var capture = ReplayCapture.Open(game);
            var snapshots = OffscreenResources.Snapshots();
            if (snapshots != null) Directory.CreateDirectory(snapshots.Value.Directory);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var due = TimeSpan.Zero;
            recording.Play(game, project, capture == null && frameProfile == null && offscreen == null ? null : frame =>
            {
                capture?.Frame(frame);
                frameProfile?.Profiler.EndFrame();
                if (snapshots is var (folder, frames) && frames.Contains(frame))
                    offscreen!.Snapshot(game, Path.Combine(folder, $"{frame:D4}.png"));
                if (offscreen == null || !OffscreenResources.Paced) return;
                // Wait out the recorded frame time like a 60 Hz window; the profile excludes the wait.
                due += TimeSpan.FromTicks(recording.FrameTicks[frame]);
                if (due > clock.Elapsed) Thread.Sleep(due - clock.Elapsed);
                else due = clock.Elapsed;
                frameProfile?.Profiler.Restart();
            });
            Console.WriteLine($"Headless replay completed: {recording.FrameTicks.Length} frames, {recording.Events.Length} events.");
            if (offscreen != null)
            {
                // Released decode buffers are freed by a worker continuation after cancellation.
                for (int i = 0; i < 200 && offscreen.Resources.DecodedCount != 0; i++) Thread.Sleep(5);
                Console.WriteLine($"Replay resources at end: {offscreen.Counts}");
            }
        }
        else
        {
            if (File.Exists(args[1])) throw new IOException($"Recording already exists: {args[1]}");
            var recorder = new InputRecorder(project, settings.StartScene!.Value);
            DesktopHost.Run(settings.GameTitle ?? "Game", game, recorder, trace);
            var recording = recorder.Save(args[1]);
            Console.WriteLine($"Recorded {recording.FrameTicks.Length} frames, {recording.Events.Length} events to {args[1]}");
        }
        return 0;
    }
    if (smoke)
    {
        string? loadError = null;
        game.LoadFailed += message => loadError = message;
        void Tick()
        {
            game.FixedUpdate(game.Actions.Take(), 1f / 60);
            frameProfile?.Profiler.EndFrame();
            if (loadError != null) throw new InvalidDataException(loadError);
        }
        Tick();
        foreach (var control in args.Skip(2))
        {
            if (!game.Actions.Press(control)) throw new InvalidDataException($"Unbound control '{control}'.");
            Tick();
            game.Actions.Release(control);
            Tick();
        }
        Console.WriteLine($"PASS player smoke: {path} ({args.Length - 2} control presses)");
    }
    else
    {
        game.LoadFailed += GameLog.Error;
        DesktopHost.Run(settings.GameTitle ?? "Game", game, null, trace);
    }
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    if (playing) Report(ex);
    return 1;
}
