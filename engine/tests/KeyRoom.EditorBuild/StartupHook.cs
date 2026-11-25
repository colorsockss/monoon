using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Engine.Core;

// Drives File > Build Game… in the packaged editor through the dialog's own controls. MONOON_BUILD_STEP picks the session:
// "release" captures the dialog at 1280/1920px, saves settings, reopens to read them back and builds Release with packages;
// "aot" reopens after an editor restart, checks the platform fallback and builds Native AOT. Any other window fails the run.
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_BUILD_EDITOR")!;
    private static readonly string Step = Environment.GetEnvironmentVariable("MONOON_BUILD_STEP")!;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string DialogTitle = "Build Game";

    public static void Initialize()
    {
        // The Native AOT build starts `dotnet publish`; it must not inherit this hook.
        Environment.SetEnvironmentVariable("DOTNET_STARTUP_HOOKS", null);
        Start(Path.Combine(Output, "error.txt"), life => Run(life.MainWindow!), allow: [DialogTitle]);
    }

    private static object? Call(object target, string method, params object?[] args) =>
        target.GetType().GetMethod(method, Any)!.Invoke(target, args);
    private static SceneDocument Scene(Window w) => (SceneDocument)w.GetType().GetProperty("Scene", Any)!.GetValue(w)!;
    private static bool Busy(Window w) => (bool)w.GetType().GetField("busy", Any)!.GetValue(w)!;
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, b));

    private static void Capture(Visual area, string name)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(area.Bounds.Width), (int)Math.Ceiling(area.Bounds.Height)));
        bitmap.Render(area);
        bitmap.Save(Path.Combine(Output, name + ".png"));
    }

    // Opens the dialog the way the File menu does (RunAction → BuildGame) and returns the dialog with the action's task.
    private static async Task<(Window dialog, Task action)> Open(Window main)
    {
        Task? action = null;
        Call(main, "RunAction", (Func<Task>)(() => action = (Task)Call(main, "BuildGame")!));
        await Until(() => main.OwnedWindows.Any(w => w.Title == DialogTitle && w.IsVisible), "Build Game dialog");
        await Delay(500);
        return (main.OwnedWindows.Single(w => w.Title == DialogTitle), action!);
    }

    private static async Task Finish(Window main, Task action, string what, int seconds)
    {
        await Until(() => action.IsCompleted && !Busy(main), what, seconds);
        await action; // rethrows the editor's own failure
    }

    private static Grid Rows(Window dialog) =>
        dialog.GetVisualDescendants().OfType<Grid>().First(g => g.Children.OfType<TextBlock>().Any(t => t.Text == "Platform"));

    private static T Field<T>(Window dialog, string label) where T : Control
    {
        var grid = Rows(dialog);
        var text = grid.Children.OfType<TextBlock>().Single(t => t.Text == label && Grid.GetColumn(t) == 0);
        return (T)grid.Children.Single(c => Grid.GetColumn(c) == 1 && Grid.GetRow(c) == Grid.GetRow(text));
    }

    private static Button FindButton(Window dialog, string content) =>
        dialog.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == content);

    private static string? Label(object? item) => item?.GetType().GetProperty("Label")?.GetValue(item) as string ?? item as string;

    private static object State(Window d) => new
    {
        platform = Label(Field<ComboBox>(d, "Platform").SelectedItem),
        build_type = Label(Field<ComboBox>(d, "Build type").SelectedItem),
        build_types = Field<ComboBox>(d, "Build type").Items.Select(Label).ToArray(),
        profile = Label(Field<ComboBox>(d, "Profile").SelectedItem),
        package = Field<CheckBox>(d, "Package").IsChecked == true,
        full_content = Field<CheckBox>(d, "Content").IsChecked == true,
        icon = Label(Field<ComboBox>(d, "Icon").SelectedItem),
    };

    private static void Select(ComboBox box, string label)
    {
        box.SelectedItem = box.Items.Single(i => Label(i) == label);
        if (Label(box.SelectedItem) != label) throw new InvalidOperationException($"Could not select {label}.");
    }

    // Every label/field pair in the dialog grid: one label x, one field x, centres within 1px.
    private static object Measure(Window main, Window d, string capture)
    {
        var grid = Rows(d);
        var root = (Visual)d.Content!;
        double X(Visual v) => v.TranslatePoint(default, root)!.Value.X;
        double Mid(Control c) => c.TranslatePoint(new(0, c.Bounds.Height / 2), root)!.Value.Y;
        var rows = grid.Children.OfType<TextBlock>().Where(t => Grid.GetColumn(t) == 0).Select(label =>
        {
            var field = grid.Children.Single(c => Grid.GetColumn(c) == 1 && Grid.GetRow(c) == Grid.GetRow(label));
            return new { text = label.Text, label_x = Math.Round(X(label), 2), field_x = Math.Round(X(field), 2),
                field_width = Math.Round(field.Bounds.Width, 2), field_height = Math.Round(field.Bounds.Height, 2),
                field_type = field.GetType().Name, center_offset = Math.Round(Math.Abs(Mid(label) - Mid(field)), 2) };
        }).ToArray();
        var buttons = d.GetVisualDescendants().OfType<Button>().Where(b => b.Content is "Save Settings" or "Build To…" or "Build")
            .Select(b => new { text = (string)b.Content!, y = Math.Round(b.TranslatePoint(default, root)!.Value.Y, 2), height = Math.Round(b.Bounds.Height, 2) }).ToArray();
        return new { capture, main_width = main.Bounds.Width, dialog_width = d.Bounds.Width, dialog_height = d.Bounds.Height, rows, buttons };
    }

    private static async Task Run(Window main)
    {
        string root = Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!;
        await Until(() => Scene(main).Objects.Count > 1, "KeyRoom");
        main.Activate();
        var result = new Dictionary<string, object?> { ["step"] = Step };
        if (Step == "release")
        {
            var layouts = new List<object>();
            foreach (var (w, h) in new[] { (1280, 800), (1920, 1080) })
            {
                main.Width = w; main.Height = h;
                await Delay(700);
                var (d, action) = await Open(main);
                if (layouts.Count == 0) result["initial"] = State(d);
                Capture(d, $"dialog-{w}");
                layouts.Add(Measure(main, d, $"dialog-{w}"));
                d.Close();
                await Finish(main, action, "closing the dialog", 30);
            }
            result["layouts"] = layouts;
            main.Width = 1280; main.Height = 800;
            await Delay(500);

            var (dialog, save) = await Open(main);
            Select(Field<ComboBox>(dialog, "Platform"), "osx-arm64");
            Select(Field<ComboBox>(dialog, "Build type"), "Release");
            Select(Field<ComboBox>(dialog, "Profile"), "Distribution");
            Field<CheckBox>(dialog, "Package").IsChecked = true;
            var icon = Field<ComboBox>(dialog, "Icon");
            Select(icon, icon.Items.Select(Label).Contains("Assets/Showcase/Key.png") ? "Assets/Showcase/Key.png" : Label(icon.Items.Cast<object>().ElementAt(1))!);
            result["chosen"] = State(dialog);
            Click(FindButton(dialog, "Save Settings"));
            await Finish(main, save, "Save Settings", 60);
            result["after_save_status"] = main.FindControl<TextBlock>("Status")!.Text;

            (dialog, var build) = await Open(main);
            result["reopened"] = State(dialog);
            Capture(dialog, "dialog-reopened");
            Click(FindButton(dialog, "Build"));
            await Finish(main, build, "Release build", 1200);
        }
        else
        {
            // A saved Native AOT choice for another platform shows Release, as switching Platform does.
            var file = Path.Combine(root, BuildSettings.FileName);
            var kept = File.ReadAllText(file);
            File.WriteAllText(file, "target: win-x64\ntype: native-aot\n");
            var (other, closeOther) = await Open(main);
            result["foreign_aot"] = State(other);
            other.Close();
            await Finish(main, closeOther, "closing the dialog", 30);
            File.WriteAllText(file, kept);

            var (dialog, build) = await Open(main);
            result["reopened"] = State(dialog);
            Select(Field<ComboBox>(dialog, "Platform"), "linux-x64");
            result["linux_build_types"] = Field<ComboBox>(dialog, "Build type").Items.Select(Label).ToArray();
            Select(Field<ComboBox>(dialog, "Platform"), "osx-arm64");
            Select(Field<ComboBox>(dialog, "Build type"), "Native AOT");
            result["chosen"] = State(dialog);
            Click(FindButton(dialog, "Build"));
            await Finish(main, build, "Native AOT build", 1800);
        }
        // What the build wrote to the Console, including the asset report summary, and how the panel shows it.
        var rows = (System.Collections.IEnumerable)main.GetType().GetField("consoleRows", Any)!.GetValue(main)!;
        result["console"] = rows.Cast<object>().Select(r => $"{r.GetType().GetProperty("Level")!.GetValue(r)}: {r.GetType().GetProperty("Message")!.GetValue(r)}").ToArray();
        Call(main, "ShowConsole");
        await Task.Delay(700);
        Capture(main, $"console-{Step}");
        result["status"] = main.FindControl<TextBlock>("Status")!.Text;
        result["build_settings"] = File.ReadAllText(Path.Combine(root, BuildSettings.FileName));
        result["game_icon"] = GameSettings.Load(root).Icon is Guid id ? ProjectAssets.ReadManifest(root).PathFor(id) : null;
        File.WriteAllText(Path.Combine(Output, $"result-{Step}.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        // The build marks the scene dirty (it saves GameSettings); save so closing does not ask.
        if (!await (Task<bool>)Call(main, "Save")!) throw new InvalidOperationException("Scene save cancelled");
    }
}
