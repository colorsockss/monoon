using System;
using System.Numerics;
using Engine.Core;
using KeyRoom.Rules;

public sealed class OpenChest : GameBehaviour
{
    public ComponentReference<PlayerMovement> Player;
    public Guid Lid;
    public PrefabReference Loot;
    public DataReference Rules;
    public bool SaveProgress;
    private float reach = LootRules.ChestReach;
    private bool opened;

    public override void OnCreate(ScriptLife life)
    {
        if (Rules.Id != Guid.Empty)
            reach = (float)life.Objects.ReadData(Rules).UpgradeTo(1).GetNumber("chestReach", reach);
        if (!SaveProgress || !life.Session.Has("chest")) return;
        opened = true;
        if (life.Objects.HasRestoredWorld) return;
        life.Objects.TrackForSave(Lid, WorldSaveFields.Active);
        life.Objects.SetActive(Lid, false);
        if (!life.Session.Has(LootRules.CoinFlag) && life.Objects.TryGetWorldMatrix(life.ObjectId, out var world))
            SpawnLoot(life.Objects, life.ObjectId, Vector2.Transform(new Vector2(52, -5), world));
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        if (opened || !tick.Input.InteractPressed || Player.Resolve(tick.Objects) == null ||
            !tick.Objects.TryGetPosition(Player.ObjectId, out var player) ||
            Vector2.DistanceSquared(tick.Position, player) > reach * reach) return;
        SpawnLoot(tick.Objects, tick.ObjectId, tick.LocalToWorld(new Vector2(52, -5)));
        if (SaveProgress) tick.Objects.TrackForSave(Lid, WorldSaveFields.Active);
        tick.Objects.SetActive(Lid, false);
        if (SaveProgress) tick.Session.Set("chest");
        opened = true;
    }

    private void SpawnLoot(IGameObjects objects, Guid chest, Vector2 at)
    {
        var coin = objects.Instantiate(Loot, at, chest);
        if (SaveProgress) objects.TrackForSave(coin);
    }
}
