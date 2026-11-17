using Engine.Core;

public sealed class InventoryAction : GameBehaviour
{
    public ComponentReference<InventoryWorkbench> Workbench;
    public string Command;
    public string Item;
    public int Slot;

    public override void OnCreate(ScriptLife life)
    {
        if (Command == "choose") life.Ui.SetVisible(life.ObjectId, false);
    }

    public override void OnUiClick(UiClick click)
    {
        var bench = Workbench.Resolve(click.Objects);
        switch (Command)
        {
            case "add": bench.Add(Item, click.Ui); break;
            case "select": bench.Select(Slot, click.Ui); break;
            case "use": bench.Use(click.Ui); break;
            case "quest": bench.AdvanceQuest(click.Ui); break;
            case "talk": bench.ToggleTalk(click.Ui); break;
            case "choose": bench.ChooseButton(click.ObjectId, click.Ui); break;
            case "more": bench.NextChoices(click.Ui); break;
            case "save": bench.Store(click.Session.Storage, click.Ui, true); break;
            case "load": bench.Store(click.Session.Storage, click.Ui, false); break;
        }
    }
}
