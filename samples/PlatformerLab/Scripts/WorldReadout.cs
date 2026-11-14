using System;
using System.Numerics;
using Engine.Core;

public sealed class WorldReadout : GameBehaviour
{
    public Guid Readout;
    private bool initialized, sawStart, outbound, returned;
    private long lastLoads = -1, lastUnloads;
    private bool Solid(ScriptTick tick, float localX)
    {
        var p = tick.LocalToWorld(new(localX, -3));
        return tick.Objects.Physics.Raycast(p,Vector2.UnitY,8,new PhysicsFilter(1u<<3))?.Target.ObjectId == tick.ObjectId;
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        if (!tick.Objects.TryGetTileStream(tick.ObjectId,out var s)) return;
        if (!initialized)
        {
            sawStart = s.Center.X == 0 && Solid(tick,4) && !Solid(tick,3*256+4);
            initialized = true;
        }
        if (s.Loads == lastLoads && s.Unloads == lastUnloads) return;
        lastLoads = s.Loads; lastUnloads = s.Unloads;
        bool startSolid = Solid(tick,4), farSolid = Solid(tick,3*256+4);
        bool startLoaded = tick.Objects.IsTileChunkLoaded(tick.ObjectId,0,0);
        bool farLoaded = tick.Objects.IsTileChunkLoaded(tick.ObjectId,3,0);
        tick.Ui.SetText(Readout,$"Chunk {s.Center.X}, {s.Center.Y}     Active {s.Resident} / {s.Total}     Loaded {s.Loads}     Unloaded {s.Unloads}");
        if (!outbound && s.Center.X == 2) { outbound=true; Save(2, sawStart && !startLoaded && farLoaded && !startSolid && farSolid); }
        else if (outbound && !returned && s.Center.X == 0) { returned=true; Save(3, sawStart && startLoaded && !farLoaded && startSolid && !farSolid); }

        void Save(int slot, bool passed)
        {
            var data=new GameData();
            data.Set("passed",passed); data.Set("center",s.Center.X); data.Set("resident",s.Resident);
            data.Set("total",s.Total); data.Set("loads",s.Loads); data.Set("unloads",s.Unloads);
            data.Set("originLoaded",startLoaded); data.Set("farLoaded",farLoaded);
            data.Set("originCollision",startSolid); data.Set("farCollision",farSolid);
            if(tick.Session.Storage.IsAvailable) tick.Session.Storage.SaveSlot(slot,data);
        }
    }
}
