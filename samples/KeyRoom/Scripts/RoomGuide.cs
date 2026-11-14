using Engine.Core;

public sealed class RoomGuide : GameBehaviour
{
    public SceneReference Guide;

    public override void UpdateUi(UiTick tick)
    {
        if (!tick.Input.Pressed("RoomGuide") || tick.Session.PendingScene != null) return;
        if (tick.Session.IsSceneLoaded(Guide.Id)) tick.Session.UnloadScene(Guide.Id);
        else tick.Session.AddSceneAsync(Guide.Id);
    }
}
