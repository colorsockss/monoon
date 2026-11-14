using System.Drawing;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Engine.Core;

var repository = Path.GetFullPath(args.FirstOrDefault() ?? ".");
if (File.Exists(Path.Combine(repository, "MoNoonEngine.slnx"))) repository = Directory.GetParent(repository)!.FullName;
var project = Path.Combine(repository, "samples", "KeyRoom");
var tileSample=Path.Combine(repository,"samples","KeyRoomTiles");
if (args is [_, "--tile-build",var fixture,var output])
{
    TileChecks.Prepare(tileSample,fixture);
    foreach (var rid in new[]{"osx-arm64","win-x64","linux-x64"})
        Console.WriteLine(GameBuild.Export(fixture,GameBuild.FindPlayerTemplate(Path.Combine(repository,"engine"),rid),rid,output,GameSettings.Load(fixture)));
    TileChecks.Run(fixture);
    return;
}
if (args is [_, "--sprite-sample",var folderPath]) { SpriteChecks.Prepare(Path.GetFullPath(folderPath)); return; }
var scratch = Directory.CreateTempSubdirectory("monoon-check-").FullName;
var previousProfile = Environment.GetEnvironmentVariable("MONOON_USER_DATA");
Environment.SetEnvironmentVariable("MONOON_USER_DATA",Path.Combine(scratch,".profiles"));
try
{
    if (args is [_, "--inventory-replay", var replayBuilds])
    {
        InputReplayChecks.CheckRecording(scratch);
        await InputReplayChecks.RunInventory(repository, replayBuilds, scratch);
        return;
    }
    if (args is [_, "--inventory-builds", _, ..])
    {
        BuildProfile? expectedProfile = args switch
        {
            [_, _, _] => null,
            [_, _, _, "--profile", "distribution"] => BuildProfile.Distribution,
            [_, _, _, "--profile", "development"] => BuildProfile.Development,
            _ => throw new ArgumentException("Expected --inventory-builds <directory> [--profile development|distribution].")
        };
        DialogueChecks.Run(repository, scratch);
        QuestChecks.Run(repository, scratch);
        InventoryChecks.Run(repository, scratch);
        await InventoryBuildChecks.Run(args[2], scratch, expectedProfile);
        return;
    }
    DialogueChecks.Run(repository, scratch);
    DeckChecks.Run(repository, scratch);
    if (args.Contains("--deck-only")) return;
    QuestChecks.Run(repository, scratch);
    if (args.Contains("--quest-only")) return;
    InventoryChecks.Run(repository, scratch);
    if (args.Contains("--inventory-only")) return;
    SpritePoolChecks.Run(repository);
    if (args.Contains("--pool-only")) return;
    RhythmChecks.Run(repository);
    if (args.Contains("--rhythm-only")) return;
    TurnReplayChecks.Run(repository,scratch);
    if (args.Contains("--turn-only")) return;
    GenerationChecks.Run(repository);
    if (args.Contains("--generation-only")) return;
    CutsceneChecks.Run(repository,scratch);
    if (args.Contains("--cutscene-only")) return;
    RigChecks.Run(repository,scratch);
    if (args.Contains("--rig-only")) return;
    EffectsChecks.Run(repository);
    if (args.Contains("--effects-only")) return;
    NavigationChecks.Run(repository,scratch);
    if(args.Contains("--nav-only")) return;
    TileWorldChecks.Run(repository,scratch);
    if (args.Contains("--world-only")) return;
    RigidBodyChecks.Run(repository, scratch);
    if (args.Contains("--platformer-only")) return;
    WorkspaceChecks.Run(project, scratch);
    AudioChecks.Run(repository,scratch);
    CameraChecks.Run(repository,scratch);
    CollisionChecks.Run(repository);
    DiagnosticsChecks.Run(project);
    var animatorRoot=Path.Combine(scratch,"AnimatorTest");
    AnimatorChecks.Run(animatorRoot,Path.Combine(repository,"samples","AnimatorLab")); Directory.Delete(animatorRoot,true);
    var spriteRoot=Path.Combine(scratch,"SpriteTest");
    SpriteChecks.Run(spriteRoot,repository); Directory.Delete(spriteRoot,true);
    ScriptingChecks.Run(scratch);
    InputMapChecks.Run(scratch);
    InputReplayChecks.CheckRecording(scratch);
    YamlChecks.Run(scratch);
    var tileRoot=Path.Combine(scratch,"TileTest");
    TileChecks.Prepare(tileSample,tileRoot);
    TileChecks.Run(tileRoot);
    Directory.Delete(tileRoot,true);
    foreach (var folder in new[] { "Assets", "Scenes", "Scripts" })
        foreach (var file in Directory.EnumerateFiles(Path.Combine(project, folder), "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(scratch, Path.GetRelativePath(project, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    foreach (var file in new[] { GameSettings.FileName, ProjectAssets.Manifest }) File.Copy(Path.Combine(project, file), Path.Combine(scratch, file));
    var sceneFile = Path.Combine(scratch, "Scenes", "KeyRoom.scene");
    ProjectCatalogChecks.Run(scratch);
    var scene = SceneDocument.Load(sceneFile);
    SceneStreamingChecks.Run(scratch, scene);
    PlayerDataChecks.Run(scratch, scene);
    TimeChecks.Run(scene);
    TransformChecks.Run(scratch);
    UiChecks.Run(scene, scratch);
    AccessibilityChecks.Run();
    PrefabEditingChecks.Run(scratch);
    var original = scene.CaptureObjects();
    var main = scene.Camera!;
    var doorCam = scene.Objects.Single(o => o.Name == "Door Camera");
    scene.DefaultCameraId = doorCam.Id;
    var copy = scene.DuplicateObject(scene.Objects.Single(o => o.Name == "Player"));
    Check(scene.Cameras.Count() == 3 && scene.Camera == doorCam && scene.Cameras.Any(o => o.Parent == copy), "duplicate camera subtree");
    var dirty = scene.CaptureObjects();
    scene.RemoveObject(doorCam);
    Check(scene.Camera == main, "start camera fallback");
    scene.RestoreObjects(dirty);
    Check(scene.Camera?.Id == doorCam.Id, "camera history restore");
    scene.Save(sceneFile);
    Check(SceneDocument.Load(sceneFile).DefaultCameraId == doorCam.Id, "start camera round trip");
    scene.RestoreObjects(original);
    scene.Save(sceneFile);
    var legacyFile = Path.Combine(scratch, "Scenes", "Migration.scene");
    for (var version = 1; version <= 5; version++)
    {
        File.Delete(legacyFile);
        var oldFile = legacyFile + ".json";
        File.WriteAllText(oldFile, JsonSerializer.Serialize(new { schemaVersion = version, objects = Array.Empty<object>() }));
        LegacyProject.Upgrade(scratch);
        var migrated = SceneDocument.Load(legacyFile);
        Check((migrated.Camera != null) == (version < 4), $"v{version} camera migration");
    }
    var broken = Regex.Replace(File.ReadAllText(sceneFile), @"(?m)^defaultCameraId: .+$", "defaultCameraId: " + Guid.NewGuid());
    File.WriteAllText(legacyFile, broken);
    var repaired = SceneDocument.Load(legacyFile);
    Check(repaired.LoadNotice != null && repaired.DefaultCameraId == main.Id, "recover missing start camera");

    var noCamera = SceneDocument.Empty(new());
    noCamera.RemoveObject(noCamera.Camera!);
    noCamera.Save(legacyFile);
    try { using var bad = new SceneGame(SceneDocument.Load(legacyFile)); throw new Exception("Expected a missing camera error."); }
    catch (InvalidOperationException ex) { Check(ex.Message.Contains("Create Camera"), "empty scene save and play guidance"); }
    var orders = SceneDocument.Starter();
    var player = orders.Objects[0];
    var front = orders.DuplicateObject(player);
    Check(orders.DrawOrder().First() == player, "stable sorting ties");
    front.GetComponent<SpriteRendererComponent>()!.Order = -1;
    Check(orders.DrawOrder().First() == front, "sprite draw order");
    try { scene.OpenScripts(scratch).Save("Broken", "using Engine.Core;\npublic class Broken : GameBehaviour { + }", null); throw new Exception("Expected script error."); }
    catch (InvalidDataException ex) { Check(ex.Message.Contains("Broken.cs(2,"), "script error file and line"); }
    var actions = new InputActions(new InputBindings());
    actions.Press("A"); actions.Press("W"); actions.Release("A"); actions.Release("W");
    Check(Vector2.Distance(actions.Take().Move, Vector2.Normalize(new Vector2(-1, -1))) < .0001 && actions.Take().Move == Vector2.Zero, "short diagonal movement");
    actions.Press("Space"); actions.Press("Space"); actions.Press("E"); actions.Release("Space"); actions.Release("E");
    Check(actions.Take().InteractPressed && !actions.Take().InteractPressed, "one interact per press");
    ClipChecks.Run(scratch,sceneFile);
    WalkGame(sceneFile);
    RuntimePrefabChecks.Chest(scene);
    RuntimePrefabChecks.Subtrees(scene, scratch);

    var wall = scene.Objects.Single(o => o.Name == "Wall");
    const string prefabPath = "Assets/Wall.prefab";
    scene.ExportPrefab(wall, Path.Combine(scratch, prefabPath));
    var instance1 = scene.PlacePrefab(prefabPath, new(123, 45));
    var instance2 = scene.PlacePrefab(prefabPath, new(-10, 60));
    Check(!instance1.Prefab!.Instances.Values.Intersect(instance2.Prefab!.Instances.Values).Any(), "unique prefab instance IDs");
    wall.GetComponent<SpriteRendererComponent>()!.SquareColor = "#E7DCC9";
    scene.ExportPrefab(wall, Path.Combine(scratch, prefabPath), true);
    Check(scene.RefreshPrefabs(ProjectAssets.Scan(scratch)), "source update propagated");
    foreach (var inst in new[] { instance1, instance2 })
    {
        var updated = scene.Objects.Single(o => o.Id == inst.Id);
        Check(updated.WorldPosition == inst.WorldPosition && updated.GetComponent<SpriteRendererComponent>()!.SquareColor == "#E7DCC9", "instance placement preserved");
    }
    var key = scene.Objects.Single(o => o.Name == "Key");
    scene.ExportPrefab(key, Path.Combine(scratch, "Assets", "Key.prefab"));
    var keyInstance = scene.PlacePrefab("Assets/Key.prefab", new(700, 333));
    var attached = keyInstance.GetComponent<ScriptComponent>()!;
    Check(attached.Values["KeyImage"].GetGuid() == scene.Objects.Single(o => o.Parent == keyInstance).Id && !attached.Values.ContainsKey("Collector"), "prefab internal and external references");

    scene.RestoreObjects(original);
    scene.Save(sceneFile);
    var from = Path.Combine(scratch, "Assets", "Door.png");
    var to = Path.Combine(scratch, "Assets", "DoorMoved.png");
    File.Move(from, to);
    var reopened = SceneDocument.Load(sceneFile);
    Check(reopened.Objects.Single(o => o.Name == "Door Panel").GetComponent<SpriteRendererComponent>()!.Asset == "Assets/DoorMoved.png", "move PNG without breaking sprite");
    var previousScene = reopened.AssetId;
    using (var game = new GameSession(reopened))
    {
        int prepareCount = 0;
        game.UseRenderer(_ => { if (++prepareCount > 1) throw new InvalidDataException("Texture upload failed."); });
        game.State.LoadScene(ProjectAssets.ReadManifest(reopened.ProjectDirectory!).IdFor("Scenes/SecondRoom.scene")!.Value);
        game.FixedUpdate(Vector2.Zero, 1f / 60);
        Check(prepareCount == 2 && previousScene == game.SceneId, "failed asset preload preserves running scene");
    }
    var settings = GameSettings.Load(project);
    var editorDirectory = Path.Combine(repository, "editor", "MoNoon.app", "Contents", "MacOS");
    foreach (var rid in new[] { "osx-arm64", "win-x64", "linux-x64" })
    {
        var dest = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.Combine(scratch, "Builds");
        var template = GameBuild.FindPlayerTemplate(editorDirectory, rid);
        Check(template == GameBuild.FindPlayerTemplate(Path.Combine(repository, "engine"), rid), "app and source use the same external template for " + rid);
        var product = GameBuild.Export(project, template, rid, dest, settings);
        var contents = rid == "osx-arm64" ? Path.Combine(product, "Contents", "MacOS") : product;
        Check(!Directory.EnumerateFiles(contents, "Avalonia*.dll").Any() && !Directory.Exists(Path.Combine(contents, "Game", "Scripts")), "standalone " + rid);
        var data = Path.Combine(contents, "Game");
        Check(!Directory.EnumerateFiles(data, "*", SearchOption.AllDirectories).Any(p => Path.GetExtension(p) is ".json" or ".yaml")
            && File.Exists(Path.Combine(data, "Assets", "Door.clip")), "game data uses asset extensions for " + rid);
        Check(File.Exists(Path.Combine(data,"Assets","KeyRoomRules.data")), "referenced YAML data exported for " + rid);
        Check(GameTable.Load(Path.Combine(data,"Assets","Loot.table")).Row("coin").GetText("flag")=="coin", "referenced YAML table exported for "+rid);
        foreach (var entry in ProjectAssets.Scan(data).Entries)
        {
            using var input = File.OpenRead(Path.Combine(data, entry.Path));
            if (Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(input)) != entry.Hash)
                throw new Exception("Build manifest hash mismatch: " + entry.Path);
        }
        Check(Directory.GetFiles(Path.Combine(data,".monoon-compiled"),"*.dll").Select(Path.GetFileName).SequenceEqual(["MoNoon.GameScripts.dll"]), "one shared project assembly for " + rid);
        WalkGame(Path.Combine(data, "Scenes", "KeyRoom.scene"));
        RuntimePrefabChecks.Chest(SceneDocument.Load(Path.Combine(data, "Scenes", "KeyRoom.scene")));
        Console.WriteLine(product);
    }
    Console.WriteLine("All checks passed.");
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    Environment.ExitCode = 1;
}
finally
{
    Environment.SetEnvironmentVariable("MONOON_USER_DATA",previousProfile);
    Directory.Delete(scratch, recursive: true);
}

static void WalkGame(string file)
{
    var scene = SceneDocument.Load(file);
    var start = scene.DefaultCameraId;
    var door = scene.Objects.Single(o => o.Name == "Door Camera").Id;
    using var session = new GameSession(scene);
    var error = ""; int sounds = 0;
    session.LoadFailed += s => error = s;
    session.SoundRequested += _ => sounds++;
    Check(session.Current.ActiveCameraId == start && !session.State.Has("key"), "fresh session");
    const float step = 1f / 60;
    // Same script runs both from project C# and from each export's compiled assembly.
    session.Current.Ui.Pointer(true, 740, 25);
    session.Current.Ui.Pointer(false, 680, 25);
    session.FixedUpdate(Vector2.Zero, step);
    Check(!session.State.Has("controls"), "release outside cancels UI click");
    session.Current.Ui.Pointer(true, 740, 25);
    session.Current.Ui.Pointer(false, 740, 25);
    session.FixedUpdate(Vector2.Zero, step);
    Check(session.State.Has("controls"), "screen button invokes project script once");
    session.Current.Ui.Key("Return", true);
    session.Current.Ui.Key("Return", true);
    session.FixedUpdate(Vector2.Zero, step);
    Check(!session.State.Has("controls"), "focused button ignores repeated keydown");
    for (int i = 0; i < 18; i++) session.FixedUpdate(-Vector2.UnitX, step);
    Check(sounds == 1 && session.State.Has("key") && session.Current.ActiveCameraId == door, "pickup and door camera");
    var canvas = new CaptureCanvas();
    session.Draw(canvas, 1);
    Check(canvas.Images.Any(i => i.Path.EndsWith("Key.png") && i.Rect.X == 14), "screen-space key HUD");
    for (int i = 0; i < 60; i++) session.FixedUpdate(Vector2.Zero, step);
    Check(session.Current.ActiveCameraId == start, "camera restored after one second");
    for (int i = 0; i < 115; i++) session.FixedUpdate(Vector2.UnitX, step);
    SceneStreamingChecks.Wait(session);
    Check(error == "" && session.SceneId != scene.AssetId && session.State.Has("key") && sounds == 1, "enter next room with key: " + error);
    var lastRoom = session.Current;
    session.State.LoadScene(Guid.NewGuid());
    session.FixedUpdate(Vector2.Zero, step);
    Check(error != "" && session.Current == lastRoom, "failed switch retains room");
}

static void Check(bool ok, string label)
{
    if (!ok) throw new Exception(label);
    Console.WriteLine("PASS " + label);
}

sealed class CaptureCanvas : ICanvas2D
{
    public List<(string Path, RectangleF Rect, SpriteRegion? Source)> Images { get; } = [];
    public void Clear(Color color) => Images.Clear();
    public void FillRectangle(RectangleF rectangle, Color color) { }
    public bool DrawTile(string path, RectangleF rect, SpriteRegion source, string layout) => DrawSprite(path,rect,source);
    public bool DrawSprite(string path, RectangleF rect) => DrawSprite(path, rect, null);
    public bool DrawSprite(string path, RectangleF rect, SpriteRegion? source) { Images.Add((path, rect, source)); return true; }
}
