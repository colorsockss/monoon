using System.Globalization;
using Engine.Core;

/// <summary>Reads a development player's MONOON_FRAME_PROFILE capture and prints one frame's call tree.</summary>
internal static class FrameProfileReport
{
    private sealed record Row(long Frame, int Node, int Parent, string Name, int Calls, double Total, double Self, long Alloc, int Gen0, int Dropped);

    public static void Print(string path, long? pick)
    {
        using var reader = new StreamReader(path);
        if (reader.ReadLine() != FrameProfileWriter.Header) throw new InvalidDataException($"{path}: not a MoNoon frame profile.");
        var frames = new Dictionary<long, List<Row>>();
        var I = CultureInfo.InvariantCulture;
        while (reader.ReadLine() is { Length: > 0 } line)
        {
            var c = line.Split(',');
            if (c.Length != 11) throw new InvalidDataException($"{path}: malformed row '{line}'.");
            var row = new Row(long.Parse(c[0], I), int.Parse(c[1], I), int.Parse(c[2], I), c[4], int.Parse(c[5], I),
                double.Parse(c[6], I) / 1000, double.Parse(c[7], I) / 1000,
                c[8].Length > 0 ? long.Parse(c[8], I) : 0, c[9].Length > 0 ? int.Parse(c[9], I) : 0, c[10].Length > 0 ? int.Parse(c[10], I) : 0);
            if (!frames.TryGetValue(row.Frame, out var rows)) frames[row.Frame] = rows = [];
            rows.Add(row);
        }
        if (frames.Count == 0) throw new InvalidDataException($"{path}: no frames.");
        var slowest = frames.Values.OrderByDescending(r => r[0].Total).ToList();
        var times = frames.Values.Select(r => r[0].Total).Order().ToList();
        Console.WriteLine(string.Create(I, $"{frames.Count} frames · mean {times.Average():F2} ms · p95 {times[(int)Math.Ceiling(times.Count * .95) - 1]:F2} ms · max {times[^1]:F2} ms"));
        Console.WriteLine("Slowest: " + string.Join("  ", slowest.Take(5).Select(r => string.Create(I, $"#{r[0].Frame} {r[0].Total:F2} ms"))));
        var chosen = pick is long n ? frames.GetValueOrDefault(n) ?? throw new InvalidDataException($"Frame {n} is not in the capture.") : slowest[0];
        var root = chosen[0];
        Console.WriteLine();
        Console.WriteLine(string.Create(I, $"Frame {root.Frame} · {root.Total:F3} ms · alloc {root.Alloc / 1024d:F1} KiB · gen0 {root.Gen0}{(root.Dropped > 0 ? $" · {root.Dropped} samples dropped" : "")}"));
        Console.WriteLine($"{"Calls",9} {"Total ms",10} {"Self ms",10}  Work");
        var children = chosen.ToLookup(r => r.Parent);
        void Show(Row r, int depth)
        {
            Console.WriteLine(string.Create(I, $"{r.Calls,9:N0} {r.Total,10:F3} {r.Self,10:F3}  {new string(' ', depth * 2)}{r.Name}"));
            foreach (var child in children[r.Node].OrderByDescending(x => x.Total)) Show(child, depth + 1);
        }
        Show(root, 0);
    }
}
