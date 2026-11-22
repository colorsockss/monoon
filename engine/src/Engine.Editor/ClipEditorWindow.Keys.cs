using System.Globalization;
using Engine.Core;

namespace Engine.Editor;

internal partial class ClipEditorWindow
{
    private void RefreshKey()
    {
        if (KeyProperty.SelectedItem is not string property) return;
        var track = tracks.FirstOrDefault(t => t.Property == property);
        Track.SetKeys(track?.Keys.Select(k => k.Time).ToArray() ?? []);
        var exact = track?.Keys.FirstOrDefault(k => k.Time == position);
        KeyTime.Text = exact != null ? exact.Time.ToString("R", CultureInfo.InvariantCulture)
            : position.ToString("0.#########", CultureInfo.InvariantCulture);
        var pose = curves?.Sample(position) ?? default;
        KeyValue.Text = exact?.Value ?? (property switch
        {
            "Tint" => pose.Tint is {} color ? $"#{color.R:X2}{color.G:X2}{color.B:X2}" : "#FFFFFF",
            "Rotation" => (pose.Rotation ?? 0).ToString("G7",CultureInfo.InvariantCulture),
            "ScaleX" => (pose.ScaleX ?? 1).ToString("G7",CultureInfo.InvariantCulture),
            "ScaleY" => (pose.ScaleY ?? 1).ToString("G7",CultureInfo.InvariantCulture),
            _ => (pose.Opacity ?? 1).ToString("G7",CultureInfo.InvariantCulture)
        });
    }

    private void EditKey(bool remove)
    {
        if (KeyProperty.SelectedItem is not string property) return;
        if (!double.TryParse(KeyTime.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double at) || !double.IsFinite(at))
        {
            StatusLine.Text = "Enter a key time in seconds.";
            return;
        }
        string value = (KeyValue.Text ?? "").Trim();
        var old = tracks.FirstOrDefault(t => t.Property == property);
        var keys = old?.Keys.Where(k => k.Time != at).ToList() ?? [];
        if (remove && (old == null || old.Keys.Length == keys.Count)) { StatusLine.Text = "No key at this time."; return; }
        if (!remove) keys.Add(new(at,value));
        var next = tracks.Where(t => t != old).ToList();
        if (keys.Count != 0) next.Add(new(property,keys.OrderBy(k => k.Time).ToArray()));
        var replacement = next.ToArray();
        try
        {
            // Allow each offending key to be removed after the clip was shortened.
            if (!remove) _ = new ComponentCurves(replacement,axis.Duration);
        }
        catch (InvalidDataException ex) { StatusLine.Text = ex.Message; return; }
        Change(() => tracks = replacement);
        Seek(at);
    }
}
