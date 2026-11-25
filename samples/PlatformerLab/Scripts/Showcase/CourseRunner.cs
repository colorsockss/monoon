using System;
using System.Numerics;
using Engine.Core;

// PlatformerLab motion showcase: drives the capsule runner through the course without input. It walks right, jumps from the
// sinking pulley deck to the rising one and from there to the ledge, stands still on the path lift (which it starts with PlayTween) until the lift has
// arrived, then walks over the chain hill to the end. The pulley ropes follow the decks. Raises the HUD flags as it goes.
public sealed class CourseRunner : GameBehaviour
{
    public Guid Lift, DeckA, DeckB, RopeA, RopeB;
    public float Speed = 150, Jump = 360, JumpAt = 514, SecondJumpAt = 636, LiftEndX = 1320, StopAt = 2240;
    private bool jumped, jumpedAgain, liftStarted, liftDone;
    private readonly Vector2[] rope = new Vector2[2];

    public override void FixedUpdate(ScriptTick tick)
    {
        var objects = tick.Objects;
        var motion = objects.Motion;
        if (!motion.TryGetMotion(tick.ObjectId, out var body)) return;
        float x = tick.Position.X;
        var session = tick.Session;
        session.Set("rig");
        if (x > 180) session.Set("bridge");
        if (x > 460) session.Set("pulley");
        if (x > 1640) session.Set("chain");

        float drive = Speed, jump = 0;
        if (!jumped && x >= JumpAt && body.Support is Guid) { jump = Jump; jumped = true; }   // from the sinking deck to the rising one
        if (!jumpedAgain && x >= SecondJumpAt && body.Support == DeckB) { jump = Jump; jumpedAgain = true; }   // and from that deck, now sinking, to the ledge
        if (body.Support == Lift && !liftDone)
        {
            if (!liftStarted) { liftStarted = objects.PlayTween(Lift); session.Set("path"); }
            objects.TryGetPosition(Lift, out var lift);
            liftDone = lift.X >= LiftEndX - .5f;
            drive = liftDone ? Speed : 0;
        }
        if (x >= StopAt) drive = 0;
        motion.DriveCharacter(tick.ObjectId, drive, jump);

        // Ropes from the wheel to each deck's centre.
        Rope(objects, RopeA, DeckA, new Vector2(562, 146));
        Rope(objects, RopeB, DeckB, new Vector2(582, 146));
    }

    private void Rope(IGameObjects objects, Guid line, Guid deck, Vector2 wheel)
    {
        if (!objects.TryGetPosition(deck, out var at)) return;
        rope[0] = wheel;
        rope[1] = at + new Vector2(35, 0);
        objects.SetLinePoints(line, rope);
    }
}
