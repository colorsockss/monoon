using System;
using Engine.Core;

// The slot belongs to the session so a scene change cannot switch the destination of a save.
internal static class TrailProfile
{
    private static readonly string[] SlotFlags = ["", "TrailSlot1", "TrailSlot2", "TrailSlot3"];

    internal static int Selected(SessionState session)
    {
        for (int slot = 1; slot <= 3; slot++)
            if (session.Has(SlotFlags[slot])) return slot;
        return 1;
    }

    internal static void Select(SessionState session, int slot)
    {
        for (int i = 1; i <= 3; i++) session.Clear(SlotFlags[i]);
        session.Set(SlotFlags[slot]);
    }

    internal static GameData Load(SessionState session, int slot) =>
        session.Storage.IsAvailable ? session.Storage.LoadSlot(slot, 1) ?? new() : new();

    internal static void ClearStage(SessionState session, int stage, int tokens, int gems, long ticks)
    {
        if (!session.Storage.IsAvailable) return;
        int slot = Selected(session);
        var data = Load(session, slot);
        string prefix = FormattableString.Invariant($"stage{stage}.");
        data.Set(prefix + "clear", true);
        data.Set(prefix + "bestTokens", Math.Max(tokens, data.GetNumber(prefix + "bestTokens")));
        data.Set(prefix + "bestGems", Math.Max(gems, data.GetNumber(prefix + "bestGems")));
        double best = data.GetNumber(prefix + "bestTicks", ticks);
        data.Set(prefix + "bestTicks", Math.Min(best, ticks));
        data.Set("unlocked", Math.Max(Math.Min(4, stage + 1), data.GetNumber("unlocked", 1)));
        session.Storage.SaveSlot(slot, data);
    }
}
