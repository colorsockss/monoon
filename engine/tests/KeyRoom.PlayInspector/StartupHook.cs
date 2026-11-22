using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Engine.Core;
using Engine.Editor;

// Loaded only by the packaged-editor regression. OS input comes from verify.py.
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_PLAY_INSPECTOR_QA")!;
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int sequence;

    public static void Initialize() => Task.Run(async () =>
    {
        try
        {
            while (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: not null })
                await Task.Delay(100);
            await Dispatcher.UIThread.InvokeAsync(async () => await Run());
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(Output, "error.txt"), error.ToString()); }
    });

    private static async Task Until(Func<bool> condition, string message)
    {
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException(message);
            await Task.Delay(70);
        }
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }

    private static async Task Input(string action, Control control, string? value = null)
    {
        control.BringIntoView();
        var top = TopLevel.GetTopLevel(control)!;
        // A scroll request and hit-test geometry can settle on different desktop frames.
        // Click only once the actual hit target and its screen coordinates stay stable.
        PixelPoint point = default;
        int stable = 0;
        await Until(() =>
        {
            var center = new Point(control.Bounds.Width * .5, control.Bounds.Height * .5);
            var next = control.PointToScreen(center);
            var hit = top.InputHitTest(top.PointToClient(next)) as Visual;
            bool matches = hit == control || hit?.GetVisualAncestors().Contains(control) == true;
            if (control is TextBlock && control.FindAncestorOfType<TreeViewItem>() is {} item)
                matches = hit == item || hit?.FindAncestorOfType<TreeViewItem>() == item;
            stable = matches && next == point ? stable + 1 : 0;
            point = next;
            return stable >= 7;
        }, $"stable hit target for {action} on {AutomationProperties.GetName(control)}");
        var file = Path.Combine(Output, $"input-{++sequence}");
        File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(new { action, x = point.X, y = point.Y, value }));
        File.Move(file + ".tmp", file + ".json");
        await Until(() => File.Exists(file + ".done"), action);
    }

    private static async Task Run()
    {
        var window = ((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).MainWindow!;
        T Control<T>(string id) where T : Control => window.FindControl<T>(id)!;
        var viewport = Control<Control>("Viewport");
        var play = Control<Button>("PlayButton");
        var apply = Control<Button>("ApplyPlayValues");
        var hierarchy = Control<TreeView>("Hierarchy");
        void Trace(string message) => File.AppendAllText(Path.Combine(Output, "input.log"), message + Environment.NewLine);
        window.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
            Trace($"click {e.GetPosition(window)} on {e.Source?.GetType().Name}, selected {((hierarchy.SelectedItem as TreeViewItem)?.Tag as SceneObject)?.Name}"),
            RoutingStrategies.Tunnel, handledEventsToo: true);
        hierarchy.SelectionChanged += (_, _) => Trace($"selected {((hierarchy.SelectedItem as TreeViewItem)?.Tag as SceneObject)?.Name}");
        var project = Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!;
        string path = Path.Combine(project, "Scenes", "KeyRoom.scene");
        string OnDisk() => SceneDocument.Load(path).CaptureObjects();
        SceneDocument Authored() => (SceneDocument)window.GetType().GetProperty("Scene", Members)!.GetValue(window)!;
        var history = window.GetType().GetField("history", Members)!.GetValue(window)!;
        bool CanUndo() => (bool)history.GetType().GetProperty("CanUndo")!.GetValue(history)!;
        GameSession? Session() => (GameSession?)viewport.GetType().GetField("game", Members)!.GetValue(viewport);
        static SceneObject Player(IEnumerable<SceneObject> objects) => objects.Single(o => o.Name == "Player");
        static ScriptComponent Movement(SceneObject o) => o.Components.OfType<ScriptComponent>().Single(c => c.Script == "PlayerMovement");
        float SavedSpeed() => Movement(Player(SceneDocument.Load(path).Objects)).Values.TryGetValue("MoveSpeed", out var v) ? v.GetSingle() : 240;
        T Field<T>(string name) where T : Control => window.GetVisualDescendants().OfType<T>()
            .Single(c => c.IsEffectivelyVisible && AutomationProperties.GetName(c) == name);

        await Until(() => window.IsVisible && hierarchy.Items.OfType<TreeViewItem>().Any(i => (string?)i.Header == "Player"), "KeyRoom Hierarchy");
        window.Width = 1040;
        window.Height = 720;
        if (OperatingSystem.IsWindows() && window.Screens.Primary is {} screen)
        {
            var area = screen.WorkingArea;
            window.Width = Math.Min(window.Width, (area.Width - 32) / screen.Scaling);
            window.Height = Math.Min(window.Height, (area.Height - 64) / screen.Scaling);
            window.Position = new PixelPoint(area.X + 12, area.Y + 12);
        }
        window.Activate();
        var player = Player(Authored().Objects);
        var expected = SceneDocument.Load(path);
        string baseline = Authored().CaptureObjects(), initialDisk = expected.CaptureObjects();
        Check(!CanUndo() && SavedSpeed() == 240, "Expected a fresh KeyRoom copy at 240");
        async Task SelectPlayer()
        {
            var row = hierarchy.Items.OfType<TreeViewItem>().Single(i => (string?)i.Header == "Player");
            bool IsPlayerSelected() => hierarchy.SelectedItem == row && Control<TextBox>("ObjectName").IsEffectivelyVisible
                && Control<TextBox>("ObjectName").Text == "Player";
            if (IsPlayerSelected()) return;
            await Input("click", row.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Player"));
            await Until(IsPlayerSelected, "select Player");
        }
        async Task Start()
        {
            await Input("click", play);
            await Until(() => Session() != null && Session()!.Current.Time.TickCount > 1, "Scene Play");
            await SelectPlayer();
            foreach (var drawer in window.GetVisualDescendants().OfType<ComponentSection>()
                .Where(d => d.Header is "Sprite Renderer" or "Sorting Group 2D").ToArray())
            {
                var toggle = drawer.GetVisualDescendants().OfType<ToggleButton>().First(t => t.Name == "HeaderToggle");
                if (toggle.IsChecked == true) await Input("click", toggle);
            }
            await Until(() => window.GetVisualDescendants().OfType<TextBox>().Any(b =>
                b.IsEffectivelyVisible && AutomationProperties.GetName(b) == "PlayerMovement.MoveSpeed"), "live Player field");
        }
        async Task Stop()
        {
            await Input("click", play);
            await Until(() => Session() == null, "Scene Stop");
        }
        async Task Edit(string name, string number)
        {
            var box = Field<TextBox>(name);
            await Input("click", box);
            await Until(() => box.IsKeyboardFocusWithin, "focus " + name);
            await Input("number", box, number);
            await Until(() => Field<CheckBox>("Apply " + name).IsChecked == true, "Committed " + name);
        }
        float[] Measure(float expected)
        {
            var game = Session()!;
            float Y() => game.Current.TryGetPosition(player.Id, out var p) ? p.Y : throw new Exception("Player missing");
            float start = Y();
            float Step(string key)
            {
                Check(game.Actions.Press(key), "Unbound input " + key);
                float first = 0;
                for (int i = 0; i < 6; i++)
                {
                    game.FixedUpdate(game.Actions.Take(), 1f / 60);
                    if (i == 0) first = Y();
                }
                game.Actions.Release(key);
                game.FixedUpdate(game.Actions.Take(), 1f / 60);
                return first;
            }
            float first = start - Step("Up");
            float total = start - Y();
            Check(Math.Abs(first - expected / 60) < .001 && Math.Abs(total - expected / 10) < .001,
                $"Speed {expected}: first {first}, six ticks {total}");
            Step("Down");
            Check(Math.Abs(Y() - start) < .001, "Return to start");
            return [first, total];
        }

        await Start();
        var slow = Measure(240);
        await Edit("PlayerMovement.MoveSpeed", "320");
        var fast = Measure(320);
        Check(OnDisk() == initialDisk && Authored().CaptureObjects() == baseline && !CanUndo(), "Play edit escaped into the source");
        await Stop();
        Check(OnDisk() == initialDisk && !CanUndo(), "Plain Stop changed disk or history");
        await Start();
        var discarded = Measure(240);
        await Edit("Transform.Local X", "280");
        var exclude = Field<CheckBox>("Apply Transform.Local X");
        await Input("click", exclude);
        await Until(() => exclude.IsChecked == false, "deselect Local X");
        await Edit("PlayerMovement.MoveSpeed", "320");
        var selective = Measure(320);
        await Input("capture", Field<TextBox>("PlayerMovement.MoveSpeed"));
        await Input("click", apply);
        await Until(() => SavedSpeed() == 320 && CanUndo(), "selected value saved");
        Movement(Player(expected.Objects)).Values["MoveSpeed"] = JsonSerializer.SerializeToElement(320f);
        Check(Authored().CaptureObjects() == expected.CaptureObjects() && OnDisk() == expected.CaptureObjects(),
            "Only PlayerMovement.MoveSpeed should have been applied");
        var savedX = Player(SceneDocument.Load(path).Objects).X;
        var selectedSpeedSaved = SavedSpeed();
        await Stop();
        await Start();
        var persisted = Measure(320);
        await Stop();
        await Input("undo", play);
        await Until(() => !CanUndo() && Authored().CaptureObjects() == baseline, "one Undo restores original scene");
        await Input("save", play);
        await Until(() => SavedSpeed() == 240 && OnDisk() == initialDisk, "save and reopen restored scene");
        var report = new { platform = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            fixedHz = 60, ticks = 6, displacementFirstAndSixTicks = new { slow, fast, discarded, selective, persisted },
            initialX = player.X, unselectedX = 280, savedX, selectedSpeedSaved, restoredSpeed = SavedSpeed(), undoSteps = 1 };
        File.WriteAllText(Path.Combine(Output, "result.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        window.Close();
    }
}
