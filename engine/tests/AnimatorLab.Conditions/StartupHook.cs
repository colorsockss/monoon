using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Engine.Core;

// Opens Guard.animator, checks and captures the two-condition transition dialog, drives the Animator panel's preview player
// with the replay's parameter changes (preview.trace), then runs Scene Play with the same recording.
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_GUARD_EDITOR")!;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    public static void Initialize() => Start(Path.Combine(Output, "error.txt"), life => Test(life.MainWindow!), allow: ["Transition"]);

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }

    private static async Task Test(Window main)
    {
        string project = Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!;
        var view = main.FindControl<Control>("Viewport")!;
        var scene = view.GetType().GetProperty("Scene")!;
        await Until(() => ((SceneDocument)scene.GetValue(view)!).Objects.Any(o => o.Name == "Guard"), "Guard");
        var opening = (Task)main.GetType().GetMethod("EditAnimator", Private)!.Invoke(main, ["Assets/Showcase/Guard.animator"])!;
        Visual? found = null;
        for (int i = 0; i < 200 && (found = main.GetVisualDescendants().SingleOrDefault(c => c.GetType().Name == "AnimatorWindow")) == null; i++) await Delay(50);
        var panel = (ContentControl)(found ?? throw new InvalidDataException("Animator panel did not open"));
        var type = panel.GetType();

        // Idle -> Walk: two conditions and no exit time. Rows share columns with each other and labels share the label column.
        var lifetime = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
        type.GetMethod("EditTransition", Private)!.Invoke(panel, [1]);
        Window? dialog = null;
        await Until(() => (dialog = lifetime.Windows.FirstOrDefault(w => w.Title == "Transition")) != null, "Transition dialog");
        await Delay(250);
        var combos = dialog!.GetVisualDescendants().OfType<ComboBox>().Where(c => c.IsVisible).ToArray();
        var labels = dialog.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Text is "From" or "To" or "Conditions" or "Exit time").ToArray();
        double X(Visual v) => v.TranslatePoint(default, dialog)!.Value.X;
        double Y(Visual v) => v.TranslatePoint(default, dialog)!.Value.Y;
        // From, To, then parameter/test per condition row.
        Require(combos.Length == 6, $"{combos.Length} combo boxes");
        Require(Math.Abs(X(combos[0]) - X(combos[2])) < 1 && Math.Abs(X(combos[2]) - X(combos[4])) < 1 && Math.Abs(X(combos[3]) - X(combos[5])) < 1,
            "condition rows are not on the same columns as From/To");
        var conditions = labels.Single(t => t.Text == "Conditions");
        Require(Math.Abs(Y(conditions) + conditions.Bounds.Height / 2 - (Y(combos[2]) + combos[2].Bounds.Height / 2)) < 1.5,
            "Conditions label is not on the first condition row");
        Require(labels.Select(X).Distinct().Count() == 1, "labels are not in one column");
        foreach (int width in new[] { 420, 340 })
        {
            dialog.Width = width;
            await Delay(200);
            Require(combos.All(c => X(c) + c.Bounds.Width <= dialog.Bounds.Width + 1), $"clipped at {width}px");
            using var bitmap = new RenderTargetBitmap(new((int)dialog.Bounds.Width, (int)dialog.Bounds.Height));
            bitmap.Render((Control)dialog.Content!); bitmap.Save(Path.Combine(Output, $"transition-{width}.png"));
        }
        dialog.Close(false);
        await Delay(200);

        // Preview: the panel's own AnimationPlayer, stepped at the fixed rate with the replay's parameter changes.
        var tabs = (TabControl)type.GetField("tabs", Private)!.GetValue(panel)!;
        tabs.SelectedItem = type.GetField("previewTab", Private)!.GetValue(panel);
        await Until(() => type.GetField("player", Private)!.GetValue(panel) != null, "preview player");
        ((DispatcherTimer)type.GetField("timer", Private)!.GetValue(panel)!).Stop();
        var player = (AnimationPlayer)type.GetField("player", Private)!.GetValue(panel)!;
        player.Play();
        var presses = File.ReadAllLines(Path.Combine(Output, "presses.txt")).Select(l => l.Split(' ')).ToDictionary(p => int.Parse(p[0]), p => p[1]);
        int frames = int.Parse(File.ReadAllText(Path.Combine(Output, "frames.txt")));
        int combo = 0; bool grounded = true;
        var trace = new System.Text.StringBuilder();
        for (int tick = 0; tick < frames; tick++)
        {
            trace.Append($"{tick}|{player.State}|{player.Frame}\n");
            switch (presses.GetValueOrDefault(tick))
            {
                case "Right": player.SetFloat("Speed", 1); break;
                case "Left": player.SetFloat("Speed", 0); player.SetInt("Combo", combo = 0); break;
                case "Space": player.SetInt("Combo", ++combo); player.SetTrigger("Attack"); break;
                case "Up": player.SetTrigger("Hit"); break;
                case "Down": player.SetBool("Grounded", grounded = !grounded); break;
            }
            player.Step((float)GameTime.FixedStep, null);
        }
        File.WriteAllText(Path.Combine(Output, "preview.trace"), trace.ToString());
        tabs.SelectedIndex = 0;
        Click(panel.GetVisualDescendants().OfType<Button>().First(b => b.Content as string == "Close"));
        await opening;

        var play = main.FindControl<Button>("PlayButton")!;
        Click(play);
        var game = (GameSession)view.GetType().GetField("game", Private)!.GetValue(view)!;
        game.LoadFailed += message => throw new InvalidDataException(message);
        InputReplay.Load(Path.Combine(Output, "guard.inputreplay")).Play(game, GameSettings.Load(project).ProjectId!.Value);
        Click(play);
        await Delay(200);
    }
}
