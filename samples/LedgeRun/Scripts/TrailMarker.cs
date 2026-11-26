using System.Numerics;
using Engine.Core;

public enum TrailMarkerKind { Token, Gem, Checkpoint, Goal, Hazard }

public sealed class TrailMarker : GameBehaviour
{
    public TrailMarkerKind Kind;
    private bool used;

    public override void OnTriggerEnter(ScriptContact contact)
    {
        if (used || contact.Objects.GetComponent<TrailRunner>(contact.OtherId) is not { Completed: false } runner) return;

        switch (Kind)
        {
            case TrailMarkerKind.Token:
            case TrailMarkerKind.Gem:
                used = true;
                runner.Collect(Kind == TrailMarkerKind.Gem, contact.Objects, contact.Ui);
                contact.Objects.Destroy(contact.ObjectId);
                break;
            case TrailMarkerKind.Checkpoint:
                used = true;
                if (contact.Objects.TryGetPosition(contact.ObjectId, out var at))
                    runner.SaveCheckpoint(at + new Vector2(6, 8), contact.Ui);
                break;
            case TrailMarkerKind.Goal:
                runner.Finish(contact.Session, contact.Objects, contact.Ui);
                break;
            case TrailMarkerKind.Hazard:
                runner.Damage(contact.OtherId, contact.Objects, contact.Ui);
                break;
        }
    }
}
