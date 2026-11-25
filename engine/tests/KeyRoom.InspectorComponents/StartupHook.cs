using System.Collections;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Brutal.SdlApi;
using Engine.Core;
using Engine.Desktop;
using Engine.Editor;

// Loaded into the packaged editor with DOTNET_STARTUP_HOOKS (roadmap 13.2, 13.3, 13.4, 13.12, 13.14). MONOON_IC_PHASE selects one
// launch: trace (Scene Play replay of the QA objects), save (save the untouched scene), first (every Inspector row through the UI,
// alignment captures, ends locked to Door) and restart (lock and saved field values after a restart).
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_IC_OUTPUT")!;
    private static readonly string Phase = Environment.GetEnvironmentVariable("MONOON_IC_PHASE")!;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string LongName = "Door to the courtyard with an intentionally long object name for trimming";
    private static readonly Dictionary<string, object?> Result = [];
    private static Window main = null!;
    private static SceneViewport view = null!;

    public static void Initialize() => Start(Path.Combine(Output, $"{Phase}-error.txt"), async life =>
    {
        main = life.MainWindow!;
        view = main.FindControl<SceneViewport>("Viewport")!;
        await Until(() => main.IsVisible && Find<ListBox>("Hierarchy").Items.Count > 1, "KeyRoom", 120);
        if (Phase == "trace") { await Trace(); return; }
        main.Width = 1280; main.Height = 800;
        await Delay(800);
        await (Phase switch { "save" => SavePhase(), "first" => First(), "restart" => Restart(), _ => throw new ArgumentException(Phase) });
        File.WriteAllText(Path.Combine(Output, $"{Phase}.json"), JsonSerializer.Serialize(Result, new JsonSerializerOptions { WriteIndented = true }));
    });

    // ---- helpers
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static T Find<T>(string name) where T : Control => main.FindControl<T>(name) ?? throw new InvalidDataException($"{name} not found");
    private static object? Get(object target, string name) =>
        target.GetType().GetProperty(name, Any) is { } p ? p.GetValue(target) : target.GetType().GetField(name, Any)?.GetValue(target);
    private static object? Call(object target, string method, params object?[] args) => target.GetType().GetMethod(method, Any)!.Invoke(target, args);
    private static SceneDocument Scene => (SceneDocument)Get(main, "Scene")!;
    private static SceneObject Object(string name) => Scene.Objects.First(o => o.Name == name);
    private static void Select(SceneObject? obj) => Call(main, "Select", obj);
    private static Rect In(Visual c, Visual root) => new(c.TranslatePoint(default, root)!.Value, c.Bounds.Size);
    private static Rect In(Visual c) => In(c, main);
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, b));
    private static void Step(string name, object? value)
    {
        Result[name] = value;
        File.AppendAllText(Path.Combine(Output, $"{Phase}.log"), $"{name}: {JsonSerializer.Serialize(value)}\n");
    }
    private static string Sha(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
    private static string ScenePath => (string)Get(main, "ScenePath")!;
    private static string Text => Scene.CaptureObjects();
    private static async Task<bool> Save() => await (Task<bool>)Call(main, "Save")!;
    private static async Task Undo() { await (Task)Call(main, "RestoreEdit", false)!; await Delay(250); }
    private static async Task Redo() { await (Task)Call(main, "RestoreEdit", true)!; await Delay(250); }
    private static async Task Resize(int width, int height) { main.Width = width; main.Height = height; await Delay(800); }
    private static JsonElement UserState() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Environment.GetEnvironmentVariable("MONOON_USER_DATA")!, "editor-state.json"))).RootElement;
    private static void Capture(Visual area, string name)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(area.Bounds.Width), (int)Math.Ceiling(area.Bounds.Height)));
        bitmap.Render(area);
        bitmap.Save(Path.Combine(Output, name + ".png"));
    }
    private static StackPanel InspectorStack => Find<StackPanel>("Inspector");
    private static ComponentSection[] Sections() => InspectorStack.GetVisualDescendants().OfType<ComponentSection>().Where(s => s.IsEffectivelyVisible).ToArray();
    private static ComponentSection Section(string header) => Sections().FirstOrDefault(s => s.Header as string == header)
        ?? throw new InvalidDataException($"no {header} section: {string.Join(", ", Sections().Select(s => s.Header))}");
    private static ToggleButton HeaderToggle(ComponentSection s) => s.GetVisualDescendants().OfType<ToggleButton>().First(t => t.Name == "HeaderToggle");
    private static CheckBox EnabledCheck(ComponentSection s) => s.GetVisualDescendants().OfType<CheckBox>().First(c => c.Name == "EnabledCheck");
    private static Button Gear(ComponentSection s) => s.GetVisualDescendants().OfType<Button>().First(b => ToolTip.GetTip(b) as string == "Component settings");
    // AttachedComponent is internal to the editor: read its members by name.
    private static object Row(ComponentSection s) => s.DataContext!;
    private static SceneComponent? Component(ComponentSection s) => s.DataContext?.GetType().Name == "AttachedComponent" ? (SceneComponent)Get(s.DataContext, "Component")! : null;
    private static async Task Gear(string section, string command)
    {
        Click(Gear(Section(section)));
        await Delay(200);
        var menu = (MenuFlyout)Get(main, "settingsMenu")!;
        var item = menu.Items.OfType<MenuItem>().First(i => i.Header as string == command);
        Require(item.IsEnabled, $"{section} · {command} is disabled");
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
        menu.Hide();
        await Delay(300);
    }
    private static T Tab<T>(string name) where T : Control => main.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    // ---- trace (13.2 Scene Play)
    private static async Task Trace()
    {
        await Until(() => Scene.Objects.Any(o => o.Name == "QA Gate"), "QA objects", 60);
        await Delay(500);
        var play = Find<Button>("PlayButton");
        Click(play);
        var game = (GameSession)view.GetType().GetField("game", Any)!.GetValue(view)!;
        game.LoadFailed += message => throw new InvalidDataException(message);
        var project = GameSettings.Load(Scene.ProjectDirectory!).ProjectId!.Value;
        var frames = File.ReadAllText(Path.Combine(Output, "captures.txt")).Split(',').Select(int.Parse).ToHashSet();
        Replay(game, project, Directory.GetFiles(Output, "*.inputreplay").Single(), frames, Directory.CreateDirectory(Path.Combine(Output, "captures")).FullName);
        Environment.SetEnvironmentVariable("MONOON_COMPONENT_TRACE", null); // the live Scene panel keeps ticking until Stop
        await Delay(500);
        Click(play);
        await Delay(500);
    }

    private static unsafe void Replay(GameSession game, Guid project, string recording, HashSet<int> frames, string captures)
    {
        var size = game.LogicalSize;
        var surface = Sdl.PInvoke.CreateSurface(size.Width, size.Height, SdlPixelFormat.Rgba32);
        var renderer = Sdl.Render.CreateSoftwareRenderer(surface);
        using (var canvas = new SdlCanvas(renderer, previewShader: ShaderPixels.Preview))
        {
            InputReplay.Load(recording).Play(game, project, frame =>
            {
                if (!frames.Contains(frame)) return;
                game.Draw(canvas, 1);
                canvas.FlushSprites();
                var pixels = Sdl.PInvoke.RenderReadPixels(renderer, null);
                if (pixels == null || Sdl.PInvoke.SavePNG(pixels, Path.Combine(captures, $"{frame:D4}.png")) == 0)
                    throw new IOException(Sdl.PInvoke.GetError());
                Sdl.PInvoke.DestroySurface(pixels);
            });
        }
        renderer.Dispose();
        Sdl.PInvoke.DestroySurface(surface);
    }

    // ---- phases
    private static async Task SavePhase()
    {
        Require(await Save(), "save cancelled");
        Step("scene", new { path = ScenePath, sha = Sha(ScenePath) });
    }

    private static async Task First()
    {
        main.Activate();
        Step("active_enabled", await ActiveEnabled());
        Step("script_fields", await ScriptFields());
        Step("component_menu", await ComponentMenu());
        Step("warnings", await Warnings());
        Step("alignment", await Alignment());
        Step("lock_debug", await LockAndDebug());
        Require(await Save(), "save cancelled");
        Step("scene", new { path = ScenePath, sha = Sha(ScenePath), state = File.ReadAllText(Path.Combine(Environment.GetEnvironmentVariable("MONOON_USER_DATA")!, "editor-state.json")) });
    }

    // 13.2: the name-row check and a header check each make one Undo record; the Hierarchy dims the object, the Scene view
    // stops drawing it, and on a prefab instance both show in the override list.
    private static async Task<object> ActiveEnabled()
    {
        var before = Text;
        Select(null); // an empty selection shows the empty Inspector, not the multi-object one
        await Delay(200);
        Require(!Find<StackPanel>("MultiInspector").IsVisible && Find<TextBlock>("EmptyInspector").IsVisible, "empty selection");
        Select(Object("Door"));
        await Delay(300);
        var active = Find<CheckBox>("ObjectActive");
        Require(active.IsChecked == true && active.IsEnabled, "Door starts active");
        active.IsChecked = false;
        await Delay(300);
        var door = Object("Door");
        var items = (IDictionary)Get(main, "treeItems")!;
        double opacity = (double)Get(items[door]!, "Opacity")!;
        bool drawn = ((IEnumerable)Call(view, "EditItems")!).Cast<System.Runtime.CompilerServices.ITuple>().Any(item => item[0] == door);
        Require(!door.Active && opacity == 0.55 && !drawn && Text.Contains("  active: false\n"), $"inactive Door: opacity {opacity}, drawn {drawn}");
        Capture(Find<Border>("InspectorPanel"), "i2-door-inactive");
        Capture(Find<ListBox>("Hierarchy"), "i2-hierarchy-dimmed");
        await Undo();
        Require(Text == before && Object("Door").Active, "one Undo restores the active Door");
        await Redo();
        Require(!Object("Door").Active, "Redo makes it inactive again");
        await Undo();

        Select(Object("Door"));
        await Delay(300);
        var check = EnabledCheck(Section("Sprite Renderer"));
        Require(check.IsVisible && check.IsChecked == true, "Sprite Renderer header check");
        check.IsChecked = false;
        await Delay(300);
        Require(!Object("Door").GetComponent<SpriteRendererComponent>()!.Enabled && Text.Contains("    enabled: false\n"), "Sprite Renderer turned off");
        Capture(Section("Sprite Renderer"), "i2-sprite-off");
        await Undo();
        Require(Text == before, "one Undo restores the Sprite Renderer");
        Select(Object("Main Camera"));
        await Delay(300);
        bool cameraCheck = EnabledCheck(Section("Camera 2D")).IsVisible;
        Require(!cameraCheck, "a Camera 2D cannot be turned off (no header check)");

        var pot = Object("Small pot");
        Require(pot.Prefab != null, "Small pot is a prefab instance");
        Select(pot);
        await Delay(300);
        Find<CheckBox>("ObjectActive").IsChecked = false;
        await Delay(200);
        EnabledCheck(Section("Sprite Renderer")).IsChecked = false;
        await Delay(300);
        var overrides = ((IEnumerable)Call(Scene, "PropertyOverrides", Object("Small pot"))!).Cast<object>().Select(o => o.ToString()!).ToArray();
        Require(overrides.Any(o => o.EndsWith("· active")) && overrides.Any(o => o.EndsWith("· spriteRenderer.enabled")), "overrides: " + string.Join(" | ", overrides));
        await Undo();
        await Undo();
        Require(Text == before, "two Undos restore the prefab instance");
        return new { door_opacity = opacity, door_drawn = drawn, camera_check = cameraCheck, overrides };
    }

    // 13.3: Header, Range slider, tooltips ([Tooltip] and /// summary), [SerializeField] private field with [Min].
    private static async Task<object> ScriptFields()
    {
        var before = Text;
        Select(Object("Player"));
        await Delay(400);
        var section = Section("PlayerMovement");
        var header = section.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Classes.Contains("fieldHeader"));
        var slider = section.GetVisualDescendants().OfType<Slider>().Single();
        var speedBox = section.GetVisualDescendants().OfType<TextBox>().First(t => AutomationProperties.GetName(t) == "MoveSpeed");
        var reachBox = section.GetVisualDescendants().OfType<TextBox>().First(t => AutomationProperties.GetName(t) == "coinReach");
        string? Tip(Control c) => c.GetVisualAncestors().OfType<Grid>().Select(g => ToolTip.GetTip(g) as string).FirstOrDefault(t => t != null);
        Require(header?.Text == "Movement", "Movement header");
        Require(slider is { Minimum: 60, Maximum: 480, Value: 240 } && speedBox.Text == "240", $"slider {slider.Minimum}-{slider.Maximum} at {slider.Value}");
        Require(Tip(speedBox) == "Walking speed in pixels per second." && Tip(reachBox) == "How far a coin can be picked up or dropped, in pixels.",
            $"tooltips '{Tip(speedBox)}' / '{Tip(reachBox)}'");
        var speedRow = In(speedBox); var reachRow = In(reachBox);
        var sliderRow = In(slider);
        Require(Math.Abs(speedRow.Height - reachRow.Height) < .5 && sliderRow.Height <= reachRow.Height + .5 && Math.Abs(speedRow.Right - reachRow.Right) < .5,
            $"Range row keeps the input height and right edge: speed {speedRow}, slider {sliderRow}, reach {reachRow}");
        Capture(section, "i3-player-movement");
        ScriptComponent Movement() => Object("Player").Components.OfType<ScriptComponent>().Single(c => c.Script == "PlayerMovement");
        slider.Value = 300;
        await Delay(300);
        Require(Movement().Values.TryGetValue("MoveSpeed", out var speed) && speed.GetSingle() == 300, "slider stores MoveSpeed 300");
        await Undo();
        Require(Text == before, "one Undo restores MoveSpeed");
        await Redo();
        Require(Movement().Values["MoveSpeed"].GetSingle() == 300, "Redo stores 300 again");
        Select(Object("Player"));
        await Delay(400);
        reachBox = Section("PlayerMovement").GetVisualDescendants().OfType<TextBox>().First(t => AutomationProperties.GetName(t) == "coinReach");
        reachBox.Text = "5";
        await Delay(200);
        Call(main, "CommitEdit");
        float clamped = Movement().Values["coinReach"].GetSingle();
        Require(clamped == 16, $"[Min(16)] keeps coinReach at 16, got {clamped}");
        await Undo();
        Require(!Movement().Values.ContainsKey("coinReach") && Movement().Values["MoveSpeed"].GetSingle() == 300, "Undo removes the coinReach override");
        return new { header = header!.Text, slider = new { slider.Minimum, slider.Maximum }, clamped,
            speed_box = new { x = Math.Round(speedRow.X, 1), right = Math.Round(speedRow.Right, 1), height = Math.Round(speedRow.Height, 1) },
            reach_box = new { x = Math.Round(reachRow.X, 1), right = Math.Round(reachRow.Right, 1), height = Math.Round(reachRow.Height, 1) } };
    }

    // 13.4: Add Component search and groups; gear commands, each one Undo record.
    private static async Task<object> ComponentMenu()
    {
        var before = Text;
        Select(Object("Door"));
        await Delay(300);
        Click(Find<Button>("AddComponentButton"));
        await Delay(500);
        var flyout = (Flyout)Get(main, "componentMenu")!;
        var content = (StackPanel)flyout.Content!;
        var search = content.Children.OfType<TextBox>().Single();
        var choices = content.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("componentChoice")).ToArray();
        var groups = content.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("componentGroup")).Select(t => t.Text!).ToArray();
        Require(groups.SequenceEqual(["Rendering", "Physics", "UI", "Audio", "Animation", "Navigation", "Scripts"]), "groups " + string.Join(",", groups));
        search.Text = "col";
        await Delay(300);
        string[] Shown() => choices.Where(b => b.IsVisible).Select(b => AutomationProperties.GetName(b)!).ToArray();
        var col = Shown();
        string[] colliders = ["Box Collider 2D", "Capsule Collider 2D", "Circle Collider 2D", "Edge Collider 2D", "Polygon Collider 2D"];
        Require(colliders.All(col.Contains) && col.Except(colliders).All(n => ((ProjectScripts)Get(main, "scripts")!).Components().Contains(n)), "col: " + string.Join(",", col));
        object Columns(string label)
        {
            var visible = choices.Where(b => b.IsVisible).ToArray();
            var icons = visible.Select(b => b.GetVisualDescendants().OfType<PathIcon>().FirstOrDefault()).Where(i => i != null).Select(i => Math.Round(In(i!, content).X, 1)).Distinct().ToArray();
            var labels = visible.Select(b => Math.Round(In(b.GetVisualDescendants().OfType<TextBlock>().First(), content).X, 1)).Distinct().ToArray();
            var inside = visible.All(b => In(b.GetVisualDescendants().OfType<TextBlock>().First(), content).Right <= content.Bounds.Width + .5);
            Require(icons.Length == 1 && labels.Length == 1 && inside, $"{label}: icon x {string.Join(",", icons)} label x {string.Join(",", labels)} inside {inside}");
            return new { width = Math.Round(content.Bounds.Width, 1), icon_x = icons[0], label_x = labels[0] };
        }
        var narrow = Columns("menu");
        Capture(content, "i4-add-component-col");
        content.Width = 360;
        await Delay(300);
        var wide = Columns("wide menu");
        search.Text = "";
        await Delay(200);
        Require(choices.All(b => b.IsVisible), "empty search shows everything");
        content.Width = 180;
        await Delay(300);
        var tight = Columns("narrow menu");
        Capture(content, "i4-add-component-narrow");
        flyout.Hide();
        await Delay(300);

        async Task<string> Once(string what, Func<Task> act)
        {
            var start = Text;
            await act();
            var edited = Text;
            Require(edited != start, what + " changed nothing");
            await Undo();
            Require(Text == start, what + ": one Undo restores the scene");
            await Redo();
            Require(Text == edited, what + ": Redo repeats it");
            await Undo();
            return what;
        }
        var done = new List<string>();
        // Door's collider pasted onto the chest (it has none: Paste Component as New), the exit trigger's values onto the chest's base.
        Select(Object("Door")); await Delay(300);
        await Gear("Box Collider 2D", "Copy Component");
        done.Add(await Once("Paste Component as New (Door collider → Chest)", async () =>
        {
            Select(Object("Chest")); await Delay(300);
            await Gear("OpenChest", "Paste Component as New");
            Require(Object("Chest").GetComponent<BoxCollider2DComponent>() is { IsTrigger: false }, "Chest got Door's collider");
        }));
        Select(Object("Exit")); await Delay(300);
        var exitCollider = Object("Exit").GetComponent<Collider2DComponent>()!;
        await Gear(Sections().First(s => Component(s) is Collider2DComponent).Header as string ?? "", "Copy Component");
        done.Add(await Once("Paste Component Values (Exit → Chest base)", async () =>
        {
            Select(Object("Chest base")); await Delay(300);
            var baseSection = Sections().First(s => Component(s) is Collider2DComponent).Header as string ?? "";
            await Gear(baseSection, "Paste Component Values");
            Require(Object("Chest base").GetComponent<Collider2DComponent>()!.IsTrigger == exitCollider.IsTrigger, "Chest base collider has Exit's values");
        }));
        done.Add(await Once("Reset (Door Sprite Renderer)", async () =>
        {
            Select(Object("Door")); await Delay(300);
            await Gear("Sprite Renderer", "Reset");
            Require(Object("Door").GetComponent<SpriteRendererComponent>()!.Asset == null, "reset clears the sprite");
        }));
        done.Add(await Once("Move Up (Door Box Collider 2D)", async () =>
        {
            Select(Object("Door")); await Delay(300);
            await Gear("Box Collider 2D", "Move Up");
            Require(Object("Door").Components[0] is BoxCollider2DComponent, "collider moved first");
        }));
        done.Add(await Once("Move Down (Door DoorLock)", async () =>
        {
            Select(Object("Door")); await Delay(300);
            await Gear("DoorLock", "Move Down");
            Require(Object("Door").Components[1] is ScriptComponent { Script: "DoorLock" }, "script moved down");
        }));
        done.Add(await Once("Remove (Door Audio Source)", async () =>
        {
            Select(Object("Door")); await Delay(300);
            await Gear("Audio Source", "Remove");
            Require(Object("Door").GetComponent<AudioSourceComponent>() == null, "audio source removed");
        }));
        Require(Text == before, "component menu leaves the scene as it was");
        return new { groups, col, menus = new[] { narrow, wide, tight }, undone = done };
    }

    // 13.14: a Rigidbody 2D without a collider gets a yellow line; Undo removes it.
    private static async Task<object> Warnings()
    {
        var before = Text;
        Select(Object("Door"));
        await Delay(300);
        Require(!Find<TextBlock>("SceneWarning").IsVisible, "KeyRoom has a start camera");
        Require((bool)Call(main, "AttachComponent", Object("Door"), new Rigidbody2DComponent { BodyType = BodyType2D.Kinematic })!, "Rigidbody 2D attached");
        await Delay(300);
        string? Warning() => Sections().Where(s => Component(s) is Rigidbody2DComponent).Select(s => (string?)Get(Row(s), "Warning")).Single();
        Require(Warning() == null, "with a collider: no warning");
        await Gear("Box Collider 2D", "Remove");
        var warning = Warning();
        var line = InspectorStack.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Classes.Contains("componentWarning") && t.IsEffectivelyVisible && t.Text == warning);
        Require(warning?.StartsWith("Rigidbody 2D without a collider") == true && line != null, $"warning '{warning}'");
        Capture(Find<Border>("InspectorPanel"), "i14-rigidbody-warning");
        await Undo();
        Require(Warning() == null, "Undo (collider back) clears the warning");
        await Undo();
        Require(Text == before, "Undo removes the Rigidbody 2D");
        return new { warning };
    }

    // Header titles, icons and checks share one x in every section; the name row keeps the field column; at three window
    // widths and with a long object name.
    private static async Task<object> Alignment()
    {
        var rows = new List<object>();
        object Measure(string label)
        {
            var sections = Sections();
            double X(Visual v) => Math.Round(In(v).X, 1);
            var titles = sections.Select(s => X(HeaderToggle(s).GetVisualDescendants().OfType<TextBlock>().First())).Distinct().ToArray();
            var icons = sections.Select(s => X(HeaderToggle(s).GetVisualDescendants().OfType<PathIcon>().ElementAt(1))).Distinct().ToArray();
            var checks = sections.Select(EnabledCheck).Where(c => c.IsVisible).Select(X).Distinct().ToArray();
            var name = Find<TextBox>("ObjectName"); var tag = Find<TextBox>("ObjectTag"); var activeCheck = Find<CheckBox>("ObjectActive");
            double middle(Visual v) => In(v).Y + v.Bounds.Height / 2;
            bool nameRow = Math.Abs(In(name).X - In(tag).X) < .5 && Math.Abs(middle(activeCheck) - middle(name)) < 1 && In(activeCheck).Right < In(name).X;
            Require(titles.Length == 1 && icons.Length == 1 && checks.Length == 1 && nameRow,
                $"{label}: titles {string.Join(",", titles)} icons {string.Join(",", icons)} checks {string.Join(",", checks)} name row {nameRow}");
            return new { label, sections = sections.Length, title_x = titles[0], icon_x = icons[0], check_x = checks[0], name_x = X(name) };
        }
        Select(Object("Door"));
        foreach (var (w, h) in new[] { (1280, 800), (1600, 900), (1920, 1080) })
        {
            await Resize(w, h);
            rows.Add(Measure($"window {w}"));
            Capture(Find<Border>("InspectorPanel"), $"inspector-{w}");
        }
        await Resize(1280, 800);
        var door = Object("Door");
        door.Name = LongName;
        Call(main, "FinishObjectChange", door, null);
        Select(door);
        await Delay(500);
        rows.Add(Measure("long name 1280"));
        Capture(Find<Border>("InspectorPanel"), "inspector-long-name");
        await Undo();
        Require(Object("Door").Name == "Door", "rename undone");
        return rows;
    }

    // 13.12: the lock keeps Door while Player is selected and is stored in editor-state.json; the Debug view lists the saved keys.
    private static async Task<object> LockAndDebug()
    {
        var before = Text;
        Select(Object("Door"));
        await Delay(300);
        var lockTool = Tab<ToggleButton>("InspectorLockToggle");
        lockTool.IsChecked = true;
        await Delay(300);
        Select(Object("Player"));
        await Delay(400);
        bool kept = Find<TextBox>("ObjectName").Text == "Door" && Sections().Any(s => s.Header as string == "DoorLock");
        Require(kept, $"locked Inspector shows {Find<TextBox>("ObjectName").Text}");
        var state = UserState().GetProperty("InspectorLock");
        var door = Object("Door");
        Require(state.TryGetProperty(ScenePath, out var ids) && ids[0].GetGuid() == door.Id, "lock stored per scene: " + state);
        // Edits on the locked Inspector go to Door, not the selection.
        EnabledCheck(Section("Sprite Renderer")).IsChecked = false;
        await Delay(300);
        Require(!Object("Door").GetComponent<SpriteRendererComponent>()!.Enabled && Object("Hero").GetComponent<SpriteRendererComponent>()!.Enabled, "edit reaches the locked object");
        await Undo();
        Require(Text == before, "Undo of the locked edit");
        var debugTool = Tab<ToggleButton>("InspectorDebugToggle");
        debugTool.IsChecked = true;
        await Delay(400);
        var objectRows = Find<StackPanel>("DebugObjectRows").GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("fieldLabel")).Select(t => t.Text).ToArray();
        // Every component's fields are a Debug panel (a folded section may not have realized it yet).
        var debugPanels = Sections().Count(s => Component(s) != null && Get(Row(s), "Fields") is StackPanel p && p.Classes.Contains("debugFields"));
        bool readOnly = !Find<ItemsControl>("AttachedComponents").GetVisualDescendants().OfType<TextBox>().Any(t => t.IsEffectivelyVisible) &&
            Sections().Where(s => Component(s) != null).All(s => !Gear(s).IsVisible && !EnabledCheck(s).IsVisible);
        Require(Find<ComponentSection>("DebugSection").IsVisible && objectRows.Contains("id") && objectRows.Contains("name") && debugPanels == door.Components.Length && readOnly,
            $"debug rows {string.Join(",", objectRows)}; panels {debugPanels}; read-only {readOnly}");
        Capture(Find<Border>("InspectorPanel"), "i12-debug");
        // A script's private fields: the Debug view of the Player (unlock) lists PlayerMovement's skipped fields too.
        lockTool.IsChecked = false;
        await Delay(300);
        Select(Object("Player"));
        await Delay(400);
        var movementKeys = Section("PlayerMovement").GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("fieldLabel")).Select(t => t.Text).ToArray();
        Require(movementKeys.Contains("script") && movementKeys.Contains("values"), "PlayerMovement debug keys " + string.Join(",", movementKeys));
        debugTool.IsChecked = false;
        await Delay(300);
        Select(Object("Door"));
        await Delay(300);
        lockTool.IsChecked = true;
        await Delay(300);
        Select(Object("Player"));
        await Delay(300);
        Require(Text == before && UserState().GetProperty("InspectorDebug").GetBoolean() == false, "lock and Debug view change no scene data");
        return new { kept, object_keys = objectRows, debug_panels = debugPanels, movement_keys = movementKeys };
    }

    private static async Task Restart()
    {
        main.Activate();
        await Delay(500);
        var door = Object("Door");
        bool restored = Find<TextBox>("ObjectName").Text == "Door" && Tab<ToggleButton>("InspectorLockToggle").IsChecked == true;
        Select(Object("Player"));
        await Delay(400);
        restored &= Find<TextBox>("ObjectName").Text == "Door";
        Require(restored, $"lock restored after restart: {Find<TextBox>("ObjectName").Text}");
        var speed = Object("Player").Components.OfType<ScriptComponent>().Single(c => c.Script == "PlayerMovement").Values["MoveSpeed"].GetSingle();
        Require(speed == 300, $"MoveSpeed saved as 300, got {speed}");
        Tab<ToggleButton>("InspectorDebugToggle").IsChecked = true;
        await Delay(300);
        Capture(Find<Border>("InspectorPanel"), "restart-locked-debug");
        Tab<ToggleButton>("InspectorDebugToggle").IsChecked = false;
        await Delay(300);
        Require(await Save(), "save cancelled");
        Step("restart", new { restored, door = door.Name, speed, sha = Sha(ScenePath) });
    }
}
