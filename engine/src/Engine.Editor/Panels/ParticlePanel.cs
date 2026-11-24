using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Engine.Core;
using static Engine.Editor.FieldRows;

namespace Engine.Editor;

// The .particles file (and its .meta) before and after one committed Particle panel change: one Undo step in the scene history.
internal sealed record ParticleAssetEdit(PrefabFileEdit Files, Action Restored) : IProjectFileEdit
{
    public void Restore(bool redo) { Files.Restore(redo); Restored(); }
}

// Dock panel "Particle": edits the .particles asset of the selected Particle Emitter 2D (or one opened from the Project panel)
// in foldout sections, and plays it in two previews driven by one toolbar: the Scene view (the selected emitter and its
// sub-emitter children, or every emitter with Play all) and an isolated view here. Both step the player's ParticleSystem2D
// (ParticlePreview), so scrubbing to t shows what Play shows after t. Fields apply live; every committed change writes the
// asset once and records one Undo step. Previews never write the scene or the asset. During Scene Play the panel stays
// editable: the isolated view keeps playing and a saved asset reloads into the running game (MainWindow.ReloadPlayParticles).
internal sealed class ParticlePanel : EditorPanel
{
    private readonly Func<string?> project;
    private readonly Func<SceneDocument> scene;
    private readonly SceneViewport viewport;
    private readonly Action<IProjectFileEdit> record;
    private readonly ParticlePreview inScene = new(), isolated = new();
    private readonly SceneViewport preview = new(spritePreview: true) { IsHitTestVisible = false };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(GameTime.FixedStep) };
    private readonly Stopwatch clock = new();
    private readonly StackPanel sections = new();
    private readonly TextBlock title = new() { FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock status = new() { Classes = { "label" }, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock stats = new() { Classes = { "label" }, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock time = new() { Classes = { "label" }, VerticalAlignment = VerticalAlignment.Center, MinWidth = 44, TextAlignment = TextAlignment.Right };
    private readonly Slider scrub = new() { Minimum = 0, Maximum = 1, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button play = EditorTokens.IconButton("IconPause", "Pause preview");
    private readonly CheckBox all = new() { Content = "Play all in Scene" };
    private readonly Button presets = EditorTokens.IconButton("IconAdd", "New particle effect from a preset");
    private bool playing = true, quiet, dirty, gamePlaying;
    private double carry;
    private float speed = 1;
    private uint seed;
    private string? root, relative;
    private Guid? asset;
    private ParticleEffect? effect;
    private ProjectAssets? assets;
    private SceneObject? owner, selected;
    private ParticleEmitter2DComponent? emitterState;
    private SceneDocument? shownScene;

    internal ParticlePreview ScenePreview => inScene;
    internal ParticlePreview Isolated => isolated;
    internal string? AssetPath => relative;
    internal ParticleEffect? Edited => effect;
    internal bool Playing => playing;
    internal CheckBox PlayAll => all;
    internal StackPanel Sections => sections;
    // Creates a new asset from a built-in preset (ParticlePresets) and opens it here.
    internal Action<ParticlePresets.Preset>? NewFromPreset { get; init; }

    public ParticlePanel(Func<string?> project, Func<SceneDocument> scene, SceneViewport viewport, Action<IProjectFileEdit> record)
    {
        (this.project, this.scene, this.viewport, this.record) = (project, scene, viewport, record);
        Title = "Particle";
        MinWidth = 480;
        MinHeight = 240;
        var restart = EditorTokens.IconButton("IconRotate", "Restart preview");
        var step = EditorTokens.IconButton("IconStep", "Step one tick");
        AutomationProperties.SetName(scrub, "Preview time");
        ToolTip.SetTip(scrub, "Preview time: simulates again from 0 with the same seed");
        var speedBox = Number("Speed (0.1–4×)", 1, .1m, 4, .1m, (v, _) => speed = v);
        var seedBox = Number("Preview seed (0 uses the emitter's)", 0, 0, 16777216, 1, (v, c) => { seed = (uint)v; if (c) Rebuild(keepTime: false); });
        speedBox.Width = 72; seedBox.Width = 96;
        ToolTip.SetTip(all, "Simulate every emitter in the Scene view, not only the selected one");
        var transport = new Grid { ColumnDefinitions = new("Auto,Auto,Auto,*,Auto"), ColumnSpacing = EditorTokens.FieldGap };
        EditorControls.Put(transport, play, 0, 0); EditorControls.Put(transport, restart, 0, 1); EditorControls.Put(transport, step, 0, 2);
        EditorControls.Put(transport, scrub, 0, 3); EditorControls.Put(transport, time, 0, 4);
        var options = new Grid { ColumnDefinitions = new("Auto,Auto,Auto,Auto,Auto,*"), ColumnSpacing = EditorTokens.FieldGap };
        EditorControls.Put(options, Label("Speed"), 0, 0); EditorControls.Put(options, speedBox, 0, 1);
        EditorControls.Put(options, Label("Seed"), 0, 2); EditorControls.Put(options, seedBox, 0, 3);
        EditorControls.Put(options, all, 0, 4); EditorControls.Put(options, stats, 0, 5);
        var view = new Border { Child = preview, BorderBrush = EditorTokens.Brush("EditorBorder"), BorderThickness = new(1), Width = 200, ClipToBounds = true };
        var fields = new ScrollViewer { Content = sections, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        var body = new Grid { ColumnDefinitions = new("Auto,*"), ColumnSpacing = EditorTokens.Gap2 };
        EditorControls.Put(body, view, 0, 0); EditorControls.Put(body, fields, 0, 1);
        var main = new Grid { Margin = new(EditorTokens.Gap2), RowDefinitions = new("Auto,Auto,Auto,*,Auto"), RowSpacing = EditorTokens.Gap };
        var menu = new MenuFlyout();
        foreach (var preset in ParticlePresets.All)
        {
            var item = new MenuItem { Header = preset.Name };
            item.Click += (_, _) => NewFromPreset?.Invoke(preset);
            menu.Items.Add(item);
        }
        presets.Flyout = menu;
        var heading = new Grid { ColumnDefinitions = new("*,Auto"), ColumnSpacing = EditorTokens.FieldGap };
        EditorControls.Put(heading, title, 0, 0); EditorControls.Put(heading, presets, 0, 1);
        EditorControls.Put(main, heading, 0); EditorControls.Put(main, transport, 1); EditorControls.Put(main, options, 2);
        EditorControls.Put(main, body, 3); EditorControls.Put(main, status, 4);
        Content = main;

        play.Click += (_, _) => SetPlaying(!playing);
        restart.Click += (_, _) => Seek(0);
        step.Click += (_, _) => { SetPlaying(false); Advance(); Show(); };
        scrub.ValueChanged += (_, _) =>
        {
            if (quiet) return;
            SetPlaying(false);
            Seek((int)Math.Round(scrub.Value / ParticlePreview.Step));
        };
        all.IsCheckedChanged += (_, _) => Rebuild(keepTime: false);
        timer.Tick += (_, _) => Tick();
        AttachedToVisualTree += (_, _) => { clock.Restart(); timer.Start(); Rebuild(keepTime: false); };
        DetachedFromVisualTree += (_, _) => { Commit(); timer.Stop(); viewport.Particles = null; viewport.RefreshScene(); };
        ShowEmpty("Select an object with a Particle Emitter 2D, or double-click a .particles asset in the Project panel.");
    }

    private static TextBlock Label(string text) => new() { Text = text, Classes = { "label" }, VerticalAlignment = VerticalAlignment.Center };

    // Opens an asset from the Project panel. The Scene view keeps previewing the selected emitter.
    internal void Open(string path)
    {
        if (project() is not { } folder) return;
        Commit();
        assets = ProjectAssets.Scan(folder);
        if (assets.Entries.FirstOrDefault(e => e.Path == path) is { } entry) Load(folder, entry.Id);
    }

    private void SetPlaying(bool on)
    {
        playing = on;
        inScene.Interpolation = isolated.Interpolation = 1;
        play.Content = EditorTokens.Icon(on ? "IconPause" : "IconPlay");
        ToolTip.SetTip(play, on ? "Pause preview" : "Play preview");
        AutomationProperties.SetName(play, on ? "Pause preview" : "Play preview");
        clock.Restart();
        carry = 0;
    }

    // Follows the selection, project and scene; steps both previews at the fixed tick times the speed.
    private void Tick()
    {
        Follow();
        // Play draws the game's own particles in the Scene view; only the isolated view previews here meanwhile.
        if (viewport.IsPlaying != gamePlaying)
        {
            gamePlaying = viewport.IsPlaying;
            presets.IsEnabled = !gamePlaying;
            Rebuild(keepTime: false);
        }
        if (!gamePlaying && viewport.Particles == null && !inScene.IsEmpty) viewport.Particles = inScene;
        double elapsed = clock.Elapsed.TotalSeconds;
        clock.Restart();
        if (playing)
        {
            carry += Math.Min(elapsed, .25) * speed;
            for (int n = 0; carry >= ParticlePreview.Step && n < 16; n++)
            {
                carry -= ParticlePreview.Step;
                if (inScene.Tick + 1 >= SpanTicks) Seek(0, quietly: true);
                else Advance();
            }
            if (carry >= ParticlePreview.Step) carry = 0;
        }
        if (!playing) return;
        inScene.Interpolation = isolated.Interpolation = (float)(carry / ParticlePreview.Step);
        Show();
    }

    private int SpanTicks => (int)Math.Ceiling(MathF.Max(inScene.Span, isolated.Span) / ParticlePreview.Step);

    private void Advance() { inScene.Advance(); isolated.Advance(); }

    internal void Seek(int tick, bool quietly = false)
    {
        tick = Math.Clamp(tick, 0, Math.Max(0, SpanTicks - 1));
        inScene.Seek(tick);
        isolated.Seek(tick);
        if (!quietly) Show();
    }

    private void Show()
    {
        quiet = true;
        scrub.Maximum = SpanTicks * ParticlePreview.Step;
        scrub.Value = isolated.Tick * ParticlePreview.Step;
        quiet = false;
        time.Text = $"{isolated.Tick * ParticlePreview.Step:0.00} s";
        int count = isolated.Count;
        stats.Text = $"{count:N0} particles · {isolated.StepMicroseconds:0} µs/tick" + (inScene.IsEmpty ? "" : $" · Scene {inScene.Count:N0}");
        ToolTip.SetTip(stats, stats.Text);
        if (!gamePlaying) viewport.RefreshScene();
        preview.RefreshScene();
    }

    private void Follow()
    {
        var folder = project();
        if (folder != root)
        {
            Commit();
            (root, asset, effect, relative, assets, owner, selected, emitterState) = (folder, null, null, null, null, null, null, null);
            ShowEmpty("Select an object with a Particle Emitter 2D, or double-click a .particles asset in the Project panel.");
        }
        if (root == null) return;
        var current = scene();
        var pick = viewport.Selected;
        var emitter = pick?.GetComponent<ParticleEmitter2DComponent>();
        if (pick == selected && current == shownScene && (emitter == null || emitter.Equals(emitterState))) { ShowTitle(); return; }
        bool reload = pick != selected || emitter?.Particles != emitterState?.Particles;
        (selected, shownScene, emitterState) = (pick, current, emitter == null ? null : emitter with { });
        owner = emitter == null ? null : pick;
        if (reload && emitter?.Particles is Guid id && id != asset)
        {
            Commit();
            assets = ProjectAssets.Scan(root);
            Load(root, id);
            return;
        }
        Rebuild(keepTime: !reload);
    }

    private void Load(string folder, Guid id)
    {
        (asset, relative, effect, dirty) = (id, assets!.PathFor(id), null, false);
        try
        {
            if (relative == null) throw new InvalidDataException($"Missing particle effect {id}.");
            effect = ParticleEffect.Load(Path.Combine(folder, relative));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            effect = null;
            status.Text = ex.Message;
        }
        BuildSections();
        Rebuild(keepTime: false);
    }

    // Undo and Redo wrote the file back; show what is on disk now.
    private void Reload()
    {
        if (root == null || asset is not Guid id) return;
        assets = ProjectAssets.Scan(root);
        Load(root, id);
        status.Text = $"Restored {relative}.";
    }

    private void ShowEmpty(string message)
    {
        sections.Children.Clear();
        title.Text = "Particle";
        status.Text = message;
        inScene.Clear();
        isolated.Clear();
        viewport.Particles = null;
        preview.Particles = null;
    }

    // The emitters each preview simulates. The asset being edited uses its unsaved copy; others load from the project.
    private void Rebuild(bool keepTime)
    {
        if (root == null) return;
        int tick = keepTime ? isolated.Tick : 0;
        IReadOnlyList<SceneObject> owners = [];
        if (!viewport.IsPlaying)
        {
            var objects = scene().Objects;
            if (all.IsChecked == true) owners = objects.Where(o => o.GetComponent<ParticleEmitter2DComponent>() != null && !viewport.IsHidden(o)).ToList();
            else if (owner != null) owners = [owner, .. objects.Where(o => o.Parent == owner && o.GetComponent<ParticleEmitter2DComponent>() != null)];
        }
        try
        {
            inScene.Build(owners, For, seed);
            var source = owner?.GetComponent<ParticleEmitter2DComponent>();
            // The isolated view centres the effect: a stand-in object at the origin with the emitter's seed stream and trigger.
            var stand = new SceneObject { Id = owner?.Id ?? asset ?? Guid.Empty, Name = "Preview", Width = 0, Height = 0 };
            stand.AddComponent(new ParticleEmitter2DComponent { Particles = asset, Trigger = source?.Trigger ?? ParticleTrigger.Start, Seed = source?.Seed ?? 0 });
            if (effect != null) isolated.Build([stand], For, seed); else isolated.Clear();
            inScene.Seek(Math.Min(tick, Math.Max(0, SpanTicks - 1)));
            isolated.Seek(inScene.IsEmpty ? Math.Min(tick, Math.Max(0, SpanTicks - 1)) : inScene.Tick);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            status.Text = ex.Message;
            inScene.Clear();
            isolated.Clear();
        }
        viewport.Particles = inScene.IsEmpty || viewport.IsPlaying ? null : inScene;
        preview.Particles = isolated.IsEmpty ? null : isolated;
        ShowTitle();
        Show();
    }

    // The asset and the emitter object; a rename shows on the next tick.
    private void ShowTitle()
    {
        var text = relative == null ? "Particle" : owner == null ? relative : $"{relative} · {owner.Name}";
        if (title.Text == text) return;
        title.Text = text;
        ToolTip.SetTip(title, text);
    }

    private ParticleEffect For(ParticleEmitter2DComponent e)
    {
        if (e.Legacy) return e.LegacyEffect();
        try
        {
            if (e.Particles == asset && effect != null && root != null) return effect.WithTexture(root, assets!);
            if (e.Particles is Guid id && root != null) return (assets ??= ProjectAssets.Scan(root)).LoadParticles(root, id);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            status.Text = ex.Message;
            if (e.Particles == asset && effect != null) return effect with { Texture = null };
        }
        return new();
    }

    // Applies a change to the unsaved copy and the previews. commit writes it as one Undo step; rebuild redraws the sections
    // (an optional module turned on or off, a list item added or removed).
    private void Edit(Func<ParticleEffect, ParticleEffect> change, bool commit, bool rebuild = false)
    {
        if (effect == null) return;
        var next = change(effect);
        try { next.Validate(relative ?? "Particle"); }
        catch (InvalidDataException ex) { status.Text = ex.Message; return; }
        (effect, dirty) = (next, true);
        if (rebuild) BuildSections();
        Rebuild(keepTime: true);
        if (commit) Commit();
    }

    internal void Commit()
    {
        if (!dirty || effect == null || root == null || relative == null) return;
        dirty = false;
        string path = Path.Combine(root, relative);
        try
        {
            var bytes = ProjectYaml.Encode(effect);
            var before = PrefabFileEdit.Read(path);
            if (before.Document is { } old && old.AsSpan().SequenceEqual(bytes)) return;
            ProjectYaml.WriteBytes(path, bytes);
            record(new ParticleAssetEdit(new PrefabFileEdit(path, before, PrefabFileEdit.Read(path)), Reload));
            status.Text = $"Saved {relative}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { status.Text = ex.Message; }
    }

    private void BuildSections()
    {
        sections.Children.Clear();
        if (effect is not { } fx) return;
        status.Text = "";
        Control Num(string label, float value, decimal min, decimal max, decimal inc, Func<ParticleEffect, float, ParticleEffect> set, bool enabled = true)
        {
            var row = Row(label, Number(label, value, min, max, inc, (v, c) => Edit(e => set(e, v), c)));
            row.IsEnabled = enabled;
            return row;
        }
        Control Range(string label, ParticleRange range, decimal min, decimal max, decimal inc, Func<ParticleEffect, ParticleRange> get, Func<ParticleEffect, ParticleRange, ParticleEffect> set)
        {
            NumericUpDown low = null!, high = null!;
            low = Number(label + " min", range.Min, min, max, inc, (v, c) => { Edit(e => set(e, new(v, MathF.Max(v, get(e).Max))), c); Set(high, get(effect!).Max); });
            high = Number(label + " max", range.Max, min, max, inc, (v, c) => { Edit(e => set(e, new(MathF.Min(v, get(e).Min), v)), c); Set(low, get(effect!).Min); });
            return Row(label, Split(low, high));
        }
        Control Check(string label, string text, bool value, Func<ParticleEffect, bool, ParticleEffect> set, bool rebuild = false, bool enabled = true)
        {
            var box = new CheckBox { Content = text, IsChecked = value, IsEnabled = enabled };
            box.IsCheckedChanged += (_, _) => Edit(e => set(e, box.IsChecked == true), true, rebuild);
            return Row(label, box);
        }
        Control Choice<T>(string label, T value, Func<ParticleEffect, T, ParticleEffect> set, bool rebuild = false) where T : struct, Enum
        {
            var box = new ComboBox { ItemsSource = Enum.GetValues<T>(), SelectedItem = value, HorizontalAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetName(box, label);
            box.SelectionChanged += (_, _) => { if (box.SelectedItem is T v) Edit(e => set(e, v), true, rebuild); };
            return Row(label, box);
        }
        Control Color(string label, string value, Func<ParticleEffect, string, ParticleEffect> set)
        {
            var field = new ColorField(label, value, alpha: true);
            field.Changed += (hex, c) => Edit(e => set(e, hex), c);
            return Row(label, field);
        }
        // The Inspector's object field (13.17): Project or Finder drop of the accepted type, picker, Ping; one Undo step.
        Control Asset(string label, string suffix, Guid? value, Func<ParticleEffect, Guid?, ParticleEffect> set, bool allowNone = true)
        {
            string folder = root!;
            ReferenceChoice Choice(Guid id, string path, string text) =>
                new(id, text, SpriteAssets.IsPng(path) ? Path.Combine(folder, path) : null, EditorTokens.AssetIcon(path, false));
            var none = new ReferenceChoice(Guid.Empty, allowNone ? "None" : "Choose…");
            var shown = value is not Guid id ? none : assets!.PathFor(id) is { } path ? Choice(id, path, Path.GetFileName(path))
                : new ReferenceChoice(id, $"Missing: {id}", null, EditorTokens.Geometry("IconWarning"));
            IReadOnlyList<ReferenceChoice> Choices() => [.. allowNone ? [none] : Array.Empty<ReferenceChoice>(),
                .. assets!.Entries.Where(e => e.Path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)).Select(e => Choice(e.Id, e.Path, e.Path))];
            var field = new ReferenceField(label, asset: true, shown, Choices, picked =>
            {
                if (picked == null && !allowNone) return;
                // A file imported by the drop is not in the panel's scan yet.
                if (picked is Guid g && assets!.PathFor(g) == null) assets = ProjectAssets.Scan(folder);
                Edit(e => set(e, picked), true, rebuild: true);
            }, MainWindow.EndsWith(suffix));
            return Row(label, field);
        }
        Control Curve(string label, string toggle, ParticleCurve? curve, float low, float high, decimal step, ParticleKey[] initial,
            Func<ParticleEffect, ParticleCurve?, ParticleEffect> set)
        {
            var rows = new StackPanel { Spacing = 2 };
            var on = new CheckBox { Content = toggle, IsChecked = curve != null };
            on.IsCheckedChanged += (_, _) => Edit(e => set(e, on.IsChecked == true ? new ParticleCurve(initial) : null), true, rebuild: true);
            rows.Children.Add(Row("", on));
            if (curve != null)
            {
                var field = new CurveField(label, curve, low, high, step);
                field.Changed += (c, commit) => Edit(e => set(e, c), commit);
                rows.Children.Add(field);
            }
            return rows;
        }
        Control Button(string text, Action click)
        {
            var b = new Button { Content = text, HorizontalAlignment = HorizontalAlignment.Left };
            b.Click += (_, _) => click();
            return Row("", b);
        }
        Control Remove(string tip, Action click)
        {
            var b = EditorTokens.IconButton("IconClose", tip);
            b.Click += (_, _) => click();
            return b;
        }
        T[] Without<T>(T[] items, int i) => [.. items[..i], .. items[(i + 1)..]];

        var main = new List<Control>
        {
            Num("Duration", fx.Duration, .001m, 3600, .1m, (e, v) => e with { Duration = v }),
            Check("", "Looping", fx.Looping, (e, v) => e with { Looping = v, Prewarm = v && e.Prewarm }, rebuild: true),
            Check("", "Prewarm", fx.Prewarm, (e, v) => e with { Prewarm = v }, enabled: fx.Looping),
            Num("Max", fx.MaxParticles, 1, 65536, 16, (e, v) => e with { MaxParticles = (int)v }),
            Choice("Space", fx.Space, (e, v) => e with { Space = v, Collision = v == ParticleSpace.World ? e.Collision : null }, rebuild: true),
            Range("Lifetime", fx.Lifetime, .001m, 600, .1m, e => e.Lifetime, (e, r) => e with { Lifetime = r }),
            Range("Speed", fx.Speed, -16384, 16384, 5, e => e.Speed, (e, r) => e with { Speed = r }),
            Range("Size", fx.Size, 0, 4096, 1, e => e.Size, (e, r) => e with { Size = r }),
            Num("Aspect", fx.Aspect, .01m, 100, .1m, (e, v) => e with { Aspect = v }),
            Range("Rotation", fx.Rotation, -3600, 3600, 5, e => e.Rotation, (e, r) => e with { Rotation = r }),
            Color("Color", fx.Color.Min, (e, v) => e with { Color = e.Color with { Min = v } }),
            Check("", "Random up to a second colour", fx.Color.Max != null, (e, v) => e with { Color = e.Color with { Max = v ? e.Color.Min : null } }, rebuild: true),
        };
        if (fx.Color.Max != null) main.Add(Color("Color 2", fx.Color.Max, (e, v) => e with { Color = e.Color with { Max = v } }));
        main.Add(Num("Gravity", fx.Gravity, -16384, 16384, 10, (e, v) => e with { Gravity = v }));
        main.Add(Num("End size", fx.EndSize, 0, 100, .1m, (e, v) => e with { EndSize = v }, fx.SizeOverLifetime == null));
        main.Add(Num("End alpha", fx.EndAlpha, 0, 1, .1m, (e, v) => e with { EndAlpha = v }, fx.ColorOverLifetime?.Alphas is not { Length: > 0 }));
        Section("Main", main);

        var emission = new List<Control> { Num("Rate", fx.Rate, 0, 100000, 1, (e, v) => e with { Rate = v }) };
        var bursts = fx.Bursts ?? [];
        for (int i = 0; i < bursts.Length; i++)
        {
            int k = i;
            ParticleEffect With(ParticleEffect e, Func<ParticleBurst, ParticleBurst> f) { var b = (ParticleBurst[])e.Bursts!.Clone(); b[k] = f(b[k]); return e with { Bursts = b }; }
            var at = Number("Burst time (s)", bursts[i].Time, 0, 3600, .1m, (v, c) => Edit(e => With(e, b => b with { Time = v }), c));
            var count = Number("Burst count", bursts[i].Count, 0, 65536, 1, (v, c) => Edit(e => With(e, b => b with { Count = (int)v }), c));
            var grid = new Grid { ColumnDefinitions = new("*,*,Auto"), ColumnSpacing = EditorTokens.FieldGap };
            EditorControls.Put(grid, at, 0, 0); EditorControls.Put(grid, count, 0, 1);
            EditorControls.Put(grid, Remove("Delete burst", () => Edit(e => e with { Bursts = Without(e.Bursts!, k) is { Length: > 0 } left ? left : null }, true, rebuild: true)), 0, 2);
            emission.Add(Row($"Burst {i + 1}", grid));
            emission.Add(Row("Repeat", Split(
                Number("Burst cycles (0 or 1: once)", bursts[i].Cycles, 0, 10000, 1, (v, c) => Edit(e => With(e, b => b with { Cycles = (int)v, Interval = v > 1 && b.Interval <= 0 ? .5f : b.Interval }), c)),
                Number("Burst interval (s)", bursts[i].Interval, 0, 3600, .1m, (v, c) => Edit(e => With(e, b => b with { Interval = v }), c)))));
        }
        if (bursts.Length < 32) emission.Add(Button("Add burst", () => Edit(e => e with { Bursts = [.. e.Bursts ?? [], new(0, 10)] }, true, rebuild: true)));
        Section("Emission", emission);

        Section("Shape",
        [
            Choice("Shape", fx.Shape, (e, v) => e with { Shape = v }),
            Num("Radius", fx.Radius, 0, 16384, 1, (e, v) => e with { Radius = v }),
            Num("Width", fx.Width, 0, 16384, 1, (e, v) => e with { Width = v }),
            Num("Height", fx.Height, 0, 16384, 1, (e, v) => e with { Height = v }),
            Num("Direction", fx.Direction, -3600, 3600, 5, (e, v) => e with { Direction = v }),
            Num("Spread", fx.Spread, 0, 360, 5, (e, v) => e with { Spread = v }),
        ]);

        var gradient = new StackPanel { Spacing = 2 };
        var tint = new CheckBox { Content = "Color over lifetime", IsChecked = fx.ColorOverLifetime != null };
        tint.IsCheckedChanged += (_, _) => Edit(e => e with { ColorOverLifetime = tint.IsChecked == true ?
            new ParticleGradient([new(0, "#FFFFFF"), new(1, "#FFFFFF")], [new(0, 1), new(1, 0)]) : null }, true, rebuild: true);
        gradient.Children.Add(Row("", tint));
        if (fx.ColorOverLifetime is { } g)
        {
            var field = new GradientField("Gradient", g);
            field.Changed += (value, c) => Edit(e => e with { ColorOverLifetime = value }, c);
            gradient.Children.Add(field);
        }
        Section("Over lifetime",
        [
            Curve("Size", "Size over lifetime", fx.SizeOverLifetime, 0, 100, .1m, [new(0, 1), new(1, 0)], (e, c) => e with { SizeOverLifetime = c }),
            gradient,
            Curve("Spin °/s", "Rotation speed", fx.RotationSpeed, -36000, 36000, 10, [new(0, 180)], (e, c) => e with { RotationSpeed = c }),
        ]);

        var forces = new List<Control>
        {
            Num("Force X", fx.ForceX, -16384, 16384, 10, (e, v) => e with { ForceX = v }),
            Num("Force Y", fx.ForceY, -16384, 16384, 10, (e, v) => e with { ForceY = v }),
            Curve("Vel X", "Velocity X over lifetime", fx.VelocityX, -16384, 16384, 5, [new(0, 0)], (e, c) => e with { VelocityX = c }),
            Curve("Vel Y", "Velocity Y over lifetime", fx.VelocityY, -16384, 16384, 5, [new(0, 0)], (e, c) => e with { VelocityY = c }),
            Num("Drag", fx.Drag, 0, 60, .1m, (e, v) => e with { Drag = v }),
            Check("", "Noise", fx.Noise != null, (e, v) => e with { Noise = v ? new ParticleNoise(60) : null }, rebuild: true),
        };
        if (fx.Noise is { } noise)
        {
            forces.Add(Num("Strength", noise.Strength, 0, 16384, 5, (e, v) => e with { Noise = e.Noise! with { Strength = v } }));
            forces.Add(Num("Frequency", noise.Frequency, 0, 16, .005m, (e, v) => e with { Noise = e.Noise! with { Frequency = v } }));
            forces.Add(Num("Scroll", noise.Scroll, -1000, 1000, .1m, (e, v) => e with { Noise = e.Noise! with { Scroll = v } }));
        }
        Section("Forces", forces);

        var collision = new List<Control>();
        var collide = Check("", "Collide (World space)", fx.Collision != null, (e, v) => e with { Collision = v ? new ParticleCollision() : null }, rebuild: true);
        collide.IsEnabled = fx.Space == ParticleSpace.World;
        collision.Add(collide);
        if (fx.Collision is { } hit)
        {
            collision.Add(Choice("Mode", hit.Mode, (e, v) => e with { Collision = e.Collision! with { Mode = v } }));
            collision.Add(Num("Bounce", hit.Bounce, 0, 1, .05m, (e, v) => e with { Collision = e.Collision! with { Bounce = v } }));
            collision.Add(Num("Dampen", hit.Dampen, 0, 1, .05m, (e, v) => e with { Collision = e.Collision! with { Dampen = v } }));
            collision.Add(Num("Life loss", hit.LifetimeLoss, 0, 1, .05m, (e, v) => e with { Collision = e.Collision! with { LifetimeLoss = v } }));
            // shortcut: the mask goes through a float field, exact for layer masks up to 24 bits; a bit list belongs to 13.x mask rows.
            collision.Add(Num("Mask", hit.Mask, 0, uint.MaxValue, 1, (e, v) => e with { Collision = e.Collision! with { Mask = (uint)v } }));
            collision.Add(Num("Queries", hit.MaxQueries, 1, 65536, 8, (e, v) => e with { Collision = e.Collision! with { MaxQueries = (int)v } }));
            collision.Add(Row("", new TextBlock { Text = "The edit preview does not collide; Play does.", Classes = { "label" }, TextWrapping = TextWrapping.Wrap }));
        }
        Section("Collision", collision);

        var subs = new List<Control>();
        var rules = fx.SubEmitters ?? [];
        for (int i = 0; i < rules.Length; i++)
        {
            int k = i;
            ParticleEffect With(ParticleEffect e, Func<ParticleSubEmitter, ParticleSubEmitter> f) { var r = (ParticleSubEmitter[])e.SubEmitters!.Clone(); r[k] = f(r[k]); return e with { SubEmitters = r }; }
            var when = new ComboBox { ItemsSource = Enum.GetValues<ParticleEvent>(), SelectedItem = rules[i].When, HorizontalAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetName(when, "Sub-emitter event");
            when.SelectionChanged += (_, _) => { if (when.SelectedItem is ParticleEvent v) Edit(e => With(e, r => r with { When = v }), true); };
            var head = new Grid { ColumnDefinitions = new("*,Auto"), ColumnSpacing = EditorTokens.FieldGap };
            EditorControls.Put(head, when, 0, 0);
            EditorControls.Put(head, Remove("Delete sub-emitter", () => Edit(e => e with { SubEmitters = Without(e.SubEmitters!, k) is { Length: > 0 } left ? left : null }, true, rebuild: true)), 0, 1);
            subs.Add(Row($"Sub {i + 1}", head));
            subs.Add(Asset("Particles", ParticleEffect.Extension, rules[i].Particles, (e, v) => v is Guid id ? With(e, r => r with { Particles = id }) : e, allowNone: false));
            subs.Add(Num("Count", rules[i].Count, 1, 1024, 1, (e, v) => With(e, r => r with { Count = (int)v })));
            var inheritColor = new CheckBox { Content = "Color", IsChecked = rules[i].InheritColor };
            inheritColor.IsCheckedChanged += (_, _) => Edit(e => With(e, r => r with { InheritColor = inheritColor.IsChecked == true }), true);
            subs.Add(Row("Inherit", Split(Number("Inherit velocity (0–1)", rules[i].InheritVelocity, 0, 1, .05m, (v, c) => Edit(e => With(e, r => r with { InheritVelocity = v }), c)), inheritColor)));
        }
        if (rules.Length < 8 && asset is Guid self)
            subs.Add(Button("Add sub-emitter", () => Edit(e => e with { SubEmitters = [.. e.SubEmitters ?? [], new(ParticleEvent.Death,
                assets!.Entries.FirstOrDefault(a => a.Id != self && a.Path.EndsWith(ParticleEffect.Extension, StringComparison.OrdinalIgnoreCase))?.Id ?? self, 8)] }, true, rebuild: true)));
        subs.Add(Row("", new TextBlock { Text = "Targets are direct children whose emitter uses that asset.", Classes = { "label" }, TextWrapping = TextWrapping.Wrap }));
        Section("Sub-emitters", subs);

        var renderer = new List<Control>
        {
            Asset("Texture", ".png", fx.Texture, (e, v) => e with { Texture = v }),
            Choice("Blend", fx.Blend, (e, v) => e with { Blend = v }),
            Check("", "Texture sheet", fx.Sheet != null, (e, v) => e with { Sheet = v ? new ParticleSheet() : null }, rebuild: true),
        };
        if (fx.Sheet is { } sheet)
        {
            renderer.Add(Row("Cells", Split(
                Number("Sheet columns", sheet.Columns, 0, 64, 1, (v, c) => Edit(e => e with { Sheet = e.Sheet! with { Columns = (int)v } }, c)),
                Number("Sheet rows", sheet.Rows, 0, 64, 1, (v, c) => Edit(e => e with { Sheet = e.Sheet! with { Rows = (int)v } }, c)))));
            renderer.Add(Row("Frames", Split(
                Number("Sheet frames (0: every cell)", sheet.Frames, 0, 4096, 1, (v, c) => Edit(e => e with { Sheet = e.Sheet! with { Frames = (int)v } }, c)),
                Number("Sheet fps (0: once per lifetime)", sheet.Fps, 0, 240, 1, (v, c) => Edit(e => e with { Sheet = e.Sheet! with { Fps = v } }, c)))));
        }
        Section("Renderer", renderer);
        EditorTokens.TrimComboBoxes(sections);
    }

    private void Section(string name, IEnumerable<Control> rows)
    {
        var content = new StackPanel { Spacing = 2 };
        content.Children.AddRange(rows);
        sections.Children.Add(new ComponentSection
        {
            Header = name, HasSettings = false, Icon = name == "Main" ? EditorTokens.Geometry("IconParticle") : null,
            Foldout = new ComponentFoldout("particles." + name), Content = content,
        });
    }

    private sealed record AssetChoice(Guid Id, string Path)
    {
        public override string ToString() => Path;
    }
}
