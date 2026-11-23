using System.Numerics;
using Engine.Core;

internal static class TransformChecks
{
    internal static void Run(string root)
    {
        var scene = SceneDocument.Load(Path.Combine(root,"Scenes","KeyRoom.scene"));
        var chest = scene.Objects.Single(o => o.Name == "Chest");
        var lid = scene.Objects.Single(o => o.Name == "Chest Lid");
        // This fixture tests the authored (and reparented) basis without a clip driving it.
        if (lid.GetComponent<SpriteAnimatorComponent>() is {} animator) animator.PlayOnStart = false;
        Require(chest.ScaleX == 1 && chest.ScaleY == 1 && lid.WorldPosition == new Vector2(332,126),"legacy defaults");
        chest.Rotation = 90; chest.ScaleX = 2; chest.ScaleY = .5f; chest.PivotX = chest.PivotY = .5f;
        // Calculated directly: scale and rotate the lid origin around the chest's (17,10.5).
        Near(lid.WorldPosition,new(356.75f,107.5f),"lid inherits parent transform");
        Near(lid.LocalToWorld(new(34,0)),new(356.75f,175.5f),"lid width turns and scales");
        Near(lid.WorldToLocal(lid.LocalToWorld(new(17,3))),new(17,3),"round trip point");
        Require(!lid.ContainsWorldPoint(lid.LocalToWorld(new(-1,4))),"pick outside local bounds");
        chest.Rotation = 31;
        var wall = scene.Objects.Single(o => o.Name == "Wall");
        wall.Rotation = -23; wall.ScaleX = -1.4f; wall.ScaleY = .7f; wall.PivotX = .5f;
        var pose = lid.WorldMatrix;
        Require(scene.Reparent(lid,wall) && Math.Abs(lid.Shear) > .01f,"reparent requires shear");
        Same(pose,lid.WorldMatrix,"world corners after reparent");
        var duplicate = scene.DuplicateObject(lid);
        Same(pose,duplicate.WorldMatrix,"duplicate transform");
        var path = Path.Combine(root,"Scenes","TransformCheck.scene");
        scene.Save(path);
        var reopened = SceneDocument.Load(path);
        var saved = reopened.Objects.Single(o => o.Id == lid.Id);
        Require(saved.ParentId == wall.Id,"saved parent");
        Same(pose,saved.WorldMatrix,"saved world corners");
        var template = Path.Combine(root,"Assets","TransformCheck.prefab");
        reopened.ExportPrefab(saved,template);
        using var game = new SceneGame(reopened);
        Require(game.TryGetWorldMatrix(saved.Id,out var runtime),"Play instance");
        Same(pose,runtime,"Play world corners");
        var parent = reopened.Objects.Single(o => o.Id == wall.Id);
        var spawned = game.Instantiate(new(ProjectAssets.Scan(root).IdFor("Assets/TransformCheck.prefab")!.Value),new(-10,55),parent.Id);
        game.FixedUpdate(Vector2.Zero,1f/60);
        Require(game.TryGetWorldMatrix(spawned,out var spawnMatrix),"instantiated transformed prefab");
        Near(spawnMatrix.Translation,new(-10,55),"prefab position under rotated parent");
        Near(Vector2.TransformNormal(Vector2.UnitX,spawnMatrix),Vector2.TransformNormal(Vector2.UnitX,pose),"prefab axes");
        // Slender 45° trigger: an empty corner of its world AABB must not count as overlap.
        var physics = SceneDocument.Empty(new());
        var bar = new SceneObject { Width=100, Height=4, Rotation=45 };
        bar.AddComponent(new BoxCollider2DComponent { IsTrigger=true });
        var inside = new SceneObject { X=30,Y=31,Width=2,Height=2 };
        var outside = new SceneObject { X=4,Y=60,Width=2,Height=2 };
        foreach (var o in new[]{inside,outside}) o.AddComponent(new BoxCollider2DComponent());
        physics.Objects.AddRange([bar,inside,outside]);
        using var sim = new SceneGame(physics);
        var hits = new List<Guid>(); sim.TriggerEntered += (_,other) => hits.Add(other);
        sim.FixedUpdate(Vector2.Zero,1f/60);
        Require(hits.SequenceEqual([inside.Id]),"rotated trigger footprint");
        File.Delete(path); File.Delete(template);
        // Separate multiply and add on every CPU and build: these inputs round differently when fused.
        var m = new Matrix3x2(.1f, .7f, .1f, .9f, 5, 6); var q = new Vector2(.1f, .9f);
        float xx = .1f * .1f, xy = .1f * .9f, yx = .7f * .1f, yy = .9f * .9f;
        Require(Affine2D.Transform(q, m) == new Vector2(xx + xy + 5, yx + yy + 6) && MathF.FusedMultiplyAdd(.1f, .9f, xx) != xx + xy, "unfused transform");
        Require(Affine2D.Lerp(new Vector2(.1f, 0), new(.1f, 0), .1f).X == xy + xx && Affine2D.Lerp(new Vector3(.1f), new(.1f), .1f).X == xy + xx
            && Affine2D.Lerp(new Vector4(.1f), new(.1f), .1f).W == xy + xx, "unfused lerp");
        Console.WriteLine("PASS affine hierarchy, pivot, nonuniform mirrored reparent, world corners, YAML/Play/prefab, transformed trigger and unfused transform");
    }
    private static void Require(bool ok,string name) { if (!ok) throw new Exception("Transform 2D: "+name); }
    private static void Near(Vector2 a,Vector2 b,string name) => Require(Vector2.Distance(a,b)<.001f,$"{name}: {a} / {b}");
    private static void Same(Matrix3x2 a,Matrix3x2 b,string name)
    {
        foreach (var p in new[]{Vector2.Zero,new Vector2(34,0),new Vector2(0,8),new Vector2(34,8)})
            Near(Vector2.Transform(p,a),Vector2.Transform(p,b),name);
    }
}
