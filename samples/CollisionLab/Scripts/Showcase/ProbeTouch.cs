using System;
using Engine.Core;

// CollisionLab casts showcase: a gate or the wall reacting when the rolling probe reaches it. The first step the probe's right
// edge reaches this object's left edge, the sprite flickers to Flash for a moment and the world text Note says Says.
// shortcut: tests x only because the probe rolls right along one line; use the probe's collider bounds if it ever turns.
public sealed class ProbeTouch : GameBehaviour
{
    public Guid Probe, Note;
    public string Flash = "#FFFFFF", Says = "";
    private const float ProbeWidth = 40, Seconds = .45f;
    private string? tint;
    private bool started, touched;
    private float left;
    public override void FixedUpdate(ScriptTick tick)
    {
        var objects = tick.Objects;
        if (!started) { started = true; tint = objects.GetComponent<SpriteRendererComponent>(tick.ObjectId)?.Tint; }
        if (!touched && objects.TryGetPosition(Probe, out var probe) && probe.X + ProbeWidth >= tick.Position.X - .5f)
        {
            touched = true; left = Seconds;
            objects.SetWorldText(Note, Says);
        }
        if (left <= 0) return;
        left -= tick.DeltaTime;
        objects.SetSpriteTint(tick.ObjectId, left > 0 && (int)(left * 12) % 2 == 0 ? Flash : tint);
    }
}
