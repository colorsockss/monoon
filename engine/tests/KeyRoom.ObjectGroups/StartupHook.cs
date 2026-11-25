using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Engine.Core;

public static class StartupHook
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_GROUP_QA")!;
    private const string Group = "수집품";

    // No shutdown here: the run ends itself or the outside driver closes the editor.
    public static void Initialize() => Start(Path.Combine(Output, "error.txt"), _ => Run(), allow: ["Prefab · *"], shutdown: false);

    private static void Check(bool yes, string what)
    {
        if (!yes) throw new InvalidOperationException(what);
    }

    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, b));

    private static void Type(TextBox box, string value)
    {
        box.BringIntoView();
        box.Focus();
        Check(box.IsKeyboardFocusWithin && !box.IsReadOnly, "Cannot edit " + box.Name);
        box.SelectAll();
        box.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = value });
        Check(box.Text == value, "Text input did not reach " + box.Name);
    }

    private static void Capture(Control area, string name)
    {
        var size = area.Bounds.Size;
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height)));
        bitmap.Render(area);
        bitmap.Save(Path.Combine(Output, name + ".png"));
    }

    private static async Task Run()
    {
        var main = ((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).MainWindow!;
        var project = Path.Combine(Output, "KeyRoom");
        var path = Path.Combine(project, "Assets", "Showcase", "Coin.prefab");
        await Until(() => main.IsVisible && main.FindControl<ListBox>("Hierarchy")!.Items.Count > 1, "KeyRoom");
        var edit = main.GetType().GetMethod("EditPrefabSource", Private)!;
        async Task EditPrefab(bool write)
        {
            var closing = (Task)edit.Invoke(main, ["Assets/Showcase/Coin.prefab"])!;
            await Until(() => main.OwnedWindows.Any(w => w.IsVisible), "Coin prefab source window");
            var prefab = main.OwnedWindows.Single(w => w.IsVisible);
            prefab.Width = 850; prefab.Height = 640;
            await Delay(200);
            var tag = prefab.FindControl<TextBox>("ObjectTag")!;
            var groups = prefab.FindControl<TextBox>("ObjectGroups")!;
            if (write)
            {
                Check(string.IsNullOrEmpty(groups.Text) && string.IsNullOrEmpty(tag.Text), "Expected unassigned prefab");
                Type(tag, "Coin");
                Type(groups, Group);
                Click(prefab.FindControl<Button>("PlayButton")!); // Save Prefab in the source window.
                await Until(() => File.ReadAllText(path).Contains(Group), "Save Prefab");
            }
            else
            {
                Check(tag.Text == "Coin" && groups.Text == Group, "Reopened prefab lost its metadata");
                Check(Math.Abs(tag.TranslatePoint(default, prefab)!.Value.X - groups.TranslatePoint(default, prefab)!.Value.X) < 1,
                    "Tag and Groups fields must align");
                Capture((Control)groups.Parent!, "prefab-inspector");
            }
            prefab.Close();
            await closing;
        }
        await EditPrefab(true);
        await EditPrefab(false);
        main.Activate();
        if (!main.IsActive)
        {
            File.WriteAllText(Path.Combine(Output, "focus-main"), "");
            await Until(() => main.IsActive, "KeyRoom window focus after closing the prefab");
        }

        var play = main.FindControl<Button>("PlayButton")!;
        var viewport = main.FindControl<Control>("Viewport")!;
        GameSession? Session() => (GameSession?)viewport.GetType().GetField("game", Private)!.GetValue(viewport);
        async Task<GameSession> Start()
        {
            Click(play);
            await Until(() => Session() != null, "Start Play");
            return Session()!;
        }
        var query = main.FindControl<TextBox>("RoleQuery")!;
        var caption = main.FindControl<TextBlock>("RoleMatches")!;
        async Task Shows(string value)
        {
            await Until(() => caption.Text == value && caption.IsEffectivelyVisible, "Debug query " + value);
        }
        var game = await Start();
        main.FindControl<ToggleButton>("DebugButton")!.IsChecked = true;
        await Until(() => query.IsEffectivelyVisible, "Debug controls");
        Type(query, Group);
        await Shows("0");
        var changes = new List<object>();
        void Record(string stage, IReadOnlyList<Guid> group, IReadOnlyList<Guid> tag, int count)
        {
            Check(group.Count == count && group.SequenceEqual(tag), stage + " tag/group mismatch");
            changes.Add(new { stage, count = group.Count, ids = group.ToArray() });
            Console.WriteLine(stage + " = " + count);
        }
        void Step(Vector2 direction, bool interact = false) => game.FixedUpdate(new GameInput(direction, interact), 1f / 60);
        var members = game.Current.FindByGroup(Group);
        var tagged = game.Current.FindByTag("Coin");
        Record("before-open", members, tagged, 0);
        // The showcase hero starts 88 px from the chest: six steps east bring it within reach.
        for (int n = 0; n < 6; n++) Step(Vector2.UnitX);
        Step(Vector2.Zero, true);
        Record("spawn-tick", members, tagged, 1);
        var coin = members.Single();
        Check(game.Current.ObjectLabel(coin) == "Coin", "Wrong spawned object");
        await Shows("1 · Coin");
        Capture((Control)caption.Parent!, "spawn-query");
        for (int n = 0; n < 4; n++) Step(-Vector2.UnitY);   // level with the coin, above the chest's base
        for (int n = 0; n < 55 && !game.State.Has("coin"); n++) Step(Vector2.UnitX);
        Check(game.State.Has("coin") && !game.Current.TryGetPosition(coin, out _), "No trigger pickup");
        Record("pickup-tick", members, tagged, 0);
        await Shows("0");
        Click(play);
        await Until(() => Session() == null, "Stop");
        game = await Start();
        main.FindControl<ToggleButton>("DebugButton")!.IsChecked = true;
        var oldRoom = game.Current;
        var originalScene = game.SceneId;
        members = oldRoom.FindByGroup(Group);
        tagged = oldRoom.FindByTag("Coin");
        for (int n = 0; n < 6; n++) Step(Vector2.UnitX);
        Step(Vector2.Zero, true);
        coin = members.Single();
        Check(game.Actions.Press("C"), "CarryCoin binding");
        game.FixedUpdate(game.Actions.Take(), 1f / 60);
        game.Actions.Release("C");
        var player = oldRoom.Objects.Single(o => o.Name == "Player").Id;
        Check(oldRoom.TryGetParent(coin, out var parent) && parent == player, "C did not pick up the grouped coin for carrying");
        for (int i = 0; i < 110 && !game.State.Has("key"); i++) Step(-Vector2.UnitX);
        Check(game.State.Has("key"), "No key for exit");
        // The door is in the north wall: back east under the doorway, then north through it.
        for (int i = 0; i < 40 && oldRoom.TryGetPosition(player, out var at) && at.X < 277; i++) Step(Vector2.UnitX);
        for (int i = 0; i < 160 && game.State.PendingScene == null; i++) Step(-Vector2.UnitY);
        Check(game.State.PendingScene != null && !game.State.Has("coin"), "Expected exit transition with a live coin");
        Record("leaving-with-coin", members, tagged, 1);
        await Until(() =>
        {
            if (game.Current == oldRoom) game.UpdateUi(default, 1f / 60);
            return game.Current != oldRoom;
        }, "Enter second room");
        Check(game.SceneId != originalScene && game.SceneId == ProjectAssets.Scan(project).IdFor("Scenes/ShowcaseRoom2.scene") && !game.State.Has("coin"), "Coin was consumed before leaving");
        Record("old-room-after-exit", members, tagged, 0);
        Record("second-room", game.Current.FindByGroup(Group), game.Current.FindByTag("Coin"), 0);
        await Shows("0");
        Capture((Control)caption.Parent!, "exit-query");
        Click(play);
        var result = new { platform = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            group = Group, tag = "Coin", prefabReopened = true, reachedScene = game.SceneId, input = "Right, Interact, Up, Right; restart, Right, Interact, CarryCoin, Left, Right, Up", changes };
        File.WriteAllText(Path.Combine(Output, "result.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        main.Close();
    }
}
