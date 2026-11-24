using System.Numerics;
using Engine.Core;
using Engine.Editor;
using MoNoon.Dialogue;

internal static class DialogueChecks
{
    [Check(1500)] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    internal static void Run(string repo, string scratch)
    {
        var sample = Path.Combine(repo, "samples", "InventoryLab");
        var target = Directory.CreateDirectory(Path.Combine(scratch, "DialogueImport")).FullName;
        var package = Path.Combine(repo, "packages", "Dialogue.monopackage");
        var installed = WorkspaceArchives.Install(package, target, WorkspaceArchives.Package);
        var expected = new[] { "Assets/Dialogue", "Scripts/Dialogue" }.SelectMany(d => Directory.GetFiles(Path.Combine(sample, d)))
            .Select(p => Path.GetRelativePath(sample, p)).Order();
        Check(installed.Select(f => Path.GetRelativePath(target, f)).Order().SequenceEqual(expected) &&
            installed.All(f => File.ReadAllBytes(f).SequenceEqual(File.ReadAllBytes(Path.Combine(sample, Path.GetRelativePath(target, f))))) &&
            WorkspaceArchives.Install(package, target, WorkspaceArchives.Package).Length == 0, "standalone package imports its story, prefab and scripts idempotently");
        var scenePath = Path.Combine(Directory.CreateDirectory(Path.Combine(target, "Scenes")).FullName, "Talk.scene");
        var camera = Guid.NewGuid();
        File.WriteAllText(scenePath, $"schemaVersion: 14\nobjects:\n- id: {camera}\n  name: Camera\n  width: 1\n  height: 1\n  components:\n  - type: camera2D\ndefaultCameraId: {camera}\n");
        var scene = SceneDocument.OpenForEditing(scenePath);
        var npc = scene.PlacePrefab("Assets/Dialogue/Herbalist.prefab", Vector2.Zero);
        scene.Save(scenePath);
        using (var game = new GameSession(SceneDocument.Load(scenePath)))
        {
            game.FixedUpdate(Vector2.Zero, 1f / 60);
            Check(game.Current.HasScript(npc.Id, "Dialogue/DialoguePlayer") && !ProjectCatalog.Inspect(target).Problems.Any(),
                "imported dialogue prefab starts without a quest journal or inventory");
        }

        var graph = DialogueGraph.Read(GameData.Load(Path.Combine(target, "Assets", "Dialogue", "Herbalist.data")));
        graph.Find("greeting").Text = "어떤 약초가 필요하세요?";
        graph.Find("greeting").Choices[0].Text = "보상부터 묻는다";
        graph.Find("greeting").Choices[0].Next = "reward";
        graph = DialogueGraph.Read(GameData.Parse(graph.Capture().ToYaml()));
        var session = new DialogueSession(graph);
        session.Begin();
        Check(session.Current?.Text == "어떤 약초가 필요하세요?" && session.Current.Choices[0].Text == "보상부터 묻는다" &&
            session.Choose(0) && session.Position == "reward", "edited line, option order and retargeted branch round-trip through YAML");
        Check(!session.Choose(0) && !session.Choose(0, _ => false) && !session.Choose(99) && session.Position == "reward",
            "missing handler, rejected action and invalid choice preserve the current node");
        int calls = 0;
        Check(session.Choose(0, key =>
            {
                calls++;
                return key == "claim" && !session.Choose(0, _ => true);
            }) && calls == 1 && session.Position == "paid", "successful choice executes one action before following its branch");
        var nextSession = new DialogueSession(graph);
        nextSession.Restore(session.Position);
        Check(nextSession.Position == "paid" && nextSession.Choose(0) && nextSession.Current == null,
            "restore resumes at the saved line and an ending choice closes the dialogue");
        nextSession.Begin();
        try { nextSession.Restore("deleted"); throw new Exception("Unknown save position accepted"); }
        catch (InvalidDataException) { Check(nextSession.Position == "greeting", "unknown saved node preserves the active conversation"); }
        graph.Find("offer").Choices[0].Next = "missing";
        try { graph.Capture(); throw new Exception("Broken target accepted"); }
        catch (InvalidDataException) { Check(true, "save rejects a dangling branch"); }
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("Dialogue: " + message);
        Console.WriteLine("PASS dialogue: " + message);
    }
}
