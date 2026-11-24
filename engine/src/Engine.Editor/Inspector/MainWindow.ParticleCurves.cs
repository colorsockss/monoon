using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    // 13.15: the emitter's .particles lifetime curves and colour gradient as Inspector rows, with the Particle panel's
    // CurveField and GradientField (the drawn line is the runtime ParticleLut table). A committed change re-reads the file,
    // applies only that field and writes it once: one Undo step, and never an older copy over the Particle panel's edits.
    private void ParticleCurveRows(ParticleEmitter2DComponent emitter, StackPanel fields)
    {
        if (project is not { } root || emitter.Particles is not Guid id || InspectorAssets(root).PathFor(id) is not { } relative) return;
        ParticleEffect fx;
        try { fx = ParticleEffect.Load(Path.Combine(root, relative)); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { return; }
        if (fx is { SizeOverLifetime: null, ColorOverLifetime: null, RotationSpeed: null, VelocityX: null, VelocityY: null }) return;
        var header = new TextBlock { Text = "Over lifetime · " + Path.GetFileName(relative), FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 4, 0, 0), Classes = { "fieldHeader" } };
        ToolTip.SetTip(header, relative);
        fields.Children.Add(header);
        void Curve(string label, ParticleCurve? curve, float low, float high, decimal step, Func<ParticleEffect, ParticleCurve, ParticleEffect> set)
        {
            if (curve == null) return;
            var field = new CurveField(label, curve, low, high, step);
            field.Changed += (c, commit) => { if (commit) WriteParticles(root, relative, e => set(e, c)); };
            fields.Children.Add(field);
        }
        Curve("Size", fx.SizeOverLifetime, 0, 100, .1m, (e, c) => e with { SizeOverLifetime = c });
        if (fx.ColorOverLifetime is { } gradient)
        {
            var field = new GradientField("Gradient", gradient);
            field.Changed += (g, commit) => { if (commit) WriteParticles(root, relative, e => e with { ColorOverLifetime = g }); };
            fields.Children.Add(field);
        }
        Curve("Spin °/s", fx.RotationSpeed, -36000, 36000, 10, (e, c) => e with { RotationSpeed = c });
        Curve("Vel X", fx.VelocityX, -16384, 16384, 5, (e, c) => e with { VelocityX = c });
        Curve("Vel Y", fx.VelocityY, -16384, 16384, 5, (e, c) => e with { VelocityY = c });
    }

    private void WriteParticles(string root, string relative, Func<ParticleEffect, ParticleEffect> change)
    {
        string path = Path.Combine(root, relative);
        try
        {
            var next = change(ParticleEffect.Load(path));
            next.Validate(relative);
            var bytes = ProjectYaml.Encode(next);
            var before = PrefabFileEdit.Read(path);
            if (before.Document is { } old && old.AsSpan().SequenceEqual(bytes)) return;
            ProjectYaml.WriteBytes(path, bytes);
            void Shown() { if (particlePanel.AssetPath == relative) particlePanel.Open(relative); }
            RecordEdit(Selected?.Id, asset: new ParticleAssetEdit(new PrefabFileEdit(path, before, PrefabFileEdit.Read(path)), () => { Shown(); RefreshComponentList(); }));
            Shown();
            Status.Text = $"Saved {relative}.";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { Status.Text = ex.Message; }
    }
}
