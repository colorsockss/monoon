using Engine.Core;

public sealed class CatalogButton : GameBehaviour
{
    public ComponentReference<CatalogScreen> Screen;
    public int Order = -1;

    public override void OnUiClick(UiClick click)
    {
        if (Order >= 0) Screen.Resolve(click.Objects).Sort(Order, click.Ui);
        else click.Session.LoadScene(Screen.Resolve(click.Objects).Inventory.Id);
    }
}
