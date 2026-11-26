using System;
using System.Collections.Generic;
using System.Drawing;
using Engine.Core;

public sealed class RealmLayoutTests
{
    [GameTest("Scenes/Realm.scene")]
    public IEnumerable<ScriptWait> CampaignAndCameraMoveInStages(GameTestRun run)
    {
        var board = run.Script<RealmBoard>("Board controller");
        CameraView View() => run.World.TryGetView(run.Find("Atlas camera"), out var view)
            ? view : throw new InvalidOperationException("Atlas camera is missing.");
        void Near(float expected, float actual, string measurement) =>
            run.Assert(MathF.Abs(expected - actual) < .001f, $"{measurement}: {actual}, expected {expected}");

        run.Tap("N");
        yield return ScriptWait.Frames(8);
        run.Assert(!board.InCampaign && run.Session.Screen.Current.Fade.A is > 0 and < 255,
            "title remains during the first half of the fade");
        yield return ScriptWait.Frames(24);
        run.Assert(board.InCampaign && run.Session.Screen.Current.Fade.A == 0,
            "campaign is visible after the fade");
        var atlas = View();
        Near(1.26f, atlas.Zoom, "atlas zoom");

        run.Tap("Equal");
        float previousZoom = atlas.Zoom;
        for (int i = 0; i < 27; i++)
        {
            yield return ScriptWait.Frames(1);
            var zoom = View().Zoom;
            run.Assert(zoom >= previousZoom - .001f && zoom - previousZoom < .04f,
                "detail zoom changes gradually");
            previousZoom = zoom;
        }
        var close = View();
        Near(1.62f, close.Zoom, "detail zoom");
        var scout = close.WorldToScreen(run.World.TileCellCenter(run.Find("Ground"), new Point(4, 5))!.Value);
        run.Click((int)scout.X, (int)scout.Y);
        yield return ScriptWait.Frames(8);
        var moving = View();
        yield return ScriptWait.Frames(19);
        var selected = View();
        run.Assert(close.Center.X < moving.Center.X && moving.Center.X < selected.Center.X,
            $"detail camera travels toward the selected unit: {close.Center.X}, {moving.Center.X}, {selected.Center.X}");
        Near(close.Zoom, selected.Zoom, "selection keeps zoom");

        run.Tap("Minus");
        yield return ScriptWait.Frames(8);
        run.Assert(View().Zoom is > 1.30f and < 1.60f, "the return is still in motion");
        yield return ScriptWait.Frames(19);
        Near(atlas.Zoom, View().Zoom, "return zoom");
        Near(atlas.Center.X, View().Center.X, "return center X");
        Near(atlas.Center.Y, View().Center.Y, "return center Y");
    }

    [GameTest("Scenes/Realm.scene")]
    public IEnumerable<ScriptWait> RowsAndKeycapsShareTheirAlignment(GameTestRun run)
    {
        var ui = run.World.Ui;
        RectangleF Box(string name) => ui.Bounds(run.Find(name))!.Value;
        void Near(float expected, float actual, string measurement) =>
            run.Assert(MathF.Abs(expected - actual) < 1, $"{measurement}: {actual}, expected {expected}");
        void AlignedCommand()
        {
            var card = Box("Command");
            var end = Box("End");
            Near(card.Left, end.Left, "end action and card left");
            Near(card.Width, end.Width, "end action and card width");
            Near(card.Bottom + 8, end.Top, "gap above end action");
            Near(card.Left + 52, Box("Selection").Left, "heading gutter");
            foreach (string name in new[] { "Command chips", "Command advice", "Context" })
                Near(Box("Selection").Left, Box(name).Left, $"{name} gutter");
        }
        void CenteredButton(string name, bool withSeal, int option = 0)
        {
            var button = run.Script<RealmButton>(name);
            var box = Box(name);
            var caption = Box(name + " caption");
            var chip = Box(name + " key");
            var key = Box(name + " key text");
            float scale = ui.Preferences.TextScale;
            float centerY = box.Top + box.Height / 2;
            Near(centerY, caption.Top + button.TextMetrics[option * 2 + 1] * scale, $"{name} caption baseline offset");
            Near(centerY, chip.Top + chip.Height / 2, $"{name} keycap center");
            Near(centerY, key.Top + button.KeyMetrics[option * 2 + 1] * scale, $"{name} key text center");
            float groupLeft = withSeal ? Box(name + " seal").Left : caption.Left;
            Near(box.Left + box.Width / 2, (groupLeft + chip.Right) / 2, $"{name} horizontal center");
            run.Assert(groupLeft >= box.Left && chip.Right <= box.Right, $"{name} content fits");
        }

        run.Tap("N");
        yield return ScriptWait.Frames(40);
        foreach (float scale in new[] { 1f, 2f })
        {
            ui.Preferences.TextScale = scale;
            yield return ScriptWait.Frames(12);
            AlignedCommand();
            CenteredButton("Context", false);
            CenteredButton("End", true);
            foreach (string name in new[] { "Turn", "Gold", "Land", "Lore" })
                Near(Box(name + " label").Left, Box(name + " value").Left, $"{name} column");
        }

        float settlerCardHeight = Box("Command").Height;
        run.Tap("B");
        yield return ScriptWait.Frames(12);
        AlignedCommand();
        run.Assert(Box("Command").Height > settlerCardHeight, "the city fits its extra growth row");
        CenteredButton("Context", false, option: 3);
        float cityTitleX = Box("Selection").Left;
        ui.FocusWidget(run.Find("Context"));
        yield return ScriptWait.Frames(3);
        Near(cityTitleX, Box("Selection").Left, "keyboard focus leaves the title in place");
    }
}
