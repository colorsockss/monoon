using System.Numerics;
using System.Text;
using Engine.Core;

// Roadmap 13.2: SceneObject.Active and SceneComponent.Enabled. Only false is written (existing files keep their bytes), the
// runtime starts an inactive object and its children disabled, leaves turned-off components out and starts a turned-off
// collider with SetColliderEnabled false.
internal static class ComponentStateChecks
{
    private static void Require(bool ok, string message)
    {
        if (!ok) throw new Exception("Component state: " + message);
    }

    internal static void Run(string repository)
    {
        // Defaults are not written; false round-trips under "active" / "enabled".
        var plain = new SceneObject { Name = "Plain" };
        plain.AddComponent(new SpriteRendererComponent());
        var plainText = Encoding.UTF8.GetString(ProjectYaml.Encode(plain));
        Require(!plainText.Contains("active:") && !plainText.Contains("enabled:"), "default true is not written");
        var off = new SceneObject { Name = "Off", Active = false };
        off.AddComponent(new BoxCollider2DComponent { Enabled = false });
        var offText = Encoding.UTF8.GetString(ProjectYaml.Encode(off));
        Require(offText.Contains("active: false") && offText.Contains("enabled: false"), "false is written: " + offText);
        var back = ProjectYaml.Decode<SceneObject>(Encoding.UTF8.GetBytes(offText));
        Require(!back.Active && !back.Components[0].Enabled && ProjectYaml.Decode<SceneObject>(Encoding.UTF8.GetBytes(plainText)) is { Active: true } p &&
            p.Components[0].Enabled, "round trip");

        // Every sample scene and prefab: one that turns nothing off writes no keys; one that does (the showcases start some
        // objects hidden) is version 32 or later and writes exactly its false values back.
        int files = 0;
        foreach (var file in Directory.EnumerateFiles(Path.Combine(repository, "samples"), "*.*", SearchOption.AllDirectories)
                     .Where(f => f.EndsWith(".scene", StringComparison.Ordinal) || f.EndsWith(".prefab", StringComparison.Ordinal)))
        {
            var doc = SceneDocument.ReadAsset(file);
            string Off(SceneDocument d) => string.Join(",", d.Objects.Where(o => !o.Active).Select(o => o.Id)) + "|" +
                string.Join(",", d.Objects.SelectMany(o => o.Components.Where(c => !c.Enabled).Select(c => $"{o.Id}/{c.Kind}")));
            var bytes = ProjectYaml.Encode(doc);
            var text = Encoding.UTF8.GetString(bytes);
            bool keys = text.Contains("\n  active:") || text.Contains("    enabled:");
            if (Off(doc) == "|")
                // Schemas 33 (parallax, sprite draw modes) and 34 (capsule/edge colliders) are above the active/enabled version 32 but do not imply it.
                Require(doc.SchemaVersion != 32 && !keys, $"{file} turns nothing off, is not version 32 and writes no active/enabled keys");
            else
                Require(doc.SchemaVersion >= 32 && keys && Off(ProjectYaml.Decode<SceneDocument>(bytes)) == Off(doc),
                    $"{file} keeps its inactive objects and disabled components through a save ({doc.SchemaVersion})");
            files++;
        }

        // Structural components cannot be turned off; scenes using the keys are version 32.
        var scene = SceneDocument.Empty(new GameSettings());
        scene.Validate();
        Require(scene.SchemaVersion < 32, "a scene without inactive objects keeps its version");
        scene.Camera!.GetComponent<Camera2DComponent>()!.Enabled = false;
        try { scene.Validate(); throw new Exception("Component state: a turned-off camera must be rejected"); }
        catch (InvalidDataException) { }
        scene.Camera!.GetComponent<Camera2DComponent>()!.Enabled = true;

        var parent = new SceneObject { Name = "Parent", X = 100, Y = 100, Active = false };
        var child = new SceneObject { Name = "Child", X = 10, Y = 0 };
        child.AddComponent(new SpriteRendererComponent { SquareColor = "#336699" });
        var gate = new SceneObject { Name = "Gate", X = 300, Y = 100 };
        gate.AddComponent(new BoxCollider2DComponent { Enabled = false });
        var ghost = new SceneObject { Name = "Ghost", X = 400, Y = 100 };
        ghost.AddComponent(new SpriteRendererComponent { SquareColor = "#FF0000", Enabled = false });
        scene.Objects.AddRange([parent, child, gate, ghost]);
        scene.Reparent(child, parent, keepWorld: false);
        scene.Validate();
        Require(scene.SchemaVersion == 32, $"inactive objects make version 32, got {scene.SchemaVersion}");
        var authored = scene.CaptureObjects();
        using (var game = new SceneGame(scene))
        {
            game.FixedUpdate(Vector2.Zero, 1f / 60);
            Require(!game.IsActive(parent.Id) && !game.IsActive(child.Id), "inactive parent and its child start disabled");
            Require(!game.IsColliderEnabled(gate.Id), "a turned-off collider starts disabled");
            var gateHits = game.Physics.Overlap(Shape2D.Circle(new(320, 120), 4), new PhysicsFilter(IncludeTriggers: true)).Count;
            Require(gateHits == 0, $"a turned-off collider does not collide ({gateHits})");
            Require(!game.TryGetSpriteColor(ghost.Id, out _) && game.GetComponent<SpriteRendererComponent>(ghost.Id) == null,
                "a turned-off renderer is left out");
            Require(game.SetActive(parent.Id, true) && game.IsActive(parent.Id) && game.IsActive(child.Id), "SetActive turns the subtree on");
            Require(game.SetColliderEnabled(gate.Id, true) && game.IsColliderEnabled(gate.Id), "SetColliderEnabled turns the collider on");
            game.FixedUpdate(Vector2.Zero, 1f / 60);
            Require(game.Physics.Overlap(Shape2D.Circle(new(320, 120), 4), new PhysicsFilter(IncludeTriggers: true)).Count == 1, "the enabled collider is found");
        }
        Require(scene.CaptureObjects() == authored, "Play did not change the authored state");
        Console.WriteLine($"PASS object active / component enabled: only false is stored ({files} sample files keep their keys), " +
            "runtime starts inactive subtrees and turned-off components disabled");
    }
}
