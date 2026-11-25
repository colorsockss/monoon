using System;
using Engine.Core;

// Showcase: a small picture of the guard's animator. States are UI boxes (StateBoxes, in the order of StateNames); the
// gold Highlight frame moves to the box of the current state, and Params shows the parameter values the transitions test.
public sealed class StateGraph : GameBehaviour
{
    public Guid Animated, Highlight, Params;
    public Guid[] StateBoxes = [];
    public string StateNames = "Idle,Walk,Attack,Recover,Hurt";
    private string[] names = [];
    private string state = "";
    private System.Drawing.RectangleF? home;
    private string shown = "";

    public override void OnEnable(ScriptLife life) => names = StateNames.Split(',');

    public override void FixedUpdate(ScriptTick tick)
    {
        if (tick.Objects.Animator(Animated) is not { } animator) return;
        var ui = tick.Ui;
        home ??= ui.Bounds(Highlight);
        if (animator.State != state && Array.IndexOf(names, animator.State) is var i and >= 0 && i < StateBoxes.Length &&
            home is { } box && ui.Bounds(StateBoxes[i]) is { } target)
        {
            state = animator.State;
            tick.Objects.Tween(Highlight, TweenClock.Ui).UiRect(new(target.X - box.X, target.Y - box.Y, 0, 0), .12f, TweenEase.EaseOut);
        }
        string text = $"State <b><color=#FFD84A>{animator.State}</color></b>    frame {animator.Frame + 1}";
        if (text != shown) ui.SetText(Params, shown = text);
    }
}
