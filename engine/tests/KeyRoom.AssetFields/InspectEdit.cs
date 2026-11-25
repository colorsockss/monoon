using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Engine.Core;
using Engine.Editor;

// Phase "inspect" (roadmap 13.5, 13.6, 13.10, 13.16) on the prepared KeyRoom copy. Saves the Showcase scene twice:
// inspect-base.scene (Door rotated, pivot untouched) and the scene itself (Door pivot 0.5/0.5 from the PNG import
// pivot, Door.png packed into a new atlas), so Release builds of both show what the asset Inspector changed.
public static partial class StartupHook
{
    private const string DoorPng = "Assets/Showcase/Door.png";
    private static StackPanel Multi => Find<StackPanel>("MultiInspector");
    private static StackPanel AssetPanel => Find<StackPanel>("AssetInspector");
    private static T Named<T>(Visual root, string name) where T : Control =>
        Visible(root).OfType<T>().FirstOrDefault(c => AutomationProperties.GetName(c) == name) ?? throw new InvalidDataException($"no {typeof(T).Name} '{name}'");
    private static Control SectionNamed(Visual root, string header) =>
        Visible(root).First(c => c.GetType().Name == "ComponentSection" && (string?)Get(c, "Header") == header);
    private static SceneObject[] Walls => Scene.Objects.Where(o => o.Name.StartsWith("Wall ", StringComparison.Ordinal)).OrderBy(o => o.Name).ToArray();
    // "Owner · component.field" for each prefab property override of an instance (SceneDocument.PropertyOverrides is internal).
    private static string[] OverrideList(SceneObject o) =>
        ((System.Collections.IEnumerable)typeof(SceneDocument).GetMethod("PropertyOverrides", Any)!.Invoke(Scene, [o])!).Cast<object>().Select(c => c.ToString()!).ToArray();
    private static async Task Show(IEnumerable<SceneObject> objects) { Call(main, "ShowSelection", objects.ToList()); await Delay(500); }

    // The x of every value-column control: one left edge when rows share the label column.
    private static double[] ValueEdges(Visual root) => Visible(root)
        .Where(c => c.Parent is Grid g && Grid.GetColumn(c) == 1 && g.ColumnDefinitions.Count > 1 && g.ColumnDefinitions[0].Width.Value == 56)
        .Select(c => Math.Round(In(c).X, 1)).Distinct().ToArray();

    // Presses a label, moves it dx pixels in steps and releases: the Inspector's own scrub handlers run.
    private static async Task ScrubLabel(TextBlock label, double dx)
    {
        label.BringIntoView();
        await Delay(200);
        var start = In(label).Center;
        label.RaiseEvent(new PointerPressedEventArgs(label, Mouse, main, start, time++, Props(PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        for (int i = 1; i <= 6; i++)
        {
            label.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, label, Mouse, main, start + new Point(dx * i / 6, 0), time++,
                Props(PointerUpdateKind.Other), KeyModifiers.None));
            await Delay(40);
        }
        label.RaiseEvent(new PointerReleasedEventArgs(label, Mouse, main, start + new Point(dx, 0), time++, Props(PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
        await Delay(400);
    }

    // A click on a Project row: press and release on the same point (no drag), the Project panel's handlers.
    private static async Task ClickProject(string path)
    {
        var dock = Find<DockHost>("Dock");
        dock.Activate("Project");
        Call(main, "BrowseFolder", Path.GetDirectoryName(path)!.Replace('\\', '/'), path);
        await Delay(400);
        var browser = Find<ListBox>("ProjectBrowser");
        var entry = browser.Items.Cast<object>().First(e => (string)Get(e, "RelativePath")! == path);
        browser.ScrollIntoView(entry);
        await Delay(200);
        var item = (Control)browser.ContainerFromItem(entry)!;
        var at = In(item).Center;
        item.RaiseEvent(new PointerPressedEventArgs(item, Mouse, main, at, time++, Props(PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        browser.RaiseEvent(new PointerReleasedEventArgs(browser, Mouse, main, at, time++, Props(PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
        await Delay(500);
        Find<DockHost>("Dock").Activate("Inspector");
        await Delay(200);
    }

    private static string AssetRow(string label)
    {
        var row = Visible(AssetPanel).OfType<Grid>().First(g => g.Children.OfType<TextBlock>().FirstOrDefault(t => Grid.GetColumn(t) == 0)?.Text == label);
        return row.Children.OfType<TextBlock>().First(t => Grid.GetColumn(t) == 1).Text ?? "";
    }

    private static async Task Inspect()
    {
        await MultiEdit();
        await OverrideRows();
        await Scrubbing();
        await AssetInspector();
    }

    // ---- 13.5: ten WallWorkshop walls
    private static async Task MultiEdit()
    {
        await (Task)Call(main, "SwitchScene", "WallWorkshop")!;
        await Until(() => Walls.Length == 10, "WallWorkshop open", 60);
        // Fixture: QaWall with a different Strength on most walls, saved so the edits start from a clean scene.
        for (int i = 0; i < 10; i++)
            Walls[i].AddComponent(new ScriptComponent { Script = "QaWall",
                Values = i % 3 == 0 ? [] : new() { ["Strength"] = JsonSerializer.SerializeToElement(i + 1) } });
        Call(main, "MarkDirty", new object?[] { null });
        Require(await Save(), "save the wall fixture");
        await Show(Walls);
        Require(Multi.IsVisible, "the multi-selection Inspector is shown");
        var before = Text;
        var overrides = Walls.Select(OverrideList).ToArray();
        int at = Position;
        var edges = new Dictionary<int, double[]>();
        foreach (var width in new[] { 1280, 1600, 1920 })
        {
            await Resize(width, 900);
            edges[width] = ValueEdges(Multi);
            Require(edges[width].Length == 1, $"{width}px: multi-selection value column has one left edge ({string.Join(",", edges[width])})");
            if (width == 1280) Capture(InspectorPanel, "i5-multi-mixed");
        }
        await Resize(1280, 800);
        var shownWidth = Named<NumericUpDown>(Multi, "Width");
        var strength = Named<NumericUpDown>(Multi, "Strength");
        var trigger = Named<CheckBox>(Multi, "Is Trigger");
        Require(shownWidth.Value == 76 && strength.Value == null && strength.Watermark == "—" && Named<NumericUpDown>(Multi, "Position X").Value == null,
            $"shared width 76, mixed Strength and X show a dash ({shownWidth.Value}, {strength.Value}, {strength.Watermark})");
        bool mixedTrigger = Walls.Select(w => w.GetComponent<BoxCollider2DComponent>()!.IsTrigger).Distinct().Count() > 1;
        Require(!mixedTrigger || trigger.IsChecked == null, "a mixed Is Trigger shows the indeterminate check");

        shownWidth.Value = 80;
        await Delay(500);
        Require(Walls.All(w => w.Width == 80) && Position == at + 1, $"width 80 on all ten, one record ({Position - at})");
        Named<CheckBox>(Multi, "Is Trigger").IsChecked = true;
        await Delay(500);
        Require(Walls.All(w => w.GetComponent<BoxCollider2DComponent>()!.IsTrigger) && Position == at + 2, "Is Trigger on all ten, one record");
        Named<NumericUpDown>(Multi, "Strength").Value = 7;
        await Delay(500);
        Require(Walls.All(w => w.GetComponent<ScriptComponent>()!.Values["Strength"].GetInt32() == 7) && Position == at + 3, "Strength 7 on all ten, one record");
        Require(Walls.All(w => OverrideList(w).Any(s => s.EndsWith("· width")) && OverrideList(w).Any(s => s.EndsWith(".isTrigger"))),
            "the width and Is Trigger edits are prefab overrides on every wall");
        Capture(InspectorPanel, "i5-multi-edited");
        var after = Text;
        await Undo(); await Undo(); await Undo();
        Require(Text == before && Position == at, "three Undo steps restore the walls");
        Require(Walls.Select(OverrideList).Zip(overrides).All(p => p.First.SequenceEqual(p.Second)),
            "every wall's override list is restored");
        Require(Multi.IsVisible && Named<NumericUpDown>(Multi, "Width").Value == 76, "the ten walls stay selected with width 76");
        await Redo(); await Redo(); await Redo();
        Require(Text == after, "Redo gives the edited walls back");
        await Undo(); await Undo(); await Undo();
        Step("multi_edit", new { walls = 10, records = 3, undo = "each one step; scene text and overrides restored", edges });
    }

    // ---- 13.6: the showcase's two Pot.prefab instances. "Small pot" overrides name, size and sprite; "Pot" has none.
    private static async Task OverrideRows()
    {
        Require(await Save(), "save WallWorkshop");
        await (Task)Call(main, "SwitchScene", "Showcase")!;
        await Until(() => Scene.Objects.Any(o => o.Name == "Small pot"), "Showcase open", 60);
        Border? Bar(string rowName)
        {
            var row = (Panel)main.FindControl<Control>(rowName)!;
            return row.Children.OfType<Border>().FirstOrDefault(b => b.Classes.Contains("overrideBar") && b.IsVisible);
        }
        string LabelWeight(string rowName) => ((Panel)main.FindControl<Control>(rowName)!).Children.OfType<TextBlock>().First().FontWeight.ToString();
        var rows = new Dictionary<string, object>();
        foreach (var width in new[] { 1280, 1600, 1920 })
        {
            await Resize(width, 900);
            await Select(Scene.Objects.Single(o => o.Name == "Pot" && o.Prefab != null));
            var plain = Layout(InspectorStack());
            Require(Bar("SizeRow") == null && LabelWeight("SizeRow") == "Normal", "the plain pot has no Size override mark");
            if (width == 1280) Capture(InspectorPanel, "i6-pot-plain");
            await Select(Scene.Objects.Single(o => o.Name == "Small pot"));
            var marked = Layout(InspectorStack());
            var bar = Bar("SizeRow");
            Require(bar != null && LabelWeight("SizeRow") == "Bold", "the Small pot's Size row is marked");
            var barBox = In(bar!);
            var panelLeft = In(InspectorStack()).X;
            Require(barBox.Width == 2 && barBox.Right <= panelLeft, $"the bar sits in the panel padding ({barBox}, content x {panelLeft})");
            Require(plain == marked, $"{width}px: the override marks moved no value column");
            if (width == 1280) Capture(InspectorPanel, "i6-pot-small");
            rows[$"{width}"] = new { bar = $"{barBox.X:0.#},{barBox.Width}", content_left = panelLeft, value_columns_same = true,
                sprite_dot = (bool)Get(SectionNamed(InspectorStack(), "Sprite Renderer"), "IsOverridden")! };
        }
        await Resize(1280, 800);
        var transformDot = (bool)Get(Find<Control>("TransformSection"), "IsOverridden")!;
        Require(transformDot, "the Transform header has the override dot");
        // Row Revert: the Size row's overrides (width and height) only; the name override stays.
        string before = Text;
        int at = Position;
        var menu = ((Control)main.FindControl<Control>("SizeRow")!).ContextMenu ?? throw new InvalidDataException("no Size row menu");
        var revert = menu.Items.OfType<MenuItem>().First(i => (string?)i.Header == "Revert");
        revert.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        await Until(() => Scene.Objects.Single(o => o.Name == "Small pot").Width == 34, "Size reverted", 20);
        await Delay(400);
        var pot = Scene.Objects.Single(o => o.Name == "Small pot");
        Require(pot.Height == 40 && Position == at + 1 && Bar("SizeRow") == null && OverrideList(pot).Any(s => s.EndsWith("· name")),
            "Revert took back width and height only, one record, the bar is gone");
        Capture(InspectorPanel, "i6-pot-reverted");
        await Undo();
        Require(Text == before && Scene.Objects.Single(o => o.Name == "Small pot").Width == 24, "one Undo restores the Size override");
        // Apply to Source of the name row writes Pot.prefab; one Undo restores the file and the scene.
        await Select(Scene.Objects.Single(o => o.Name == "Small pot"));
        var prefab = Path.Combine(Root, "Assets/Showcase/Pot.prefab");
        var prefabBytes = File.ReadAllBytes(prefab);
        var nameMenu = ((Control)Find<TextBox>("ObjectName").Parent!).ContextMenu ?? throw new InvalidDataException("no name row menu");
        nameMenu.Items.OfType<MenuItem>().First(i => (string?)i.Header == "Apply to Source").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        await Until(() => !File.ReadAllBytes(prefab).SequenceEqual(prefabBytes), "Pot.prefab written", 20);
        await Delay(500);
        Require(File.ReadAllText(prefab).Contains("Small pot") && Position == at + 1, "Apply wrote the name to the prefab, one record");
        await Undo();
        Require(File.ReadAllBytes(prefab).SequenceEqual(prefabBytes) && Text == before, "one Undo restores Pot.prefab and the scene");
        Step("override_rows", new { rows, transform_dot = transformDot, revert = "Size row: width+height, one step", apply = "name → Pot.prefab, one step" });
    }

    // ---- 13.10: MoveSpeed label 240 → 300, Door X axis label
    private static async Task Scrubbing()
    {
        await Select(Object("Player"));
        var section = SectionNamed(InspectorStack(), "PlayerMovement");
        var label = Visible(section).OfType<TextBlock>().First(t => t.Text == "MoveSpeed" && t.Classes.Contains("fieldLabel"));
        ScriptComponent Movement() => Object("Player").Components.OfType<ScriptComponent>().Single(c => c.Script == "PlayerMovement");
        string before = Text;
        int at = Position;
        await ScrubLabel(label, 60);
        var slider = Visible(SectionNamed(InspectorStack(), "PlayerMovement")).OfType<Slider>().Single();
        Require(Movement().Values["MoveSpeed"].GetSingle() == 300 && slider.Value == 300 && Position == at + 1,
            $"MoveSpeed label drag 240→300, slider follows, one record ({Movement().Values["MoveSpeed"]}, {slider.Value}, {Position - at})");
        var moveSpeed = await OneStep("MoveSpeed scrub", before, () => Movement().Values["MoveSpeed"].GetSingle() == 300);
        await Undo();
        // A drag past [Range(60, 480)] stops at 480.
        await Select(Object("Player"));
        label = Visible(SectionNamed(InspectorStack(), "PlayerMovement")).OfType<TextBlock>().First(t => t.Text == "MoveSpeed" && t.Classes.Contains("fieldLabel"));
        await ScrubLabel(label, 400);
        Require(Movement().Values["MoveSpeed"].GetSingle() == 480, "the scrub stops at the Range maximum");
        await Undo();
        Require(Text == before, "undo the clamped scrub");
        // Transform: the Door's Position X axis label, −15 px = −15.
        await Select(Object("Door"));
        float x = Object("Door").X;
        string beforeX = Text;
        var axis = ((Visual)Find<NumericUpDown>("PosX").Parent!).GetVisualChildren().OfType<TextBlock>().First();
        await ScrubLabel(axis, -15);
        var doorX = await OneStep("Door X scrub", beforeX, () => Object("Door").X == x - 15);
        await Undo();
        Step("scrub", new { move_speed = moveSpeed, clamp = 480, door_x = doorX });
    }

    // ---- 13.16: Project clicks
    private static async Task AssetInspector()
    {
        await ClickProject(DoorPng);
        Require(AssetPanel.IsVisible && !InspectorStack().IsVisible, "a Project click shows the asset Inspector");
        var size = SpriteRegion.ReadPngSize(Path.Combine(Root, DoorPng));
        Require(AssetRow("Size") == $"{size.Width} × {size.Height} px", "Door.png size " + AssetRow("Size"));
        Require(Named<NumericUpDown>(AssetPanel, "Import pivot X").Value == 0 && Named<NumericUpDown>(AssetPanel, "Import pivot Y").Value == 0, "pivot 0, 0");
        var edges = new Dictionary<int, double[]>();
        foreach (var width in new[] { 1280, 1600, 1920 })
        {
            await Resize(width, 900);
            edges[width] = ValueEdges(AssetPanel);
            Require(edges[width].Length == 1, $"{width}px: asset Inspector value column has one left edge ({string.Join(",", edges[width])})");
        }
        await Resize(1280, 800);
        Capture(InspectorPanel, "i16-door-png");

        // Fixture: the Door turned 20° so its pivot shows in the build; saved as the baseline scene.
        var door = Object("Door");
        door.Rotation = 20;
        Call(main, "MarkDirty", new object?[] { null });
        Require(await Save(), "save the rotated Door");
        File.Copy(Path.Combine(Root, "Scenes/Showcase.scene"), Path.Combine(Output, "inspect-base.scene"), true);
        await ClickProject(DoorPng);
        string before = Text;
        int at = Position;
        var meta = Path.Combine(Root, DoorPng + ".meta");
        Named<NumericUpDown>(AssetPanel, "Import pivot X").Value = 0.5m;
        await Delay(600);
        Require(Object("Door").PivotX == .5f && File.ReadAllText(meta).Contains("pivotX: 0.5") && Position == at + 1, "pivot X 0.5: meta and the Door, one record");
        Named<NumericUpDown>(AssetPanel, "Import pivot Y").Value = 0.5m;
        await Delay(600);
        Require(Object("Door").PivotY == .5f && Position == at + 2, "pivot Y 0.5");
        string afterPivot = Text;
        await Undo();
        Require(Object("Door").PivotY == 0 && !File.ReadAllText(meta).Contains("pivotY"), "one Undo restores pivot Y in the scene and the meta");
        await Redo();
        Require(Text == afterPivot && File.ReadAllText(meta).Contains("pivotY: 0.5"), "Redo");
        await ClickProject(DoorPng); // Undo/Redo reselect the Door object
        Require(Named<NumericUpDown>(AssetPanel, "Import pivot Y").Value == 0.5m, "the asset Inspector reads the new pivot");
        Capture(InspectorPanel, "i16-door-pivot");

        // Atlas group: a new atlas with Door.png; Undo removes it, Redo packs it again.
        await ClickProject(DoorPng);
        var atlas = Path.Combine(Root, "Assets/Showcase/Door Atlas.atlas");
        Named<ComboBox>(AssetPanel, "Atlas group").SelectedItem = "New atlas";
        await Until(() => File.Exists(atlas), "Door Atlas.atlas", 30);
        await Delay(600);
        Require(SpriteAtlas.Load(atlas).Entries.Single().Sprite == Id(DoorPng) && Position == at + 3, "Door.png packed, one record");
        Require((string?)Named<ComboBox>(AssetPanel, "Atlas group").SelectedItem == "Assets/Showcase/Door Atlas.atlas", "the field shows the atlas");
        await Undo();
        Require(!File.Exists(atlas), "Undo removes the new atlas");
        await Redo();
        Require(File.Exists(atlas), "Redo packs it again");
        await ClickProject(DoorPng);
        Capture(InspectorPanel, "i16-door-atlas");
        Require(await Save(), "save the pivot and atlas");

        // Other kinds: summary rows and the button for their panel.
        var kinds = new Dictionary<string, object>();
        foreach (var (path, row, button) in new[] { ("Assets/Showcase/DoorOpen.clip", "Frames", "Open Clip Editor"), ("Assets/KeyRoomRules.data", "Fields", "Edit Data…"),
                     ("Assets/Showcase/Pot.prefab", "Instances", "Open Prefab"), ("Assets/QaSparks.particles", "Particles", "Open Particle Panel"),
                     ("Assets/" + "QaLongNameDoorMaskForInspectorTrimmingChecks0123456789.png", "Size", "") })
        {
            await ClickProject(path);
            Require(AssetPanel.IsVisible, path + " shows");
            var value = AssetRow(row);
            bool hasButton = button.Length == 0 || Visible(AssetPanel).OfType<Button>().Any(b => (b.Content as string) == button);
            Require(value.Length > 0 && hasButton, $"{path}: {row} '{value}', button {button}");
            Require(ValueEdges(AssetPanel).Length == 1, path + ": one value column edge");
            kinds[path] = new { row, value, button };
        }
        Capture(InspectorPanel, "i16-long-png");
        Require(AssetRow("Size") == "8 × 8 px", "long-named PNG size");
        await ClickProject("Assets/Showcase/DoorOpen.clip");
        Visible(AssetPanel).OfType<Button>().First(b => (b.Content as string) == "Open Clip Editor").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(() => Get(main, "clipEditor") != null, "the clip editor opened", 20);
        Find<DockHost>("Dock").Activate("Inspector");
        await Select(Object("Door"));
        Require(InspectorStack().IsVisible && !AssetPanel.IsVisible, "selecting an object takes the Inspector back");
        Step("asset_inspector", new { door_png = $"{size.Width}x{size.Height}", pivot = "0.5, 0.5 (2 records, Undo/Redo)", atlas = "Assets/Showcase/Door Atlas.atlas", edges, kinds });
    }
}
