using System.Numerics;
using Engine.Core;

internal static class RuntimeTransformChecks
{
    [Check(8400)] static void Registered(CheckRun c) => Run(c.Scene);
    internal static void Run(SceneDocument scene)
    {
        var player = scene.Objects.Single(o => o.Name == "Player");
        var chest = scene.Objects.Single(o => o.Name == "Chest");
        var lid = scene.Objects.Single(o => o.Name == "Chest Lid");
        using var session = new GameSession(scene);
        var game = session.Current;
        var loop = new GameLoop(session, session.Actions);
        var input = new GameInputRouter(session.Actions, () => session.Current.Ui, session.LogicalSize);
        void Frame() => loop.Advance(GameTime.FixedStep);
        void Press(string key) { input.Key(key, true); Frame(); input.Key(key, false); Frame(); }
        Matrix3x2 World(Guid id) => game.TryGetWorldMatrix(id, out var m) ? m : throw new Exception("Missing runtime object");
        Guid? Parent(Guid id) => game.TryGetParent(id, out var parent) ? parent : throw new Exception("Missing runtime parent");
        static void Check(bool ok, string what) { if (!ok) throw new Exception("Runtime Transform: " + what); }
        static void Same(Matrix3x2 a, Matrix3x2 b, string what)
        {
            foreach (var point in new Vector2[] { Vector2.Zero, new(22, 0), new(22, 22), new(0, 22) })
                Check(Vector2.Distance(Vector2.Transform(point, a), Vector2.Transform(point, b)) < .002f, what);
        }
        Press("E");
        var coin = game.Objects.Single(o => o.Name == "Coin").Id;
        Press("C");
        Check(Parent(coin) == player.Id, "carry under Player");
        var held = World(coin);
        var mount = Matrix3x2.CreateScale(1.5f, .75f) * Matrix3x2.CreateRotation(float.DegreesToRadians(30))
            * Matrix3x2.CreateTranslation(56, -30);
        Same(held, mount * World(player.Id), "local rotation, scale and mount point");
        input.Key("Down", true);
        for (int i = 0; i < 8; i++) Frame();
        input.Key("Down", false); Frame();
        var moved = World(coin);
        Check(Vector2.Distance(held.Translation, moved.Translation) > 20, "carried displacement");
        Same(moved, mount * World(player.Id), "follow parent");
        CheckFootprint();
        Press("F5"); Press("C");
        Check(Parent(coin) == null, "drop to scene root");
        Same(World(coin), moved, "drop preserves full world transform");
        Press("F9"); Frame(); game = session.Current;
        Check(Parent(coin) == player.Id, "restore held parent");
        Same(World(coin), moved, "restore held pose"); CheckFootprint();
        Press("C"); Press("F5"); Press("C");
        Press("F9"); Frame(); game = session.Current;
        Check(Parent(coin) == null, "restore dropped root");
        Same(World(coin), moved, "restore dropped pose"); CheckFootprint();

        var originalLid = lid.WorldMatrix;
        var transformed = new LocalTransform2D(new(30, 40), 38, new(-1.7f, .6f), .15f);
        Check(game.SetLocalTransform(chest.Id, transformed), "mirrored parent pose");
        var oldWorld = World(lid.Id);
        Check(game.SetParent(lid.Id, player.Id), "reparent authored child");
        Same(World(lid.Id), oldWorld, "keep four world corners under mirrored scale");
        var localBefore = World(lid.Id) * Affine2D.Inverse(World(player.Id));
        Check(game.SetParent(lid.Id, chest.Id, false), "keep local pose");
        Same(World(lid.Id), localBefore * World(chest.Id), "keep-local corners");
        var beforeInvalid = World(chest.Id);
        Check(!game.SetParent(chest.Id, lid.Id) && !game.SetLocalTransform(chest.Id, transformed with { Scale = Vector2.Zero }),
            "reject cycle and singular scale");
        Same(World(chest.Id), beforeInvalid, "failed changes leave world intact");

        game.TrackForSave(lid.Id);
        Check(game.SetParent(lid.Id, null), "detach authored child");
        var detached = World(lid.Id);
        Press("F5"); Press("F9"); Frame(); game = session.Current;
        Check(Parent(lid.Id) == null, "explicit root in saved authored hierarchy");
        Same(World(lid.Id), detached, "restored mirrored basis");
        Same(lid.WorldMatrix, originalLid, "authored scene untouched");
        Check(lid.Parent == chest && !scene.Objects.Any(o => o.Id == coin), "authored parent and object list");
        Console.WriteLine("PASS runtime Transform: carry/drop, F5/F9, collision footprint, child corners, cycles and isolated scene");

        void CheckFootprint()
        {
            var filter = new PhysicsFilter(IncludeTriggers: true);
            foreach (var (p, expected) in new[] { (new Vector2(21, 11), true), (new Vector2(23, 11), false) })
            {
                var point = Vector2.Transform(p, moved);
                bool hit = game.Physics.Overlap(Shape2D.Circle(point, .1f), filter).Any(h => h.ObjectId == coin);
                Check(hit == expected, "rotated/scaled trigger edge");
            }
        }
    }
}
