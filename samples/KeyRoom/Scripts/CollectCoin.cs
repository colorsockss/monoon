using Engine.Core;
using KeyRoom.Rules;

public sealed class CollectCoin : GameBehaviour
{
    public TableReference LootTable;
    private string pickupFlag = LootRules.CoinFlag;
    private bool available, collected;
    public override void OnCreate(ScriptLife life)
    {
        if (LootTable.Id != System.Guid.Empty)
            pickupFlag = life.Objects.ReadTable(LootTable).Row("coin").GetText("flag", LootRules.CoinFlag);
        if (life.Objects.GetComponent<BoxCollider2DComponent>(life.ObjectId)?.IsTrigger != true)
            throw new System.InvalidOperationException("Coin needs a trigger collider.");
    }

    public override void OnEnable(ScriptLife life) => available = true;
    public override void OnDisable(ScriptLife life) => available = false;

    public override void OnTriggerEnter(ScriptContact contact)
    {
        if (!available || collected || contact.Objects.GetComponent<PlayerMovement>(contact.OtherId) == null) return;
        collected = true;
        contact.Objects.Destroy(contact.ObjectId);
    }

    public override void OnDestroy(ScriptLife life)
    {
        if (collected) { life.Session.Set(pickupFlag); life.Session.Log("Coin collected."); }
    }
}
