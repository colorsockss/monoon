using System.Collections.Generic;
using Engine.Core;

// The input showcase: hold, tap and double-tap interactions from GameSettings, and the K controls menu rebinding Dash.
public sealed class MoveTests
{
    private const string Moves = "Scenes/ShowcaseMoves.scene";

    [GameTest(Moves)]
    public IEnumerable<ScriptWait> ChargeNeedsThirtyHeldTicks(GameTestRun run)
    {
        run.Press("J");
        yield return ScriptWait.Frames(20);
        run.Assert(!run.Session.Has("interact"), "20 ticks is not a charge");
        yield return ScriptWait.Frames(15);
        run.Assert(run.Session.Has("interact"), "30 held ticks charge");
        yield return ScriptWait.Frames(12);   // the crate shudders for 0.14 s before it bursts
        run.Assert(!run.Objects.IsActive(run.Find("Target crate")), "the charge smashes the crate");
    }

    [GameTest(Moves)]
    public IEnumerable<ScriptWait> FlickIsAShortTap(GameTestRun run)
    {
        run.Press("U");
        yield return ScriptWait.Frames(20);
        run.Release("U");
        yield return ScriptWait.Frames(3);
        run.Assert(!run.Session.Has("interact"), "a 20-tick press is no tap");
        run.Tap("U");
        yield return ScriptWait.Frames(3);
        run.Assert(run.Session.Has("interact"), "a quick tap flicks");
    }

    [GameTest(Moves)]
    public IEnumerable<ScriptWait> DashNeedsTwoQuickTaps(GameTestRun run)
    {
        float x = run.PositionOf("Hero").X;
        run.Tap("O");
        yield return ScriptWait.Frames(25);
        run.Tap("O");
        yield return ScriptWait.Frames(3);
        run.Assert(!run.Session.Has("interact"), "taps 25 ticks apart do not dash");
        yield return ScriptWait.Frames(30);
        run.Tap("O");
        yield return ScriptWait.Frames(5);
        run.Tap("O");
        yield return ScriptWait.Frames(6);
        run.Assert(run.Session.Has("interact") && run.PositionOf("Hero").X > x + 40, "a quick double tap dashes the hero right");
    }

    [GameTest(Moves)]
    public IEnumerable<ScriptWait> ControlsMenuRebindsDash(GameTestRun run)
    {
        var menu = new System.Guid("cd60e7de-8966-4519-aabb-7209fb04fe2f");   // Scenes/ShowcaseBindings.scene
        run.Tap("K");
        yield return ScriptWait.Until(() => run.Session.IsSceneLoaded(menu));
        yield return ScriptWait.Frames(2);   // the menu focuses Charge's key cell
        run.Tap("Down");
        yield return ScriptWait.Frames(2);
        run.Tap("Down");
        yield return ScriptWait.Frames(2);
        run.Tap("Return");
        yield return ScriptWait.Frames(2);
        run.AreEqual("Dash", run.Session.RebindingAction, "Return on Dash's key cell waits for a key");
        run.Tap("P");
        yield return ScriptWait.Frames(3);
        run.AreEqual("P", run.Session.BindingFor("Dash", InputDevice.Keyboard), "dash key after rebinding");
        run.Assert(run.Session.Has("rebind"), "the rebind is announced");
        run.Tap("K");
        yield return ScriptWait.Until(() => !run.Session.IsSceneLoaded(menu));
        run.Tap("P");
        yield return ScriptWait.Frames(5);
        run.Tap("P");
        yield return ScriptWait.Frames(3);
        run.Assert(run.Session.Has("interact"), "the new key dashes");
    }
}
