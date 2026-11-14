using Engine.Core;

public sealed class ToggleControls : GameBehaviour
{
    public override void OnUiClick(UiClick click)
    {
        if (!click.Session.Clear("controls")) click.Session.Set("controls");
    }
}
