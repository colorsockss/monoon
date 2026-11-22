using System.Drawing;
using System.Globalization;
using System.Numerics;
using VYaml.Annotations;

namespace Engine.Core;

[YamlObject]
public sealed partial record ComponentKey(double Time, string Value);

[YamlObject]
public sealed partial record ComponentTrack(string Property, ComponentKey[] Keys);

public readonly record struct ComponentPose(float? Rotation = null, float? ScaleX = null, float? ScaleY = null,
    Color? Tint = null, float? Opacity = null)
{
    public Color Paint(Color source) => Color.FromArgb(Opacity is float alpha ? (int)MathF.Round(255 * alpha) : source.A, Tint ?? source);
}

public sealed class ComponentCurves
{
    public static IReadOnlyList<string> Properties { get; } = Array.AsReadOnly(new[] { "Rotation", "ScaleX", "ScaleY", "Tint", "Opacity" });
    private sealed record Curve(string Property, double[] Times, Vector3[] Values);
    private readonly Curve[] curves;

    public ComponentCurves(ComponentTrack[]? tracks, double duration)
    {
        curves = new Curve[tracks?.Length ?? 0];
        var seen = new HashSet<string>();
        for (int i = 0; i < curves.Length; i++)
        {
            var track = tracks![i];
            if (track == null || !Properties.Contains(track.Property) || !seen.Add(track.Property) || track.Keys is not { Length: > 0 })
                throw new InvalidDataException("Expected a unique component property with at least one key.");
            var times = new double[track.Keys.Length];
            var values = new Vector3[times.Length];
            for (int j = 0; j < times.Length; j++)
            {
                var key = track.Keys[j];
                if (key == null || !double.IsFinite(key.Time) || key.Time < 0 || key.Time > duration || j > 0 && key.Time <= times[j-1])
                    throw new InvalidDataException($"{track.Property}: key times must increase within the clip duration.");
                times[j] = key.Time;
                if (track.Property == "Tint")
                {
                    if (!SceneObject.TryColor(key.Value, out var rgb)) throw new InvalidDataException("Tint needs #RRGGBB.");
                    values[j] = new(rgb.R, rgb.G, rgb.B);
                }
                else
                {
                    if (!float.TryParse(key.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) || !float.IsFinite(v))
                        throw new InvalidDataException($"{track.Property}: enter a finite number.");
                    if (track.Property == "Opacity" && v is < 0 or > 1) throw new InvalidDataException("Opacity must be between 0 and 1.");
                    if (track.Property is "ScaleX" or "ScaleY" && (v == 0 || j > 0 && Math.Sign(v) != Math.Sign(values[j-1].X)))
                        throw new InvalidDataException("Scale keys must stay on the same side of zero.");
                    values[j] = new(v,0,0);
                }
            }
            curves[i] = new(track.Property,times,values);
        }
    }

    public ComponentPose Sample(double time)
    {
        ComponentPose result = default;
        foreach (var curve in curves)
        {
            int i = Array.BinarySearch(curve.Times,time);
            Vector3 value;
            if (i >= 0) value = curve.Values[i];
            else
            {
                int next = ~i;
                if (next == 0) value = curve.Values[0];
                else if (next == curve.Values.Length) value = curve.Values[^1];
                else
                {
                    float t = (float)((time-curve.Times[next-1]) / (curve.Times[next]-curve.Times[next-1]));
                    value = Vector3.Lerp(curve.Values[next-1],curve.Values[next],t);
                }
            }
            result = curve.Property switch
            {
                "Rotation" => result with { Rotation = value.X },
                "ScaleX" => result with { ScaleX = value.X },
                "ScaleY" => result with { ScaleY = value.X },
                "Opacity" => result with { Opacity = value.X },
                _ => result with { Tint = Color.FromArgb((int)MathF.Round(value.X),(int)MathF.Round(value.Y),(int)MathF.Round(value.Z)) }
            };
        }
        return result;
    }
}
