using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Engine.Core;
using Engine.Editor;

// Loaded into the packaged editor with DOTNET_STARTUP_HOOKS (roadmap 13.11). In Scene Play: PlayKnobs' bool, enum, Vector2, string
// and colour fields and the Door's sprite tint and collider through the Play Inspector rows, each checked on the running game;
// Apply selected with Banner unchecked, then Apply all for Banner; an unapplied edit that Stop discards; Undo twice after Stop.
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_PF_OUTPUT")!;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Dictionary<string, object?> Result = [];
    // The showcase hero's root is a feet box without a picture; the door has a sprite renderer and a collider.
    private const string Target = "Door";
    private static Window main = null!;
    private static SceneViewport view = null!;

    public static void Initialize() => Start(Path.Combine(Output, "error.txt"), async life =>
    {
        main = life.MainWindow!;
        view = main.FindControl<SceneViewport>("Viewport")!;
        await Until(() => main.IsVisible && Scene.Objects.Any(o => o.Name == Target), "KeyRoom", 120);
        main.Width = 1280; main.Height = 800;
        await Delay(800);
        await Run();
        File.WriteAllText(Path.Combine(Output, "play.json"), JsonSerializer.Serialize(Result, new JsonSerializerOptions { WriteIndented = true }));
    });

    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static object? Get(object target, string name) =>
        target.GetType().GetProperty(name, Any) is { } p ? p.GetValue(target) : target.GetType().GetField(name, Any)?.GetValue(target);
    private static object? Call(object target, string method, params object?[] args) =>
        target.GetType().GetMethods(Any).First(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(target, args);
    private static SceneDocument Scene => (SceneDocument)Get(main, "Scene")!;
    private static SceneGame World => (SceneGame)Get(view, "PlayWorld")!;
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, b));
    private static void Leave(Control c) => c.RaiseEvent(new RoutedEventArgs(InputElement.LostFocusEvent, c));
    private static T Named<T>(string name) where T : Control =>
        main.FindControl<StackPanel>("Inspector")!.GetVisualDescendants().OfType<T>().FirstOrDefault(c => AutomationProperties.GetName(c) == name && c.IsEffectivelyVisible)
        ?? throw new InvalidDataException($"no {typeof(T).Name} '{name}'");
    private static bool Has(string name) => main.FindControl<StackPanel>("Inspector")!.GetVisualDescendants().Any(c => AutomationProperties.GetName(c) == name);
    private static void Step(string name, object? value)
    {
        Result[name] = value;
        File.AppendAllText(Path.Combine(Output, "play.log"), $"{name}: {JsonSerializer.Serialize(value)}\n");
    }
    private static object Knob(string field)
    {
        var player = World.Objects.First(o => o.Name == Target);
        var knobs = ((IEnumerable<GameBehaviour>)Call(World, "LiveScripts", player.Id)!).First(s => s.GetType().Name == "PlayKnobs");
        return knobs.GetType().GetField(field)!.GetValue(knobs)!;
    }
    private static SceneObject Authored => Scene.Objects.First(o => o.Name == Target);
    private static string Saved(string field) =>
        Authored.Components.OfType<ScriptComponent>().First(s => s.Script == "PlayKnobs").Values.TryGetValue(field, out var v) ? v.GetRawText().Replace(" ", "").Replace("\n", "") : "(default)";
    private static async Task Typed(string name, string text)
    {
        var box = Named<TextBox>(name);
        box.Text = text;
        Leave(box); // in the same UI turn, before the 100 ms live refresh
        await Delay(300);
    }
    // A colour field applies its text on TextChanged (dispatched), and commits when the text loses focus.
    private static async Task TypedColor(string name, string text)
    {
        var box = Named<TextBox>(name);
        box.Focus();
        box.Text = text;
        await Delay(150);
        Leave(box);
        await Delay(300);
    }
    private static async Task Undo() { await (Task)Call(main, "RestoreEdit", false)!; await Delay(300); }

    private static async Task Run()
    {
        main.Activate();
        var before = Scene.CaptureObjects();
        var play = main.FindControl<Button>("PlayButton")!;
        Click(play);
        await Until(() => view.IsPlaying && Get(view, "PlayWorld") != null, "Play", 60);
        await Delay(800);
        Call(main, "Select", World.Objects.First(o => o.Name == Target));
        await Until(() => Has("PlayKnobs.Frozen") && Has("Sprite Renderer.Tint"), "Play rows", 30);
        await Delay(400);

        Named<CheckBox>("PlayKnobs.Frozen").IsChecked = true; await Delay(300);
        Require(Knob("Frozen") is true, "Frozen live");
        Named<ComboBox>("PlayKnobs.Facing").SelectedItem = "Up"; await Delay(300);
        Require(Knob("Facing").ToString() == "Up", "Facing live");
        await Typed("PlayKnobs.Push X", "5");
        Require((Vector2)Knob("Push") == new Vector2(5, 2), "Push live " + Knob("Push"));
        await Typed("PlayKnobs.Banner", "play");
        Require((string)Knob("Banner") == "play", "Banner live");
        await TypedColor("PlayKnobs.Glow", "#FF0000");
        Require((string)Knob("Glow") == "#FF0000", "Glow live " + Knob("Glow"));
        var player = World.Objects.First(o => o.Name == Target).Id;
        await TypedColor("Sprite Renderer.Tint", "#00FF00");
        Require(World.TryGetSpriteColor(player, out var tint) && tint.R == 0 && tint.G == 255 && tint.B == 0, $"tint live {tint}");
        Named<CheckBox>("Collider.Enabled").IsChecked = false; await Delay(300);
        Require(!World.IsColliderEnabled(player), "collider live");
        Require(Has("Object.Active"), "Active row");
        Require(Saved("Frozen") == "(default)" && Authored.GetComponent<SpriteRendererComponent>()!.Tint == null, "nothing reaches the edited scene before Apply");

        // Alignment: the Play rows share the Local X row's value column at three widths.
        var widths = new Dictionary<int, object>();
        foreach (var width in new[] { 1280, 1600, 1920 })
        {
            main.Width = width; await Delay(900);
            double X(Visual c) => Math.Round(c.TranslatePoint(default, main)!.Value.X, 1);
            var names = new[] { "Transform.Local X", "PlayKnobs.Frozen", "PlayKnobs.Facing", "PlayKnobs.Banner", "Sprite Renderer.Tint", "Collider.Enabled" };
            var xs = names.Select(n => X(Named<Control>(n).GetVisualAncestors().OfType<Grid>().First(g => g.ColumnDefinitions.Count == 2 && g.ColumnDefinitions[1].Width.Value == 24))).Distinct().ToArray();
            Require(xs.Length == 1, $"{width}: Play value column x {string.Join(",", xs)}");
            widths[width] = xs[0];
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)main.FindControl<StackPanel>("Inspector")!.Bounds.Width, (int)main.FindControl<StackPanel>("Inspector")!.Bounds.Height));
            bitmap.Render(main.FindControl<StackPanel>("Inspector")!);
            bitmap.Save(Path.Combine(Output, $"play-{width}.png"));
        }
        main.Width = 1280; await Delay(600);
        Step("alignment", widths);

        // Apply selected without Banner, then Apply all for it: one Undo step each.
        Named<CheckBox>("Apply PlayKnobs.Banner").IsChecked = false; await Delay(200);
        var applySelected = main.FindControl<Button>("ApplyPlayValues")!;
        var applyAll = main.FindControl<Button>("ApplyAllPlayValues")!;
        Require(applySelected.IsVisible && applySelected.IsEnabled && applyAll.IsVisible && applyAll.IsEnabled, "apply buttons");
        Click(applySelected); await Delay(800);
        Step("after_selected", new { Frozen = Saved("Frozen"), Facing = Saved("Facing"), Push = Saved("Push"), Banner = Saved("Banner"), Glow = Saved("Glow"),
            Tint = Authored.GetComponent<SpriteRendererComponent>()!.Tint, Collider = Authored.GetComponent<Collider2DComponent>()!.Enabled });
        Require(Saved("Frozen") == "true" && Saved("Facing") == "\"Up\"" && Saved("Push") == "{\"x\":5,\"y\":2}" && Saved("Banner") == "(default)" &&
            Saved("Glow") == "\"#FF0000\"" && Authored.GetComponent<SpriteRendererComponent>()!.Tint == "#00FF00" &&
            !Authored.GetComponent<Collider2DComponent>()!.Enabled, "Apply selected");
        Click(applyAll); await Delay(800);
        Require(Saved("Banner") == "\"play\"", "Apply all " + Saved("Banner"));

        // An edit not applied: Stop discards it.
        Named<CheckBox>("PlayKnobs.Frozen").IsChecked = false; await Delay(300);
        Require(Knob("Frozen") is false && Saved("Frozen") == "true", "unapplied Frozen");
        Click(play);
        await Until(() => !view.IsPlaying, "Stop", 30);
        await Delay(800);
        Require(Saved("Frozen") == "true", "Stop discards the unapplied edit");
        Step("after_stop", new { Frozen = Saved("Frozen"), Banner = Saved("Banner") });
        await Undo();
        Require(Saved("Banner") == "(default)" && Saved("Frozen") == "true", "Undo Apply all");
        await Undo();
        // The save after Apply wrote schema 32 (a turned-off collider); Undo restores the objects, the header keeps the saved version.
        static string Objects(string text) => text[(text.IndexOf('\n') + 1)..];
        if (Objects(Scene.CaptureObjects()) != Objects(before))
        {
            File.WriteAllText(Path.Combine(Output, "before.txt"), before);
            File.WriteAllText(Path.Combine(Output, "after-undo.txt"), Scene.CaptureObjects());
        }
        Require(Objects(Scene.CaptureObjects()) == Objects(before), "Undo Apply selected gives back the scene from before Play");
        Step("undo", new { objects = "equal to before Play", header = Scene.CaptureObjects()[..Scene.CaptureObjects().IndexOf('\n')], before = before[..before.IndexOf('\n')] });
    }
}
