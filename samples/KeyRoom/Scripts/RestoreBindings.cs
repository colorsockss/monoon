using Engine.Core;

public sealed class RestoreBindings : GameBehaviour
{
    public override void OnUiClick(UiClick click) => click.Session.RestoreDefaultBindings();
}
