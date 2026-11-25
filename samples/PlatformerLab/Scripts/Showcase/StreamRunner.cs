using System;
using System.Drawing;
using System.Numerics;
using Engine.Core;

// PlatformerLab tile-streaming showcase: runs the hero along a ridge seven chunks long whose Ground layer streams (radius 1,
// following him). He jumps when the ground two cells ahead rises or is missing; that ground is read with TryGetTile, which only
// answers for chunks in memory, so the runner depends on streaming keeping the chunk ahead loaded. Each tick the corner panel
// shows which chunks IsTileChunkLoaded reports and how many. At EndX he stops under confetti. Raises "stream", "minimap".
public sealed class StreamRunner : GameBehaviour
{
    public Guid Ground, Count, Confetti;
    public Guid[] Lit = [];
    public float EndX = 3392, Speed = 150, Jump = 320;
    private const int Cell = 16, Chunk = 32, Rows = 32;
    private int shown = -1, mask = -1;
    private bool done;

    public override void FixedUpdate(ScriptTick tick)
    {
        var objects = tick.Objects;
        tick.Session.Set("stream");
        if (tick.Position.X > 200) tick.Session.Set("minimap");

        // Which chunks are in memory: a bit per chunk, the panel updated only when it changes.
        int now = 0, loaded = 0;
        for (int i = 0; i < Lit.Length; i++)
            if (objects.IsTileChunkLoaded(Ground, i, 0)) { now |= 1 << i; loaded++; }
        if (now != mask)
        {
            for (int i = 0; i < Lit.Length; i++) tick.Ui.SetVisible(Lit[i], (now & 1 << i) != 0);
            mask = now;
        }
        if (loaded != shown) { tick.Ui.SetText(Count, $"{loaded} / {Lit.Length}"); shown = loaded; }

        if (!objects.Motion.TryGetMotion(tick.ObjectId, out var body)) return;
        float x = tick.Position.X;
        float drive = Speed, jump = 0;
        if (x >= EndX)
        {
            drive = 0;
            if (!done && body.Grounded) { done = true; objects.EmitParticles(Confetti); jump = Jump * .6f; }
        }
        else if (body.Grounded)
        {
            int here = Top((int)((x + 11) / Cell), objects), ahead = Top((int)((x + 22) / Cell) + 2, objects);
            if (ahead < 0 || (here >= 0 && ahead < here)) jump = Jump;   // a pit or higher ground two cells ahead
        }
        objects.Motion.DriveCharacter(tick.ObjectId, drive, jump);
    }

    // The top ground row of a column, -1 for none (a pit, or a chunk that is not loaded).
    private int Top(int column, IGameObjects objects)
    {
        for (int y = 10; y < Rows; y++)
            if (objects.TryGetTile(Ground, new Point(column, y), out var info) && info.Tile != 0) return y;
        return -1;
    }
}
