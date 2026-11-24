using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Engine.Core;

// Loaded into the packaged editor with DOTNET_STARTUP_HOOKS (16.10 editor follow-up). The project is AudioLab without its
// audioSnapshots and audioDucking; the probe enters the sample's Night snapshot and Voice → Music rule in the Audio Mixer
// dialog, checks the runtime's messages for invalid values, saves, compares the GameSettings bytes with the sample's file
// saved by the engine, undoes and redoes once, records the row columns, then previews the transition in Scene Play.
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_MXE_OUT")!;
    private static readonly string Project = Environment.GetEnvironmentVariable("MONOON_MXE_PROJECT")!;
    private static readonly string Reference = Environment.GetEnvironmentVariable("MONOON_MXE_REFERENCE")!;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Dictionary<string, object?> Result = [];
    private static Window main = null!;
    private static IClassicDesktopStyleApplicationLifetime life = null!;

    public static void Initialize() => Start(Path.Combine(Output, "error.txt"), async l =>
    {
        life = l;
        main = life.MainWindow!;
        await Until(() => main.IsVisible && Scene.Objects.Any(o => o.Name == "Line"), "AudioLab", 120);
        main.Width = 1280; main.Height = 800;
        await Delay(800);
        await Run();
        File.WriteAllText(Path.Combine(Output, "result.json"), JsonSerializer.Serialize(Result, new JsonSerializerOptions { WriteIndented = true }));
    }, allow: ["Audio Mixer"]);

    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static object? Get(object target, string name) =>
        target.GetType().GetProperty(name, Any) is { } p ? p.GetValue(target) : target.GetType().GetField(name, Any)?.GetValue(target);
    private static object? Call(object target, string method, params object?[] args) =>
        target.GetType().GetMethods(Any).First(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(target, args);
    private static SceneDocument Scene => (SceneDocument)Get(main, "Scene")!;
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, b));
    private static byte[] Saved() => File.ReadAllBytes(Path.Combine(Project, GameSettings.FileName));
    private static byte[] Normalized(string project)
    {
        var temp = Directory.CreateTempSubdirectory("mxe-").FullName;
        GameSettings.Load(project).Save(temp);
        return File.ReadAllBytes(Path.Combine(temp, GameSettings.FileName));
    }
    private static string? RuleMessage(GameSettings settings)
    {
        try { settings.Save(Directory.CreateTempSubdirectory("mxe-").FullName); return null; }
        catch (InvalidDataException ex) { return ex.Message; }
    }

    private static Window? Dialog() => life.Windows.FirstOrDefault(w => w.Title == "Audio Mixer" && w.IsVisible);
    private static T Named<T>(string name) where T : Control =>
        Dialog()!.GetVisualDescendants().OfType<T>().FirstOrDefault(c => AutomationProperties.GetName(c) == name)
        ?? throw new InvalidDataException($"no {typeof(T).Name} '{name}'");
    private static string Hint() => Named<TextBlock>("Mixer message").Text ?? "";
    private static async Task Tab(int index) { Dialog()!.GetVisualDescendants().OfType<TabControl>().First().SelectedIndex = index; await Delay(250); }
    private static async Task<Task> Open()
    {
        var task = (Task)Call(main, "EditMixer")!;
        await Until(() => Dialog() is { Bounds.Width: > 0 }, "Audio Mixer dialog");
        await Delay(400);
        return task;
    }
    private static Button ButtonText(string text) => Dialog()!.GetVisualDescendants().OfType<Button>().First(b => b.Content as string == text);

    // Every row of the visible tab: label left edge, field left edge, row right edge (field rows only, not the tab strip).
    private static List<(double Label, double Field, double Right, string Text)> Rows()
    {
        var dialog = Dialog()!;
        var tabs = dialog.GetVisualDescendants().OfType<TabControl>().First();
        var page = (Control)((TabItem)tabs.Items[tabs.SelectedIndex]!).Content!;
        return page.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("fieldLabel") && t.IsEffectivelyVisible).Select(label =>
        {
            var grid = (Grid)label.GetVisualParent()!;
            var field = grid.Children.First(c => Grid.GetColumn(c) == 1);
            return (label.TranslatePoint(default, dialog)!.Value.X, field.TranslatePoint(default, dialog)!.Value.X,
                grid.TranslatePoint(new Point(grid.Bounds.Width, 0), dialog)!.Value.X, label.Text ?? "");
        }).ToList();
    }
    private static async Task Columns(string capture)
    {
        await Delay(400);
        var all = new List<string>();
        var first = (Label: 0.0, Field: 0.0, Right: 0.0, Text: "");
        foreach (var tab in new[] { 0, 1, 2 })
        {
            await Tab(tab);
            var rows = Rows();
            if (first.Text == "") first = rows[0];
            foreach (var r in rows)
            {
                Require(Math.Abs(r.Label - first.Label) < .5 && Math.Abs(r.Field - first.Field) < .5 && Math.Abs(r.Right - first.Right) < .5,
                    $"{capture} tab {tab} row {r.Text}: {r.Label:F1}/{r.Field:F1}/{r.Right:F1} against {first.Label:F1}/{first.Field:F1}/{first.Right:F1}");
                // A label that fits its column is not cut off (the five-letter names and Snapshot, Ambience, Release, Preview).
                var free = new TextBlock { Text = r.Text, FontSize = 12 };
                free.Measure(Size.Infinity);
                Require(free.DesiredSize.Width <= r.Field - r.Label, $"{capture}: label '{r.Text}' is cut off ({free.DesiredSize.Width:F1} > {r.Field - r.Label:F1})");
            }
            // At the default height every row of every tab is in view, without scrolling.
            var tabs = Dialog()!.GetVisualDescendants().OfType<TabControl>().First();
            var grids = ((Control)((TabItem)tabs.Items[tab]!).Content!).GetVisualDescendants().OfType<TextBlock>()
                .Where(t => t.Classes.Contains("fieldLabel")).Select(t => (Control)t.GetVisualParent()!).ToList();
            double bottom = grids.Max(g => g.TranslatePoint(new Point(0, g.Bounds.Height), tabs)!.Value.Y);
            if (Dialog()!.Height == 540) Require(bottom <= tabs.Bounds.Height, $"{capture} tab {tab}: last row ends at {bottom:F1} below the page ({tabs.Bounds.Height:F1})");
            all.Add($"tab {tab}: {rows.Count} rows, label x {first.Label:F1}, field x {first.Field:F1}, right {first.Right:F1}, " +
                $"row heights {string.Join("/", grids.Select(g => g.Bounds.Height.ToString("F0")).Distinct())}, last row ends {bottom:F1} of {tabs.Bounds.Height:F1}");
            var content = (Control)Dialog()!.Content!;
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)content.Bounds.Width, (int)content.Bounds.Height));
            bitmap.Render(content);
            bitmap.Save(Path.Combine(Output, $"mixer-{capture}-tab{tab}.png"));
        }
        Result["columns-" + capture] = all;
    }

    private static async Task Run()
    {
        var before = Normalized(Project);
        var expected = Normalized(Reference);
        Require(!before.AsSpan().SequenceEqual(expected), "reference differs from the stripped copy");
        var dialog = await Open();
        await Tab(1);
        Require(!Named<Button>("Preview transition to snapshot").IsEnabled, "Preview needs Play");
        Click(Named<Button>("Add snapshot"));
        await Delay(100);
        Named<TextBox>("Snapshot name").Text = "Night";
        Named<Slider>("Snapshot Music").Value = 50;
        Named<Slider>("Snapshot Effects").Value = 60;
        await Tab(2);
        Click(Named<Button>("Add ducking rule"));
        await Delay(100);
        Named<ComboBox>("Ducking Trigger").SelectedItem = AudioBus.Voice;
        Named<ComboBox>("Ducking Target").SelectedItem = AudioBus.Music;
        Named<NumericUpDown>("Ducking Depth").Value = -8;
        Named<NumericUpDown>("Ducking Attack").Value = .05m;
        Named<NumericUpDown>("Ducking Release").Value = .4m;
        Require(Hint() == "", "valid values show no message: " + Hint());

        // Invalid values: the runtime's GameSettings message, and Save defaults keeps the dialog open.
        var messages = new Dictionary<string, string>();
        var fileBefore = Saved();
        async Task Invalid(string what, int tab, Action set, Action reset, string? expect)
        {
            await Tab(tab);
            set();
            await Delay(100);
            Require(expect != null && Hint() == expect, $"{what}: message '{Hint()}', expected '{expect}'");
            Click(ButtonText("Save defaults"));
            await Delay(200);
            Require(Dialog() != null && !dialog.IsCompleted, $"{what}: dialog stays open");
            messages[what] = Hint();
            reset();
            await Delay(100);
            Require(Hint() == "", $"{what}: message clears after the fix ('{Hint()}')");
        }
        var ducking = new GameSettings();
        await Invalid("same bus", 2, () => Named<ComboBox>("Ducking Target").SelectedItem = AudioBus.Voice,
            () => Named<ComboBox>("Ducking Target").SelectedItem = AudioBus.Music,
            RuleMessage(ducking with { AudioDucking = [new(AudioBus.Voice, AudioBus.Voice)] }));
        await Invalid("depth -90 dB", 2, () => Named<NumericUpDown>("Ducking Depth").Value = -90, () => Named<NumericUpDown>("Ducking Depth").Value = -8,
            RuleMessage(ducking with { AudioDucking = [new(Decibels: -90)] }));
        await Invalid("depth +3 dB", 2, () => Named<NumericUpDown>("Ducking Depth").Value = 3, () => Named<NumericUpDown>("Ducking Depth").Value = -8,
            RuleMessage(ducking with { AudioDucking = [new(Decibels: 3)] }));
        await Invalid("release 11 s", 2, () => Named<NumericUpDown>("Ducking Release").Value = 11, () => Named<NumericUpDown>("Ducking Release").Value = .4m,
            RuleMessage(ducking with { AudioDucking = [new(Release: 11)] }));
        await Invalid("attack -1 s", 2, () => Named<NumericUpDown>("Ducking Attack").Value = -1, () => Named<NumericUpDown>("Ducking Attack").Value = .05m,
            RuleMessage(ducking with { AudioDucking = [new(Attack: -1)] }));
        await Invalid("empty snapshot name", 1, () => Named<TextBox>("Snapshot name").Text = "  ", () => Named<TextBox>("Snapshot name").Text = "Night",
            RuleMessage(ducking with { AudioSnapshots = [new("")] }));
        await Invalid("65-character name", 1, () => Named<TextBox>("Snapshot name").Text = new string('N', 65), () => Named<TextBox>("Snapshot name").Text = "Night",
            RuleMessage(ducking with { AudioSnapshots = [new(new string('N', 65))] }));
        await Invalid("duplicate snapshot", 1, () => { Click(Named<Button>("Add snapshot")); Named<TextBox>("Snapshot name").Text = "Night"; },
            () => Click(Named<Button>("Remove snapshot")), RuleMessage(ducking with { AudioSnapshots = [new("Night"), new("Night")] }));
        Require(Saved().AsSpan().SequenceEqual(fileBefore), "invalid values left GameSettings unchanged");
        Result["messages"] = messages;

        // Columns at three main-window widths (the modal dialog keeps its own size), two dialog widths and a 64-character name.
        foreach (var width in new[] { 1280, 1600, 1920 }) { main.Width = width; await Columns($"main{width}"); }
        main.Width = 1280;
        foreach (var width in new[] { 300, 480 }) { Dialog()!.Width = width; await Columns($"dialog{width}"); }
        Dialog()!.Width = 340;
        await Tab(1);
        Named<TextBox>("Snapshot name").Text = new string('N', 64);
        await Columns("long-name");
        await Tab(1);
        Named<TextBox>("Snapshot name").Text = "Night";
        await Delay(100);

        Click(ButtonText("Save defaults"));
        await dialog;
        await Delay(300);
        var ui = Saved();
        File.WriteAllBytes(Path.Combine(Output, "GameSettings.ui"), ui);
        File.WriteAllBytes(Path.Combine(Output, "GameSettings.yaml-only"), expected);
        Require(ui.AsSpan().SequenceEqual(expected), "UI save is byte-identical to the YAML-only GameSettings");
        Result["bytes"] = new { ui = ui.Length, yamlOnly = expected.Length, identical = true };

        await (Task)Call(main, "RestoreEdit", false)!;
        await Delay(300);
        Require(Saved().AsSpan().SequenceEqual(before) && Scene.Settings.AudioSnapshots == null && Scene.Settings.AudioDucking == null, "one Undo restores the GameSettings");
        await (Task)Call(main, "RestoreEdit", true)!;
        await Delay(300);
        Require(Saved().AsSpan().SequenceEqual(expected), "Redo restores the edited GameSettings");

        // Reopened and saved without a change: no Undo step.
        var history = Get(main, "history")!;
        int position = (int)Get(history, "position")!;
        dialog = await Open();
        await Tab(1);
        Require(Named<TextBox>("Snapshot name").Text == "Night" && Named<Slider>("Snapshot Music").Value == 50, "reopened snapshot");
        Click(ButtonText("Save defaults"));
        await dialog;
        Require((int)Get(history, "position")! == position && Saved().AsSpan().SequenceEqual(expected), "unchanged Save defaults: same bytes, no Undo step");
        Result["undo"] = "one Undo restores the stripped file, Redo the edited file, unchanged Save adds nothing";

        await Preview(expected);
    }

    // Scene Play: Blend and Reset drive the running mixer; a Play-only depth change and Cancel leave the project alone.
    private static async Task Preview(byte[] expected)
    {
        var view = main.FindControl<Control>("Viewport")!;
        var play = main.FindControl<Button>("PlayButton")!;
        Click(play);
        await Until(() => Get(view, "PlayWorld") != null, "Play", 60);
        await Delay(800);
        var mixer = ((SceneGame)Get(view, "PlayWorld")!).Mixer;
        float[] Targets() => [(float)Get(mixer, "snapMasterTarget")!, .. (float[])Get(mixer, "snapTarget")!];
        float DuckGain() => (float)Get(((Array)Get(mixer, "ducking")!).GetValue(0)!, "Gain")!;
        var dialog = await Open();
        await Tab(1);
        Require(Named<Button>("Preview transition to snapshot").IsEnabled, "Preview enabled in Play");
        Named<NumericUpDown>("Preview seconds").Value = 1;
        Click(Named<Button>("Preview transition to snapshot"));
        await Delay(100);
        var night = Targets();
        Require(night.SequenceEqual([1f, .6f, .5f, 1f, 1f, 1f]), "Blend targets Night: " + string.Join(",", night));
        Click(Named<Button>("Preview transition back to 100%"));
        await Delay(100);
        Require(Targets().All(t => t == 1), "Reset targets 100%");
        await Tab(2);
        Named<NumericUpDown>("Ducking Depth").Value = -20;
        await Delay(100);
        float live = DuckGain();
        Require(Math.Abs(live - MathF.Pow(10, -1)) < 1e-6, "Play depth change reaches the mixer: " + live);
        await Tab(1);
        Click(Named<Button>("Preview transition to snapshot"));
        await Delay(100);
        Click(ButtonText("Cancel"));
        await dialog;
        await Delay(200);
        Require(Targets().All(t => t == 1) && Math.Abs(DuckGain() - MathF.Pow(10, -8 / 20f)) < 1e-6, "Cancel restores the mixer");
        Require(Saved().AsSpan().SequenceEqual(expected), "Play edits never write GameSettings");
        Result["preview"] = new { night, depthInPlay = live, afterCancel = Targets() };
        Click(play);
        await Until(() => Get(view, "PlayWorld") == null, "Stop", 60);
        await Delay(300);
    }
}
