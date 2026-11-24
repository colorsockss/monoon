using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Engine.Core;
using Engine.Editor;

// Loaded into the packaged editor with DOTNET_STARTUP_HOOKS (roadmap 13.8). MONOON_SF_PHASE selects one launch:
// edit: every SupplyTuning field kind changed through its Inspector row (Undo/Redo on four of them), alignment at 1280/1600/1920 px
// with a long dictionary key, captures, Save. reopen: the saved values in a fresh editor, then the Scene Play replay of FieldTrace.
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_SF_OUTPUT")!;
    private static readonly string Phase = Environment.GetEnvironmentVariable("MONOON_SF_PHASE")!;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Dictionary<string, object?> Result = [];
    private static Window main = null!;

    public static void Initialize() => Start(Path.Combine(Output, $"{Phase}-error.txt"), async life =>
    {
        main = life.MainWindow!;
        await Until(() => main.IsVisible && Scene.Objects.Any(o => o.Name == "QA Supply"), "InventoryLab with QA Supply", 120);
        main.Width = 1280; main.Height = 800;
        await Delay(800);
        await (Phase switch { "edit" => Edit(), "reopen" => Reopen(), _ => throw new ArgumentException(Phase) });
        File.WriteAllText(Path.Combine(Output, $"{Phase}.json"), JsonSerializer.Serialize(Result, new JsonSerializerOptions { WriteIndented = true }));
    });

    // ---- helpers
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static object? Get(object target, string name) =>
        target.GetType().GetProperty(name, Any) is { } p ? p.GetValue(target) : target.GetType().GetField(name, Any)?.GetValue(target);
    private static object? Call(object target, string method, params object?[] args) => target.GetType().GetMethod(method, Any)!.Invoke(target, args);
    private static SceneDocument Scene => (SceneDocument)Get(main, "Scene")!;
    private static SceneObject Supply => Scene.Objects.First(o => o.Name == "QA Supply");
    private static ScriptComponent Tuning => Supply.Components.OfType<ScriptComponent>().First(c => c.Script == "SupplyTuning");
    private static string Saved(string field) => Tuning.Values.TryGetValue(field, out var v) ? v.GetRawText().Replace("\n", "").Replace(" ", "") : "(default)";
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, b));
    private static void Leave(Control c) => c.RaiseEvent(new RoutedEventArgs(InputElement.LostFocusEvent, c));
    private static void Step(string name, object? value)
    {
        Result[name] = value;
        File.AppendAllText(Path.Combine(Output, $"{Phase}.log"), $"{name}: {JsonSerializer.Serialize(value)}\n");
    }
    private static async Task<bool> Save() => await (Task<bool>)Call(main, "Save")!;
    private static async Task Undo() { await (Task)Call(main, "RestoreEdit", false)!; await Delay(300); }
    private static async Task Redo() { await (Task)Call(main, "RestoreEdit", true)!; await Delay(300); }
    private static void Commit() => Call(main, "CommitEdit");
    private static StackPanel InspectorStack => main.FindControl<StackPanel>("Inspector")!;
    private static ComponentSection Section() =>
        InspectorStack.GetVisualDescendants().OfType<ComponentSection>().FirstOrDefault(s => s.IsEffectivelyVisible && (s.Header as string ?? "").Contains("SupplyTuning"))
        ?? throw new InvalidDataException("no SupplyTuning section");
    private static T Named<T>(string name) where T : Control =>
        Section().GetVisualDescendants().OfType<T>().FirstOrDefault(c => AutomationProperties.GetName(c) == name)
        ?? throw new InvalidDataException($"no {typeof(T).Name} '{name}' in SupplyTuning");
    private static async Task Type(string name, string text)
    {
        var box = Named<TextBox>(name);
        box.Text = text;
        await Delay(100);
        Leave(box);
        Commit();
        await Delay(200);
    }
    private static async Task Select()
    {
        Call(main, "Select", Supply);
        await Until(() => { try { Section(); return true; } catch (InvalidDataException) { return false; } }, "SupplyTuning section", 30);
        await Delay(300);
    }
    private static void Capture(Visual area, string name)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(area.Bounds.Width), (int)Math.Ceiling(area.Bounds.Height)));
        bitmap.Render(area);
        bitmap.Save(Path.Combine(Output, name + ".png"));
    }
    // An edit and its Undo/Redo: Undo puts back the saved JSON before, Redo the one after.
    private static async Task Undoable(string field, Func<Task> edit, string want)
    {
        string before = Saved(field);
        want = want.Replace(" ", ""); // Saved() drops spaces
        await edit();
        Require(Saved(field) == want, $"{field}: saved {Saved(field)}, want {want}");
        await Undo();
        Require(Saved(field) == before, $"{field} undo: {Saved(field)}, want {before}");
        await Redo();
        Require(Saved(field) == want, $"{field} redo: {Saved(field)}");
        await Select();
    }

    // ---- edit
    private static async Task Edit()
    {
        main.Activate();
        await Select();
        string longKey = File.ReadAllText(Path.Combine(Scene.ProjectDirectory!, "qa-long-key.txt"));
        await Undoable("Seed", () => Type("Seed", "9000000000"), "9000000000");
        await Type("SlotMask", "4294967295");
        var tint = Named<TextBox>("CrateTint");
        tint.Text = "#10203040"; await Delay(100); Leave(tint); await Delay(200);
        var label = Named<TextBox>("LabelColor");
        label.Text = "#ABCDEF"; await Delay(100); Leave(label); await Delay(200);
        var tags = (StackPanel)((Flyout)Named<Button>("Tags").Flyout!).Content!;
        tags.Children.OfType<CheckBox>().First(c => AutomationProperties.GetName(c) == "Tags Key").IsChecked = true;
        await Delay(200);
        tags = (StackPanel)((Flyout)Named<Button>("Tags").Flyout!).Content!;
        tags.Children.OfType<CheckBox>().First(c => AutomationProperties.GetName(c) == "Tags Food").IsChecked = false;
        await Delay(200);
        await Type("StackSizes 2", "7");
        Click(Named<Button>("Add to StackSizes")); await Delay(300);
        await Type("StackSizes 4", "9");
        Click(Named<Button>("Remove Weights 3")); await Delay(300);
        await Type("Names 1", "moss");
        await Undoable("Unlocked", async () => { Named<CheckBox>("Unlocked 3").IsChecked = true; await Delay(300); }, "[true,true,true]");
        Click(Named<Button>("Add to Restock")); await Delay(300);
        await Type("Restock 3", "6");
        Click(Named<Button>("Add to Prices")); await Delay(300);
        var key = Named<TextBox>("Prices key 4");
        Require(key.Text == "key2", "new dictionary key " + key.Text);
        key.Text = longKey; await Delay(100); Leave(key); await Delay(300);
        await Type("Prices 4", "99");
        await Undoable("Prices", async () => { Click(Named<Button>("Remove Prices 2")); await Delay(300); }, $"{{\"herb\":3,\"key\":25,\"{longKey}\":99}}");
        Click(Named<Button>("Unfold FirstWave")); await Delay(300);
        await Type("FirstWave Count", "8");
        Named<ComboBox>("FirstWave Rarity").SelectedItem = "Epic"; await Delay(300);
        await Undoable("FirstWave", () => Type("FirstWave Label", "Evening"), "{\"Count\":8,\"Delay\":1.5,\"Rarity\":\"Epic\",\"Label\":\"Evening\"}");
        Step("saved", Tuning.Values.ToDictionary(p => p.Key, p => p.Value.GetRawText()));
        Require(Saved("Tags") == "\"Gem,Key\"", "flags " + Saved("Tags"));
        Require(Saved("CrateTint") == "\"#10203040\"" && Saved("LabelColor") == "\"#ABCDEF\"", "colours " + Saved("CrateTint") + Saved("LabelColor"));

        // Alignment: every value input in the section starts at one x and every +/− ends at one x, at three widths; captures.
        var widths = new Dictionary<int, object>();
        foreach (var width in new[] { 1280, 1600, 1920 })
        {
            main.Width = width; await Delay(900);
            await Select();
            var section = Section();
            var rows = section.GetVisualDescendants().OfType<Grid>().Where(g => g.ColumnDefinitions.Count >= 2 && g.ColumnDefinitions[0].Width.IsAbsolute &&
                g.Children.OfType<TextBlock>().Any(t => t.Classes.Contains("fieldLabel") && Grid.GetColumn(t) == 0)).ToArray();
            double X(Visual c) => Math.Round(c.TranslatePoint(default, main)!.Value.X, 1);
            var lefts = rows.SelectMany(g => g.Children.Where(c => Grid.GetColumn(c) == 1)).Select(X).Distinct().ToArray();
            var ends = rows.SelectMany(g => g.Children.OfType<Button>().Where(b => Grid.GetColumn(b) == g.ColumnDefinitions.Count - 1 && g.ColumnDefinitions.Count > 2))
                .Select(b => Math.Round(X((Visual)b.Parent!) + b.Bounds.Right, 1)).Distinct().ToArray(); // layout bounds: the fold chevron is rotated
            var keyBox = Named<TextBox>("Prices key 3");
            var valueBox = Named<TextBox>("Prices 3");
            Require(lefts.Length == 1, $"{width}: value column x {string.Join(",", lefts)}");
            Require(ends.Length == 1, $"{width}: +/− right edge {string.Join(",", ends)}: " + string.Join("; ", rows.Select(g =>
                $"{g.Children.OfType<TextBlock>().First().Text} x{X(g)} w{g.Bounds.Width} cols {string.Join("/", g.ColumnDefinitions.Select(c => Math.Round(c.ActualWidth)))}")));
            Require(keyBox.Bounds.Width > 20 && Math.Abs(keyBox.Bounds.Width - valueBox.Bounds.Width) < 1.5, $"{width}: long key box {keyBox.Bounds.Width} vs value {valueBox.Bounds.Width}");
            widths[width] = new { rows = rows.Length, valueX = lefts[0], buttonRight = ends[0], keyWidth = keyBox.Bounds.Width };
            Capture(section, $"supply-{width}");
        }
        Step("alignment", widths);
        main.Width = 1280; await Delay(500);
        Require(await Save(), "save cancelled");
        Step("scene", (string)Get(main, "ScenePath")!);
    }

    // ---- reopen
    private static async Task Reopen()
    {
        await Select();
        Require(Named<TextBox>("Seed").Text == "9000000000" && Named<TextBox>("SlotMask").Text == "4294967295", "numbers after reopening");
        Require(Named<TextBox>("CrateTint").Text == "#10203040", "colour after reopening");
        Require(((Named<Button>("Tags").Content as TextBlock)?.Text) == "Gem, Key", "flags after reopening");
        Require(Named<TextBox>("StackSizes 4").Text == "9" && Named<TextBox>("Names 1").Text == "moss" && Named<CheckBox>("Unlocked 3").IsChecked == true, "lists after reopening");
        Require(Named<TextBox>("Prices 3").Text == "99", "dictionary after reopening");
        Step("values", Tuning.Values.ToDictionary(p => p.Key, p => p.Value.GetRawText()));

        var view = main.FindControl<SceneViewport>("Viewport")!;
        var play = main.FindControl<Button>("PlayButton")!;
        Click(play);
        var game = (GameSession)view.GetType().GetField("game", Any)!.GetValue(view)!;
        game.LoadFailed += message => throw new InvalidDataException(message);
        var project = GameSettings.Load(Scene.ProjectDirectory!).ProjectId!.Value;
        InputReplay.Load(Directory.GetFiles(Output, "*.inputreplay").Single()).Play(game, project, _ => { });
        Environment.SetEnvironmentVariable("MONOON_FIELD_TRACE", null);
        await Delay(500);
        Click(play);
        await Delay(500);
        Step("trace", File.Exists(Path.Combine(Output, "trace.txt")));
    }
}
