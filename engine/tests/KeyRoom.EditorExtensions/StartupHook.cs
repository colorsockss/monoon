using System.Collections;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Engine.Core;
using Engine.Editor;

// Loaded into the packaged editor with DOTNET_STARTUP_HOOKS (roadmap 11.10). Exercises KeyRoom's Editor/KeyRoomTools.cs:
// the Place Coin menu command, the CollectCoin Inspector, the Coins panel, and an extension compile error.
public static class StartupHook
{
    // MONOON_QA_SLOW scales waits and time limits for emulated x64 guests (TCG); 1 on a normal desktop.
    private static readonly double Slow = double.TryParse(Environment.GetEnvironmentVariable("MONOON_QA_SLOW"), System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 1;
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_EXT_OUTPUT")!;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Dictionary<string, object?> Result = [];
    private static Window main = null!;
    private static SceneViewport view = null!;
    private static DockHost dock = null!;

    public static void Initialize() => Task.Run(async () =>
    {
        while (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: not null })
            await Delay(100);
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var life = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
            try
            {
                main = life.MainWindow!;
                view = main.FindControl<SceneViewport>("Viewport")!;
                dock = main.FindControl<DockHost>("Dock")!;
                await Until(() => main.IsVisible && Scene.Objects.Any(o => o.Name == "Coin counter"), "KeyRoom");
                Result["os"] = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
                await Resize(1280, 800);
                await Run();
                File.WriteAllText(Path.Combine(Output, "extensions.json"), JsonSerializer.Serialize(Result, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(Output, "error.txt"), ex.ToString()); }
            finally { life.Shutdown(); }
        });
    });

    private static async Task Run()
    {
        Step("extension_menu", MenuItem("Project", "Extensions").Menu!.Items.OfType<NativeMenuItem>().Select(i => i.Header).ToArray());
        Step("window_menu_has_coins", MenuItem("Window", "Coins") != null);

        // Menu command moved from Editor/KeyRoom.tools to C#: one undoable placement at the view centre.
        int coins = CoinCount();
        Choose("Project", "Extensions", "Place Coin");
        await Until(() => CoinCount() == coins + 1, "Place Coin");
        Step("place_coin", new { before = coins, after = CoinCount(), selected = Selected?.Name, prefab = Selected?.Prefab != null });

        // Custom Inspector for CollectCoin (the placed coin is selected): the built-in LootTable row plus two extension rows.
        await Until(() => Label("Coins") != null && Label("LootTable") != null, "CollectCoin Inspector");
        Label("Place")!.BringIntoView();
        await Delay(600);
        Step("inspector_1280", Rows("LootTable", "Coins", "Place"));
        Step("inspector_coin_text", ((TextBlock)Input(Label("Coins")!)).Text);
        ScreenCapture("inspector-1280");

        // Dockable panel from Window → Coins.
        Choose("Window", "Coins");
        await Until(() => dock.IsOpen("Coins") && Label("In scene", inPanel: true) is { IsEffectivelyVisible: true }, "Coins panel");
        await Delay(700);
        Step("panel_1280", PanelRows());
        ScreenCapture("panel-1280");
        foreach (var (w, h) in new[] { (1600, 900), (1920, 1080), (1280, 800) })
        {
            await Resize(w, h);
            Step($"inspector_{w}", Rows("LootTable", "Coins", "Place"));
            Step($"panel_{w}", PanelRows());
        }
        var place = (Button)Input(Label("Place", inPanel: true)!);
        place.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, place));
        await Until(() => CoinCount() == coins + 2, "panel Place Coin");
        await Until(() => ((TextBlock)Input(Label("In scene", inPanel: true)!)).Text == (coins + 2).ToString(), "panel count", 5);
        Step("panel_place", new { after = CoinCount(), shown = ((TextBlock)Input(Label("In scene", inPanel: true)!)).Text });
        Choose("Edit", "Undo");
        await Until(() => CoinCount() == coins + 1, "Undo panel placement");
        Choose("Edit", "Undo");
        await Until(() => CoinCount() == coins, "Undo Place Coin");
        Step("undo_both", CoinCount());

        // A compile error in Editor/ unloads the extensions and names file:line in the Console; fixing it restores them.
        string project = Scene.ProjectDirectory!;
        string broken = Path.Combine(project, "Editor", "Broken.cs");
        File.WriteAllText(broken, "public sealed class Broken\n{\n    int x = ;\n}\n");
        Call(main, "RefreshExtensions");
        var error = ConsoleRows().LastOrDefault(r => r.Level == "Error" && r.File != null);
        Step("extension_compile_error", new { error?.Message, error?.File, error?.Line,
            menu = MenuItem("Project", "Extensions").Menu!.Items.Count, panel = dock.Panels.Any(p => DockHost.GetPanel(p) == "Coins") });
        await Delay(300);
        Call(main, "ShowConsole");
        await Delay(500);
        ScreenCapture("extension-error-1280");
        File.Delete(broken);
        Call(main, "RefreshExtensions");
        Step("extension_restored", MenuItem("Project", "Extensions").Menu!.Items.OfType<NativeMenuItem>().Select(i => i.Header).ToArray());
    }

    // ---- helpers
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static Task Delay(int ms) => Task.Delay((int)(ms * Slow));
    private static async Task Until(Func<bool> done, string what, int seconds = 60)
    {
        var limit = DateTime.UtcNow.AddSeconds(seconds * Slow);
        while (!done()) { if (DateTime.UtcNow > limit) throw new TimeoutException(what); await Delay(50); }
    }
    private static object? Get(object target, string name) =>
        target.GetType().GetProperty(name, Any) is { } p ? p.GetValue(target) : target.GetType().GetField(name, Any)?.GetValue(target);
    private static object? Call(object target, string method, params object?[] args) =>
        target.GetType().GetMethods(Any).First(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(target, args);
    private static SceneDocument Scene => (SceneDocument)Get(main, "Scene")!;
    private static SceneObject? Selected => (SceneObject?)Get(view, "Selected");
    private static int CoinCount() => Scene.Objects.Count(o => o.Components.OfType<ScriptComponent>().Any(s => s.Script == "CollectCoin"));
    private static Rect In(Visual c) => new(c.TranslatePoint(default, main)!.Value, c.Bounds.Size);
    private static Control Panel(string id) => dock.Panels.Single(p => DockHost.GetPanel(p) == id);
    private static async Task Resize(int width, int height) { main.Width = width; main.Height = height; await Delay(900); }
    private static void Step(string name, object? value)
    {
        Result[name] = value;
        File.AppendAllText(Path.Combine(Output, "extensions.log"), $"{name}: {JsonSerializer.Serialize(value)}\n");
    }

    private static TextBlock? Label(string text, bool inPanel = false) =>
        (inPanel ? Panel("Coins") : Panel("Inspector")).GetVisualDescendants().OfType<TextBlock>()
            .FirstOrDefault(t => t.Text == text && t.Classes.Contains("fieldLabel") && t.IsEffectivelyVisible);
    private static Control Input(TextBlock label) => ((Grid)label.Parent!).Children.First(c => Grid.GetColumn(c) == 1);

    private static object Row(TextBlock label)
    {
        var input = Input(label);
        double centre(Control c) => In(c).Y + In(c).Height / 2;
        return new { label = label.Text, label_x = Math.Round(In(label).X, 1), input_x = Math.Round(In(input).X, 1),
            column = Math.Round(In(input).X - In(label).X, 1), right = Math.Round(In(input).Right, 1),
            vertical = Math.Round(Math.Abs(centre(label) - centre(input)), 1) };
    }

    // Extension rows against the built-in LootTable row and the Name row of the Inspector.
    private static object Rows(params string[] labels)
    {
        var rows = labels.Select(l => Label(l) ?? throw new InvalidDataException(l)).ToArray();
        var xs = rows.Select(r => In(r).X).ToArray();
        var inputs = rows.Select(r => In(Input(r)).X).ToArray();
        var rights = rows.Select(r => In(Input(r)).Right).ToArray();
        var name = main.FindControl<TextBox>("ObjectName")!;
        var nameLabel = ((Grid)name.Parent!).Children.OfType<TextBlock>().First();
        return new { window = Math.Round(main.Bounds.Width), rows = rows.Select(Row).ToArray(),
            label_drift = Math.Round(xs.Max() - xs.Min(), 2), input_drift = Math.Round(inputs.Max() - inputs.Min(), 2),
            right_drift = Math.Round(rights.Max() - rights.Min(), 2),
            name_column = Math.Round(In(name).X - In(nameLabel).X, 1) };
    }

    // Panel rows share the Inspector's label column (56 + 4) and the panel padding of the Inspector's Name row.
    private static object PanelRows()
    {
        var rows = new[] { "In scene", "Prefab", "Place" }.Select(l => Label(l, inPanel: true) ?? throw new InvalidDataException(l)).ToArray();
        var panel = In(Panel("Coins"));
        var inspector = In(Panel("Inspector"));
        var name = main.FindControl<TextBox>("ObjectName")!;
        var nameLabel = ((Grid)name.Parent!).Children.OfType<TextBlock>().First();
        var inputs = rows.Select(r => In(Input(r)).X).ToArray();
        return new { window = Math.Round(main.Bounds.Width), panel = new[] { Math.Round(panel.X), Math.Round(panel.Y), Math.Round(panel.Width), Math.Round(panel.Height) },
            rows = rows.Select(Row).ToArray(), input_drift = Math.Round(inputs.Max() - inputs.Min(), 2),
            padding = Math.Round(In(rows[0]).X - panel.X, 1), inspector_padding = Math.Round(In(nameLabel).X - inspector.X, 1),
            inspector_name_column = Math.Round(In(name).X - In(nameLabel).X, 1) };
    }

    private sealed record Row2(string Level, string Message, string? File, int Line);
    private static List<Row2> ConsoleRows()
    {
        var rows = new List<Row2>();
        foreach (var row in (IEnumerable)Get(main, "consoleRows")!)
            rows.Add(new(Get(row, "Level")!.ToString()!, (string)Get(row, "Message")!, (string?)Get(row, "File"), (int)Get(row, "Line")!));
        return rows;
    }

    private static NativeMenuItem MenuItem(params string[] path)
    {
        var items = NativeMenu.GetMenu(main)!.Items;
        NativeMenuItem? item = null;
        foreach (var header in path)
        {
            item = items.OfType<NativeMenuItem>().Single(i => i.Header == header);
            items = item.Menu?.Items ?? [];
        }
        return item!;
    }
    // The same entry point the native menu bar uses for a click.
    private static void Choose(params string[] path)
    {
        var item = MenuItem(path);
        Require(item.IsEnabled, string.Join(" > ", path) + " is disabled");
        typeof(NativeMenuItem).GetMethods(Any).First(m => m.Name.Contains("RaiseClick") && m.GetParameters().Length == 0).Invoke(item, null);
    }

    [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.A.dylib")] private static extern nint objc_msgSend(nint receiver, nint selector);
    [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.A.dylib")] private static extern nint sel_registerName(string name);
    private static void ScreenCapture(string name)
    {
        // Windows and Linux have no native Scene view (Avalonia draws it): capture the window with RenderTargetBitmap there.
        if (main.TryGetPlatformHandle() is not { HandleDescriptor: "NSWindow" } handle)
        {
            using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)main.Bounds.Width, (int)main.Bounds.Height));
            bitmap.Render(main);
            bitmap.Save(Path.Combine(Output, name + ".png"));
            return;
        }
        var number = objc_msgSend(handle.Handle, sel_registerName("windowNumber"));
        using var capture = System.Diagnostics.Process.Start("/usr/sbin/screencapture", ["-x", "-o", "-l", number.ToString(), Path.Combine(Output, name + ".png")])!;
        capture.WaitForExit();
    }
}
