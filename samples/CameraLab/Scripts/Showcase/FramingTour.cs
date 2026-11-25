using System;
using System.Numerics;
using Engine.Core;

// CameraLab framing showcase. First the Follow camera (deadzone 120 × 80, look-ahead 0.25 s): the hero shuffles inside the
// deadzone (the camera stays put), then runs east (the camera catches up and leads). At 12 s the Frame camera takes over,
// framing the group "Party": the partner walks in from far away to meet the hero (zoom in), then both walk apart to opposite
// corners (zoom out). Each walker feeds its picture's animator (Move vector, Speed). Raises "deadzone", "lead", "frame".
public sealed class FramingTour : GameBehaviour
{
    public Guid Player, Partner, PlayerPicture, PartnerPicture, Follow, Frame, Deadzone;
    private const float SwitchAt = 12f;
    // (start time, x, y, speed): each walker heads for the next target once its start time has come.
    private static readonly (float At, float X, float Y, float Speed)[] HeroPath =
    [
        (1.2f, 470, 520, 70), (2.0f, 400, 545, 70), (3.0f, 465, 500, 70), (3.8f, 420, 520, 70),
        (4.8f, 1180, 520, 165),
        (13.2f, 800, 540, 120), (19.2f, 240, 330, 150),
    ];
    private static readonly (float At, float X, float Y, float Speed)[] PartnerPath =
    [
        (13.2f, 850, 545, 150), (19.2f, 1380, 760, 150),
    ];
    private int heroNext, partnerNext;
    private float time;
    private bool switched;

    public override void FixedUpdate(ScriptTick tick)
    {
        time += tick.DeltaTime;
        var objects = tick.Objects;
        if (time > 1.4f) tick.Session.Set("deadzone");
        if (time > 5.6f) tick.Session.Set("lead");
        if (!switched && time >= SwitchAt)
        {
            switched = true;
            objects.SetActive(Frame, true);
            tick.Cameras.TrySetActive(Frame);
            tick.Ui.SetVisible(Deadzone, false);
            tick.Session.Set("frame");
        }
        Walk(objects, tick.DeltaTime, Player, PlayerPicture, HeroPath, ref heroNext);
        Walk(objects, tick.DeltaTime, Partner, PartnerPicture, PartnerPath, ref partnerNext);
    }

    private void Walk(IGameObjects objects, float dt, Guid who, Guid picture, (float At, float X, float Y, float Speed)[] path, ref int next)
    {
        var animator = objects.Animator(picture);
        if (!objects.TryGetPosition(who, out var at)) return;
        if (next >= path.Length || time < path[next].At) { animator?.SetFloat("Speed", 0); return; }
        var target = new Vector2(path[next].X, path[next].Y);
        var to = target - at;
        float step = path[next].Speed * dt;
        Vector2 move;
        if (to.Length() <= step) { move = to; next++; }
        else move = Vector2.Normalize(to) * step;
        objects.SetLocalTransform(who, new LocalTransform2D(at + move, 0, Vector2.One));
        var facing = MathF.Abs(move.X) >= MathF.Abs(move.Y) ? new Vector2(MathF.Sign(move.X), 0) : new Vector2(0, MathF.Sign(move.Y));
        animator?.SetVector2("Move", facing);
        animator?.SetFloat("Speed", move == Vector2.Zero ? 0 : 1);
    }
}
