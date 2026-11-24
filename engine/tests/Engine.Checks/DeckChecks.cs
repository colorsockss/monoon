using System.Numerics;
using Engine.Core;
using Engine.Editor;
using MoNoon.Decks;

internal static class DeckChecks
{
    [Check(1700, StopAfter = "deck")] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    internal static void Run(string repo, string scratch)
    {
        var sample = Path.Combine(repo, "samples", "CardLab");
        var target = Directory.CreateDirectory(Path.Combine(scratch, "DeckImport")).FullName;
        var package = Path.Combine(repo, "packages", "Decks.monopackage");
        var installed = WorkspaceArchives.Install(package, target, WorkspaceArchives.Package);
        var expected = new[] { "Assets/Decks", "Scripts/Decks" }.SelectMany(d => Directory.GetFiles(Path.Combine(sample, d)))
            .Select(p => Path.GetRelativePath(sample, p)).Order();
        Check(installed.Select(f => Path.GetRelativePath(target, f)).Order().SequenceEqual(expected) &&
            installed.All(f => File.ReadAllBytes(f).SequenceEqual(File.ReadAllBytes(Path.Combine(sample, Path.GetRelativePath(target, f))))) &&
            WorkspaceArchives.Install(package, target, WorkspaceArchives.Package).Length == 0, "package matches the sample and imports idempotently");
        var scenePath = Path.Combine(Directory.CreateDirectory(Path.Combine(target, "Scenes")).FullName, "Cards.scene");
        var camera = Guid.NewGuid();
        File.WriteAllText(scenePath, $"schemaVersion: 14\nobjects:\n- id: {camera}\n  name: Camera\n  width: 1\n  height: 1\n  components:\n  - type: camera2D\ndefaultCameraId: {camera}\n");
        var scene = SceneDocument.OpenForEditing(scenePath);
        var owner = scene.PlacePrefab("Assets/Decks/Deck.prefab", Vector2.Zero);
        scene.Save(scenePath);
        using (var game = new GameSession(SceneDocument.Load(scenePath)))
        {
            game.FixedUpdate(Vector2.Zero, 1f / 60);
            Check(game.Current.HasScript(owner.Id, "Decks/DeckSource") && !ProjectCatalog.Inspect(target).Problems.Any(),
                "deck prefab starts in a project without CardLab scripts or other packages");
        }

        var catalog = GameTable.Load(Path.Combine(target, "Assets", "Decks", "Trail.table"));
        var deck = new CardDeck(catalog, 42);
        DrawAll(deck);
        string[] first = ["stream", "bridge", "ridge", "moon", "dew", "pine"];
        Check(deck.Hand.SequenceEqual(first) && !deck.TryDraw(out _) && deck.ShuffleNumber == 1,
            "initial seed 42 order and no draw or shuffle when every card is in hand");
        while (deck.Hand.Count > 0) deck.TryPlay(0, out _);
        DrawAll(deck);
        Check(deck.Hand.SequenceEqual(["dew","pine","bridge","stream","moon","ridge"]) && deck.ShuffleNumber == 2,
            "discard-only shuffle has a stable second order");
        deck.Reset(42);
        deck.TryDraw(out _); deck.TryDraw(out _); deck.TryPlay(0, out _);
        var save = GameData.Parse(deck.Capture().ToYaml());
        var another = new CardDeck(catalog, -7);
        another.Restore(save);
        Check(another.Capture().ToYaml() == deck.Capture().ToYaml(), "restore seed, shuffle index, hand, discard and remaining draw order");
        DrawAll(deck); DrawAll(another);
        Check(another.Hand.SequenceEqual(deck.Hand) && deck.ShuffleNumber == 2 && deck.Hand.Count == 6,
            "restore continues through a one-card recycle without recycling held cards");
        var original = another.Capture().ToYaml();
        foreach (string invalid in new[] { "[dew, dew, dew, dew, dew, dew]", "[unknown]", "[]", "[null]" })
        {
            var bad = GameData.Parse(original);
            bad.Values["hand"] = GameData.ParseValue(invalid);
            try { another.Restore(bad); throw new Exception("Corrupt deck accepted"); }
            catch (InvalidDataException) { Check(another.Capture().ToYaml() == original, $"invalid hand {invalid} preserves live deck"); }
        }
        var duplicates = GameTable.Parse("""
            version: 1
            columns: [{name: label, type: Text}, {name: copies, type: Number}]
            rows: [{key: a, cells: ["A", 2]}, {key: b, cells: ["B", 1]}]
            """);
        var mixed = new CardDeck(duplicates, int.MinValue);
        DrawAll(mixed);
        mixed.TryPlay(1, out string used);
        var checkpoint = mixed.Capture();
        mixed.Reset(0); mixed.Restore(checkpoint);
        Check(!mixed.TryPlay(-1,out _) && !mixed.TryPlay(2,out _) && mixed.TryDraw(out var next) && next == used &&
            mixed.Hand.Count(c => c == "a") == 2 && mixed.Hand.Count(c => c == "b") == 1, "duplicate definitions, non-first hand play and negative seed round trip");
    }

    private static void DrawAll(CardDeck deck)
    {
        while (deck.TryDraw(out _)) { }
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("Deck: " + message);
        Console.WriteLine("PASS deck: " + message);
    }
}
