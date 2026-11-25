using System.Globalization;
using System.Numerics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Engine.Core;

// Opens Scout.animator, checks and captures the Walk state's 8-direction dialog, drives the Animator panel's preview player with
// the replay's Move/Speed values (preview.trace), then runs Scene Play with the same recording.
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_SCOUT_EDITOR")!;
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    public static void Initialize() => Start(Path.Combine(Output, "error.txt"), life => Test(life.MainWindow!), allow: ["State"]);

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }

    private static async Task Test(Window main)
    {
        string project = Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!;
        var view = main.FindControl<Control>("Viewport")!;
        var scene = view.GetType().GetProperty("Scene")!;
        await Until(() => ((SceneDocument)scene.GetValue(view)!).Objects.Any(o => o.Name == "Scout"), "Scout");
        var opening = (Task)main.GetType().GetMethod("EditAnimator", Private)!.Invoke(main, ["Assets/Showcase/Scout.animator"])!;
        Visual? found = null;
        for (int i = 0; i < 200 && (found = main.GetVisualDescendants().SingleOrDefault(c => c.GetType().Name == "AnimatorWindow")) == null; i++) await Delay(50);
        var panel = (ContentControl)(found ?? throw new InvalidDataException("Animator panel did not open"));
        var type = panel.GetType();

        // Walk: 8 direction rows share the dialog's label and field columns; 4 directions keep only N, E, S and W.
        var lifetime = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
        type.GetMethod("EditState", Private)!.Invoke(panel, [1]);
        Window? dialog = null;
        await Until(() => (dialog = lifetime.Windows.FirstOrDefault(w => w.Title == "State")) != null, "State dialog");
        await Delay(250);
        double X(Visual v) => v.TranslatePoint(default, dialog!)!.Value.X;
        double Y(Visual v) => v.TranslatePoint(default, dialog!)!.Value.Y;
        ComboBox[] Combos() => dialog!.GetVisualDescendants().OfType<ComboBox>().Where(c => c.IsEffectivelyVisible).ToArray();
        string[] Labels() => dialog!.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Text is { Length: <= 9 } &&
            (AnimationState.Compass.Contains(t.Text) || t.Text is "Name" or "Clips" or "Clip" or "Parameter")).Select(t => t.Text!).ToArray();
        var combos = Combos();
        // Clips mode, Parameter, then N..NW.
        Require(combos.Length == 10 && (string)combos[0].SelectedItem! == "8 directions" && (string)combos[1].SelectedItem! == "Move",
            $"{combos.Length} combo boxes, mode {combos.FirstOrDefault()?.SelectedItem}");
        Require(string.Join(",", Labels()) == "Name,Clips,Parameter,N,NE,E,SE,S,SW,W,NW", "labels: " + string.Join(",", Labels()));
        Require(combos.Select(X).Distinct().Count() == 1 && combos.Select(c => c.Bounds.Width).Distinct().Count() == 1, "direction rows are not one column");
        Require(combos.Skip(2).Select((c, i) => ClipOption(c).EndsWith($"ScoutWalk{AnimationState.Compass[i]}.clip")).All(ok => ok), "clips out of compass order");
        var labelBlocks = dialog!.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && AnimationState.Compass.Contains(t.Text)).ToArray();
        Require(labelBlocks.Select(X).Distinct().Count() == 1, "direction labels are not one column");
        for (int i = 0; i < 8; i++)
            Require(Math.Abs(Y(labelBlocks[i]) + labelBlocks[i].Bounds.Height / 2 - (Y(combos[i + 2]) + combos[i + 2].Bounds.Height / 2)) < 1.5,
                $"{labelBlocks[i].Text} label is not on its row");
        foreach (int width in new[] { 325, 270 })
        {
            dialog.Width = width;
            await Delay(200);
            Require(Combos().All(c => X(c) + c.Bounds.Width <= dialog.Bounds.Width + 1), $"clipped at {width}px");
            using var bitmap = new RenderTargetBitmap(new((int)dialog.Bounds.Width, (int)dialog.Bounds.Height));
            bitmap.Render((Control)dialog.Content!); bitmap.Save(Path.Combine(Output, $"state-{width}.png"));
        }
        combos[0].SelectedIndex = 1;
        await Delay(200);
        Require(string.Join(",", Labels()) == "Name,Clips,Parameter,N,E,S,W" && Combos().Length == 6, "4 directions: " + string.Join(",", Labels()));
        dialog.Close(false);
        await Delay(200);

        // Preview: the panel's own AnimationPlayer, stepped at the fixed rate with the values DriveScout sets.
        var tabs = (TabControl)type.GetField("tabs", Private)!.GetValue(panel)!;
        tabs.SelectedItem = type.GetField("previewTab", Private)!.GetValue(panel);
        await Until(() => type.GetField("player", Private)!.GetValue(panel) != null, "preview player");
        ((DispatcherTimer)type.GetField("timer", Private)!.GetValue(panel)!).Stop();
        var player = (AnimationPlayer)type.GetField("player", Private)!.GetValue(panel)!;
        player.Play();
        var inputs = File.ReadAllLines(Path.Combine(Output, "inputs.txt")).Select(l => l.Split(' ').Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray()).ToArray();
        var trace = new System.Text.StringBuilder();
        for (int tick = 0; tick < inputs.Length; tick++)
        {
            trace.Append($"{tick}|{player.State}|{player.Frame}|{player.Facing}\n");
            player.SetVector2("Move", new Vector2(inputs[tick][0], inputs[tick][1]));
            player.SetFloat("Speed", inputs[tick][2]);
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
        InputReplay.Load(Path.Combine(Output, "scout.inputreplay")).Play(game, GameSettings.Load(project).ProjectId!.Value);
        Click(play);
        await Delay(200);
    }

    private static string ClipOption(ComboBox box) => box.SelectedItem?.ToString() ?? "";
}
