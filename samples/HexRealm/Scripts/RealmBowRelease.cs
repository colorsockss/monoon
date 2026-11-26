using Engine.Core;

public sealed class RealmBowRelease : GameBehaviour
{
    internal RealmBoard? Board { get; set; }

    public override void OnAnimationEvent(ScriptAnimationEvent frame)
    {
        if (frame.Marker.Name == "Loose")
            Board?.ReleaseArrow(frame.ObjectId);
    }
}
