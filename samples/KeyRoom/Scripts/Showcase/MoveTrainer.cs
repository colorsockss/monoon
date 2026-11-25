using System;
using System.Drawing;
using System.IO;
using System.Numerics;
using Engine.Core;

// KeyRoom input showcase. Charge, Flick and Dash are ordinary actions whose GameSettings entries carry an interaction
// (Hold 30 ticks, Tap within 12, DoubleTap within 15); Triggered reports them on the deciding tick, and each one does something
// in the room: Charge fills a meter over the hero and smashes the crate in front of him, Flick throws a coin at the dummy,
// Dash dashes at the dummy and knocks it back. A press that misses its rule says why in a word over the hero's head.
// K opens the controls menu (ShowcaseBindings); the caption follows the current keys. Raises "interact" on the first triggered
// move and "rebind" after a rebind. Controls saved in player.settings by the menu are loaded on start.
public sealed class MoveTrainer : GameBehaviour
{
    public Guid Player, Hero, Crate, CrateShadow, CrateDust, CrateSpark, Dummy, DummySpark, Coin, MeterBack, Meter, Label, Controls, DashTrail;
    // The hero's sprites: he has no animator here (it only turns him while walking), so the trainer picks the frame.
    public SpriteReference IdleE, WalkE1, WalkE2, WalkE3, WalkE4, WalkW1, WalkW2, WalkW3, WalkW4;
    private static readonly Vector2 Home = new(170, 200), DashTo = new(380, 200);
    private Vector2 dummyHome;
    private int held, pressTicks, lastDashPress = -100, tick, revision = -1;
    private float labelLeft, coinLeft = -1, crateLeft = -1, dashLeft = -1, returnLeft = -1, walkClock;
    private bool crateUp = true;

    public override void OnCreate(ScriptLife life)
    {
        life.Objects.TryGetPosition(Dummy, out dummyHome);
        life.Objects.SetSprite(Hero, IdleE);
        if (!life.Session.Storage.IsAvailable) return;
        try
        {
            if (life.Session.Storage.LoadSettings(1) is { } settings) life.Session.LoadInputBindings(settings);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        { life.Session.Log(ex.Message, GameLogLevel.Error); }
    }

    public override void FixedUpdate(ScriptTick t)
    {
        tick++;
        float dt = t.DeltaTime;
        var session = t.Session;
        var objects = t.Objects;
        var input = t.Input;
        Timers(t, dt);
        if (revision != session.InputRevision)   // a rebind, a restore or saved controls: the caption shows the keys in use
        {
            revision = session.InputRevision;
            string Key(string action) => session.BindingFor(action, InputDevice.Keyboard) ?? "-";
            t.Ui.SetText(Controls, $"Charge  hold {Key("Charge")}      Flick  tap {Key("Flick")}      Dash  {Key("Dash")} {Key("Dash")}      Controls  {Key("KeyBindings")}");
            if (session.LastRebind is { Cancelled: false }) session.Set("rebind");
        }

        // Charge: the meter over the hero fills while J is held; Triggered fires once on the 30th tick.
        if (input.Released("Charge") && held is > 0 and < 30) Say(objects, "Hold longer", false);
        held = input.Held("Charge") ? held + 1 : 0;
        objects.SetActive(MeterBack, held > 0 && held <= 30);
        objects.SetLocalTransform(Meter, new LocalTransform2D(new Vector2(1, 1), 0, new Vector2(Math.Min(held, 30) / 30f, 1)));
        if (input.Triggered("Charge")) Charge(t);

        // Flick: a tap counts only if the key comes up within 12 ticks.
        if (input.Pressed("Flick")) pressTicks = 0;
        if (input.Held("Flick")) pressTicks++;
        if (input.Triggered("Flick")) Flick(t);
        else if (input.Released("Flick") && pressTicks > 12) Say(objects, "Too long for a tap", false);

        // Dash: two presses within 15 ticks.
        bool pressed = input.Pressed("Dash");
        if (input.Triggered("Dash")) { DashMove(t); lastDashPress = -100; }
        else if (pressed)
        {
            if (tick - lastDashPress is > 15 and <= 50) Say(objects, "Too slow", false);
            lastDashPress = tick;
        }
    }

    private void Charge(ScriptTick t)
    {
        var objects = t.Objects;
        Moved(t.Session);
        objects.Tween(Player).Move(Home + new Vector2(16, 0), .06f, TweenEase.EaseOut).Move(Home, .2f);
        if (crateUp)
        {
            crateUp = false;   // it shudders, then bursts into chips (Timers)
            objects.Tween(Crate).Rotate(12, .05f, TweenEase.EaseOut).Rotate(-9, .05f).Rotate(5, .04f);
            crateLeft = .14f;
        }
        else t.Session.Screen.Shake(3, .2f);
        Say(objects, "Charge!", true);
    }

    private void Flick(ScriptTick t)
    {
        var objects = t.Objects;
        Moved(t.Session);
        objects.TryGetPosition(Player, out var at);
        objects.SetLocalTransform(Coin, new LocalTransform2D(at + new Vector2(18, -34), 0, Vector2.One));
        objects.SetActive(Coin, true);
        var to = dummyHome + new Vector2(20, 22);
        var from = at + new Vector2(18, -34);
        objects.Tween(Coin).Move((from + to) / 2 - new Vector2(0, 46), .24f, TweenEase.EaseOut).Move(to, .22f, TweenEase.EaseIn);   // an arc
        coinLeft = .46f;
        Say(objects, "Flick!", true);
    }

    private void DashMove(ScriptTick t)
    {
        var objects = t.Objects;
        Moved(t.Session);
        objects.SetActive(DashTrail, true);   // the trail exists only for the dash itself
        objects.Tween(Player).Move(DashTo, .16f, TweenEase.EaseOut);
        objects.SetSprite(Hero, WalkE2);
        dashLeft = .16f;
        returnLeft = -1;
        Say(objects, "Dash!", true);
    }

    private void Timers(ScriptTick t, float dt)
    {
        var objects = t.Objects;
        if (labelLeft > 0 && (labelLeft -= dt) <= 0) objects.SetWorldText(Label, "");
        if (crateLeft >= 0 && (crateLeft -= dt) < 0)
        {
            objects.SetActive(Crate, false);
            objects.SetActive(CrateShadow, false);
            objects.EmitParticles(CrateDust);
            objects.EmitParticles(CrateSpark);
            t.Session.Screen.Shake(6, .3f);
        }
        if (coinLeft >= 0 && (coinLeft -= dt) < 0)
        {
            objects.SetActive(Coin, false);
            objects.EmitParticles(DummySpark);
            objects.Tween(Dummy).Rotate(9, .06f, TweenEase.EaseOut).Rotate(-6, .1f).Rotate(0, .12f);
        }
        if (dashLeft >= 0 && (dashLeft -= dt) < 0)
        {
            objects.EmitParticles(DummySpark);
            objects.Tween(Dummy).Move(dummyHome + new Vector2(24, 0), .08f, TweenEase.EaseOut).Move(dummyHome, .35f);
            t.Session.Screen.Shake(4, .2f);
            returnLeft = .9f;
        }
        if (returnLeft >= 0)
        {
            returnLeft -= dt;
            if (returnLeft is < .55f and >= 0 && returnLeft + dt >= .55f)
            {
                objects.SetActive(DashTrail, false);   // clears it, so the walk back draws none
                objects.Tween(Player).Move(Home, .55f, TweenEase.Linear);
            }
            if (returnLeft is < .55f and >= 0)   // walking back west
            {
                int step = (int)((walkClock += dt) / .1f) % 4;
                objects.SetSprite(Hero, step switch { 0 => WalkW1, 1 => WalkW2, 2 => WalkW3, _ => WalkW4 });
            }
            if (returnLeft < 0) objects.SetSprite(Hero, IdleE);
        }
        else if (dashLeft < 0) objects.SetSprite(Hero, IdleE);
    }

    private void Say(IGameObjects objects, string word, bool good)
    {
        objects.SetWorldText(Label, good ? $"<color=#FFD84A><b>{word}</b></color>" : $"<color=#FFB4A8>{word}</color>");
        labelLeft = .9f;
    }

    private static void Moved(SessionState session) => session.Set("interact");
}
