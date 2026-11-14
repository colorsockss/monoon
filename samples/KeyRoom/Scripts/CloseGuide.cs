using Engine.Core;

public sealed class CloseGuide : GameBehaviour
{
    public SceneReference Guide;
    public override void OnUiClick(UiClick click) => click.Session.UnloadScene(Guide.Id);
}
