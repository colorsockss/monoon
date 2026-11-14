using Engine.Core;
using Engine.Desktop;

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

try
{
    var root = Path.Combine(AppContext.BaseDirectory, "Game");
    var settings = GameSettings.Load(root);
    var assets = ProjectAssets.Scan(root);
    var path = assets.PathFor(settings.StartScene ?? Guid.Empty) ?? throw new InvalidDataException("The build has no start scene.");
    var scene = SceneDocument.Load(Path.Combine(root, path));
    Func<ScriptComponent, GameBehaviour>? factory = null;
#if MONOON_COMPILED_SCRIPTS
    factory = CompiledGameScripts.Create;
#endif
    var storage = smoke ? new PlayerStorage(args[1]) : record || replay ? new PlayerStorage(args[2]) : null;
    using var game = new GameSession(scene, null, factory, storage);
    if (record || replay)
    {
        game.LoadFailed += message => throw new InvalidDataException(message);
        var project = settings.ProjectId ?? settings.StartScene!.Value;
        if (replay)
        {
            var recording = InputReplay.Load(args[1]);
            recording.Play(game, project);
            Console.WriteLine($"Headless replay completed: {recording.FrameTicks.Length} frames, {recording.Events.Length} events.");
        }
        else
        {
            if (File.Exists(args[1])) throw new IOException($"Recording already exists: {args[1]}");
            var recorder = new InputRecorder(project, settings.StartScene!.Value);
            DesktopHost.Run(settings.GameTitle ?? "Game", game, recorder);
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
        game.LoadFailed += Console.Error.WriteLine;
        DesktopHost.Run(settings.GameTitle ?? "Game", game);
    }
    return 0;
}
catch (Exception ex) when (smoke || record || replay)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
