using System;
using Engine.Core;

// Showcase: while the camera pair is merged (the partner camera reports the main camera's viewport) the seam between the
// halves and the partner's name tag are hidden. Raises the "merge" HUD tag flag the first time the views join.
public sealed class SplitHud : GameBehaviour
{
    public Guid Main, Partner, Seam, Label;
    private bool? merged;

    public override void FixedUpdate(ScriptTick tick)
    {
        if (!tick.Cameras.TryGetView(Main, out var a) || !tick.Cameras.TryGetView(Partner, out var b)) return;
        bool now = a.Viewport == b.Viewport;
        if (now == merged) return;
        merged = now;
        tick.Ui.SetVisible(Seam, !now);
        tick.Ui.SetVisible(Label, !now);
        if (now) tick.Session.Set("merge");
    }
}
