using System;
using System.Globalization;
using Engine.Core;

// Player of the Paths scene: stepping onto the lift starts its Path Follower 2D; the kinematic lift carries the
// character motor over the pillar at constant speed.
public sealed class PathRider : GameBehaviour
{
    public Guid Lift, Goal, Readout, Result;
    public float Speed = 150, Jump = 320;
    private int ticks;
    private bool started, arrived, rode;
    private string shown = "";

    public override void FixedUpdate(ScriptTick tick)
    {
        var motion = tick.Objects.Motion;
        if (!motion.TryGetMotion(tick.ObjectId, out var body)) return;
        ticks++;
        motion.DriveCharacter(tick.ObjectId, tick.Direction.X * Speed, tick.Input.Pressed("Jump") ? Jump : 0);
        string stage = body.Support is not Guid support ? "Air" : support == Lift ? "Path lift" : support == Goal ? "Goal" : "Ground";
        if (stage == "Path lift" && !started) started = tick.Objects.PlayTween(Lift);
        rode |= stage == "Path lift";
        string text = string.Create(CultureInfo.InvariantCulture, $"X  {tick.Position.X:F1}      Y  {tick.Position.Y:F1}      {stage}");
        if (text != shown) tick.Ui.SetText(Readout, shown = text);
        if (arrived || stage != "Goal") return;
        arrived = true;
        tick.Ui.SetText(Result, rode ? "Rode the path lift over the pillar" : "Reached the goal");
        var report = new GameData();
        report.Set("rode", rode);
        report.Set("ticks", ticks);
        if (tick.Session.Storage.IsAvailable) tick.Session.Storage.SaveSlot(1, report);
        tick.Session.Log($"Path crossing: lift {rode}, {ticks} ticks.");
    }
}
