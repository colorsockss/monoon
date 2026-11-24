using System.Drawing;
using System.Numerics;
using Engine.Core;

internal static class SpritePoolChecks
{
    [Check(2000)] static void Registered(CheckRun c) => Run(c.Repository);
    // Allocation-per-spawn control. Keep the same motion/color data as a pool slot,
    // publish the live object to an array and reuse the render buffer in both runs.
    private sealed class Bullet(Vector2 center, Vector2 velocity, Color tint)
    {
        public Vector2 Previous = center, Position = center;
        public readonly Vector2 Velocity = velocity;
        public readonly Color Tint = tint;
    }

    private sealed class Canvas : ICanvas2D
    {
        public readonly SpriteInstance2D[] Visible = new SpriteInstance2D[8];
        public int Count;
        public void Clear(Color color) { }
        public void FillRectangle(RectangleF bounds, Color color) { }
        public bool DrawSprite(string path, RectangleF bounds) => true;
        public void DrawSpriteInstances(SceneAppearance sprite, ReadOnlySpan<SpriteInstance2D> instances, Matrix3x2 view, bool batched)
        {
            instances.CopyTo(Visible);
            Count = instances.Length;
        }
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("Sprite pool: " + message);
        Console.WriteLine("PASS sprite pool: " + message);
    }

    public static void Run(string repo)
    {
        var field = new RectangleF(0, 0, 100, 100);
        var sprite = new SceneAppearance(SpriteAssets.SquarePath, Color.White);
        var pool = new SpritePool2D(3, new(4,4), sprite);
        var canvas = new Canvas();
        pool.TrySpawn(new(99, 20), new(2,0), Color.Red);
        pool.TrySpawn(new(50, 30), Vector2.Zero, Color.Blue);
        pool.TrySpawn(new(50, 40), Vector2.Zero, Color.Green);
        Check(!pool.TrySpawn(new(50, 50), Vector2.Zero, Color.White) && pool.Stats.Dropped == 1, "capacity rejects only the new spawn");
        pool.Advance(1, field);
        Check(pool.Stats.Active == 3, "partially visible sprite is retained at the edge");
        pool.Advance(1, field);
        pool.TrySpawn(new(20,60), new(0, 20), Color.Gold);
        pool.Draw(canvas,field,Matrix3x2.Identity,0);
        Check(pool.Stats is { Active:3, Returned:1, Reused:1, Spawned:4 } && canvas.Count == 3 &&
            canvas.Visible[0].Tint.ToArgb() == Color.Blue.ToArgb() && canvas.Visible[2].Bounds == new RectangleF(18,58,4,4),
            "reused slot resets interpolation and preserves spawn order");
        pool.Advance(1, field);
        pool.Draw(canvas,new RectangleF(12,65,20,10),Matrix3x2.Identity,.5f);
        Check(canvas.Count == 1 && canvas.Visible[0].Bounds == new RectangleF(18,68,4,4), "camera culling uses interpolated extents");

        void Cycle()
        {
            pool.TrySpawn(new(20,20),new(800,0),Color.White);
            pool.Draw(canvas,field,Matrix3x2.Identity,1);
            pool.Advance(1,field);
        }
        pool.Reset();
        for (int i=0; i<20000; i++) Cycle();
        long bytes=GC.GetAllocatedBytesForCurrentThread();
        for (int i=0; i<10000; i++) Cycle();
        bytes=GC.GetAllocatedBytesForCurrentThread()-bytes;
        Console.WriteLine($"Pool allocation probe: {bytes} bytes; {pool.Stats}");
        Check(bytes == 0 && pool.Stats is { Active:0, Spawned:30000, Returned:30000, Reused:29999 },
            "10,000 steady-state spawn, draw and return cycles allocate zero managed bytes");
        var live = new Bullet?[1];
        var drawList = new SpriteInstance2D[1];
        void UnpooledCycle()
        {
            var bullet = new Bullet(new(20,20), new(800,0), Color.White);
            live[0] = bullet;
            var bounds = new RectangleF(bullet.Position.X-2, bullet.Position.Y-2, 4, 4);
            drawList[0] = new(bounds, bullet.Tint);
            canvas.DrawSpriteInstances(sprite, drawList, Matrix3x2.Identity, true);
            bullet.Previous = bullet.Position;
            bullet.Position += bullet.Velocity;
            var displacement = bullet.Position - bullet.Previous;
            bounds.Offset(displacement.X, displacement.Y);
            if (!bounds.IntersectsWith(field)) live[0] = null;
        }
        for (int i = 0; i < 20000; i++) UnpooledCycle();
        long startBytes = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) UnpooledCycle();
        long unpooledBytes = GC.GetAllocatedBytesForCurrentThread() - startBytes;
        Console.WriteLine($"Unpooled allocation probe: {unpooledBytes} bytes for 10,000 new bullet objects.");
        Check(unpooledBytes > bytes && live[0] == null, "allocation-per-spawn control retires each offscreen bullet");
        var scene=SceneDocument.Load(Path.Combine(repo,"samples/BarrageLab/Scenes/Barrage.scene"));
        using var game=new GameSession(scene);
        void Tick() => game.FixedUpdate(game.Actions.Take(),1f/60);
        for (int i=0; i<480; i++) Tick();
        var a=game.Current.PoolStatistics(out var mode);
        Check(a.Active > 2000 && a.Reused > a.Capacity && a.Dropped == 0 && a.Spawned-a.Returned==a.Active && mode==0,
            "sample exceeds 2,000 live sprites and recycles more slots than capacity");
        game.Actions.Press("Space"); Tick(); game.Actions.Release("Space");
        for (int i=0; i<240; i++) Tick();
        var idle=game.Current.PoolStatistics(out _);
        Check(idle.Active==0 && idle.Spawned==idle.Returned,"stopping fire drains every projectile");
        game.Actions.Press("B");game.Actions.Press("R");Tick();game.Actions.Reset();
        for (int i=1; i<480; i++) Tick();
        Check(a==game.Current.PoolStatistics(out mode) && mode==1,"restart reproduces the same pattern and counts in individual mode");
    }
}
