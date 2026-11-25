using System;
using System.Drawing;
using System.Numerics;
using Engine.Core;

// KeyRoom input showcase. Charge, Flick and Dash are ordinary actions whose GameSettings entries carry an interaction
// (Hold 30 ticks, Tap within 12, DoubleTap within 15); Triggered reports them on the deciding tick. The trainer fills a hold
// meter, logs what each press did, and plays the move. K rebinds Dash: the next key pressed replaces O and the row shows it.
// Raises "interact" on the first triggered move and "rebind" after a rebind.
public sealed class MoveTrainer : GameBehaviour
{
    public Guid Hero, Dummy, Burst, Meter, Log;
    public Guid[] Keys = [], Names = [];
    private static readonly string[] Actions = ["Charge", "Flick", "Dash"];
    private const float MeterWidth = 304, HeroX = 400, DummyX = 600;
    private readonly float[] lit = new float[3];
    private int held, pressTicks, lastDashPress = -100, tick;
    private bool rebinding, interacted;

    public override void OnCreate(ScriptLife life)
    {
        for (int i = 0; i < Actions.Length; i++) ShowKey(life.Session, life.Ui, i);
    }

    public override void FixedUpdate(ScriptTick t)
    {
        tick++;
        var session = t.Session;
        var ui = t.Ui;
        var input = t.Input;
        if (rebinding)
        {
            if (session.RebindingAction != null) return;
            rebinding = false;
            var result = session.LastRebind;
            ShowKey(session, ui, 2);
            if (result is { Cancelled: false }) { session.Set("rebind"); Say(ui, $"Dash is now {result.Control} (was {result.Previous}). Double-tap it."); }
            else Say(ui, "Rebinding cancelled.");
            return;
        }
        if (input.Pressed("KeyBindings"))
        {
            session.BeginRebind("Dash", InputDevice.Keyboard);
            rebinding = true;
            ui.SetText(Keys[2], "...");
            Say(ui, "Rebinding Dash: press a new key.");
            return;
        }

        // Charge: the meter shows how long J has been held; Triggered fires once at the 30th tick.
        if (input.Released("Charge") && held is > 0 and < 30) Say(ui, $"Released J after {held} ticks: no charge yet.");
        held = input.Held("Charge") ? held + 1 : 0;
        t.Objects.Tween(Meter, TweenClock.Ui).UiRect(new RectangleF(0, 0, MathF.Min(held, 30) / 30f * MeterWidth, 0), .02f, TweenEase.Linear);
        if (input.Triggered("Charge"))
        {
            Light(ui, 0);
            t.Objects.EmitParticles(Burst);
            t.Objects.Tween(Dummy, TweenClock.Game).Move(new Vector2(DummyX + 18, Pos(t, Dummy).Y), .08f, TweenEase.EaseOut).Move(new Vector2(DummyX, Pos(t, Dummy).Y), .3f);
            Say(ui, "Charge: J held for 30 ticks.");
        }

        // Flick: a tap counts only if the key comes up within 12 ticks.
        if (input.Pressed("Flick")) pressTicks = 0;
        if (input.Held("Flick")) pressTicks++;
        if (input.Triggered("Flick"))
        {
            Light(ui, 1);
            float y = Pos(t, Hero).Y;
            t.Objects.Tween(Hero, TweenClock.Game).Move(new Vector2(HeroX + 14, y), .06f, TweenEase.EaseOut).Move(new Vector2(HeroX, y), .18f);
            Say(ui, "Flick: U released within 12 ticks.");   // a fast key can go down and up in one tick, so no count here
        }
        else if (input.Released("Flick") && pressTicks > 12) Say(ui, $"Held U for {pressTicks} ticks: too long for a tap.");

        // Dash: two presses within 15 ticks.
        bool pressed = input.Pressed("Dash");
        if (input.Triggered("Dash"))
        {
            Light(ui, 2);
            float y = Pos(t, Hero).Y;
            t.Objects.Tween(Hero, TweenClock.Game).Move(new Vector2(HeroX + 130, y), .14f, TweenEase.EaseOut).Delay(.25f).Move(new Vector2(HeroX, y), .6f);
            Say(ui, $"Dash: second tap {tick - lastDashPress} ticks after the first.");
            lastDashPress = -100;
        }
        else if (pressed)
        {
            if (tick - lastDashPress <= 40 && tick - lastDashPress > 15) Say(ui, $"Second tap {tick - lastDashPress} ticks later: too slow, no dash.");
            lastDashPress = tick;
        }
        if (interacted) session.Set("interact");
        for (int i = 0; i < lit.Length; i++)
            if (lit[i] > 0 && (lit[i] -= t.DeltaTime) <= 0) ui.SetText(Names[i], $"<b>{Actions[i]}</b>");
    }

    private static Vector2 Pos(ScriptTick t, Guid id) => t.Objects.TryGetPosition(id, out var p) ? p : Vector2.Zero;

    private void Light(IGameUi ui, int i)
    {
        interacted = true;
        lit[i] = .6f;
        ui.SetText(Names[i], $"<b><color=#FFD84A>{Actions[i]}</color></b>");
    }

    private void Say(IGameUi ui, string text) => ui.SetText(Log, text);

    private void ShowKey(SessionState session, IGameUi ui, int i) =>
        ui.SetText(Keys[i], session.BindingFor(Actions[i], InputDevice.Keyboard) ?? "-");
}
