// Physics tick cost, headless. Prints the median and range of the frame, Physics.Contacts, Physics.Rigidbodies and
// allocated bytes over 300 ticks after 60 warm-up ticks.
//   static: 10,000 static colliders (100 columns of equal Left) and one Rigidbody 2D, so the Box2D world exists and
//           SyncPhysics visits every static collider each tick.
//   tiles <repository>: 1,000 static colliders resting on a 64×64 solid tile layer (KeyRoomTiles tileset), so every
//           collider's contact search builds tile shapes each tick.
using System.Numerics;
using Engine.Core;

const int Warmup = 60, Ticks = 300;
var scene = SceneDocument.Empty(new());
int next = 1;
SceneObject Box(float x, float y)
{
    var box = new SceneObject { Id = new Guid(next++, 0, 0, new byte[8]), X = x, Y = y, Width = 10, Height = 10 };
    box.AddComponent(new BoxCollider2DComponent());
    scene.Objects.Add(box);
    return box;
}
if (args is ["tiles", var repository])
{
    // The KeyRoom showcase's solid brick tiles (the KeyRoomTiles scene with its "Wall" map was retired, 2026-11-26).
    var root = Path.Combine(Path.GetFullPath(repository), "samples", "KeyRoom");
    var tileset = SceneDocument.Load(Path.Combine(root, "Scenes", "Showcase.scene")).Objects.Single(o => o.Name == "Wall face")
        .GetComponent<Tilemap2DComponent>()!.Tileset;
    scene.ProjectDirectory = root;
    var map = new Tilemap2DComponent { Tileset = tileset, CellSize = new(16, 16) };
    for (int x = 0; x < 64; x++)
        for (int y = 0; y < 64; y++) map.Set(new(x, y), 1);
    var layer = new SceneObject { Id = new Guid(next++, 0, 0, new byte[8]) };
    layer.AddComponent(map);
    scene.Objects.Add(layer);
    for (int i = 0; i < 1000; i++) Box(i % 40 * 25 + 3, i / 40 * 40 + 3);
}
else
{
    for (int x = 0; x < 100; x++)
        for (int y = 0; y < 100; y++) Box(x * 16, y * 16);
    Box(-200, 0).AddComponent(new Rigidbody2DComponent { Gravity = 0 });
}

using var game = new SceneGame(scene);
var profiler = new FrameProfiler(Ticks);
profiler.Install();
for (int i = 0; i < Warmup + Ticks; i++)
{
    if (i == Warmup) profiler.Clear();
    game.FixedUpdate(Vector2.Zero, 1f / 60);
    profiler.EndFrame();
}
profiler.Uninstall();

var columns = new Dictionary<string, List<double>> { ["Frame"] = [], ["Physics.Contacts"] = [], ["Physics.Rigidbodies"] = [] };
var bytes = new List<double>();
for (int f = 0; f < profiler.Count; f++)
{
    var frame = profiler.At(f);
    var sums = columns.Keys.ToDictionary(k => k, _ => 0.0);
    for (int n = 0; n < frame.NodeCount; n++)
        if (sums.ContainsKey(frame.Name(n))) sums[frame.Name(n)] += frame.InclusiveMilliseconds(n);
    foreach (var (k, v) in sums) columns[k].Add(v);
    bytes.Add(frame.AllocatedBytes);
}
foreach (var (k, v) in columns) Console.WriteLine($"{k}: {Stat(v, "ms", 4)}");
Console.WriteLine($"Allocated: {Stat(bytes, "B/frame", 0)}");

static string Stat(List<double> v, string unit, int digits)
{
    v.Sort();
    return $"median {Math.Round(v[v.Count / 2], digits)} {unit} (min {Math.Round(v[0], digits)}, max {Math.Round(v[^1], digits)})";
}
