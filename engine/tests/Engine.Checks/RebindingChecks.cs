using System.Numerics;
using System.Text.Json;
using Engine.Core;

internal static class RebindingChecks
{
    [Check(7000, "rebinding")] static void Registered(CheckRun c) => Run(c.Project, c.Scratch);
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("Player rebinding: " + message);
        Console.WriteLine("PASS player rebinding: " + message);
    }

    internal static void Run(string project, string scratch)
    {
        Actions();
        var storage = new PlayerStorage(Path.Combine(scratch, "rebinding-profile"));
        var room = SceneDocument.Load(Path.Combine(project, "Scenes", "KeyRoom.scene"));
        var menu = SceneDocument.Load(Path.Combine(project, "Scenes", "KeyBindings.scene"));
        Guid Menu(string name) => menu.Objects.Single(o => o.Name == name).Id;
        var hint = room.Objects.Single(o => o.Name == "Controls hint").Id;

        // First run: swap, cancel on both devices, pad swap, then play with the new key.
        using (var run = new Play(room, storage))
        {
            Check(run.Text(hint).Contains("Interact: E"), "default keyboard prompt");
            run.Open(menu);
            Check(run.Ui.Focused == Menu("Interact keyboard") && run.Text(Menu("Bindings status")) == "Choose a control to change.",
                "K opens the controls screen on the first keyboard binding");
            float Left(string name) => run.Ui.Bounds(Menu(name))!.Value.Left;
            var panel = run.Ui.Bounds(Menu("Bindings panel"))!.Value;
            Check(Left("Action heading") == Left("Interact label") && Left("Interact label") == Left("Restore defaults") &&
                Left("Keyboard heading") == Left("PauseGame keyboard") && Left("PauseGame keyboard") == Left("Close bindings") &&
                Left("Gamepad heading") == Left("CarryCoin gamepad") && run.Ui.Bounds(Menu("Close bindings"))!.Value.Bottom <= panel.Bottom - 15 &&
                panel.Left >= 0 && panel.Top >= 0 && panel.Right <= 800 && panel.Bottom <= 450, $"columns line up inside the panel {panel}, restore {run.Ui.Bounds(Menu("Restore defaults"))}");
            run.Router.Key("Return", true); run.Step();
            run.Router.Key("Return", true); run.Step(); // Host key repeat while the activation key is still held.
            run.Router.Key("Return", false); run.Step();
            Check(run.State.RebindingAction == "Interact" && run.Text(Menu("Interact keyboard")) == "…", "Return starts capture, key repeat does not finish it");
            run.Key("C");
            Check(run.State.ControlsFor("Interact").SequenceEqual(["C", "Space", "Pad.South"]) &&
                run.State.ControlsFor("CarryCoin").SequenceEqual(["E"]) && run.State.LastRebind!.Swapped.SequenceEqual(["CarryCoin"]),
                "capturing C swaps it with Carry coin's E");
            Check(run.Text(Menu("Bindings status")) == "Open chest: C. Carry coin now uses E." &&
                run.Text(Menu("CarryCoin keyboard")) == "E" && run.Text(hint).Contains("Interact: C"), "screen and HUD prompt show the swap");
            var saved = storage.LoadSettings(1)!.Values[InputActions.SettingsKey];
            Check(saved.GetProperty("Interact"u8).EnumerateArray().Select(e => e.GetString()).SequenceEqual(["C", "Space", "Pad.South"]) &&
                saved.GetProperty("CarryCoin"u8)[0].GetString() == "E" && !saved.TryGetProperty("PauseGame"u8, out _), "player.settings stores only changed actions");

            run.Key("Return"); run.Key("Escape");
            Check(run.State.RebindingAction == null && run.State.LastRebind!.Cancelled && run.State.BindingFor("Interact", InputDevice.Keyboard) == "C" &&
                run.Text(Menu("Bindings status")) == "Cancelled. Open chest stays C.", "Esc cancels a keyboard capture");
            run.Ui.FocusWidget(Menu("Interact gamepad"));
            run.Key("Return"); run.Key("X");
            Check(run.State.LastRebind!.Cancelled && run.State.BindingFor("Interact", InputDevice.Gamepad) == "Pad.South", "a key cancels a pad capture");
            run.Key("Return"); run.Pad("Pad.North");
            Check(run.State.BindingFor("Interact", InputDevice.Gamepad) == "Pad.North" && run.State.ControlsFor("RoomGuide").SequenceEqual(["M", "Pad.South"]) &&
                run.Text(hint).Contains("Interact: North"), "pad capture swaps with Room guide and the pad prompt follows");
            run.Key("K");
            Check(!run.State.IsSceneLoaded(menu.AssetId!.Value), "K closes the controls screen");
            run.Key("E");
            Check(!run.State.Has("chest") && run.Text(hint).Contains("Interact: C"), "the old key no longer opens the chest");
            run.Key("C");
            Check(run.State.Has("chest"), "the new key opens the chest");
        }

        // Restart: the saved bindings come back before the first input.
        using (var run = new Play(room, storage))
        {
            Check(run.State.BindingFor("Interact", InputDevice.Keyboard) == "C" && run.Text(hint).Contains("Interact: C"), "restart restores the key and its prompt");
            run.Key("E");
            Check(!run.State.Has("chest"), "after restart E stays on Carry coin");
            run.Key("C");
            Check(run.State.Has("chest"), "after restart C opens the chest");
            run.Open(menu);
            run.Click(Menu("Restore defaults"));
            Check(run.State.ControlsFor("Interact").SequenceEqual(["E", "Space", "Pad.South"]) && run.State.ControlsFor("RoomGuide").SequenceEqual(["M", "Pad.North"]) &&
                run.Text(Menu("Bindings status")) == "Default controls restored." && run.Text(hint).Contains("Interact: E") &&
                !storage.LoadSettings(1)!.Values.ContainsKey(InputActions.SettingsKey), "Restore defaults resets every action and clears the saved map");
        }
        using (var run = new Play(room, storage))
        {
            run.Key("E");
            Check(run.State.Has("chest") && run.State.BindingFor("CarryCoin", InputDevice.Keyboard) == "C", "restored defaults persist across a restart");
        }
    }

    private static void Actions()
    {
        var actions = new InputActions(new InputBindings { Actions = [new("Jump", ["Space", "W", "Pad.South"]), new("Use", ["E"]), new("Dash", ["Pad.East"])] });
        actions.Swap("Jump", InputDevice.Keyboard, "W");
        Check(actions.ControlsFor("Jump").SequenceEqual(["W", "Space", "Pad.South"]), "a secondary control of the same action moves to the first slot");
        var result = actions.Swap("Use", InputDevice.Gamepad, "Pad.East");
        Check(actions.ControlsFor("Use").SequenceEqual(["E", "Pad.East"]) && actions.ControlsFor("Dash").Length == 0 &&
            result.Previous == null && result.Swapped.SequenceEqual(["Dash"]), "binding a device the action lacked removes the control from its old action");
        try { actions.Swap("Use", InputDevice.Keyboard, "Pad.North"); throw new Exception("Expected a device mismatch"); }
        catch (ArgumentException) { }
        var settings = new GameData();
        actions.SaveTo(settings);
        actions.RestoreDefaults();
        Check(actions.ControlsFor("Dash").SequenceEqual(["Pad.East"]), "restore brings back removed controls");
        actions.LoadFrom(settings);
        Check(actions.ControlsFor("Use").SequenceEqual(["E", "Pad.East"]) && actions.ControlsFor("Dash").Length == 0, "saved map round trips through GameData");
        settings.Values[InputActions.SettingsKey] = JsonDocument.Parse("""{"Jump":["Bogus"],"Nope":["E"],"Use":"E","Dash":["Pad.East","Pad.East"]}""").RootElement.Clone();
        actions.LoadFrom(settings);
        Check(actions.ControlsFor("Jump").SequenceEqual(["Space", "W", "Pad.South"]) && actions.ControlsFor("Use").SequenceEqual(["E"]) &&
            actions.ControlsFor("Dash").SequenceEqual(["Pad.East"]), "invalid or unknown saved entries fall back to defaults");
        actions.BeginCapture("Use", InputDevice.Keyboard);
        Check(actions.TryCapture("Mouse.Left") && actions.LastRebind!.Cancelled && actions.CaptureAction == null, "another device cancels the capture");
        actions.BeginCapture("Use", InputDevice.Keyboard);
        Check(actions.TryCapture("CapsLock") && actions.CaptureAction == "Use" && actions.TryCapture("Q") && actions.ControlsFor("Use")[0] == "Q",
            "unbindable keys keep waiting");
        actions.Press("Q");
        Check(!actions.Take().Pressed("Use"), "the captured press does not trigger the action");
    }

    private sealed class Play : IDisposable
    {
        private readonly GameSession game;
        private readonly GameLoop loop;
        public readonly GameInputRouter Router;
        public SessionState State => game.State;
        public ScreenUi Ui => game.Current.Ui;

        public Play(SceneDocument scene, PlayerStorage storage)
        {
            game = new GameSession(scene, null, null, storage);
            game.LoadFailed += message => throw new Exception(message);
            Router = new GameInputRouter(game.Actions, () => game.InputUi, game.LogicalSize, () => game.Current);
            loop = new GameLoop(game, game.Actions);
            Step();
        }
        public void Step() => loop.Advance(GameTime.FixedStep);
        public string Text(Guid id) => Ui.TextOf(id) ?? "";
        public void Key(string key) { Router.Key(key, true); Step(); Router.Key(key, false); Step(); }
        public void Pad(string control) { Router.Pad(control, 1); Step(); Router.Pad(control, 0); Step(); }
        public void Click(Guid id)
        {
            var b = Ui.Bounds(id)!.Value;
            var point = new Vector2(b.Left + 4, b.Top + 4);
            Router.Pointer(point, "Mouse.Left", true); Router.Pointer(point, "Mouse.Left", false); Step(); Step();
        }
        public void Open(SceneDocument menu)
        {
            Key("K");
            long deadline = Environment.TickCount64 + 5000; // Additive scenes load on a worker thread.
            while (!State.IsSceneLoaded(menu.AssetId!.Value) && Environment.TickCount64 < deadline) { Step(); Thread.Sleep(1); }
            Step();
            if (!State.IsSceneLoaded(menu.AssetId!.Value)) throw new Exception("Player rebinding: controls screen did not open");
        }
        public void Dispose() => game.Dispose();
    }
}
