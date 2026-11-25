using System.Collections;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Engine.Core;

// Loaded into the packaged editor with DOTNET_STARTUP_HOOKS (16.12 editor follow-up). Through the Input Actions panel only:
// a Hold action, a buffered action, four aim actions and the Walk/Aim vectors (next to the sample's own Charge/Flick/Dash), invalid values and their
// messages, Save, the GameSettings bytes against the YAML-only file, one Undo and Redo, column positions at 1280/1600/1920.
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_IME_OUT")!;
    private static readonly string Project = Environment.GetEnvironmentVariable("MONOON_IME_PROJECT")!;
    private static readonly string Reference = Environment.GetEnvironmentVariable("MONOON_IME_REFERENCE")!;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Dictionary<string, object?> Result = [];
    private static Window main = null!;

    public static void Initialize() => Start(Path.Combine(Output, "error.txt"), async life =>
    {
        main = life.MainWindow!;
        await Until(() => main.IsVisible && Scene.Objects.Any(o => o.Name == "Player"), "KeyRoom", 120);
        main.Width = 1280; main.Height = 800;
        await Delay(800);
        await Run();
        File.WriteAllText(Path.Combine(Output, "result.json"), JsonSerializer.Serialize(Result, new JsonSerializerOptions { WriteIndented = true }));
    });

    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static object? Get(object target, string name) =>
        target.GetType().GetProperty(name, Any) is { } p ? p.GetValue(target) : target.GetType().GetField(name, Any)?.GetValue(target);
    private static object? Call(object target, string method, params object?[] args) =>
        target.GetType().GetMethods(Any).First(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(target, args);
    private static SceneDocument Scene => (SceneDocument)Get(main, "Scene")!;
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, b));
    private static byte[] Saved() => File.ReadAllBytes(Path.Combine(Project, GameSettings.FileName));
    // The YAML-only path: GameSettings read from a hand-written file and saved by the engine.
    private static byte[] Normalized(string project)
    {
        var temp = Directory.CreateTempSubdirectory("ime-").FullName;
        GameSettings.Load(project).Save(temp);
        return File.ReadAllBytes(Path.Combine(temp, GameSettings.FileName));
    }
    private static string? RuleMessage(InputBindings input)
    {
        try { (new GameSettings() with { Input = input }).Save(Directory.CreateTempSubdirectory("ime-").FullName); return null; }
        catch (InvalidDataException ex) { return ex.Message; }
    }

    private static Control? Panel() => main.GetVisualDescendants().FirstOrDefault(c => c.GetType().Name == "InputMapWindow") as Control;
    private static T Named<T>(string name) where T : Control =>
        Panel()!.GetVisualDescendants().OfType<T>().FirstOrDefault(c => AutomationProperties.GetName(c) == name)
        ?? throw new InvalidDataException($"no {typeof(T).Name} '{name}'");
    private static IList Rows(string field) => (IList)Get(Panel()!, field)!;
    private static object Last(string field) { var rows = Rows(field); return rows[rows.Count - 1]!; }
    private static string Hint() => ((TextBlock)Get(Panel()!, "hint")!).Text ?? "";
    private static async Task Open()
    {
        await (Task)Call(main, "EditInput")!;
        await Until(() => Panel() is { IsEffectivelyVisible: true, Bounds.Width: > 0 }, "Input Actions panel");
        await Delay(400);
    }

    private static async Task AddAction(string name, string controls, string interaction = "Press", string ticks = "", string buffer = "")
    {
        Click(Named<Button>("Add action"));
        await Delay(100);
        var row = Last("rows");
        ((TextBox)Get(row, "Name")!).Text = name;
        ((TextBox)Get(row, "Controls")!).Text = controls;
        ((ComboBox)Get(row, "Interaction")!).SelectedItem = interaction;
        ((TextBox)Get(row, "Ticks")!).Text = ticks;
        ((TextBox)Get(row, "Buffer")!).Text = buffer;
    }
    private static async Task AddVector(string name, params string[] parts)
    {
        Click(Named<Button>("Add vector"));
        await Delay(100);
        var row = Last("vectorRows");
        ((TextBox)Get(row, "Name")!).Text = name;
        var boxes = (TextBox[])Get(row, "Parts")!;
        for (int i = 0; i < 4; i++) boxes[i].Text = parts[i];
    }
    private static object Row(string name) => Rows("rows").Cast<object>().First(r => ((TextBox)Get(r, "Name")!).Text == name);

    // Left edge and width of every column: the header labels, the first and last action rows and the first vector row.
    private static async Task Columns(string capture)
    {
        await Delay(500);
        var panel = Panel()!;
        double X(Control c) => c.TranslatePoint(default, panel)!.Value.X;
        string Line(Grid grid) => string.Join(" ", grid.Children.OfType<Control>().OrderBy(Grid.GetColumn).Select(c => $"{Grid.GetColumn(c)}:{X(c):F1}+{c.Bounds.Width:F1}"));
        var header = panel.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Action").GetVisualParent<Grid>()!;
        var first = (Grid)Get(Rows("rows")[0]!, "Grid")!;
        var last = (Grid)Get(Last("rows"), "Grid")!;
        var vector = (Grid)Get(Rows("vectorRows")[0]!, "Grid")!;
        var vectorHeader = panel.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Vector").GetVisualParent<Grid>()!;
        string Parts(Grid grid) => string.Join(" ", ((Grid)grid.Children.First(c => Grid.GetColumn(c) == 1 && c is Grid)).Children.OfType<Control>()
            .Select(c => $"{X(c):F1}+{c.Bounds.Width:F1}"));
        var line = new Dictionary<string, string>
        {
            ["panel"] = $"{panel.Bounds.Width:F0}x{panel.Bounds.Height:F0}",
            ["header"] = Line(header), ["first"] = Line(first), ["last"] = Line(last),
            ["vectorHeader"] = Line(vectorHeader) + " | " + Parts(vectorHeader), ["vector"] = Line(vector) + " | " + Parts(vector),
        };
        // Columns: the same left edges for header, rows and vectors (vector parts sit inside column 1's span).
        double Col(Grid g, int column) => X(g.Children.OfType<Control>().First(c => Grid.GetColumn(c) == column));
        for (int c = 0; c <= 4; c++)
            Require(Math.Abs(Col(header, c) - Col(first, c)) < .5 && Math.Abs(Col(first, c) - Col(last, c)) < .5, $"{capture}: column {c} edge");
        Require(Math.Abs(Col(vector, 0) - Col(first, 0)) < .5 && Math.Abs(Col(vector, 1) - Col(first, 1)) < .5 && Math.Abs(Col(vector, 6) - Col(first, 6)) < .5,
            $"{capture}: vector columns");
        Require(Math.Abs(Col(vectorHeader, 0) - Col(vector, 0)) < .5, $"{capture}: vector header");
        Require(first.Bounds.Height == last.Bounds.Height && first.Bounds.Height == vector.Bounds.Height, $"{capture}: row heights");
        // The longest interaction name ends before the drop-down arrow.
        var dash = (ComboBox)Get(Row("Dash"), "Interaction")!;
        var text = dash.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "DoubleTap");
        var free = new TextBlock { Text = text.Text, FontSize = text.FontSize, FontFamily = text.FontFamily, FontWeight = text.FontWeight };
        free.Measure(Size.Infinity);
        line["doubleTap"] = $"needs {free.DesiredSize.Width:F1}, given {text.Bounds.Width:F1} in a {dash.Bounds.Width:F1} combo";
        Require(free.DesiredSize.Width <= text.Bounds.Width + .5, $"{capture}: DoubleTap is cut off ({line["doubleTap"]})");
        Result["columns-" + capture] = line;
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)panel.Bounds.Width, (int)panel.Bounds.Height));
        bitmap.Render(panel);
        bitmap.Save(Path.Combine(Output, $"input-map-{capture}.png"));
    }

    private static async Task Run()
    {
        var before = Normalized(Project);
        var expected = Normalized(Reference);
        Require(!before.AsSpan().SequenceEqual(expected), "reference differs from the plain sample");
        await Open();
        // editor.py's reference rules, typed into the panel.
        await AddAction("Spin", "Z", "Hold", "20");
        await AddAction("Leap", "Y", buffer: "8");
        foreach (var (name, key) in new[] { ("AimLeft", "V"), ("AimRight", "B"), ("AimUp", "T"), ("AimDown", "G") }) await AddAction(name, key);
        await AddVector("Walk", "MoveLeft", "MoveRight", "MoveUp", "MoveDown");
        await AddVector("Aim", "AimLeft", "AimRight", "AimUp", "AimDown");
        Require(((TextBox)Get(Row("Flick"), "Ticks")!).Watermark == "12" && ((TextBox)Get(Row("Leap"), "Ticks")!).Watermark == "", "Ticks default watermark");

        foreach (var width in new[] { 1280, 1600, 1920 })
        {
            main.Width = width; main.Height = 900;
            await Columns(width.ToString());
        }
        // A 64-character action name keeps every column where it was.
        var leapName = (TextBox)Get(Row("Leap"), "Name")!;
        main.Width = 1280;
        leapName.Text = "Leap" + new string('x', 60);
        await Columns("1280-long-name");
        leapName.Text = "Leap";

        // Invalid values: the panel stays open and shows the runtime's message.
        var save = Named<Button>("Save action map");
        var messages = new Dictionary<string, string>();
        async Task Invalid(string what, Action set, Action reset, string? expect)
        {
            set();
            Click(save);
            await Delay(200);
            Require(Panel() != null, $"{what}: panel stays open");
            Require(expect != null && Hint() == expect, $"{what}: message '{Hint()}', expected '{expect}'");
            messages[what] = Hint();
            reset();
        }
        var chargeTicks = (TextBox)Get(Row("Charge"), "Ticks")!;
        var leapBuffer = (TextBox)Get(Row("Leap"), "Buffer")!;
        var aimUp = ((TextBox[])Get(Rows("vectorRows")[1]!, "Parts")!)[2];
        var fileBefore = Saved();
        await Invalid("ticks 0", () => chargeTicks.Text = "0", () => chargeTicks.Text = "30",
            RuleMessage(new() { Actions = [new("Charge", ["J"]) { Interaction = ActionBinding.Hold, Ticks = 0 }] }));
        await Invalid("ticks 3601", () => chargeTicks.Text = "3601", () => chargeTicks.Text = "30",
            RuleMessage(new() { Actions = [new("Charge", ["J"]) { Interaction = ActionBinding.Hold, Ticks = 3601 }] }));
        await Invalid("buffer -1", () => leapBuffer.Text = "-1", () => leapBuffer.Text = "8",
            RuleMessage(new() { Actions = [new("Leap", ["Y"]) { Buffer = -1 }] }));
        await Invalid("ticks not a number", () => chargeTicks.Text = "2.5", () => chargeTicks.Text = "30",
            "Charge: ticks must be a whole number of game ticks, or empty.");
        await Invalid("vector unknown action", () => aimUp.Text = "AimUpp", () => aimUp.Text = "AimUp",
            RuleMessage(new() { Actions = [new("AimUp", ["T"])], Vectors = [new("Aim", "AimUp", "AimUp", "AimUpp", "AimUp")] }));
        var walkName = (TextBox)Get(Rows("vectorRows")[0]!, "Name")!;
        await Invalid("vector duplicate name", () => walkName.Text = "aim", () => walkName.Text = "Walk",
            RuleMessage(new() { Actions = [new("A", ["T"])], Vectors = [new("aim", "A", "A", "A", "A"), new("Aim", "A", "A", "A", "A")] }));
        Require(Saved().AsSpan().SequenceEqual(fileBefore), "invalid saves left GameSettings unchanged");
        Result["messages"] = messages;

        Click(save);
        await Until(() => Panel() == null, "panel closes on Save");
        await Delay(300);
        var ui = Saved();
        File.WriteAllBytes(Path.Combine(Output, "GameSettings.ui"), ui);
        File.WriteAllBytes(Path.Combine(Output, "GameSettings.yaml-only"), expected);
        Require(ui.AsSpan().SequenceEqual(expected), "UI save is byte-identical to the YAML-only GameSettings");
        Result["bytes"] = new { ui = ui.Length, yamlOnly = expected.Length, identical = true };

        // One Undo gives back the plain sample, one Redo the edited file.
        await (Task)Call(main, "RestoreEdit", false)!;
        await Delay(300);
        Require(Saved().AsSpan().SequenceEqual(before), "Undo restores the GameSettings bytes");
        Require(Scene.Settings.Input.Actions!.All(a => a.Name != "Leap") && Scene.Settings.Input.Vectors == null, "Undo restores the input map");
        await (Task)Call(main, "RestoreEdit", true)!;
        await Delay(300);
        Require(Saved().AsSpan().SequenceEqual(expected), "Redo restores the edited GameSettings bytes");

        // Reopened, the panel shows the saved rules; Save without a change writes nothing and adds no Undo step.
        await Open();
        Require((string?)((ComboBox)Get(Row("Dash"), "Interaction")!).SelectedItem == "DoubleTap" && ((TextBox)Get(Row("Dash"), "Ticks")!).Text == "15" &&
            ((TextBox)Get(Row("Leap"), "Buffer")!).Text == "8" && Rows("vectorRows").Count == 2, "reopened panel shows the saved values");
        var history = Get(main, "history")!;
        int position = (int)Get(history, "position")!;
        var stamp = File.GetLastWriteTimeUtc(Path.Combine(Project, GameSettings.FileName));
        Click(Named<Button>("Save action map"));
        await Until(() => Panel() == null, "panel closes on unchanged Save");
        await Delay(300);
        Require((int)Get(history, "position")! == position && File.GetLastWriteTimeUtc(Path.Combine(Project, GameSettings.FileName)) == stamp &&
            Saved().AsSpan().SequenceEqual(expected), "unchanged Save: no write, no Undo step");
        Result["undo"] = "one Undo restores the sample bytes, Redo the edited bytes, unchanged Save adds nothing";
    }
}
