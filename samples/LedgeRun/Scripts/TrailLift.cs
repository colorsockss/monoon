using Engine.Core;

public sealed class TrailLift : GameBehaviour
{
    public System.Guid Courier;
    public float DeckWidth = 64;
    private bool boarded;

    public override void FixedUpdate(ScriptTick tick)
    {
        if (boarded || !tick.Objects.TryGetPosition(Courier, out var rider)) return;
        float riderCenter = rider.X + 7, feet = rider.Y + 20;
        if (riderCenter < tick.Position.X || riderCenter > tick.Position.X + DeckWidth ||
            System.MathF.Abs(feet - tick.Position.Y) > 1 ||
            !tick.Objects.Motion.TryGetMotion(Courier, out var motion) || !motion.Grounded) return;
        boarded = tick.Objects.PlayTween(tick.ObjectId);
    }
}
