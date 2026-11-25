using System;
using System.Numerics;
using Engine.Core;

// Showcase: flies the ship with the arrow keys inside an area and fires bolts. P (Mode) switches between the prefab pool
// (Rent; PooledBullet returns them) and Instantiate / Destroy; BoltInfo shows how many bolt objects each way creates.
// R restarts the run and P restarts it in the other mode; both first log a report of the run that ends (bolts fired, drone hits
// and exits, live bolts, objects created and reused), so the two modes can be compared on the same ticks.
// It also raises the HUD tag flags: "barrage" from the start, "bolts" on the first volley, "compare" when B is pressed.
public sealed class ShipPilot : GameBehaviour
{
    public PrefabReference Bolt;
    public Guid BoltInfo;
    public float Speed = 170, BoltSpeed = 520;
    public int FireTicks = 7;
    public Vector2 Min = new(20, 60), Max = new(300, 340), Muzzle = new(46, 12);
    private int tickCount, instantiated, fired;
    private bool pooled = true;

    public override void OnCreate(ScriptLife life) => life.Session.Set("barrage");

    public override void FixedUpdate(ScriptTick tick)
    {
        var next = Vector2.Clamp(tick.Position + tick.Direction * Speed * tick.DeltaTime, Min, Max);
        tick.Move(next - tick.Position);
        if (tick.Input.Pressed("Compare")) tick.Session.Set("compare");
        if (tick.Input.Pressed("Mode") || tick.Input.Pressed("Restart"))
        {
            Report(tick.Session, tick.Objects);
            if (tick.Input.Pressed("Mode")) pooled = !pooled;
            Restart(tick.Objects);
            Show(tick);
        }
        if (++tickCount % FireTicks != 0) return;
        Guid id;
        if (pooled) id = tick.Objects.Rent(Bolt, next + Muzzle);
        else { id = tick.Objects.Instantiate(Bolt, next + Muzzle); instantiated++; }
        tick.Objects.GetComponent<PooledBullet>(id)!.Launch(new Vector2(BoltSpeed, 0));
        fired++;
        tick.Session.Set("bolts");
        Show(tick);
    }

    private void Restart(IGameObjects objects)
    {
        foreach (var id in objects.FindByGroup("Bullets"))
            if (!objects.Return(id)) objects.Destroy(id);
        foreach (var id in objects.FindByGroup("Drones")) objects.GetComponent<Drone>(id)!.Restart(objects, id);
        tickCount = fired = 0;
    }

    private void Report(SessionState session, IGameObjects objects)
    {
        int hits = 0, exits = 0;
        foreach (var id in objects.FindByGroup("Drones"))
            if (objects.GetComponent<Drone>(id) is { } drone) (hits, exits) = (hits + drone.Hits, exits + drone.Exits);
        var pool = objects.PoolStats(Bolt);
        session.Log($"BarrageLab bolts mode={(pooled ? "pool" : "instantiate")} ticks={tickCount} fired={fired} hits={hits} exits={exits} " +
            $"active={objects.FindByGroup("Bullets").Count} created={pool.Created + instantiated} reused={pool.Reused} returned={pool.Returned}");
    }

    private void Show(ScriptTick tick)
    {
        if (BoltInfo == Guid.Empty) return;
        var pool = tick.Objects.PoolStats(Bolt);
        tick.Ui.SetText(BoltInfo, pooled
            ? $"<b>Bolts</b>   Rent / Return     <color=#BFD0DC>created {pool.Created}   reused {pool.Reused:N0}</color>"
            : $"<b>Bolts</b>   <color=#FF9A7A>Instantiate / Destroy</color>     <color=#BFD0DC>created {pool.Created + instantiated:N0}</color>");
    }
}
