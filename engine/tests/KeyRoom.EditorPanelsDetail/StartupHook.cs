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
using Avalonia.Threading;
using Avalonia.VisualTree;
using Engine.Core;
using Engine.Editor;

// Loaded into the packaged editor with DOTNET_STARTUP_HOOKS (roadmap 12.7 panel details). MONOON_PANELS_PHASE selects one launch:
// save (save the untouched scene), first (Hierarchy, Project, Inspector, Console, alignment) and restart (per-user state after a restart).
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_PANELS_OUTPUT")!;
    private static readonly string Phase = Environment.GetEnvironmentVariable("MONOON_PANELS_PHASE")!;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    // MONOON_QA_SLOW scales waits and time limits for emulated x64 guests (TCG); 1 on a normal desktop.
    private static readonly double Slow = double.TryParse(Environment.GetEnvironmentVariable("MONOON_QA_SLOW"), System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 1;
    private static Task Delay(int ms) => Task.Delay((int)(ms * Slow));
    // How each window capture was taken: screencapture -l on macOS, RenderTargetBitmap (no native Scene view) elsewhere.
    private static readonly Dictionary<string, string> Captures = [];
    private const string LongName = "Door to the courtyard with an intentionally long object name for trimming";
    private static readonly Dictionary<string, object?> Result = [];
    private static Window main = null!;
    private static SceneViewport view = null!;

    public static void Initialize() => Task.Run(async () =>
    {
        while (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: not null })
            await Task.Delay(100);
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var life = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
            try
            {
                main = life.MainWindow!;
                view = main.FindControl<SceneViewport>("Viewport")!;
                await Until(() => main.IsVisible && Find<TreeView>("Hierarchy").Items.Count > 1, "KeyRoom");
                Result["os"] = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
                Result["captures"] = Captures;
                main.Width = 1280; main.Height = 800;
                await Delay(800);
                await (Phase switch { "save" => SavePhase(), "first" => First(), "restart" => Restart(), _ => throw new ArgumentException(Phase) });
                File.WriteAllText(Path.Combine(Output, $"{Phase}.json"), JsonSerializer.Serialize(Result, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(Output, $"{Phase}-error.txt"), ex.ToString()); }
            finally { life.Shutdown(); }
        });
    });

    // ---- helpers
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static async Task Until(Func<bool> done, string what, int seconds = 60)
    {
        var limit = DateTime.UtcNow.AddSeconds(seconds * Slow);
        while (!done()) { if (DateTime.UtcNow > limit) throw new TimeoutException(what); await Delay(50); }
    }
    private static T Find<T>(string name) where T : Control => main.FindControl<T>(name) ?? throw new InvalidDataException($"{name} not found");
    private static object? Get(object target, string name) =>
        target.GetType().GetProperty(name, Any) is { } p ? p.GetValue(target) : target.GetType().GetField(name, Any)?.GetValue(target);
    private static object? Call(object target, string method, params object?[] args) => target.GetType().GetMethod(method, Any)!.Invoke(target, args);
    private static SceneDocument Scene => (SceneDocument)Get(main, "Scene")!;
    private static SceneObject? Selected => (SceneObject?)Get(view, "Selected");
    private static SceneObject Object(string name) => Scene.Objects.First(o => o.Name == name);
    private static Rect In(Visual c) => new(c.TranslatePoint(default, main)!.Value, c.Bounds.Size);
    private static double Mid(Visual c) => In(c).Y + c.Bounds.Height / 2;
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, b));
    // The button's own click path, so a ToggleButton flips IsChecked as with the mouse.
    private static void Press(Button b) => typeof(Button).GetMethod("OnClick", Any)!.Invoke(b, null);
    private static void Step(string name, object? value)
    {
        Result[name] = value;
        File.AppendAllText(Path.Combine(Output, $"{Phase}.log"), $"{name}: {JsonSerializer.Serialize(value)}\n");
    }
    private static string Sha(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
    private static string ScenePath => (string)Get(main, "ScenePath")!;
    private static JsonElement UserState() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Environment.GetEnvironmentVariable("MONOON_USER_DATA")!, "editor-state.json"))).RootElement;

    private static void Capture(Visual area, string name)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(area.Bounds.Width), (int)Math.Ceiling(area.Bounds.Height)));
        bitmap.Render(area);
        bitmap.Save(Path.Combine(Output, name + ".png"));
    }
    [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.A.dylib")] private static extern nint objc_msgSend(nint receiver, nint selector);
    [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.A.dylib")] private static extern nint sel_registerName(string name);
    // The OS capture includes the native SDL Scene view, which RenderTargetBitmap leaves blank.
    private static void ScreenCapture(string name)
    {
        if (main.TryGetPlatformHandle() is not { HandleDescriptor: "NSWindow" } handle) { Capture(main, name); Captures[name] = "RenderTargetBitmap"; return; }
        Captures[name] = "screencapture -l";
        var number = objc_msgSend(handle.Handle, sel_registerName("windowNumber"));
        using var capture = System.Diagnostics.Process.Start("/usr/sbin/screencapture", ["-x", "-o", "-l", number.ToString(), Path.Combine(Output, name + ".png")]);
        capture.WaitForExit();
    }
    private static async Task Resize(int width, int height) { main.Width = width; main.Height = height; await Delay(800); }
    private static async Task<bool> Save() => await (Task<bool>)Call(main, "Save")!;

    // ---- phases
    private static async Task SavePhase()
    {
        Require(await Save(), "save cancelled");
        Step("scene", new { path = ScenePath, sha = Sha(ScenePath) });
    }

    private static async Task First()
    {
        main.Activate();
        // The manual's workspace picture: default layout, Player selected (docs/manual/editor-workspace.png).
        Call(main, "Select", Object("Player"));
        await Delay(800);
        ScreenCapture("workspace-1280");
        Step("hierarchy_search", await HierarchySearch());
        Step("scene_marks", await SceneMarks());
        Step("project", await ProjectPanel());
        Step("inspector", await InspectorFolds());
        Step("component_enable", new { qualifying = Array.Empty<string>(), reason = "No Engine.Core component has an enabled/active property; no runtime field was added." });
        Step("console", await Console());
        Step("alignment", await Alignment());
        // Restore the name, leave Door hidden and Wall Top locked for the restart phase, then save the scene.
        var renamed = Object(LongName);
        renamed.Name = "Door";
        Call(main, "FinishObjectChange", renamed, null);
        await Delay(400);
        SetMark(Object("Door"), hide: true, on: true);
        SetMark(Object("Wall Top"), hide: false, on: true);
        Require(await Save(), "save cancelled");
        Step("scene", new { path = ScenePath, sha = Sha(ScenePath), state = File.ReadAllText(Path.Combine(Environment.GetEnvironmentVariable("MONOON_USER_DATA")!, "editor-state.json")) });
    }

    private static async Task Restart()
    {
        main.Activate();
        var door = Object("Door");
        var top = Object("Wall Top");
        var hidden = (HashSet<Guid>)Get(view, "HiddenIds")!;
        var locked = (HashSet<Guid>)Get(view, "LockedIds")!;
        Require(hidden.SetEquals([door.Id]) && locked.SetEquals([top.Id]), $"marks not restored: hidden {hidden.Count}, locked {locked.Count}");
        Require(Row(door).Children.OfType<ToggleButton>().First().IsChecked == true, "Door eye is not shown as hidden after restart");
        Require(!EditItems().Contains(door), "restored hidden Door is drawn");
        // Fold state: Sprite Renderer was folded in the first run.
        Call(main, "Select", door);
        await Delay(400);
        Require(Section("Sprite Renderer").HeaderToggle().IsChecked == false, "Sprite Renderer fold state not restored");
        Require(Find<ToggleButton>("ProjectViewToggle").IsChecked == true && Find<ListBox>("ProjectBrowser").Classes.Contains("grid"), "grid view not restored");
        ScreenCapture("restart-1280");
        // Unchanged scene bytes after a save with the marks restored.
        Require(await Save(), "save cancelled");
        Step("restart", new { hidden = door.Name, locked = top.Name, folded = "Sprite Renderer", grid = true, sha = Sha(ScenePath) });
    }

    // ---- Hierarchy
    private static TreeView Tree => Find<TreeView>("Hierarchy");
    private static IEnumerable<TreeViewItem> Items(ItemCollection items) => items.OfType<TreeViewItem>().SelectMany(i => Items(i.Items).Prepend(i));
    private static Grid Row(SceneObject obj) => Tree.GetVisualDescendants().OfType<Grid>().First(g => g.Classes.Contains("hierarchyRow") && g.Tag == obj);

    private static async Task<object> HierarchySearch()
    {
        var search = Find<TextBox>("HierarchySearch");
        var results = new List<object>();
        foreach (var query in new[] { "satchel", "Door", "zzz" })
        {
            search.Text = query;
            await Delay(200);
            var all = Items(Tree.Items).ToList();
            bool Matches(TreeViewItem i) => ((SceneObject)i.Tag!).Name.Contains(query, StringComparison.OrdinalIgnoreCase);
            bool Within(TreeViewItem i) => Matches(i) || Items(i.Items).Any(Matches);
            var visible = all.Where(i => i.IsVisible && (i.Parent is not TreeViewItem p || p.IsVisible)).ToList();
            Require(all.All(i => i.IsVisible == Within(i)), $"'{query}': rows other than matches and their parents are visible");
            Require(all.Where(Matches).All(i => i.IsEffectivelyVisible), $"'{query}': a match is collapsed out of view");
            var none = Find<TextBlock>("HierarchyNoMatch").IsVisible;
            Require(none == !all.Any(Matches), $"'{query}': empty-result text {none}");
            results.Add(new { query, matches = all.Count(Matches), shown = visible.Select(i => (string)i.Header!).ToArray(), no_match_text = none });
            if (query == "satchel") Capture(Find<Control>("Hierarchy").Parent as Visual ?? Tree, "hierarchy-search");
        }
        search.Text = "";
        await Delay(200);
        Require(Items(Tree.Items).All(i => i.IsVisible), "clearing the search left rows hidden");
        return results;
    }

    // ---- Scene eye and lock
    private static List<SceneObject> EditItems() => ((IEnumerable)Call(view, "EditItems")!).Cast<object>()
        .Select(t => (SceneObject)t.GetType().GetField("Item1")!.GetValue(t)!).ToList();
    private static void SetMark(SceneObject obj, bool hide, bool on)
    {
        var toggle = Row(obj).Children.OfType<ToggleButton>().ElementAt(hide ? 0 : 1);
        if (toggle.IsChecked != on) Press(toggle);
        Require(toggle.IsChecked == on, "toggle did not change");
    }
    // A click in the Scene at the object's centre, through the viewport's own press handling.
    private static SceneObject? ClickAt(SceneObject obj)
    {
        Call(main, "Select", (SceneObject?)null);
        var c = obj.Bounds;
        var scale = (double)Get(view, "Scale")!;
        var origin = (Point)Get(view, "Origin")!;
        var point = new Point((c.X + c.Width / 2) * scale + origin.X, (c.Y + c.Height / 2) * scale + origin.Y);
        Require(new Rect(view.Bounds.Size).Contains(point), $"{obj.Name} centre {point} is outside the Scene view");
        Call(view, "BeginDrag", point, false);
        Call(view, "EndDrag");
        return Selected;
    }

    private static async Task<object> SceneMarks()
    {
        var door = Object("Door");
        var before = Sha(ScenePath);
        var row = Row(door);
        var name = row.Children.OfType<TextBlock>().First();
        var eye = row.Children.OfType<ToggleButton>().ElementAt(0);
        var lockToggle = row.Children.OfType<ToggleButton>().ElementAt(1);
        double nameX = In(name).X, nameW = name.Bounds.Width;
        Require(eye.Opacity == 0 && lockToggle.Opacity == 0, "unset toggles are visible without hover");
        Require(ClickAt(door) == door, "Door is not picked before hiding");
        Require(EditItems().Contains(door), "Door is not drawn before hiding");
        ScreenCapture("marks-before");

        SetMark(door, hide: true, on: true);
        await Delay(300);
        bool drawnHidden = EditItems().Contains(door);
        var pickedHidden = ClickAt(door);
        Require(!drawnHidden && pickedHidden != door, $"hidden Door drawn {drawnHidden}, picked {pickedHidden?.Name}");
        Require(eye.Opacity == 1 && Math.Abs(In(name).X - nameX) < .5 && Math.Abs(name.Bounds.Width - nameW) < .5, "setting the eye moved the row");
        ScreenCapture("marks-hidden");
        // Play ignores the editor-only mark: the runtime object stays active.
        view.GetType().GetField("KeepSimulationActive", Any)!.SetValue(view, (Func<bool>)(() => true));
        Click(Find<Button>("PlayButton"));
        await Until(() => view.IsPlaying, "Play");
        await Delay(800);
        var world = (SceneGame)Get(view, "PlayWorld")!;
        bool activeInPlay = world.IsActive(door.Id);
        Click(Find<Button>("PlayButton"));
        await Until(() => !view.IsPlaying, "Stop");
        await Delay(400);
        Require(activeInPlay, "hidden Door is inactive in Play");
        row = Row(door);
        SetMark(door, hide: true, on: false);

        SetMark(door, hide: false, on: true);
        await Delay(300);
        bool drawnLocked = EditItems().Contains(door);
        var pickedLocked = ClickAt(door);
        Require(drawnLocked && pickedLocked != door, $"locked Door drawn {drawnLocked}, picked {pickedLocked?.Name}");
        // A locked object is still selectable from the Hierarchy.
        Call(main, "Select", door);
        Require(Selected == door, "locked Door cannot be selected in the Hierarchy");
        // Hiding the parent hides the child too.
        var wall = Object("Wall");
        SetMark(door, hide: false, on: false);
        SetMark(wall, hide: true, on: true);
        await Delay(300);
        bool childHidden = !EditItems().Contains(door);
        SetMark(wall, hide: true, on: false);
        Require(childHidden, "hiding Wall did not hide its child Door");

        var state = UserState();
        Require(state.GetProperty("Hidden"u8).EnumerateObject().All(p => p.Value.GetArrayLength() == 0) || !state.GetProperty("Hidden"u8).EnumerateObject().Any(), "cleared marks remain in the user state");
        // Marks never touch the document: save and compare bytes with the file before any mark.
        Require(await Save(), "save cancelled");
        var after = Sha(ScenePath);
        Require(before == after, "saving with marks changed the scene file");
        return new { @object = door.Name, hidden = new { drawn = drawnHidden, picked = pickedHidden?.Name, active_in_play = activeInPlay },
            locked = new { drawn = drawnLocked, picked = pickedLocked?.Name, hierarchy_select = true }, parent_hides_child = childHidden,
            scene_sha_before = before, scene_sha_after = after };
    }

    // ---- Project
    private static ListBox Browser => Find<ListBox>("ProjectBrowser");
    private static string Folder => (string)Get(main, "browserFolder")!;
    private static string[] Crumbs() => Find<StackPanel>("ProjectPath").Children.OfType<Button>().Select(b => ((TextBlock)b.Content!).Text!).ToArray();
    private static void Open(string relative)
    {
        var entry = Browser.Items.Cast<object>().First(e => (string)Get(e!, "RelativePath")! == relative);
        Call(main, "OpenEntry", entry);
    }

    private static async Task<object> ProjectPanel()
    {
        var steps = new List<object>();
        Require(Folder == "" && Crumbs().SequenceEqual(["KeyRoom"]), $"root path bar {string.Join('/', Crumbs())}");
        Open("Assets"); await Delay(300);
        Open("Assets/Fonts"); await Delay(300);
        Require(Folder == "Assets/Fonts" && Crumbs().SequenceEqual(["KeyRoom", "Assets", "Fonts"]), $"path bar {string.Join('/', Crumbs())}");
        steps.Add(new { folder = Folder, crumbs = Crumbs() });
        Capture(Find<Control>("ProjectPanel"), "project-path");
        Click(Find<StackPanel>("ProjectPath").Children.OfType<Button>().First(b => AutomationProperties.GetName(b) == "Folder Assets"));
        await Delay(300);
        Require(Folder == "Assets", "Assets crumb did not navigate");
        steps.Add(new { clicked = "Assets", folder = Folder, crumbs = Crumbs() });
        var up = main.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Parent folder");
        Click(up); await Delay(300);
        Require(Folder == "", "Parent folder did not go up");
        steps.Add(new { clicked = "Parent folder", folder = Folder, parent_visible = up.IsVisible });
        Open("Assets"); await Delay(300);

        // List rows, then the thumbnail grid.
        var toggle = Find<ToggleButton>("ProjectViewToggle");
        var listRows = Browser.GetVisualDescendants().OfType<ListBoxItem>().Select(i => i.Bounds.Height).Distinct().ToArray();
        Capture(Find<Control>("ProjectPanel"), "project-list");
        Press(toggle);
        await Delay(600);
        var tiles = Browser.GetVisualDescendants().OfType<ListBoxItem>().ToList();
        var thumbs = tiles.Select(t => (name: (string)Get(t.DataContext!, "Name")!, image: t.GetVisualDescendants().OfType<Image>().FirstOrDefault()))
            .Where(t => t.name.EndsWith(".png")).ToList();
        Require(Browser.Classes.Contains("grid") && Browser.GetVisualDescendants().OfType<WrapPanel>().Any(), "grid mode did not use a wrap panel");
        Require(tiles.All(t => Math.Abs(t.Bounds.Width - 76) < .5 && Math.Abs(t.Bounds.Height - 76) < .5), "grid tiles are not 76px");
        Require(thumbs.Count >= 5 && thumbs.All(t => t.image?.Source is Bitmap), $"PNG thumbnails missing ({thumbs.Count(t => t.image?.Source != null)} of {thumbs.Count})");
        // Selecting a tile only recolours it.
        var first = tiles[0].Bounds;
        Browser.SelectedIndex = 1; await Delay(200);
        Require(tiles[0].Bounds == first, "selection moved the grid");
        Require(UserState().GetProperty("ProjectGrid"u8).GetBoolean(), "grid choice not saved per user");
        Capture(Find<Control>("ProjectPanel"), "project-grid");

        // Folder tree in a wide Project panel (Animation layout puts Project in the bottom tab group).
        var dock = Find<DockHost>("Dock");
        var preset = (DockNode)typeof(MainWindow).GetMethod("LayoutPreset", Any)!.Invoke(null, ["Animation"])!;
        dock.Apply(preset);
        dock.Activate("Project");
        await Delay(800);
        var pane = Find<Border>("FolderTreePane");
        Require(pane.IsVisible, $"folder tree hidden in a {Find<Control>("ProjectPanel").Bounds.Width}px Project panel");
        var folderTree = Find<TreeView>("FolderTree");
        var shared = Items(folderTree.Items).First(i => (string?)i.Tag == "Scripts/Shared");
        folderTree.SelectedItem = shared;
        await Delay(400);
        Require(Folder == "Scripts/Shared" && Crumbs().SequenceEqual(["KeyRoom", "Scripts", "Shared"]), $"tree selection opened {Folder}");
        Click(Find<StackPanel>("ProjectPath").Children.OfType<Button>().First(b => AutomationProperties.GetName(b) == "Folder Scripts"));
        await Delay(300);
        Require((string?)(folderTree.SelectedItem as TreeViewItem)?.Tag == "Scripts", "tree did not follow the path bar");
        Open("Scripts/Shared"); await Delay(300);
        ScreenCapture("project-tree");
        Capture(Find<Control>("ProjectPanel"), "project-tree-panel");
        steps.Add(new { tree = true, panel_width = Find<Control>("ProjectPanel").Bounds.Width, folder = Folder });
        dock.Apply((DockNode)typeof(MainWindow).GetMethod("LayoutPreset", Any)!.Invoke(null, ["Default"])!);
        await Delay(800);
        Require(!pane.IsVisible, "folder tree shown in the 176px Default Project panel");
        // Search still lists matches from every folder.
        var search = Find<TextBox>("AssetSearch");
        search.Text = "door t:sprite";
        await Delay(400);
        var hits = Browser.Items.Cast<object>().Select(e => (string)Get(e!, "RelativePath")!).ToArray();
        Require(hits.Contains("Assets/Door.png") && Find<TextBlock>("ProjectName").IsVisible && !Find<ScrollViewer>("ProjectPathScroll").IsVisible, "search results");
        steps.Add(new { search = search.Text, hits, caption = Find<TextBlock>("ProjectName").Text });
        search.Text = "";
        await Delay(300);
        return new { steps, list_row_heights = listRows, tiles = tiles.Count, thumbnails = thumbs.Count };
    }

    // ---- Inspector
    private static ComponentSection Section(string header) => Find<StackPanel>("Inspector").GetVisualDescendants().OfType<ComponentSection>()
        .First(s => s.IsEffectivelyVisible && (string?)s.Header == header);
    private static ToggleButton HeaderToggle(this ComponentSection s) => s.GetVisualDescendants().OfType<ToggleButton>().First(t => t.Name == "HeaderToggle");

    private static async Task<object> InspectorFolds()
    {
        var door = Object("Door");
        Call(main, "Select", door);
        await Delay(400);
        Require(!Find<Border>("InspectorActions").IsVisible, "the Inspector shows a header row for a single edit selection");
        Require(!main.GetVisualDescendants().OfType<TextBlock>().Any(t => t.IsEffectivelyVisible && t.Text == "Inspector" && t.GetVisualAncestors().Contains(Find<Border>("InspectorPanel"))),
            "Inspector title repeated under the tab");
        var sprite = Section("Sprite Renderer");
        Require(sprite.HeaderToggle().IsChecked == true, "Sprite Renderer starts folded");
        var headers = Find<StackPanel>("Inspector").GetVisualDescendants().OfType<ComponentSection>().Where(s => s.IsEffectivelyVisible).ToList();
        var icons = headers.Select(s => Math.Round(In(s.HeaderToggle().GetVisualDescendants().OfType<PathIcon>().ElementAt(1)).X, 1)).Distinct().ToArray();
        var gears = headers.Select(s => s.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.IsVisible && ToolTip.GetTip(b) as string == "Component settings"))
            .Where(b => b != null).Select(b => Math.Round(In(b!).Right, 1)).Distinct().ToArray();
        Require(icons.Length == 1 && gears.Length == 1, $"component headers out of line: icons {string.Join(',', icons)} gears {string.Join(',', gears)}");
        Press(sprite.HeaderToggle());
        await Delay(200);
        Require(UserState().GetProperty("Folds"u8).GetProperty("SpriteRendererComponent"u8).GetBoolean() == false, "fold state not saved per user");
        // Same component type on another object uses the remembered state.
        Call(main, "Select", Object("Player"));
        await Delay(400);
        bool shared = Section("Sprite Renderer").HeaderToggle().IsChecked == false;
        Require(shared, "Player's Sprite Renderer ignores the remembered fold state");
        // Multi-selection: the count sits in the slim action row.
        Call(main, "ShowSelection", (IReadOnlyList<SceneObject>)[Object("Wall"), Object("Player")]);
        await Delay(300);
        var caption = Find<TextBlock>("InspectorCaption").Text;
        Require(Find<Border>("InspectorActions").IsVisible && caption == "2 selected", $"multi-selection caption {caption}");
        Capture(Find<Border>("InspectorPanel"), "inspector-multi");
        Call(main, "Select", door);
        await Delay(400);
        // Long ComboBox values trim (Door's audio clip and animation clip pickers).
        var combos = Find<StackPanel>("Inspector").GetVisualDescendants().OfType<ComboBox>().Where(c => c.IsEffectivelyVisible && c.SelectedItem != null).ToList();
        var shownCombos = combos.Select(c =>
        {
            var text = c.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text == c.SelectedItem!.ToString());
            return new { value = c.SelectedItem!.ToString(), trims = text?.TextTrimming == TextTrimming.CharacterEllipsis, inside = text != null && In(text).Right <= In(c).Right + .5,
                box_template = c.SelectionBoxItemTemplate != null };
        }).ToList();
        Require(shownCombos.Count >= 2 && shownCombos.All(c => c.trims && c.inside), $"ComboBox values: {JsonSerializer.Serialize(shownCombos)}");
        Capture(Find<Border>("InspectorPanel"), "inspector-folded");
        return new { folded = "Sprite Renderer", shared_by_type = shared, header_icon_x = icons, gear_right = gears, multi_caption = caption, combos = shownCombos };
    }

    // ---- Console
    private static async Task<object> Console()
    {
        var log = main.GetType().GetMethod("WriteLog", Any)!;
        void Log(GameLogLevel level, string text) => log.Invoke(main, [new GameLogEntry(level, text, null, 0)]);
        for (int i = 0; i < 3; i++) Log(GameLogLevel.Info, "Panels probe: repeated info");
        Log(GameLogLevel.Warning, "Panels probe: warning");
        Log(GameLogLevel.Warning, "Panels probe: warning");
        Log(GameLogLevel.Error, "Panels probe: error\n   at Stack.Frame()\n   at Other.Frame()");
        Call(main, "ShowConsole");
        var dock = Find<DockHost>("Dock");
        await Until(() => dock.IsOpen("Console"), "Console");
        await Delay(600);
        var panel = dock.Panels.Single(p => DockHost.GetPanel(p) == "Console");
        var list = panel.GetVisualDescendants().OfType<ListBox>().Single();
        ToggleButton Toggle(string name) => panel.GetVisualDescendants().OfType<ToggleButton>().Single(t => AutomationProperties.GetName(t) == name);
        var info = Toggle("Show log messages"); var warn = Toggle("Show warning messages"); var error = Toggle("Show error messages");
        var collapse = Toggle("Collapse identical messages");
        var search = panel.GetVisualDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "Search messages");
        string Count(ToggleButton t) => t.GetVisualDescendants().OfType<TextBlock>().Last().Text!;
        var rows = ((IEnumerable)Get(main, "consoleRows")!).Cast<object>().Select(r => (GameLogLevel)Get(r, "Level")!).ToList();
        int infos = rows.Count(l => l == GameLogLevel.Info), warnings = rows.Count(l => l == GameLogLevel.Warning), errors = rows.Count(l => l == GameLogLevel.Error);
        async Task<int> Shown() { await Delay(400); return list.ItemCount; }
        Require(Count(info) == infos.ToString() && Count(warn) == warnings.ToString() && Count(error) == errors.ToString(),
            $"counts {Count(info)}/{Count(warn)}/{Count(error)} vs {infos}/{warnings}/{errors}");
        int all = await Shown();
        Require(all == rows.Count, $"{all} rows shown of {rows.Count}");
        Press(info);
        int withoutInfo = await Shown();
        Require(withoutInfo == warnings + errors, $"Info filter: {withoutInfo}");
        Press(collapse);
        int collapsed = await Shown();
        Require(collapsed == 2, $"collapsed warnings+error: {collapsed}");
        var badge = list.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("consoleCount"))?.Child as TextBlock;
        Require(badge?.Text == "2", "collapsed warning has no ×2 badge");
        Press(info);
        int collapsedAll = await Shown();
        Press(warn);
        search.Text = "Other.Frame";
        int searched = await Shown();
        Require(searched == 1, $"search: {searched}");
        list.SelectedIndex = 0;
        await Delay(300);
        var detail = panel.GetVisualDescendants().OfType<SelectableTextBlock>().Single();
        Require(detail.IsEffectivelyVisible && detail.Text!.Contains("at Stack.Frame()") && detail.Text.Contains("at Other.Frame()"), $"detail: {detail.Text}");
        Capture(panel, "console");
        ScreenCapture("console-1280");
        search.Text = "";
        Press(warn); Press(collapse);
        var bar = panel.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("panelHeader"));
        var centres = new Control[] { info, warn, error, collapse, search }.Select(c => Math.Round(Mid(c), 1)).Distinct().ToArray();
        Require(centres.Length == 1 && Math.Abs(bar.Bounds.Height - 28) < .5, $"Console toolbar centres {string.Join(',', centres)}");
        var clear = panel.GetVisualDescendants().OfType<Button>().First(b => b.Content as string == "Clear");
        Click(clear);
        await Delay(400);
        Require(Count(info) == "0" && Count(error) == "0" && list.ItemCount == 0, "Clear left messages");
        dock.Close("Console");
        return new { counts = new { infos, warnings, errors }, all, without_info = withoutInfo, collapsed, collapsed_all = collapsedAll, searched, detail = detail.Text, toolbar_centre = centres };
    }

    // ---- alignment
    private static object Toolbar(string name, params Control[] controls)
    {
        var centres = controls.Select(c => Math.Round(Mid(c), 1)).Distinct().ToArray();
        Require(centres.Length == 1, $"{name} toolbar controls are off one centre line: {string.Join(',', centres)}");
        return new { name, centre = centres[0] };
    }

    private static object Baseline(string label)
    {
        var name = Find<TextBox>("ObjectName");
        var nameLabel = ((Grid)name.Parent!).Children.OfType<TextBlock>().First();
        var position = Find<TextBlock>("PositionRowLabel");
        var posX = Find<NumericUpDown>("PosX");
        var tag = Find<TextBox>("ObjectTag");
        var rows = new List<(TextBlock Label, Control Input)> { (nameLabel, name), (position, (Control)posX.Parent!) };
        if (tag.IsEffectivelyVisible) rows.Add((((Grid)tag.Parent!).Children.OfType<TextBlock>().First(), tag));
        var labels = rows.Select(r => In(r.Label).X).ToList();
        var inputs = rows.Select(r => In(r.Input).X).ToList();
        double drift = Math.Max(labels.Max() - labels.Min(), inputs.Max() - inputs.Min());
        double column = inputs[0] - labels[0];
        double vertical = Math.Max(Math.Abs(Mid(nameLabel) - Mid(name)), Math.Abs(Mid(position) - Mid(posX)));
        Require(drift < 1 && Math.Abs(column - 60) < 1 && vertical < 1, $"{label}: drift {drift} column {column} vertical {vertical}");
        return new { label, window = main.Bounds.Width, label_x = Math.Round(labels[0], 1), input_x = Math.Round(inputs[0], 1), column = Math.Round(column, 1), drift, vertical };
    }

    private static async Task<object> Alignment()
    {
        var door = Object("Door");
        Call(main, "Select", door);
        var rows = new List<object>();
        var toolbars = new List<object>();
        foreach (var (w, h) in new[] { (1280, 800), (1600, 900), (1920, 1080) })
        {
            await Resize(w, h);
            rows.Add(Baseline($"window {w}"));
            toolbars.Add(Toolbar($"Hierarchy {w}", Find<TextBox>("HierarchySearch"), Find<Button>("AddButton")));
            toolbars.Add(Toolbar($"Project {w}", Find<TextBox>("AssetSearch"), Find<ToggleButton>("ProjectViewToggle"), Find<Button>("ProjectActions")));
            var crumbs = Find<StackPanel>("ProjectPath").Children.OfType<Control>().Append(Find<Button>("ParentFolderButton")).Where(c => c.IsVisible).ToArray();
            toolbars.Add(Toolbar($"Project path {w}", crumbs));
            // The path bar never cuts a crumb: whole leading folders give way to "…" and the current folder stays visible.
            var bar = Find<ScrollViewer>("ProjectPathScroll");
            var shown = Find<StackPanel>("ProjectPath").Children.Where(c => c.IsVisible).ToList();
            Require(shown.Count > 0 && In(shown[^1]).Right <= In(bar).Right + .5 && shown[^1] is Button { Classes: var last } && last.Contains("current"),
                $"Project path at {w}: {shown.Count} parts, right {In(shown[^1]).Right} of {In(bar).Right}");
            ScreenCapture($"main-{w}");
        }
        await Resize(1280, 800);
        // Hovering a row reveals the eye and lock in reserved columns: nothing in the row moves.
        var row = Row(door);
        var name = row.Children.OfType<TextBlock>().First();
        var before = In(name);
        ((IPseudoClasses)row.Classes).Set(":pointerover", true);
        await Delay(200);
        var eye = row.Children.OfType<ToggleButton>().First();
        bool shownOnHover = eye.Opacity == 1;
        Require(shownOnHover && In(name) == before, $"hover: eye opacity {eye.Opacity}, name {before} -> {In(name)}");
        Capture(Tree, "hierarchy-hover");
        ((IPseudoClasses)row.Classes).Set(":pointerover", false);

        // Long object name: the row trims inside the tree with a tooltip; the eye and lock stay inside the panel.
        double inspectorWidth = Find<Border>("InspectorPanel").Bounds.Width;
        door.Name = LongName;
        Call(main, "FinishObjectChange", door, null);
        Call(main, "Select", door);
        await Delay(600);
        row = Row(door);
        name = row.Children.OfType<TextBlock>().First();
        var toggles = row.Children.OfType<ToggleButton>().ToList();
        var natural = new TextBlock { Text = LongName, FontSize = name.FontSize };
        natural.Measure(Size.Infinity);
        double treeRight = In(Tree).Right;
        bool trimmed = name.TextTrimming == TextTrimming.CharacterEllipsis && name.Bounds.Width < natural.DesiredSize.Width && ToolTip.GetTip(name) as string == LongName;
        bool inside = toggles.All(t => In(t).Right <= treeRight + .5) && In(name).Right <= In(toggles[0]).X;
        Require(trimmed && inside, $"long name: trimmed {trimmed} inside {inside}");
        rows.Add(Baseline("long name 1280"));
        Require(Math.Abs(Find<Border>("InspectorPanel").Bounds.Width - inspectorWidth) < .5, "long name changed the Inspector width");
        ScreenCapture("long-name-1280");
        return new { rows, toolbars, hover = shownOnHover, long_name = new { shown = Math.Round(name.Bounds.Width, 1), natural = Math.Round(natural.DesiredSize.Width, 1), trimmed, inside } };
    }
}
