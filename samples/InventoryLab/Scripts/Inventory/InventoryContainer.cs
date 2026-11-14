using Engine.Core;

namespace MoNoon.Inventory;

public sealed class InventoryContainer : GameBehaviour
{
    public TableReference Catalog;
    public int Slots = 3;
    public InventoryBag Bag { get; private set; }

    public override void OnCreate(ScriptLife life) =>
        Bag = new InventoryBag(life.Objects.ReadTable(Catalog), Slots);
}
