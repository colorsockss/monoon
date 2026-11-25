using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Brutal.SdlApi;
using Engine.Core;
using Engine.Desktop;
using Engine.Editor;

// Loaded into the packaged editor with DOTNET_STARTUP_HOOKS (roadmap 13.7, 13.9, 13.15, 13.17). MONOON_AF_PHASE selects one launch:
// edit (KeyRoom showcase: colour picks, object-field drags, refusals, Finder import, Particle panel texture, arrays, multi-selection, Ping,
// alignment; saves), trace (Scene Play replay captures of the saved choice), field (CollisionLab Field10000 picker search) and
// curves (ParticleLab workshop Flame: Inspector curve and gradient rows against the runtime table; saves the asset).
public static partial class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_AF_OUTPUT")!;
    private static readonly string Phase = Environment.GetEnvironmentVariable("MONOON_AF_PHASE")!;
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Dictionary<string, object?> Result = [];
    private static readonly Avalonia.Input.Pointer Mouse = new(Avalonia.Input.Pointer.GetNextFreeId(), PointerType.Mouse, true);
    private static Window main = null!;
    private static SceneViewport view = null!;

    public static void Initialize() => Start(Path.Combine(Output, $"{Phase}-error.txt"), async life =>
    {
        main = life.MainWindow!;
        view = main.FindControl<SceneViewport>("Viewport")!;
        await Until(() => main.IsVisible && Find<ListBox>("Hierarchy").Items.Count > 1, "project open", 120);
        main.Width = 1280; main.Height = 800;
        await Delay(800);
        await (Phase switch { "edit" => Edit(), "trace" => Trace(), "field" => Field(), "curves" => Curves(), "inspect" => Inspect(), _ => throw new ArgumentException(Phase) });
        File.WriteAllText(Path.Combine(Output, $"{Phase}.json"), JsonSerializer.Serialize(Result, new JsonSerializerOptions { WriteIndented = true }));
    });

    // ---- helpers
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static T Find<T>(string name) where T : Control => main.FindControl<T>(name) ?? throw new InvalidDataException($"{name} not found");
    private static object? Get(object target, string name) =>
        target.GetType().GetProperty(name, Any) is { } p ? p.GetValue(target) : target.GetType().GetField(name, Any)?.GetValue(target);
    private static object? Call(object target, string method, params object?[] args) => target.GetType().GetMethod(method, Any)!.Invoke(target, args);
    private static SceneDocument Scene => (SceneDocument)Get(main, "Scene")!;
    private static string Root => Scene.ProjectDirectory!;
    private static ProjectAssets Assets => ProjectAssets.Scan(Root);
    private static Guid Id(string path) => Assets.IdFor(path) ?? throw new InvalidDataException("no asset " + path);
    private static SceneObject Object(string name) => Scene.Objects.First(o => o.Name == name);
    private static async Task Select(SceneObject? obj) { Call(main, "Select", obj); await Delay(400); }
    private static Rect In(Visual c) => new(c.TranslatePoint(default, main)!.Value, c.Bounds.Size);
    private static void Step(string name, object? value)
    {
        Result[name] = value;
        File.AppendAllText(Path.Combine(Output, $"{Phase}.log"), $"{name}: {JsonSerializer.Serialize(value)}\n");
    }
    private static string Text => Scene.CaptureObjects();
    private static int Position => (int)Get(Get(main, "history")!, "position")!;
    private static async Task<bool> Save() => await (Task<bool>)Call(main, "Save")!;
    private static async Task Undo() { await (Task)Call(main, "RestoreEdit", false)!; await Delay(300); }
    private static async Task Redo() { await (Task)Call(main, "RestoreEdit", true)!; await Delay(300); }
    private static async Task Resize(int width, int height) { main.Width = width; main.Height = height; await Delay(900); }
    private static void Capture(Visual area, string name)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(area.Bounds.Width), (int)Math.Ceiling(area.Bounds.Height)));
        bitmap.Render(area);
        bitmap.Save(Path.Combine(Output, name + ".png"));
    }
    private static Border InspectorPanel => Find<Border>("InspectorPanel");
    private static IEnumerable<Control> Visible(Visual root) => root.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible);
    private static Border FieldIn(Visual root, string name) => Visible(root).OfType<Border>()
        .FirstOrDefault(b => b.GetType().Name == "ReferenceField" && AutomationProperties.GetName(b) == name) ?? throw new InvalidDataException($"no {name} object field");
    private static string Shown(Border field) => Get(field, "Shown")!.ToString()!;
    private static Guid ShownId(Border field) => (Guid)Get(Get(field, "Shown")!, "Id")!;
    private static PointerPointProperties Props(PointerUpdateKind kind) =>
        new(kind == PointerUpdateKind.LeftButtonReleased ? RawInputModifiers.None : RawInputModifiers.LeftMouseButton, kind);
    private static ulong time = 1;

    // Every value-column control of the Inspector's label rows, with its bounds: a drag highlight must not move any of them.
    private static string Layout(Visual root) => string.Join(";", Visible(root)
        .Where(c => c.Parent is Grid g && Grid.GetColumn(c) == 1 && g.ColumnDefinitions.Count > 1 && g.ColumnDefinitions[0].Width.Value == 56)
        .Select(c => { var r = In(c); return $"{r.X:0.#},{r.Y:0.#},{r.Width:0.#},{r.Height:0.#}"; }));

    // Presses a Project row, moves over the target and (optionally) releases there: the Project panel's own pointer handlers run.
    private static async Task<object> DragProject(string[] paths, Visual target, string capture, bool release = true)
    {
        var dock = Find<DockHost>("Dock");
        dock.Activate("Project");
        Call(main, "BrowseFolder", Path.GetDirectoryName(paths[0])!.Replace('\\', '/'), paths[^1]);
        await Delay(400);
        var browser = Find<ListBox>("ProjectBrowser");
        var entries = browser.Items.Cast<object>().Where(e => paths.Contains((string)Get(e, "RelativePath")!)).ToArray();
        Require(entries.Length == paths.Length, "Project rows for " + string.Join(", ", paths));
        browser.SelectedItems!.Clear();
        foreach (var e in entries) browser.SelectedItems.Add(e);
        var pressed = entries.Last();
        browser.ScrollIntoView(pressed);
        await Delay(200);
        ((Control)target).BringIntoView();
        await Delay(300);
        var item = (Control)browser.ContainerFromItem(pressed)!;
        var start = In(item).Center;
        var to = In(target).Center;
        var before = Layout(InspectorStack());
        item.RaiseEvent(new PointerPressedEventArgs(item, Mouse, main, start, time++, Props(PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        for (int i = 1; i <= 8; i++)
        {
            browser.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, browser, Mouse, main, start + (to - start) * (i / 8.0), time++,
                Props(PointerUpdateKind.Other), KeyModifiers.None));
            await Delay(25);
        }
        await Delay(150);
        var classes = ((Control)target).Classes.Where(c => c is "referenceDrop" or "referenceReject").ToArray();
        bool still = Layout(InspectorStack()) == before;
        Capture(main, capture);
        if (release)
            browser.RaiseEvent(new PointerReleasedEventArgs(browser, Mouse, main, to, time++, Props(PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
        else
            browser.RaiseEvent(new PointerReleasedEventArgs(browser, Mouse, main, start, time++, Props(PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
        await Delay(500);
        Require(still, $"the drag highlight on {AutomationProperties.GetName((Control)target)} moved Inspector rows");
        return new { paths, highlight = classes, rows_unmoved = still };
    }

    private static StackPanel InspectorStack() => Find<StackPanel>("Inspector");

    // A Finder drop: the same DragDrop events the platform raises, with a file outside the project.
    private static async Task FinderDrop(Control target, string file)
    {
        var item = await main.StorageProvider.TryGetFileFromPathAsync(new Uri(file)) ?? throw new InvalidDataException("no storage item " + file);
        var data = new DataTransfer();
        data.Add(DataTransferItem.CreateFile(item));
        var ctor = typeof(DragEventArgs).GetConstructors().First(c => c.GetParameters() is { Length: 5 } p && p[1].ParameterType.IsInstanceOfType(data));
        foreach (var routed in new[] { DragDrop.DragOverEvent, DragDrop.DropEvent })
        {
            var args = (DragEventArgs)ctor.Invoke([routed, data, target, new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), KeyModifiers.None]);
            target.RaiseEvent(args);
            await Delay(200);
        }
        await Until(() => !(bool)Get(main, "busy")!, "Finder import", 30);
        await Delay(500);
    }

    // Undo once restores before, Redo restores after: one step per change.
    private static async Task<object> OneStep(string what, string before, Func<bool> applied)
    {
        string after = Text;
        int at = Position;
        Require(applied(), what + ": not applied");
        await Undo();
        Require(Text == before && Position == at - 1, what + ": one Undo did not restore the scene");
        await Redo();
        Require(Text == after && applied(), what + ": Redo did not restore the change");
        return new { what, undo_steps = 1 };
    }

    // ---- edit (KeyRoom)
    private static async Task Edit()
    {
        // Undo and Redo restore objects as new instances: look them up by name every time.

        // 13.9: Sprite tint, Line start colour and Light colour through the ColorPicker popup: live while dragging, one Undo step.
        var colours = new List<object>();
        foreach (var (obj, field, read) in new (SceneObject, string, Func<string>)[]
        {
            (Object("Door"), "Sprite tint", () => Object("Door").GetComponent<SpriteRendererComponent>()!.Tint ?? ""),
            (Object("QA Line"), "Start color", () => Object("QA Line").GetComponent<Line2DComponent>()!.StartColor),
            (Object("QA Light"), "Color", () => Object("QA Light").GetComponent<PointLight2DComponent>()!.Color),
        })
        {
            await Select(obj);
            var colour = Visible(InspectorStack()).First(c => c.GetType().Name == "ColorField" && AutomationProperties.GetName((Control)Get(c, "Swatch")!) == field + " picker");
            var swatch = (Control)Get(colour, "Swatch")!;
            var flyout = (Avalonia.Controls.Primitives.FlyoutBase)Get(colour, "Flyout")!;
            var colorView = Get(colour, "Picker")!;
            string before = Text;
            int at = Position;
            flyout.ShowAt(swatch);
            await Delay(400);
            foreach (var hex in new[] { "#204060", "#406080", "#C05030" })
            {
                colorView.GetType().GetProperty("Color")!.SetValue(colorView, Color.Parse(hex));
                await Delay(60);
            }
            Require(read() == "#C05030" && Position == at, $"{field}: live while the popup is open, nothing recorded yet ({read()}, {Position - at})");
            Capture(main, "colour-" + field.Replace(' ', '-').ToLowerInvariant());
            flyout.Hide();
            await Delay(400);
            Require(Position == at + 1, $"{field}: closing the popup records once ({Position - at})");
            colours.Add(await OneStep(field, before, () => read() == "#C05030"));
        }
        Step("colours", colours);

        // 16.2: Draw mode Sliced (one step, default borders), then one committed Slice border (one step); both undone afterwards.
        await Select(Object("Door"));
        SpriteRendererComponent DoorSprite() => Object("Door").GetComponent<SpriteRendererComponent>()!;
        string beforeMode = Text;
        Visible(InspectorStack()).OfType<ComboBox>().First(c => AutomationProperties.GetName(c) == "Draw mode").SelectedItem = SpriteDrawMode.Sliced;
        await Delay(500);
        var modeUndo = await OneStep("Door draw mode Sliced", beforeMode, () => DoorSprite() is { DrawMode: SpriteDrawMode.Sliced, Slice: not null });
        await Select(Object("Door"));
        string beforeBorder = Text;
        Visible(InspectorStack()).OfType<NumericUpDown>().First(n => AutomationProperties.GetName(n) == "Left slice border").Value = 3;
        await Delay(400);
        var borderUndo = await OneStep("Door slice left 3", beforeBorder, () => DoorSprite().Slice?.Left == 3);
        Capture(InspectorPanel, "inspector-draw-mode");
        Step("draw_mode", new { mode = modeUndo, border = borderUndo, slice = DoorSprite().Slice?.ToString() });
        await Undo(); await Undo();
        Require(DoorSprite() is { DrawMode: null, Slice: null }, "draw mode undone");

        // 13.7/13.17: Project drag of Key.png onto the Door's Sprite field.
        await Select(Object("Door"));
        string beforeDrop = Text;
        var sprite = await DragProject([KeyPng], FieldIn(InspectorStack(), "Sprite"), "drag-sprite");
        await Until(() => Object("Door").GetComponent<SpriteRendererComponent>()!.AssetId == Id(KeyPng), "Key.png on the Door sprite");
        Require(Shown(FieldIn(InspectorStack(), "Sprite")) == "Key.png", "the Sprite field shows Key.png");
        Step("sprite_drop", new { drag = sprite, undo = await OneStep("Door sprite ← Key.png", beforeDrop, () => Object("Door").GetComponent<SpriteRendererComponent>()!.Asset == KeyPng) });

        // Wrong type: a PNG on the Sound field is refused (red border only, nothing changes, nothing recorded).
        string beforeReject = Text;
        int rejectAt = Position;
        var soundId = Object("Door").GetComponent<AudioSourceComponent>()!.Sound;
        var reject = await DragProject([CoinPng], FieldIn(InspectorStack(), "Sound"), "drag-reject");
        Require(Object("Door").GetComponent<AudioSourceComponent>()!.Sound == soundId && Text == beforeReject && Position == rejectAt, "the PNG dropped on Sound was not refused");
        Require(((string[])Get(reject, "highlight")!).SequenceEqual(["referenceReject"]), "the refused drag marks the Sound field red");
        Step("sound_reject", reject);

        // A project WAV, then a Finder WAV from outside the project (imported into Assets/, then linked).
        string beforeSound = Text;
        var wav = await DragProject(["Assets/QaChime.wav"], FieldIn(InspectorStack(), "Sound"), "drag-sound");
        await Until(() => Object("Door").GetComponent<AudioSourceComponent>()!.Sound == Id("Assets/QaChime.wav"), "QaChime.wav on the Door sound");
        Require(((string[])Get(wav, "highlight")!).SequenceEqual(["referenceDrop"]), "an accepted drag marks the field with the accent border");
        var wavUndo = await OneStep("Door sound ← QaChime.wav", beforeSound, () => Object("Door").GetComponent<AudioSourceComponent>()!.Sound == Id("Assets/QaChime.wav"));
        string beforeFinder = Text;
        await FinderDrop(FieldIn(InspectorStack(), "Sound"), Environment.GetEnvironmentVariable("MONOON_AF_FINDER")!);
        Require(File.Exists(Path.Combine(Root, "Assets/FinderChime.wav")), "the Finder WAV was copied into Assets/");
        Require(Object("Door").GetComponent<AudioSourceComponent>()!.Sound == Id("Assets/FinderChime.wav"), "the Finder WAV is linked to the Door sound");
        var finderUndo = await OneStep("Door sound ← Finder FinderChime.wav", beforeFinder, () => Object("Door").GetComponent<AudioSourceComponent>()!.Sound == Id("Assets/FinderChime.wav"));
        Step("sound_drop", new { project = wav, project_undo = wavUndo, finder = "Assets/FinderChime.wav", finder_undo = finderUndo });

        // Ping and Select: click shows the asset in Project, double-click on an object field selects the object.
        var spriteField = FieldIn(InspectorStack(), "Sprite");
        spriteField.RaiseEvent(new PointerPressedEventArgs(spriteField, Mouse, main, In(spriteField).Center, time++, Props(PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        await Delay(300);
        var browser = Find<ListBox>("ProjectBrowser");
        var pinged = browser.SelectedItem is { } selected ? (string)Get(selected, "RelativePath")! : null;
        bool flashed = browser.ContainerFromItem(browser.SelectedItem!)?.Classes.Contains("ping") == true;
        Require(pinged == KeyPng && flashed, $"Ping showed {pinged} (flash {flashed})");
        Step("ping", new { field = "Sprite", project_row = pinged, flashed, selection_unchanged = Get(main, "Selected") == Object("Door") });

        // The picker: Project thumbnails in the rows, search, pick (one Undo step), None clears.
        var picker = FieldIn(InspectorStack(), "Mask");
        ((Avalonia.Controls.Primitives.FlyoutBase)Get(picker, "Picker")!).ShowAt(picker);
        await Delay(500);
        var list = (ListBox)Get(picker, "List")!;
        int thumbs = Visible(list).OfType<Image>().Count();
        ((TextBox)Get(picker, "Search")!).Text = "LongName";
        await Delay(200);
        var matches = list.Items.Cast<object>().Select(o => o.ToString()).ToArray();
        Capture((Visual?)list.FindAncestorOfType<FlyoutPresenter>() ?? (Visual)list.Parent!, "picker-search"); // the picker is a popup: render its panel
        string beforeMask = Text;
        Call(picker, "Choose", list.Items.Cast<object>().Last());
        await Delay(400);
        Require(Object("Door").GetComponent<SpriteRendererComponent>()!.Mask == Id(LongPng), "picker chose the long-named mask");
        Step("picker", new { thumbnails = thumbs, search = "LongName", matches, undo = await OneStep("Door mask ← picker", beforeMask, () => Object("Door").GetComponent<SpriteRendererComponent>()!.Mask == Id(LongPng)) });

        // Alignment at 1280/1600/1920 with the long mask name: every value-column control shares one left and right edge.
        var alignment = new List<object>();
        foreach (var width in new[] { 1280, 1600, 1920 })
        {
            await Resize(width, 860);
            await Select(Object("Door"));
            var controls = Visible(InspectorStack()).Where(c => c.Parent is Grid g && Grid.GetColumn(c) == 1 && g.ColumnDefinitions.Count > 1 &&
                g.ColumnDefinitions[0].Width.Value == 56 && c.Bounds.Width > 0 && c.FindAncestorOfType<ComponentSection>() != null).ToList();
            var fields = controls.OfType<Border>().Where(b => b.GetType().Name == "ReferenceField").ToList();
            var lefts = controls.Select(c => Math.Round(In(c).X, 1)).Distinct().ToArray();
            // Checkboxes and the Flip pair keep their own width; every stretched input ends at the same right edge.
            // The value inputs: text and number boxes, choices, buttons, colour fields and object fields.
            var stretched = controls.Where(c => c is not Avalonia.Controls.Primitives.ToggleButton && (c is TextBox or NumericUpDown or ComboBox or Button || c.GetType().Name is "ReferenceField" or "ColorField")).ToList();
            var rights = stretched.Select(c => Math.Round(In(c).Right, 1)).Distinct().ToArray();
            var odd = stretched.Where(c => Math.Round(In(c).Right, 1) != rights.Max()).Select(c => c.GetType().Name + ":" + (AutomationProperties.GetName(c) ?? "")).ToArray();
            var heights = fields.Select(f => f.Bounds.Height).Distinct().ToArray();
            var mask = FieldIn(InspectorStack(), "Mask");
            var maskText = (TextBlock)Get(mask, "Text")!;
            bool trimmed = In(maskText).Right <= In(mask).Right - 20;
            var row = new { width, controls = controls.Count, object_fields = fields.Count, lefts, rights, odd, heights, long_name_inside = trimmed };
            alignment.Add(row);
            Capture(InspectorPanel, $"inspector-{width}");
            Require(lefts.Length == 1 && rights.Length == 1 && heights.SequenceEqual([24.0]) && trimmed, "Inspector value column out of line: " + JsonSerializer.Serialize(row));
        }
        await Resize(1280, 800);
        Step("alignment", alignment);
        await Undo(); // the long mask was only for the alignment check
        Require(Object("Door").GetComponent<SpriteRendererComponent>()!.Mask == null, "mask removed");

        // Arrays: two Project PNGs dropped on the Key's Frames list append in Project order, one Undo step.
        await Select(Object("Key"));
        var framesBefore = Frames(Object("Key"));
        string beforeFrames = Text;
        var header = Visible(InspectorStack()).OfType<Border>().First(b => b.Tag?.GetType().Name == "ReferenceDrop" && b.GetType().Name != "ReferenceField");
        var many = await DragProject([CoinPng, KeyPng], header, "drag-array");
        var framesAfter = Frames(Object("Key"));
        Require(framesAfter.SequenceEqual([.. framesBefore, Id(CoinPng), Id(KeyPng)]), $"Frames {string.Join(",", framesAfter)}");
        Step("array_drop", new { drag = many, before = framesBefore.Length, after = framesAfter.Length, undo = await OneStep("Key Frames += Coin, Key", beforeFrames, () => Frames(Object("Key")).Length == framesBefore.Length + 2) });
        await Undo(); // keep the game as shipped

        // Multi-selection: one drop on the common Sprite field sets both barrels; one Undo step restores both.
        SceneObject[] Barrels() => [Object("Barrel 1"), Object("Barrel 2")];
        Call(main, "ShowSelection", (IReadOnlyList<SceneObject>)Barrels());
        await Delay(500);
        string beforeMulti = Text;
        var multi = await DragProject([PotPng], FieldIn(Find<StackPanel>("MultiInspector"), "Sprite"), "drag-multi");
        Require(Barrels().All(w => w.GetComponent<SpriteRendererComponent>()!.AssetId == Id(PotPng)), "both barrels took Pot.png");
        Step("multi_drop", new { drag = multi, undo = await OneStep("two barrels ← Pot.png", beforeMulti, () => Barrels().All(w => w.GetComponent<SpriteRendererComponent>()!.AssetId == Id(PotPng))) });
        await Undo();

        // Particle panel: Coin.png dragged onto the QA Sparks texture field writes the .particles asset (one Undo step).
        var sparks = Object("QA Sparks");
        await Select(sparks);
        Call(main, "OpenParticlePanel", (string?)null);
        await Delay(1200);
        var panel = main.GetVisualDescendants().OfType<Control>().First(c => c.GetType().Name == "ParticlePanel");
        string particles = Path.Combine(Root, "Assets/QaSparks.particles");
        string fxBefore = File.ReadAllText(particles);
        var texture = FieldIn(panel, "Texture");
        texture.BringIntoView();
        await Delay(300);
        var tex = await DragProject([CoinPng], texture, "drag-particle-texture");
        await Until(() => ParticleEffect.Load(particles).Texture == Id(CoinPng), "Coin.png on the particle texture");
        string fxAfter = File.ReadAllText(particles);
        await Undo();
        Require(File.ReadAllText(particles) == fxBefore, "Undo restored the .particles file");
        await Redo();
        Require(File.ReadAllText(particles) == fxAfter, "Redo wrote the texture back");
        var wrong = await DragProject(["Assets/QaChime.wav"], FieldIn(panel, "Texture"), "drag-particle-reject");
        Require(File.ReadAllText(particles) == fxAfter, "a WAV on the texture field was not refused");
        Step("particle_texture", new { drag = tex, reject = wrong, undo_steps = 1 });

        Require(await Save(), "save cancelled");
        var saved = SceneDocument.OpenForEditing(Path.Combine(Root, "Scenes", "Showcase.scene"));
        var savedDoor = saved.Objects.First(o => o.Name == "Door");
        Step("saved", new
        {
            door_sprite = savedDoor.GetComponent<SpriteRendererComponent>()!.Asset,
            door_sprite_id = savedDoor.GetComponent<SpriteRendererComponent>()!.AssetId,
            door_sound = Assets.PathFor(savedDoor.GetComponent<AudioSourceComponent>()!.Sound!.Value),
            door_sound_id = savedDoor.GetComponent<AudioSourceComponent>()!.Sound,
            particle_texture = Assets.PathFor(ParticleEffect.Load(particles).Texture!.Value),
            particle_texture_id = ParticleEffect.Load(particles).Texture,
            tint = savedDoor.GetComponent<SpriteRendererComponent>()!.Tint,
        });
    }

    private const string LongPng = "Assets/QaLongNameDoorMaskForInspectorTrimmingChecks0123456789.png";
    private const string KeyPng = "Assets/Showcase/Key.png", CoinPng = "Assets/Showcase/Coin.png", PotPng = "Assets/Showcase/Pot.png";
    private static Guid[] Frames(SceneObject key) => ((IEnumerable<Guid>)Call(key.Components.OfType<ScriptComponent>().First(s => s.Script == "KeySequence"), "ReadReferences", "Frames", false)!).ToArray();

    // ---- trace (Scene Play of the saved project, captured with the player's software SDL canvas)
    private static async Task Trace()
    {
        await Until(() => Scene.Objects.Any(o => o.Name == "QA Sparks"), "QA objects", 60);
        await Delay(500);
        var play = Find<Button>("PlayButton");
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        var game = (GameSession)view.GetType().GetField("game", Any)!.GetValue(view)!;
        game.LoadFailed += message => throw new InvalidDataException(message);
        var project = GameSettings.Load(Root).ProjectId!.Value;
        var frames = File.ReadAllText(Path.Combine(Output, "captures.txt")).Split(',').Select(int.Parse).ToHashSet();
        Replay(game, project, Directory.GetFiles(Output, "*.inputreplay").Single(), frames, Directory.CreateDirectory(Path.Combine(Output, "captures")).FullName);
        await Delay(500);
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, play));
        Step("trace", new { frames = frames.Order().ToArray() });
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

    // ---- field (CollisionLab Field10000): pick one of 10,000 objects by searching the Connect picker
    private static async Task Field()
    {
        await (Task)Call(main, "SwitchScene", "Field10000")!;
        await Until(() => Scene.Objects.Count > 10000, "Field10000 open", 120);
        await Delay(1000);
        var owner = Scene.Objects[0];
        var target = Scene.Objects[^7];
        await Select(owner);
        Require((bool)Call(main, "AttachComponent", owner, new DistanceJoint2DComponent())!, "joint added");
        await Delay(800);
        var connect = FieldIn(InspectorStack(), "Connect");
        var flyout = (Avalonia.Controls.Primitives.FlyoutBase)Get(connect, "Picker")!;
        var search = (TextBox)Get(connect, "Search")!;
        var list = (ListBox)Get(connect, "List")!;
        var opens = new List<double>();
        var searches = new List<double>();
        var stalls = new List<double>();
        string name = target.Name;
        for (int run = 0; run < 5; run++)
        {
            var clock = Stopwatch.StartNew();
            flyout.ShowAt(connect);
            await Until(() => list.ItemCount > 0, "picker open");
            opens.Add(((TimeSpan)Get(connect, "LastOpen")!).TotalMilliseconds);
            await Delay(200);
            clock.Restart();
            search.Text = name;
            // TextChanged runs from the dispatcher: wait until the UI thread has filtered, rebuilt the list, laid it out and rendered.
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Background);
            stalls.Add(clock.Elapsed.TotalMilliseconds);
            searches.Add(((TimeSpan)Get(connect, "LastSearch")!).TotalMilliseconds);
            Require(list.ItemCount < 50, $"the search filtered the list ({list.ItemCount} rows)");
            if (run < 4) { flyout.Hide(); await Delay(300); }
        }
        var matches = list.Items.Cast<object>().Where(o => (Guid)Get(o, "Id")! != Guid.Empty).ToArray();
        Capture((Visual?)list.FindAncestorOfType<FlyoutPresenter>() ?? (Visual)list.Parent!, "field10000-picker");
        Call(connect, "Choose", matches.First(m => (Guid)Get(m, "Id")! == target.Id));
        await Delay(500);
        var joint = owner.GetComponent<DistanceJoint2DComponent>()!;
        Require(joint.Connected == target.Id, "Connect picked the searched object");
        double Median(List<double> v) => v.Order().ElementAt(v.Count / 2);
        var row = new { objects = Scene.Objects.Count, searched = name, matches = matches.Length, open_ms = opens, search_filter_ms = searches, search_stall_ms = stalls,
            median_open_ms = Median(opens), median_stall_ms = Median(stalls), max_stall_ms = stalls.Max() };
        Step("field10000", row);
        Require(stalls.Max() < 100, "picker search stalled for 100 ms or more: " + JsonSerializer.Serialize(row));
    }

    // ---- curves (ParticleLab Flame): Inspector rows draw the runtime table; edits write the asset once per commit
    private static async Task Curves()
    {
        await (Task)Call(main, "SwitchScene", "ShowcaseWorkshop")!;
        await Until(() => Scene.Objects.Any(o => o.Name == "Flame"), "workshop scene", 60);
        await Delay(800);
        var flame = Object("Flame");
        await Select(flame);
        var file = Path.Combine(Root, Assets.PathFor(flame.GetComponent<ParticleEmitter2DComponent>()!.Particles!.Value)!);
        var curve = Visible(InspectorStack()).First(c => c.GetType().Name == "CurveField");
        var gradient = Visible(InspectorStack()).First(c => c.GetType().Name == "GradientField");
        var lut = typeof(ParticleEffect).Assembly.GetType("Engine.Core.ParticleLut")!;
        float[] Bake(params object[] args) => (float[])lut.GetMethods(Any).First(m => m.Name == "Bake" && m.GetParameters().Length == args.Length).Invoke(null, args)!;
        float[] Drawn() => (float[])Get(Get(curve, "View")!, "Samples")!;
        float[] Runtime() => Bake(ParticleEffect.Load(file).SizeOverLifetime!.Keys, ParticleEffect.Load(file).SizeOverLifetime!.Smooth);
        float[] DrawnGradient() => (float[])Get(Get(gradient, "View")!, "Samples")!;
        float[] RuntimeGradient() => Bake(ParticleEffect.Load(file).ColorOverLifetime!);
        Require(Drawn().SequenceEqual(Runtime()) && DrawnGradient().SequenceEqual(RuntimeGradient()), "the Inspector rows show the runtime tables");
        string sceneBefore = Text;
        string before = File.ReadAllText(file);
        int at = Position;
        // Key 0 value 0.6 → 1.5, typed and committed (no focus: the number box commits at once).
        Call(Get(curve, "View")!, "Select", 0);
        var value = Visible(curve).OfType<NumericUpDown>().First(n => AutomationProperties.GetName(n) == "Key value");
        value.Value = 1.5m;
        await Delay(400);
        Require(Position == at + 1, "one record for the curve commit");
        var keys = ParticleEffect.Load(file).SizeOverLifetime!.Keys;
        Require(keys[0].Value == 1.5f && Drawn().SequenceEqual(Runtime()), "the curve commit wrote the key and the line is the runtime table");
        // Gradient alpha key 1 → 0.5.
        Call(Get(gradient, "View")!, "Select", true, 1);
        var alpha = Visible(gradient).OfType<NumericUpDown>().First(n => AutomationProperties.GetName(n) == "Key alpha (0–1)");
        alpha.Value = .5m;
        await Delay(400);
        Require(Position == at + 2 && ParticleEffect.Load(file).ColorOverLifetime!.Alphas![1].Alpha == .5f, "one record for the gradient commit");
        string after = File.ReadAllText(file);
        Capture(InspectorPanel, "inspector-curves");
        await Undo(); await Undo();
        Require(File.ReadAllText(file) == before && Text == sceneBefore, "two Undos restore the asset");
        await Redo(); await Redo();
        Require(File.ReadAllText(file) == after, "two Redos write the edits back");
        Require(await Save(), "save cancelled");
        Step("curves", new { asset = Path.GetFileName(file), key0 = 1.5, alpha1 = .5, samples = Drawn().Length, drawn_equals_runtime = true, undo_steps = 2,
            runtime_first = Runtime()[0], runtime_mid = Runtime()[32] });
    }
}
