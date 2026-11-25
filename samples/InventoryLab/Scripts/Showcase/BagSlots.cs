using System;
using System.Numerics;
using Engine.Core;

// Showcase: a 4×5 bag of draggable UI Button slots. While a slot is picked up (ScreenUi.Carried: pointer drag, or click / Enter /
// pad A on it) its item leaves the slot and rides on a floating copy, Ghost: under the pointer, or with keys and a pad over
// the focused slot (arrows / D-pad move the focus, Enter / A drops, Escape / B cancels). Dropped
// on another slot it settles in there, and that slot's item flies back on GhostBack; the slots swap when both land and the
// one it landed in pops. Raises the "drag" HUD tag flag.
public sealed class BagSlots : GameBehaviour
{
    public Guid Ghost, GhostBack;
    public float Settle = .15f, Return = .45f;
    public float Lift = 10;              // how far a key-carried item hovers above the focused slot
    private Guid source, target;
    private Guid? carrying;              // the slot whose item rides on Ghost
    private string from = "", to = "";
    private ScriptTween fly, back;
    private bool flying;
    private System.Drawing.RectangleF? home;   // the ghosts' own laid-out box (they rest hidden, without an offset)

    public override void OnEnable(ScriptLife life)
    {
        life.Ui.SetVisible(Ghost, false);
        life.Ui.SetVisible(GhostBack, false);
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        var (ui, objects) = (tick.Ui, tick.Objects);
        if (flying)
        {
            if (!fly.IsPlaying && !back.IsPlaying) Land(ui, objects);
            return;
        }
        home ??= ui.Bounds(Ghost);
        var carried = (ui as ScreenUi)?.Carried;
        // Picked up: the item leaves its slot and rides on the ghost.
        if (carrying == null && carried is Guid slot && objects.TryGetParent(slot, out var parent) && parent == tick.ObjectId &&
            ui.TextOf(slot) is { Length: > 0 } item)
        {
            (carrying, from) = (slot, item);
            ui.SetText(slot, "");
            ui.SetText(Ghost, item);
            ui.SetVisible(Ghost, true);
        }
        if (carrying is not Guid held) return;
        if (carried == null)
        {
            // Put down without a drop (Escape, or released off the bag): back into its own slot.
            ui.SetText(held, from);
            ui.SetVisible(Ghost, false);
            carrying = null;
            return;
        }
        if (home is { } box && ui.Bounds(Ghost) is { } size)
        {
            Vector2 at;
            if (tick.Input.ActiveDevice == InputDevice.Pointer) at = tick.Input.PointerPosition - new Vector2(size.Width, size.Height) / 2;
            else if (ui.Focused is Guid focus && ui.Bounds(focus) is { } over) at = new(over.X, over.Y - Lift);
            else return;
            objects.Tween(Ghost, TweenClock.Ui).UiRect(new(at.X - box.X, at.Y - box.Y, 0, 0), .001f, TweenEase.Linear);
        }
    }

    public override void OnUiDrop(UiDrop drop)
    {
        var ui = drop.Ui;
        if (flying) Land(ui, drop.Objects);
        (source, target) = (drop.Source, drop.Target);
        if (carrying != source) from = ui.TextOf(source) ?? "";   // a drop the pointer-follow did not see start
        carrying = null;
        to = ui.TextOf(target) ?? "";
        ui.SetText(source, "");
        ui.SetText(target, "");
        fly = Fly(ui, drop.Objects, Ghost, from, null, target, Settle);
        back = to.Length > 0 ? Fly(ui, drop.Objects, GhostBack, to, target, source, Return) : default;
        flying = true;
        drop.Session.Set("drag");
    }

    // Moves a ghost onto slot b, from slot a or (a == null) from where it already is.
    private ScriptTween Fly(IGameUi ui, IGameObjects objects, Guid ghost, string text, Guid? a, Guid b, float seconds)
    {
        if (home is not { } box || ui.Bounds(b) is not { } end) return default;
        ui.SetText(ghost, text);
        ui.SetVisible(ghost, true);
        var tween = objects.Tween(ghost, TweenClock.Ui);
        if (a is Guid start && ui.Bounds(start) is { } at) tween = tween.UiRect(new(at.X - box.X, at.Y - box.Y, 0, 0), .001f, TweenEase.Linear);
        return tween.UiRect(new(end.X - box.X, end.Y - box.Y, 0, 0), seconds, TweenEase.EaseOut);
    }

    private void Land(IGameUi ui, IGameObjects objects)
    {
        flying = false;
        ui.SetText(target, from);
        ui.SetText(source, to);
        foreach (var ghost in (ReadOnlySpan<Guid>)[Ghost, GhostBack])
        {
            ui.SetVisible(ghost, false);
            objects.Tween(ghost, TweenClock.Ui).UiRect(default, .001f, TweenEase.Linear);
        }
        objects.Tween(target, TweenClock.Ui).UiRect(new(-5, -5, 10, 10), .07f, TweenEase.EaseOut).UiRect(default, .14f, TweenEase.EaseIn);
    }
}
