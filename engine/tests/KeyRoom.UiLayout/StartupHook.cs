using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Engine.Core;
using Engine.Editor;
using Point = Avalonia.Point;

public static class StartupHook
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_EDITOR_QA")!;
    private static int sequence;
    private static bool Emulated => Environment.GetEnvironmentVariable("MONOON_EMULATED_UI") == "1";

    // No shutdown here: the run ends itself or the outside driver closes the editor.
    public static void Initialize() => Start(Path.Combine(Output, "error.txt"), _ => Run(), allow: ["UI Layout Preview"], shutdown: false);

    private static void Check(bool ok, string name)
    {
        if (!ok) throw new InvalidDataException(name);
    }

    // Waits on the outside input driver: keep the old limit.
    private static Task Until(Func<bool> ok, string name) => Probe.Until(ok, name, 300, poll: 80);

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
    private static T Property<T>(object target, string name) => (T)target.GetType().GetProperty(name, Members)!.GetValue(target)!;

    private static async Task Input(string action, string? value = null, PixelPoint point = default, int[]? panelEdges = null)
    {
        string file = Path.Combine(Output, $"input-{++sequence}");
        File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(new { action, value, x = point.X, y = point.Y, panelEdges, fill = action == "capture" ? "23364b" : null }));
        File.Move(file + ".tmp", file + ".json");
        await Until(() => File.Exists(file + ".done"), $"Input {sequence}: {action} {value} at {point}");
    }

    private static async Task Run()
    {
        var life = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
        var main = life.MainWindow!;
        var viewport = main.FindControl<SceneViewport>("Viewport")!;
        await Until(() => viewport.Scene.Objects.Any(o => o.Name == "Inventory"), "Open KeyRoom");
        // Keep a responsive desktop viewport on the emulated x64 machines.
        if (Emulated)
        {
            main.WindowState = WindowState.Normal;
            main.Width = 1050; main.Height = 700;
            var timer = (DispatcherTimer)viewport.GetType().GetField("timer", Members)!.GetValue(viewport)!;
            timer.Interval = TimeSpan.FromMilliseconds(100);
        }
        var scene = viewport.Scene;
        var project = scene.ProjectDirectory!;
        var sourceFiles = new[] { Path.Combine(project, "GameSettings"), Path.Combine(project, "Scenes", "Showcase.scene") };
        var before = sourceFiles.Select(File.ReadAllBytes).ToArray();
        var bag = scene.Objects.Single(o => o.Name == "Inventory");
        Guid Id(string name) => scene.Objects.Single(o => o.Name == name).Id;
        var manage = Id("Manage compass");
        var close = Id("Close inventory");
        var confirm = Id("Compass confirmation");
        var play = main.FindControl<Button>("PlayButton")!;
        var results = new List<object>();
        var sceneSizes = new[] { (800,450), (640,360), (1024,768), (480,720) };

        async Task SetPreview(int w, int h, bool reset = false)
        {
            var task = (Task)main.GetType().GetMethod("EditUiPreview", Members)!.Invoke(main, null)!;
            await Until(() => main.OwnedWindows.Any(d => d.Title == "UI Layout Preview"), "Preview dialog");
            var dialog = main.OwnedWindows.Single(d => d.Title == "UI Layout Preview");
            var boxes = dialog.GetVisualDescendants().OfType<TextBox>().ToArray();
            if (!reset)
            {
                var width = boxes.Single(b => AutomationProperties.GetName(b) == "Preview width");
                var height = boxes.Single(b => AutomationProperties.GetName(b) == "Preview height");
                var language = boxes.Single(b => AutomationProperties.GetName(b) == "Preview language");
                var scale = dialog.GetVisualDescendants().OfType<NumericUpDown>().Single();
                foreach (int windowWidth in new[] { 310, 270 })
                {
                    dialog.Width = windowWidth;
                    await Delay(150);
                    double x = width.TranslatePoint(default, dialog)!.Value.X;
                    foreach (var field in new Control[] { height, language, scale })
                    {
                        var left = field.TranslatePoint(default, dialog)!.Value.X;
                        Check(Math.Abs(x - left) <= 1 && left + field.Bounds.Width <= dialog.ClientSize.Width, "Preview field alignment");
                    }
                }
                width.Text = w.ToString(); height.Text = h.ToString(); language.Text = "ko"; scale.Value = 200;
            }
            Click(dialog.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == (reset ? "Use Project" : "Preview")));
            await task;
        }

        void SelectBag() => main.GetType().GetMethod("RebuildHierarchy", Members)!.Invoke(main, [bag]);
        ScreenUi PreviewUi() => Property<ScreenUi>(viewport, "PreviewUi");
        void Box(ScreenUi ui, Guid id, RectangleF expected)
        {
            var b = ui.Bounds(id)!.Value;
            Check(Math.Abs(b.X - expected.X) < .05 && Math.Abs(b.Y - expected.Y) < .05 &&
                Math.Abs(b.Width - expected.Width) < .05 && Math.Abs(b.Height - expected.Height) < .05,
                $"{id}: {b} != {expected}");
        }
        SelectBag();
        foreach (var (width,height) in sceneSizes)
        {
            await SetPreview(width,height);
            SelectBag();
            var panel = new RectangleF(64,36,width-128,height-72);
            var preview = PreviewUi();
            Box(preview,bag.Id,panel);
            Check(preview.Preferences.Locale == "ko" && preview.Preferences.TextScale == 2, "Preview language and text scale");
            if (width == 800)
            {
                // Change a parent at one resolution independently of the viewport dimensions.
                NumericUpDown? RightField() => main.GetVisualDescendants().OfType<NumericUpDown>()
                    .FirstOrDefault(c => AutomationProperties.GetName(c) == "Right margin");
                await Until(() => RightField() != null, "Inspector edge fields");
                var right = RightField()!;
                right.Value = 124;
                var narrow = PreviewUi();
                Box(narrow, bag.Id, new(64, 36, panel.Width-60, panel.Height));
                Check(Math.Abs(narrow.Bounds(close)!.Value.Right - (panel.Right - 60 - 18)) < .05, "Child follows edited parent width");
                await (Task)main.GetType().GetMethod("RestoreEdit", Members)!.Invoke(main, [false])!;
                // Undo replaces scene objects. Rebind the authored object before selecting it.
                bag = scene.Objects.Single(o => o.Name == "Inventory");
                SelectBag();
                Box(PreviewUi(), bag.Id, panel);
            }
            Click(play);
            await Until(() => viewport.IsPlaying, "Start Scene Play");
            var world = Property<SceneGame>(viewport, "PlayWorld");
            var ui = world.Ui;
            Check(world.LogicalSize.Width == width && world.LogicalSize.Height == height, "Play logical dimensions");
            Check(ui.Preferences.Locale == "ko" && ui.Preferences.TextScale == 2, "Play preferences");
            Check(ui.Translate("bag.title") == "가방", "Korean title");
            await Input("key","i");
            await Until(() => ui.ActiveInputMode == bag.Id, "Open bag with I");
            Box(ui,bag.Id,panel);
            var button = ui.Bounds(close)!.Value;
            var other = ui.Bounds(manage)!.Value;
            Check(button.Right == panel.Right-18 && Math.Abs(button.Bottom - (panel.Bottom-18)) < .05 &&
                button.Width == 128 && other.Left == panel.Left+18 && other.Right == button.Left-12 && !other.IntersectsWith(button), "Button margins");
            var item = ui.Bounds(Id("Inventory item"))!.Value;
            var quest = ui.Bounds(Id("Key quest"))!.Value;
            var language = ui.Bounds(Id("Bag language"))!.Value;
            Check(language.Bottom <= item.Top, "Language button overlaps the first item");
            Check(item.Right == panel.Right-18 && quest.Top >= item.Bottom && quest.Bottom < button.Top, "Korean rows fit above actions");
            // Compute OS pointer coordinates from the displayed letterbox, independently of the editor's input mapper.
            PixelPoint OnScreen(float x, float y)
            {
                double scale = Math.Min(viewport.Bounds.Width/width, viewport.Bounds.Height/height);
                var at = new Point((viewport.Bounds.Width-width*scale)/2+x*scale, (viewport.Bounds.Height-height*scale)/2+y*scale);
                return viewport.PointToScreen(at);
            }
            var topLeft = OnScreen(panel.Left,panel.Top);
            var bottomRight = OnScreen(panel.Right,panel.Bottom);
            await Input("capture", $"bag-{width}.png", panelEdges: [topLeft.X,topLeft.Y,bottomRight.X,bottomRight.Y]);
            Task Pointer(float x, float y) => Input("click", point: OnScreen(x,y));
            float midX = button.Left + button.Width/2, midY = button.Top + button.Height/2;
            foreach (var (x,y,dx,dy) in new[] { (button.Left,midY,1,0), (button.Right,midY,-1,0),
                    (midX,button.Top,0,1), (midX,button.Bottom,0,-1) })
            {
                await Pointer(x-3*dx,y-3*dy);
                await Delay(160);
                Check(ui.ActiveInputMode == bag.Id, "Outside click closed the bag");
                await Pointer(x+3*dx,y+3*dy);
                await Until(() => ui.ActiveInputMode == null, "Inside click closes bag");
                await Input("key","i");
                await Until(() => ui.ActiveInputMode == bag.Id, "Reopen bag");
            }
            await Pointer(other.Left+6,other.Top+6);
            await Until(() => ui.ActiveInputMode == confirm, "Manage compass opens nested panel");
            Box(ui,confirm,panel);
            results.Add(new { width, height, panel = new[] { panel.X,panel.Y,panel.Width,panel.Height },
                close = new[] { button.X,button.Y,button.Width,button.Height }, outsideMisses = 4, insideClicks = 4 });
            Click(play);
            await Until(() => !viewport.IsPlaying, "Stop");
        }
        await SetPreview(0,0,true);
        Check(PreviewUi().Preferences.TextScale == scene.Settings.Accessibility.TextScale && scene.Settings.Width == 800 &&
            main.FindControl<TextBlock>("ResolutionLabel")!.Text == "800 × 450", "Use Project restores defaults");
        Check(sourceFiles.Select((path,i) => File.ReadAllBytes(path).SequenceEqual(before[i])).All(v=>v), "Source settings or scene changed");
        await (Task)main.GetType().GetMethod("SwitchScene", Members)!.Invoke(main, ["ShowcaseGuide"])!;
        Check(viewport.Scene.Camera == null && viewport.Scene.IsUiOnly, "Camera-free ShowcaseGuide");
        await SetPreview(640,360);
        Check(Property<Point>(viewport, "EditCenter") == new Point(320,180), "UI-only canvas framing");
        File.WriteAllText(Path.Combine(Output, "result.json"), JsonSerializer.Serialize(new { os = RuntimeInformation.OSDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(), emulatedUiTimerMs = Emulated ? 100 : (int?)null, koreanScale = 2, parentResizeAndUndo = true, projectRestored = true, cameraFreePreview = true, results },
            new JsonSerializerOptions { WriteIndented = true }));
        life.Shutdown();
    }
}
