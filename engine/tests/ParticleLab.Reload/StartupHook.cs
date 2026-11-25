using System.Collections;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Interactivity;
using Engine.Core;
using Engine.Editor;

// Roadmap 14.8 in the packaged editor: Scene Play of the ParticleLab workshop showcase, the Particle panel edits
// Assets/Presets/Embers.particles while the game runs, and the editor's file watch reloads it into both prefab instances
// (Torch, Brazier). A colour change keeps their live particles; a new Max particles restarts them. Each committed change is
// one Undo step; after Stop, Undo restores the file bytes.
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_PARTICLE_RELOAD")!;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string Embers = "Assets/Presets/Embers.particles";
    private static readonly Dictionary<string, object?> result = [];
    private static Window main = null!;

    public static void Initialize() => Start(Path.Combine(Output, "error.txt"), async life =>
    {
        main = life.MainWindow!;
        await Run();
        File.WriteAllText(Path.Combine(Output, "result.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    });

    private static object? Get(object target, string name)
    {
        var type = target.GetType();
        return type.GetProperty(name, Any)?.GetValue(target) ?? type.GetField(name, Any)?.GetValue(target);
    }
    private static object? Call(object target, string method, params object?[] args) =>
        target.GetType().GetMethods(Any).Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(target, args);

    private static List<string> Console()
    {
        var rows = new List<string>();
        lock (Get(main, "consoleRows")!)
            foreach (var row in (IEnumerable)Get(main, "consoleRows")!) rows.Add($"{Get(row, "Level")}: {Get(row, "Message")}");
        return rows;
    }

    private static async Task Run()
    {
        string root = Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!, file = Path.Combine(root, Embers);
        var view = main.FindControl<SceneViewport>("Viewport")!;
        SceneDocument Scene() => (SceneDocument)Get(main, "Scene")!;
        await Until(() => main.IsVisible && Scene().Objects.Any(o => o.Name == "Brazier smoke"), "workshop scene");
        main.Width = 1280; main.Height = 800;
        await Delay(800);
        var original = File.ReadAllBytes(file);
        int Edits() => (int)Get(Get(main, "history")!, "position")!;

        // Project + → New Particle Effect → Dust: a new asset with its .meta, opened in the panel, one Undo step that removes it.
        var actions = (MenuFlyout)main.FindControl<Button>("ProjectActions")!.Flyout!;
        var menu = actions.Items.OfType<MenuItem>().Single(i => (string?)i.Header == "New Particle Effect");
        int created = Edits();
        menu.Items.OfType<MenuItem>().Single(i => (string?)i.Header == "Dust").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        string dust = Path.Combine(root, "Assets", "Dust.particles");
        var particlePanel = (Control)Get(main, "particlePanel")!;
        await Until(() => File.Exists(dust + ".meta") && (string?)Get(particlePanel, "AssetPath") == "Assets/Dust.particles", "Dust preset created and opened");
        bool sameAsPreset = File.ReadAllText(dust) == File.ReadAllText(Path.Combine(root, "Assets", "Presets", "Dust.particles"));
        created = Edits() - created;
        await Delay(500);
        // The heading row: title and the preset button share one vertical centre inside the panel.
        var presetButton = (Control)Get(particlePanel, "presets")!;
        var titleText = (Control)Get(particlePanel, "title")!;
        double Mid(Control c) => c.TranslatePoint(new(0, c.Bounds.Height / 2), particlePanel)!.Value.Y;
        double headingOffset = Math.Abs(Mid(presetButton) - Mid(titleText));
        bool buttonInside = presetButton.TranslatePoint(new(presetButton.Bounds.Width, 0), particlePanel)!.Value.X <= particlePanel.Bounds.Width + .5;
        main.FindControl<Control>("Viewport")!.Focus();
        await (Task)Call(main, "RestoreEdit", false)!;
        await Delay(300);
        result["preset"] = new { created_records = created, same_as_preset = sameAsPreset, undo_removed = !File.Exists(dust) && !File.Exists(dust + ".meta"),
            heading_center_offset = headingOffset, button_inside = buttonInside };

        var play = main.FindControl<Button>("PlayButton")!;
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        await Until(() => view.IsPlaying, "Play");
        var game = (GameSession)Get(view, "game")!;
        var users = new[] { "Torch", "Brazier" }.Select(n => game.Current.Objects.Single(o => o.Name == n).Id).ToArray();
        var buffer = new Particle2D[256];
        Particle2D[] Read(Guid id) => buffer[..game.Current.ReadParticles(id, buffer)];
        await Until(() => users.All(id => Read(id).Length > 10), "embers burning in both prefabs");

        // The panel opens the asset during Play, stays enabled and keeps its isolated preview running.
        Call(main, "OpenParticlePanel", Embers);
        var panel = (Control)Get(main, "particlePanel")!;
        await Until(() => (string?)Get(panel, "AssetPath") == Embers && panel.IsEffectivelyVisible, "Particle panel on Embers");
        var isolated = Get(panel, "Isolated")!;
        int t0 = (int)Get(isolated, "Tick")!;
        await Delay(600);
        result["panel_during_play"] = new { enabled = panel.IsEffectivelyEnabled, isolated_ticks = (int)Get(isolated, "Tick")! - t0, playing = view.IsPlaying };
        using (var shot = new RenderTargetBitmap(new PixelSize((int)panel.Bounds.Width, (int)panel.Bounds.Height)))
        {
            shot.Render(panel);
            shot.Save(Path.Combine(Output, "panel-play.png"));
        }

        async Task<object> Change(string name, Func<ParticleEffect, ParticleEffect> edit, string expect)
        {
            int edits = Edits(), lines = Console().Count;
            var before = users.Select(Read).ToArray();
            Call(panel, "Edit", edit, true, false);
            await Until(() => Console().Skip(lines).Any(l => l.Contains("Particles reloaded: " + Embers)), name + " reload", 20);
            var log = Console().Skip(lines).First(l => l.Contains("Particles reloaded"));
            var now = users.Select(Read).ToArray();
            return new { edit = name, records = Edits() - edits, log, expected = log.Contains(expect), playing = view.IsPlaying,
                counts_before = before.Select(p => p.Length).ToArray(), counts_after = now.Select(p => p.Length).ToArray() };
        }
        var green = await Change("colour #00FF00", e => e with { Color = new("#00FF00") }, "(2 emitter(s) kept their particles, 0 restarted)");
        await Delay(2500); // longer than the 1.8 s maximum lifetime: every live ember is now from the new asset
        var colours = users.Select(id => Read(id).Select(p => $"{p.Color.R:X2}{p.Color.G:X2}{p.Color.B:X2}").Distinct().ToArray()).ToArray();
        var restart = await Change("max particles 96 → 80", e => e with { MaxParticles = 80 }, "(0 emitter(s) kept their particles, 2 restarted)");
        result["edits"] = new[] { green, restart };
        result["colours_after"] = colours;
        result["file_max_particles"] = ParticleEffect.Load(file).MaxParticles;

        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        await Until(() => !view.IsPlaying, "Stop");
        main.FindControl<Control>("Viewport")!.Focus();
        await (Task)Call(main, "RestoreEdit", false)!;
        await Delay(300);
        await (Task)Call(main, "RestoreEdit", false)!;
        await Delay(300);
        result["undo_restores_file"] = File.ReadAllBytes(file).AsSpan().SequenceEqual(original);
        result["console"] = Console().Where(l => l.Contains("article")).ToArray();
    }
}
