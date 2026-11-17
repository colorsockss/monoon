using Engine.Core;

public sealed class CrateButton : GameBehaviour
{
    private int clicks;

    public override void OnUiClick(UiClick click)
    {
        click.Ui.SetText(click.ObjectId, $"UI clicks: {++clicks}");
        click.Ui.ClearFocus();
    }
}
