using Engine.Core;
using System;

public sealed class GuideTextSize : GameBehaviour
{
    public bool Increase;
    private float lastScale = -1;

    public override void UpdateUi(UiTick tick)
    {
        float scale = tick.Ui.Preferences.TextScale;
        if (lastScale == scale) return;
        lastScale = scale;
        tick.Ui.SetInteractable(tick.ObjectId, Increase ? scale < 2 : scale > 1);
    }

    public override void OnUiClick(UiClick click)
    {
        var preferences = click.Ui.Preferences;
        preferences.TextScale = Math.Clamp(preferences.TextScale + (Increase ? .5f : -.5f), 1, 2);
    }
}
