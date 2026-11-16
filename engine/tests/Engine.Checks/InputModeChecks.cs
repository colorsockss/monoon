using System.Numerics;
using Engine.Core;

internal static class InputModeChecks
{
    internal static void Run(string project)
    {
        var scene = SceneDocument.Load(Path.Combine(project, "Scenes", "KeyRoom.scene"));
        using var game = new GameSession(scene);
        var ui = game.Current.Ui;
        var router = new GameInputRouter(game.Actions, () => ui, game.LogicalSize);
        var loop = new GameLoop(game, game.Actions);
        Guid Id(string name) => scene.Objects.Single(o => o.Name == name).Id;
        var player = Id("Player");
        var bag = Id("Inventory");
        var confirm = Id("Compass confirmation");
        var manage = Id("Manage compass");
        var apply = Id("Apply compass");
        var cancel = Id("Cancel compass");
        Vector2 Position() => game.Current.TryGetPosition(player, out var pos) ? pos : throw new Exception("missing player");
        void Step() => loop.Advance(GameTime.FixedStep);
        void Key(string key) { router.Key(key, true); router.Key(key, false); Step(); }
        void Pad(string control) { router.Pad(control, 1); router.Pad(control, 0); Step(); }
        void Click(Guid id)
        {
            var b = ui.Bounds(id)!.Value;
            var point = new Vector2(b.Left + 4, b.Top + 4);
            router.Pointer(point, "Mouse.Left", true); router.Pointer(point, "Mouse.Left", false);
        }
        void Check(bool ok, string label)
        {
            if (!ok) throw new Exception("Input modes: " + label);
        }

        Step();
        var start = Position();
        Check(!ui.PushInputMode(confirm) && !ui.PushInputMode(player), "closed parent and non-root rejected");
        router.Key("D", true);
        router.Key("I", true);
        Step();
        Check(ui.ActiveInputMode == bag && ui.Focused == manage && Position() == start, "open consumes both input histories");
        router.Key("I", true); Step(); // Host key repeat while the opening key is still down.
        Check(ui.ActiveInputMode == bag && !ui.PushInputMode(bag), "held opener cannot immediately pop the bag");
        router.Key("I", false);
        Click(Id("Controls button")); Key("P"); Key("E");
        Check(!game.State.Has("controls") && !game.Time.IsPaused && !game.State.Has("chest"), "gameplay shortcuts and HUD clicks are blocked");
        Click(manage); Click(manage); Step();
        Check(ui.ActiveInputMode == confirm && ui.Focused == apply && !ui.PopInputMode(bag), "queued clicks stop at a mode change");
        Pad("Pad.DpadRight");
        Check(ui.Focused == cancel, "gamepad navigation stays in confirmation");
        Pad("Pad.East");
        Check(ui.ActiveInputMode == bag && ui.Focused == manage && !game.State.Has("compass"), "cancel restores focus without applying");
        Key("Return"); Pad("Pad.South");
        Check(ui.ActiveInputMode == bag && ui.Focused == manage && game.State.Has("compass"), "accept changes the item once");
        Key("Backspace"); Step();
        Check(ui.ActiveInputMode == null && ui.Focused == null && Position() == start, "held movement stays suppressed after closing");
        router.Key("D", false); Key("D");
        Check(Position().X > start.X, "fresh gameplay movement resumes");
        Pad("Pad.West"); Key("Return");
        ui.SetVisible(bag, false);
        Check(ui.ActiveInputMode == null, "hiding an underlying mode unwinds its children");
        ui.SetVisible(bag, true);
        Check(ui.PushInputMode(bag) && ui.PushInputMode(confirm), "panels can reopen");
        game.Current.Destroy(confirm);
        Check(ui.ActiveInputMode == bag && ui.Focused == manage, "destroying a modal restores the previous focus");
        game.Current.SetActive(bag, false);
        Check(ui.ActiveInputMode == null, "deactivating the last root restores gameplay");
        CheckWidgetCapture();
        Console.WriteLine("PASS KeyRoom input mode stack, held keys, keyboard/gamepad/pointer routing, focus and lifetime");
    }

    private static void CheckWidgetCapture()
    {
        var scene = new SceneDocument();
        var canvas = scene.CreateUi("Canvas", null);
        var scroll = scene.CreateUi("Scroll View", canvas);
        scroll.Width = 180; scroll.Height = 80;
        var field = scene.CreateUi("Text Field", scroll);
        for (int i = 0; i < 6; i++) scene.CreateUi("Button", scroll);
        var panel = scene.CreateUi("Group", canvas);
        panel.X = 300;
        panel.GetComponent<UiRectComponent>()!.InputModeRoot = true;
        var accept = scene.CreateUi("Button", panel);
        var ui = new ScreenUi(scene, null);
        var point = ui.Bounds(field.Id)!.Value.Location + new System.Drawing.SizeF(5, 5);
        ui.Pointer(true, point.X, point.Y);
        ui.Compose("ㅎ");
        if (!ui.PushInputMode(panel.Id) || ui.Focused != accept.Id || ui.Editing != null || ui.Preedit.Length != 0)
            throw new Exception("Opening a modal should end the prior field's composition and focus a modal control.");
        if (ui.CommitText("한") || ui.Pointer(false, point.X, point.Y) || ui.Wheel(point.X, point.Y, -1) || ui.ScrollOffset(scroll.Id) != 0)
            throw new Exception("Text input, mouse capture or wheel reached the covered view.");
        if (!ui.PopInputMode(panel.Id) || ui.Focused != field.Id || !ui.Wheel(point.X, point.Y, -1) || ui.ScrollOffset(scroll.Id) <= 0)
            throw new Exception("The previous field and scroll view should regain input.");
    }
}
