using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Engine.Core;

// Scene Play probe: captures the joint Inspector rows at three widths, then replays Cross.inputreplay in Play.
public static class StartupHook
{
    // MONOON_QA_SLOW scales waits and time limits for emulated x64 guests (TCG); 1 on a normal desktop.
    private static readonly double Slow = double.TryParse(Environment.GetEnvironmentVariable("MONOON_QA_SLOW"), System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 1;
    private static Task Delay(int ms) => Task.Delay((int)(ms * Slow));
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_JOINT_EDITOR")!;
    private const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
    public static void Initialize() => Task.Run(async () =>
    {
        while (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: not null })
            await Delay(100);
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var life = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
            try { await Test(life.MainWindow!); }
            catch (Exception ex) { File.WriteAllText(Path.Combine(Output, "error.txt"), ex.ToString()); }
            finally { life.Shutdown(); }
        });
    });

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static Rect In(Visual c, Visual root) => new(c.TranslatePoint(default, root)!.Value, c.Bounds.Size);

    private static async Task Test(Window main)
    {
        var view = main.FindControl<Control>("Viewport")!;
        var scene = view.GetType().GetProperty("Scene")!;
        SceneDocument Scene() => (SceneDocument)scene.GetValue(view)!;
        while (Scene().Objects.All(o => o.Name != "Plank 3")) await Delay(100);
        var report = new List<string>();
        // A long connected-object name must trim inside its field without moving the value column.
        var neighbour = Scene().Objects.Single(o => o.Name == "Plank 2");
        neighbour.Name = "Plank 2 — the long rope bridge plank whose name overflows the connect field";
        foreach (var (name, header) in new[] { ("Plank 3", "Revolute Joint 2D"), ("Pulley deck A", "Slider Joint 2D") })
        {
            main.GetType().GetMethod("Select", Any, [typeof(SceneObject)])!.Invoke(main, [Scene().Objects.Single(o => o.Name == name)]);
            foreach (int width in new[] { 1280, 1600, 1920 })
            {
                main.Width = width; main.Height = 900;
                await Delay(700);
                var inspector = main.FindControl<StackPanel>("Inspector")!;
                foreach (var title in new[] { "Rigidbody 2D", header })
                {
                    var section = inspector.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.HeaderedContentControl>()
                        .First(s => s.GetType().Name == "ComponentSection" && s.IsEffectivelyVisible && s.Header as string == title);
                    List<Control> Fields() => section.GetVisualDescendants().OfType<Control>()
                        .Where(c => c.IsEffectivelyVisible && c is NumericUpDown or ComboBox or CheckBox && c.GetVisualAncestors().OfType<NumericUpDown>().FirstOrDefault() == null).ToList();
                    var fields = Fields();
                    var fold = section.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>().First(t => t.Name == "HeaderToggle");
                    if (fields.Count < 3)
                    {
                        typeof(Button).GetMethod("OnClick", Any)!.Invoke(fold, null); // Unfold like a mouse click.
                        await Delay(400);
                        fields = Fields();
                    }
                    var labels = section.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Classes.Contains("fieldLabel")).ToList();
                    var columns = fields.Select(f => Math.Round(In(f, main).X, 1)).Distinct().ToArray();
                    var labelColumns = labels.Select(l => Math.Round(In(l, main).X, 1)).Distinct().ToArray();
                    var clipped = fields.Where(f => In(f, main).Right > In(section, main).Right + .5).Select(f => f.GetType().Name).ToArray();
                    Require(columns.Length == 1 && labelColumns.Length <= 1 && clipped.Length == 0,
                        $"{title} at {width}px (fold {fold.IsChecked}, {section.GetVisualDescendants().OfType<NumericUpDown>().Count()} number boxes, {section.GetVisualDescendants().OfType<NumericUpDown>().Count(n => n.IsEffectivelyVisible)} visible): value columns {string.Join(',', columns)}, label columns {string.Join(',', labelColumns)}, clipped {string.Join(',', clipped)}");
                    report.Add($"{name}/{title}@{width}: {fields.Count} fields at x={columns[0]}, {labels.Count} labels at x={string.Join(',', labelColumns)}");
                }
                var panel = main.GetVisualDescendants().OfType<Border>().First(b => b.Name == "InspectorPanel");
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)panel.Bounds.Width, (int)panel.Bounds.Height));
                bitmap.Render(panel);
                bitmap.Save(Path.Combine(Output, $"inspector-{(name == "Plank 3" ? "revolute" : "slider")}-{width}.png"));
            }
        }
        File.WriteAllLines(Path.Combine(Output, "inspector.txt"), report);
        main.Width = 1280;
        Click(main.FindControl<Button>("PlayButton")!);
        var game = (GameSession)view.GetType().GetField("game", Any)!.GetValue(view)!;
        InputReplay.Load(Path.Combine(Output, "Cross.inputreplay")).Play(game, GameSettings.Load(Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!).ProjectId!.Value);
        Click(main.FindControl<Button>("PlayButton")!);
    }
}
