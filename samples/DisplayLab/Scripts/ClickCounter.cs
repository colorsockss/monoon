using Engine.Core;

public sealed class ClickCounter : GameBehaviour
{
    private int clicks;
    public override void OnUiClick(UiClick click)
    {
        clicks++;
        click.Ui.SetText(click.ObjectId, $"Clicks: {clicks}");
        click.Session.Log($"Clicks: {clicks}");
    }
}
