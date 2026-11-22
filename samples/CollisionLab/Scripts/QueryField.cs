using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Engine.Core;

public sealed class QueryField : GameBehaviour
{
    public int ColliderCount;
    public Guid Status, Detail;
    private bool measured;
    private const int Paths = 96, Passes = 6;
    private const int QueryCount = Paths * 3;
    private enum Kind { Overlap, Raycast, Sweep }
    private readonly record struct Query(Kind Kind, Shape2D Shape, Vector2 Origin, Vector2 Direction, float Distance, PhysicsFilter Filter);
    private readonly record struct Answer(IReadOnlyList<PhysicsTarget> Targets, PhysicsHit? Hit)
    {
        public bool Matches(Answer other)
        {
            if (Hit != other.Hit || Targets.Count != other.Targets.Count) return false;
            for (int i = 0; i < Targets.Count; i++) if (Targets[i] != other.Targets[i]) return false;
            return true;
        }
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        if (measured && !tick.Input.Pressed("RepeatQueries")) return;
        measured = true;
        var physics = tick.Objects.Physics;
        var diagnostics = physics.QueryDiagnostics;
        bool previous = diagnostics.UseSpatialIndex;
        try { Compare(tick, physics); }
        finally { diagnostics.UseSpatialIndex = previous; }
    }

    private static Query[] Corpus()
    {
        // Identical coordinates and filters in both field sizes. Some paths miss the field.
        uint seed = 88;
        int Next(int bound) { seed = unchecked(seed * 1664525u + 1013904223u); return (int)((seed >> 8) % (uint)bound); }
        var queries = new Query[QueryCount];
        for (int i = 0; i < Paths; i++)
        {
            var center = new Vector2((Next(48) - 5) * 32 + Next(24), (Next(30) - 4) * 32 + 192 + Next(24));
            var travel = new Vector2(48 + Next(128), Next(89) - 44);
            if (i % 3 == 0) travel.X = -travel.X;
            var filter = new PhysicsFilter(i % 4 == 0 ? 1u << 2 : uint.MaxValue, i % 3 != 0);
            // The first pair occupies one box. Exercise equal-fraction tie breaking and ordered overlaps.
            if (i == 0) { center = new(-176, 200); travel = new(48, 0); filter = new(uint.MaxValue, true); }
            var overlap = i % 2 == 0 ? Shape2D.Box(new RectangleF(center.X-48,center.Y-32,96,64)) : Shape2D.Circle(center,36);
            var circle = Shape2D.Circle(center, 6);
            queries[3*i] = new(Kind.Overlap, overlap, center, travel, 0, filter);
            queries[3*i+1] = new(Kind.Raycast, circle, center, travel, travel.Length(), filter);
            queries[3*i+2] = new(Kind.Sweep, circle, center, travel, 0, filter);
        }
        return queries;
    }

    private static Answer Execute(IPhysics2D physics, Query q) => q.Kind switch
    {
        Kind.Overlap => new(physics.Overlap(q.Shape, q.Filter), null),
        Kind.Raycast => new(Array.Empty<PhysicsTarget>(), physics.Raycast(q.Origin, q.Direction, q.Distance, q.Filter)),
        _ => new(Array.Empty<PhysicsTarget>(), physics.Sweep(q.Shape, q.Direction, q.Filter))
    };

    private void Compare(ScriptTick tick, IPhysics2D physics)
    {
        var queries = Corpus();
        var expected = new Answer[QueryCount];
        var metrics = new[] { new Measurements(), new Measurements() };
        var diag = physics.QueryDiagnostics;
        diag.UseSpatialIndex = false;
        for (int i = 0; i < queries.Length; i++) expected[i] = Execute(physics, queries[i]);
        diag.UseSpatialIndex = true;
        for (int i = 0; i < queries.Length; i++) RequireEqual(i, Execute(physics, queries[i]));

        for (int pass = 0; pass < Passes; pass++)
            for (int half = 0; half < 2; half++)
            {
                int mode = (pass + half) % 2;
                diag.UseSpatialIndex = mode == 1;
                for (int i = 0; i < queries.Length; i++)
                {
                    long start = Stopwatch.GetTimestamp();
                    var answer = Execute(physics, queries[i]);
                    long elapsed = Stopwatch.GetTimestamp() - start;
                    metrics[mode].Add(queries[i].Kind, elapsed, diag.LastColliderCandidates);
                    RequireEqual(i, answer);
                }
            }

        string digest = TargetDigest(expected);
        for (int k = 0; k <= 3; k++)
        {
            string kind = k == 3 ? "All" : ((Kind)k).ToString();
            var a = metrics[0].Summary(k);
            var b = metrics[1].Summary(k);
            tick.Session.Log(FormattableString.Invariant($"SPATIAL count={ColliderCount} kind={kind} n={a.Count} linearCandidates={a.Candidates} gridCandidates={b.Candidates} linearP95us={a.P95:F3} gridP95us={b.P95:F3} sha256={digest}"));
            if (k != 3) continue;
            tick.Ui.SetText(Status, $"Matched {QueryCount} / {QueryCount} queries across {Passes} passes");
            tick.Ui.SetText(Detail, FormattableString.Invariant($"Candidates/query  {a.Candidates/(double)a.Count:F1} -> {b.Candidates/(double)b.Count:F1}       p95  {a.P95:F1} -> {b.P95:F1} us"));
        }
        void RequireEqual(int i, Answer answer)
        {
            if (!expected[i].Matches(answer))
                throw new InvalidOperationException($"Spatial index changed query {i} ({queries[i].Kind}) in the {ColliderCount} collider field.");
        }
    }

    private sealed class Measurements
    {
        private readonly List<long>[] times = { new(), new(), new(), new() };
        private readonly long[] candidates = new long[4];
        public void Add(Kind kind, long elapsed, int visited)
        {
            times[(int)kind].Add(elapsed); times[3].Add(elapsed);
            candidates[(int)kind] += visited; candidates[3] += visited;
        }
        public (int Count, long Candidates, double P95) Summary(int kind)
        {
            var sorted = times[kind];
            sorted.Sort();
            long p95 = sorted[(int)Math.Ceiling(sorted.Count * .95) - 1];
            return (sorted.Count, candidates[kind], p95 * 1_000_000d / Stopwatch.Frequency);
        }
    }

    private static string TargetDigest(Answer[] results)
    {
        var text = new StringBuilder();
        foreach (var result in results)
        {
            foreach (var target in result.Targets) text.Append(target.ObjectId.ToString("N")).Append(',');
            if (result.Hit is {} hit) text.Append(hit.Target.ObjectId.ToString("N"));
            text.Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(text.ToString())));
    }
}
