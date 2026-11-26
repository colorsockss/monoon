using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using Engine.Core;

// CollisionLab showcase. The lantern drifts over a night field. L streams 10,000 stone colliders in with Session.AddSceneAsync
// (worker threads read the scene, each update spends at most 0.5 ms creating objects) while the game keeps
// running and the bar shows the operation's progress. Then every fixed step asks one overlap query for the stones inside the
// lantern's circle twice, through the spatial grid and as a full scan; the grid's answer is lit, and the panel compares how many
// colliders each checked, how long each took, and whether both answers are the same. Raises "loaded", "grid", "match".
// Each stone look is a group Stone<k> whose dim and lit sprites are Dim[k] and Lit[k] (cells of one atlas, so swaps keep one batch).
public sealed class LanternQuery : GameBehaviour
{
    public SceneReference Stones;
    public Guid Lantern, Hits, GridChecked, GridTime, ScanChecked, ScanTime, Verdict, LoadPanel, LoadText, LoadFill;
    public SpriteReference[] Dim = [], Lit = [];
    private const float Radius = 38, BarWidth = 325;
    private const int Window = 20;   // fixed steps averaged per readout
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private readonly PhysicsFilter filter = new();
    private readonly Dictionary<Guid, int> kinds = new();
    private HashSet<Guid> lit = new(), next = new();
    private SceneOperation? loading;
    private float time, shownProgress = -1, sinceLoaded;
    private int total, steps, hits;
    private long gridTicks, scanTicks, gridChecked, scanChecked;
    private bool mismatch;

    public override void FixedUpdate(ScriptTick tick)
    {
        time += tick.DeltaTime;
        var center = new Vector2(400 + 280 * MathF.Sin(time * .41f), 250 + 75 * MathF.Sin(time * .67f + 1));   // clear of the HUD panels
        tick.Objects.SetLocalTransform(Lantern, new LocalTransform2D(center - new Vector2(40), 0, Vector2.One));

        if (loading == null && total == 0 && tick.Input.Pressed("LoadStones"))
        {
            tick.Session.SceneLoadBudgetMilliseconds = .5;   // a small slice per frame, so the stream spans many frames
            loading = tick.Session.AddSceneAsync(Stones.Id);
        }
        if (loading != null) Track(tick);
        if (total == 0) return;

        sinceLoaded += tick.DeltaTime;
        if (sinceLoaded > 1.2f) tick.Session.Set("grid");
        if (sinceLoaded > 2.6f) tick.Session.Set("match");
        Query(tick, center);
    }

    private void Track(ScriptTick tick)
    {
        var op = loading!;
        float progress = op.Status == SceneStatus.Completed ? 1 : op.Progress;
        if (progress != shownProgress)
        {
            shownProgress = progress;
            tick.Ui.SetText(LoadText, $"Loading 10,000 colliders   {progress * 100:0}%");
            tick.Objects.Tween(LoadFill, TweenClock.Ui).UiRect(new RectangleF(0, 0, progress * BarWidth, 0), .08f, TweenEase.Linear);
        }
        if (!op.IsDone) return;
        loading = null;
        if (op.Status != SceneStatus.Completed) { tick.Ui.SetText(LoadText, op.Error ?? "Loading stopped."); return; }
        for (int k = 0; k < Math.Min(Dim.Length, Lit.Length); k++)
            foreach (var id in tick.Objects.FindByGroup($"Stone{k}")) kinds[id] = k;
        total = kinds.Count;
        tick.Ui.SetVisible(LoadPanel, false);
        tick.Session.Set("loaded");
    }

    private void Query(ScriptTick tick, Vector2 center)
    {
        var physics = tick.Objects.Physics;
        var diagnostics = physics.QueryDiagnostics;
        var shape = Shape2D.Circle(center, Radius);
        long start = Stopwatch.GetTimestamp();
        var grid = physics.Overlap(shape, filter);
        gridTicks += Stopwatch.GetTimestamp() - start;
        gridChecked += diagnostics.LastColliderCandidates;
        diagnostics.UseSpatialIndex = false;
        try
        {
            start = Stopwatch.GetTimestamp();
            var scan = physics.Overlap(shape, filter);
            scanTicks += Stopwatch.GetTimestamp() - start;
            scanChecked += diagnostics.LastColliderCandidates;
            mismatch |= !Same(grid, scan);
        }
        finally { diagnostics.UseSpatialIndex = true; }
        hits += grid.Count;
        Light(tick.Objects, grid);
        if (++steps < Window) return;

        double ms = 1000d / Stopwatch.Frequency / steps;
        tick.Ui.SetText(Hits, $"{hits / steps} stones inside the lantern");
        tick.Ui.SetText(GridChecked, string.Create(Invariant, $"{gridChecked / steps:N0} checked"));
        tick.Ui.SetText(ScanChecked, string.Create(Invariant, $"{scanChecked / steps:N0} checked"));
        tick.Ui.SetText(GridTime, string.Create(Invariant, $"{gridTicks * ms:0.000} ms"));
        tick.Ui.SetText(ScanTime, string.Create(Invariant, $"{scanTicks * ms:0.000} ms"));
        tick.Ui.SetText(Verdict, mismatch ? "<color=#FF8A80>Answers differ</color>" : "Both answers match, stone for stone");
        steps = hits = 0;
        gridTicks = scanTicks = gridChecked = scanChecked = 0;
    }

    private static bool Same(IReadOnlyList<PhysicsTarget> a, IReadOnlyList<PhysicsTarget> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
        return true;
    }

    // Swaps sprites only for stones that entered or left the circle since the last step.
    private void Light(IGameObjects objects, IReadOnlyList<PhysicsTarget> inside)
    {
        next.Clear();
        foreach (var target in inside)
        {
            if (!kinds.TryGetValue(target.ObjectId, out int kind)) continue;
            next.Add(target.ObjectId);
            if (!lit.Contains(target.ObjectId)) objects.SetSprite(target.ObjectId, Lit[kind]);
        }
        foreach (var id in lit)
            if (!next.Contains(id)) objects.SetSprite(id, Dim[kinds[id]]);
        (lit, next) = (next, lit);
    }
}
