using System.Drawing;
using System.Numerics;
using Engine.Core;

internal static class SpriteSortingChecks
{
    internal static void Run(string project, string scratch)
    {
        CheckGroups(scratch);
        var scene = SceneDocument.Load(Path.Combine(project, "Scenes", "KeyRoom.scene"));
        using var game = new GameSession(scene);
        var canvas = new Capture();
        void Expect(float interpolation, bool inFront)
        {
            game.Draw(canvas, interpolation);
            string[] behind = ["Body", "Satchel", "Flap", "Clasp", "Oak", "Courtyard oak"];
            string[] front = ["Oak", "Body", "Satchel", "Flap", "Clasp", "Courtyard oak"];
            if (!canvas.Sprites.SequenceEqual(inFront ? front : behind))
                throw new Exception($"Incorrect draw order at t={interpolation}: {string.Join(", ", canvas.Sprites)}");
        }
        for (int i = 0; i < 28; i++) game.FixedUpdate(Vector2.UnitY, 1f / 60);
        game.FixedUpdate(Vector2.UnitY, 1f / 60); // Both the camera and the player are moving.
        Expect(.25f, false); // Player foot at 267, oak at 268.
        Expect(.5f, false);  // Tied with the oak, use scene order.
        Expect(.75f, true);
        game.FixedUpdate(-Vector2.UnitY, 1f / 60);
        Expect(.25f, true);
        Expect(.75f, false);
        Console.WriteLine("PASS Y sorting: crossing in both directions, interpolated feet, fixed layers, stable ties, nested groups, transforms and YAML");
    }

    private static void CheckGroups(string scratch)
    {
        var scene = SceneDocument.Empty(new());
        SceneObject Sprite(string name, int layer = 0)
        {
            var obj = new SceneObject { Name = name, Width = 30, Height = 10 };
            obj.AddComponent(new SpriteRendererComponent { SortingLayer = layer });
            scene.Objects.Add(obj);
            return obj;
        }
        var outer = Sprite("Body");
        outer.AddComponent(new SortingGroup2DComponent { SortByY = true });
        var prop = Sprite("Prop", 1);
        prop.SetParent(outer);
        prop.AddComponent(new SortingGroup2DComponent { Order = 5 });
        var trim = Sprite("Trim", -9);
        trim.SetParent(prop);
        var tree = Sprite("Tree");
        tree.GetComponent<SpriteRendererComponent>()!.SortByY = true;
        tree.GetComponent<SpriteRendererComponent>()!.FootOffset = 11; // World Y = 21.
        var back = Sprite("Fixed");
        back.Y = 800;
        Sprite("Foreground", 1).Y = -900;
        // Deliberately store the child before its parent in the scene.
        scene.Objects.Remove(trim);
        scene.Objects.Insert(0, trim);
        void Expect(SceneDocument doc, params string[] names)
        {
            var order = doc.DrawOrder().Where(o => o.GetComponent<SpriteRendererComponent>() != null).Select(o => o.Name);
            if (!order.SequenceEqual(names)) throw new Exception($"Unexpected editor sorting: {string.Join(", ", order)}");
        }
        Expect(scene, "Fixed", "Body", "Trim", "Prop", "Tree", "Foreground");
        var parent = new SceneObject { Name = "Pose", Rotation = 90, ScaleX = 2 };
        scene.Objects.Add(parent);
        outer.SetParent(parent); // Transformed bottom center (15,10) has world Y = 30.
        Expect(scene, "Fixed", "Tree", "Body", "Trim", "Prop", "Foreground");

        var folder = Path.Combine(scratch, "Sorting", "Scenes");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "Nested.scene");
        scene.Save(path);
        var reopened = SceneDocument.Load(path);
        if (reopened.SchemaVersion != 22 || reopened.Objects.Single(o => o.Id == tree.Id).GetComponent<SpriteRendererComponent>()?.FootOffset != 11)
            throw new Exception("Y-sort settings did not survive saving and reopening.");
        Expect(reopened, "Fixed", "Tree", "Body", "Trim", "Prop", "Foreground");
    }

    private sealed class Capture : ICanvas2D
    {
        internal readonly List<string> Sprites = [];
        public void Clear(Color color) => Sprites.Clear();
        public bool DrawAffine(string? path, RectangleF rect, Matrix3x2 matrix, Color tint, SpriteRegion? source = null, string layout = TileGrid.Orthogonal)
        {
            var name = Path.GetFileName(path) == "Oak.png" ? (rect.Width == 76 ? "Courtyard oak" : "Oak") : (tint.ToArgb() & 0xffffff) switch
            {
                0x63E6C1 => "Body", 0xBB504A => "Satchel", 0xEA8661 => "Flap", 0xFFE9AD => "Clasp", _ => null
            };
            if (name != null) Sprites.Add(name);
            return true;
        }
        public bool DrawSprite(string path, RectangleF rect) => true;
        public void FillRectangle(RectangleF rect, Color color) { }
    }
}
