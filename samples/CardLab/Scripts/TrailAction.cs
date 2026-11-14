using Engine.Core;

public sealed class TrailAction : GameBehaviour
{
    public ComponentReference<TrailTable> Table;
    public string Command;
    public int Slot;

    public override void OnUiClick(UiClick click)
    {
        var table = Table.Resolve(click.Objects);
        switch (Command)
        {
            case "draw": table.Draw(click.Ui); break;
            case "play": table.Play(Slot, click.Ui); break;
            case "restart": table.Restart(false, click.Ui); break;
            case "next": table.Restart(true, click.Ui); break;
            case "save": table.Store(click.Session.Storage, true, click.Ui); break;
            case "load": table.Store(click.Session.Storage, false, click.Ui); break;
        }
    }
}
