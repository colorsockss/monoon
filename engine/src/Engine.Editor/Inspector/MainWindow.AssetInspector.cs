using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Engine.Core;
using Engine.Desktop;

namespace Engine.Editor;

// Asset Inspector (roadmap 13.16): clicking a file in Project (released without a drag) shows its summary here, a PNG's
// import settings, and a button for the panel that edits it. Selecting an object takes the Inspector back. A locked
// Inspector keeps its object. Import setting changes are one Undo record with the files they write, and reach the Scene
// through the 8.2 reimport path (the sprite monitor), and builds through the saved files.
public partial class MainWindow
{
    private string? inspectedAsset;
    private bool InspectingAsset => inspectedAsset != null;

    private void InspectAsset(string relative)
    {
        if (project == null || lockedObject != null || Viewport.IsPlaying) return;
        inspectedAsset = relative;
        RefreshAssetInspector();
        RefreshInspector();
    }

    private void RefreshAssetInspector()
    {
        AssetInspector.Children.Clear();
        if (inspectedAsset is not { } relative || project is not { } root) return;
        var full = Path.Combine(root, relative);
        var summary = new StackPanel { Spacing = 2 };
        AssetInspector.Children.Add(new ComponentSection { Header = Path.GetFileName(relative), Icon = EditorTokens.AssetIcon(relative, false),
            HasSettings = false, Foldout = new(), Content = summary });
        // Read-only rows keep the 24px input height, so summary and settings rows share one row grid.
        void Row(StackPanel fields, string label, string value) { InspectorRow(fields, label, ValueLabel(value)); ((Grid)fields.Children[^1]).MinHeight = EditorTokens.InputHeight; }
        if (!File.Exists(full)) { Row(summary, "Missing", relative); return; }
        var assets = ProjectAssets.Scan(root);
        var id = assets.IdFor(relative);
        Row(summary, "Path", relative);
        if (id is Guid guid) Row(summary, "ID", guid.ToString());
        Row(summary, "File", $"{new FileInfo(full).Length / 1024.0:0.#} KB");
        string open = "Open";
        try
        {
            if (SpriteAssets.IsPng(relative))
            {
                var size = SpriteRegion.ReadPngSize(full);
                Row(summary, "Size", $"{size.Width} × {size.Height} px");
                if (ProjectAssets.ReadSheetSlices(full) is { Length: > 0 } cells) Row(summary, "Cells", cells.Length.ToString(CultureInfo.InvariantCulture));
                int used = Scene.Objects.Count(o => o.GetComponent<SpriteRendererComponent>()?.AssetId == id);
                Row(summary, "Used", $"{used} object{(used == 1 ? "" : "s")} in this scene");
                if (id is Guid png) AssetInspector.Children.Add(PngImportSettings(root, relative, png, assets));
                open = assets.Sprites(root).Find(id) != null ? "Edit Atlas…" : "";
            }
            else if (relative.EndsWith(ProjectAssets.Clip, StringComparison.Ordinal))
            {
                var clip = SpriteClip.OpenForEditing(full);
                Row(summary, "Frames", clip.Frames.Length.ToString(CultureInfo.InvariantCulture));
                Row(summary, "Length", $"{clip.Frames.Sum(f => f.Seconds):0.###} s" + (clip.Loop ? " · loop" : ""));
                if (clip.Tracks is { Length: > 0 } tracks) Row(summary, "Tracks", tracks.Length.ToString(CultureInfo.InvariantCulture));
                open = "Open Clip Editor";
            }
            else if (relative.EndsWith(ProjectAssets.Data, StringComparison.Ordinal))
            {
                var data = GameData.Load(full);
                Row(summary, "Fields", data.Values.Count.ToString(CultureInfo.InvariantCulture));
                Row(summary, "Keys", data.Values.Count == 0 ? "(none)" : string.Join(", ", data.Values.Keys.Take(8)) + (data.Values.Count > 8 ? ", …" : ""));
                open = "Edit Data…";
            }
            else if (relative.EndsWith(ProjectAssets.Prefab, StringComparison.Ordinal))
            {
                var source = SceneDocument.OpenPrefabSource(root, relative).Document;
                var top = source.Objects.Single(o => o.Parent == null);
                Row(summary, "Root", top.Name);
                Row(summary, "Objects", source.Objects.Count.ToString(CultureInfo.InvariantCulture));
                Row(summary, "Components", top.Components.Length == 0 ? "(none)" : string.Join(", ", top.Components.Select(ComponentName)));
                Row(summary, "Instances", $"{Scene.Objects.Count(o => o.Prefab?.AssetId == id)} in this scene");
                open = "Open Prefab";
            }
            else if (relative.EndsWith(ParticleEffect.Extension, StringComparison.Ordinal))
            {
                var fx = ParticleEffect.Load(full);
                Row(summary, "Particles", $"max {fx.MaxParticles} · {fx.Rate:0.##}/s");
                Row(summary, "Duration", $"{fx.Duration:0.##} s" + (fx.Looping ? " · loop" : ""));
                Row(summary, "Texture", fx.Texture is Guid texture ? assets.PathFor(texture) ?? texture.ToString() : "(square)");
                open = "Open Particle Panel";
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            summary.Children.Add(new TextBlock { Text = ex.Message, TextWrapping = TextWrapping.Wrap, Classes = { "error" } });
        }
        if (open.Length == 0) return;
        var button = new Button { Content = open, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 4, 0, 0) };
        AutomationProperties.SetName(button, open);
        button.Click += (_, _) => OpenEntry(new ProjectEntry(relative, false, Root: root));
        AssetInspector.Children.Add(button);
    }

    private static string ComponentName(SceneComponent c) => c is ScriptComponent s ? s.Script
        : ComponentChoice.Registered.TryGetValue(c.GetType(), out var choice) ? choice.Name : c.GetType().Name;

    // Import settings of a PNG: pivot (the .meta), the sampling the game uses, and the atlas it is packed into.
    private Control PngImportSettings(string root, string relative, Guid id, ProjectAssets assets)
    {
        var fields = new StackPanel { Spacing = 2 };
        var meta = ProjectYaml.Read<AssetMeta>(Path.Combine(root, relative + ".meta"));
        Control Axis(string axis, string name, float value, Func<float, (float, float)> pivot)
        {
            var box = FieldRows.Number(name, value, 0, 1, .05m, (v, commit) => { if (commit) RunAction(() => { SetPngPivot(relative, pivot(v)); return Task.CompletedTask; }); });
            var pair = new Grid { ColumnDefinitions = new("12,*"), ColumnSpacing = 2 };
            pair.Children.Add(new TextBlock { Text = axis, Classes = { "fieldLabel", "noScrub" } });
            Grid.SetColumn(box, 1);
            pair.Children.Add(box);
            return pair;
        }
        float x = meta.PivotX ?? 0, y = meta.PivotY ?? 0;
        InspectorRow(fields, "Pivot", FieldRows.Split(Axis("X", "Import pivot X", x, v => (v, y)), Axis("Y", "Import pivot Y", y, v => (x, v))));
        ToolTip.SetTip(fields.Children[^1], "The Transform pivot of objects drawn with this PNG: new ones get it, and objects in this scene that still use the old one follow.");
        bool integer = Scene.Settings.IntegerScale == true;
        var filter = ValueLabel(integer ? "Point (integer scale)" : "Linear · cells Point");
        ToolTip.SetTip(filter, "The game samples every PNG the same way: Point when Game Settings → Integer scale is on, otherwise SDL's linear filter " +
            "for whole images and Point for sheet cells. There is no per-PNG filter in the runtime.");
        InspectorRow(fields, "Filter", filter);
        ((Grid)fields.Children[^1]).MinHeight = EditorTokens.InputHeight;
        var atlases = assets.Entries.Where(e => e.Path.EndsWith(SpriteAtlas.Extension, StringComparison.Ordinal) && File.Exists(Path.Combine(root, e.Path)))
            .Select(e => e.Path).ToList();
        const string none = "None", create = "New atlas";
        var current = assets.Sprites(root).Find(id)?.Atlas;
        var group = new ComboBox { ItemsSource = new[] { none }.Concat(atlases).Append(create).ToArray(), SelectedItem = current ?? none,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(group, "Atlas group");
        ToolTip.SetTip(group, "The sprite atlas this PNG is packed into. Changing it repacks the atlases (one Undo step).");
        group.SelectionChanged += (_, _) =>
        {
            if (group.SelectedItem is not string pick || pick == (current ?? none)) return;
            string? target = pick == none ? null : pick == create ? NewAtlasPath(root, relative) : pick;
            RunAction(() => { SetPngAtlas(relative, target); return Task.CompletedTask; });
        };
        InspectorRow(fields, "Atlas", group);
        return new ComponentSection { Header = "Import Settings", HasSettings = false, Foldout = new(), Content = fields };
    }

    private static string NewAtlasPath(string root, string png)
    {
        var folder = Path.GetDirectoryName(png)!.Replace('\\', '/');
        var stem = Path.GetFileNameWithoutExtension(png) + " Atlas";
        var path = $"{folder}/{stem}{SpriteAtlas.Extension}";
        for (int i = 2; File.Exists(Path.Combine(root, path)); i++) path = $"{folder}/{stem} {i}{SpriteAtlas.Extension}";
        return path;
    }

    // The pivot is written to the PNG's .meta; objects in the open scene drawn with the PNG that still have the old import
    // pivot follow it. One Undo record restores both.
    private void SetPngPivot(string relative, (float X, float Y) pivot)
    {
        if (project is not { } root || Viewport.IsPlaying) return;
        var full = Path.Combine(root, relative);
        var meta = ProjectYaml.Read<AssetMeta>(full + ".meta");
        (float X, float Y) old = (meta.PivotX ?? 0, meta.PivotY ?? 0);
        if (old == pivot) return;
        var before = PrefabFileEdit.Read(full);
        ProjectYaml.Write(full + ".meta", meta with { PivotX = pivot.X == 0 ? null : pivot.X, PivotY = pivot.Y == 0 ? null : pivot.Y });
        var edit = new PrefabFileEdit(full, before, PrefabFileEdit.Read(full));
        int moved = 0;
        foreach (var o in Scene.Objects)
            if (o.GetComponent<SpriteRendererComponent>()?.AssetId == meta.Id && o.PivotX == old.X && o.PivotY == old.Y)
            {
                (o.PivotX, o.PivotY) = pivot;
                moved++;
            }
        RecordEdit(Selected?.Id, asset: edit);
        spriteMonitor?.Request();
        Viewport.RefreshScene();
        RefreshAssetInspector();
        Status.Text = $"Pivot of {Path.GetFileName(relative)}: {pivot.X:0.##}, {pivot.Y:0.##} ({moved} objects follow)";
    }

    // Moves a PNG between atlases (or out of / into one) by repacking each atlas it leaves or joins.
    private void SetPngAtlas(string relative, string? target)
    {
        if (project is not { } root || Viewport.IsPlaying) return;
        var assets = ProjectAssets.Scan(root);
        var id = assets.IdFor(relative) ?? throw new InvalidDataException("Missing " + relative);
        var current = assets.Sprites(root).Find(id)?.Atlas;
        if (current == target) return;
        if (current != null && SpriteAtlas.Load(Path.Combine(root, current)).Entries.Length == 1)
        {
            Status.Text = $"{current} has no other sprite. Delete the atlas in Project instead.";
            RefreshAssetInspector();
            return;
        }
        var edits = new List<PrefabFileEdit>();
        void Pack(string atlas, bool join)
        {
            var file = Path.Combine(root, atlas);
            var before = PrefabFileEdit.Read(file);
            var ids = File.Exists(file) ? SpriteAtlas.Load(file).Entries.Select(e => e.Sprite).ToHashSet() : [];
            if (join) ids.Add(id); else ids.Remove(id);
            AtlasPacker.Pack(root, file, ids, File.Exists(file));
            ProjectAssets.Scan(root); // writes a new atlas's .meta now, so Undo sees the files it made
            edits.Add(new(file, before, PrefabFileEdit.Read(file)));
        }
        try
        {
            if (current != null) Pack(current, false);
            if (target != null) Pack(target, true);
        }
        catch
        {
            foreach (var edit in Enumerable.Reverse(edits)) edit.Restore(false);
            ProjectAssets.Scan(root);
            RefreshAssetInspector();
            throw;
        }
        RecordEdit(Selected?.Id, asset: new ProjectFileEdits([.. edits]));
        var fresh = ProjectAssets.Scan(root);
        Viewport.ClearSprites();
        clipEditor?.SetCatalog(fresh);
        spriteMonitor?.Request();
        RefreshProjectFiles(force: true);
        RefreshAssetInspector();
        Status.Text = target == null ? $"{Path.GetFileName(relative)} left {current}." : $"{Path.GetFileName(relative)} packed into {target}.";
    }
}
