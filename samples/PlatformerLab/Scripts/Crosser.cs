using System;
using System.Globalization;
using System.Linq;
using Engine.Core;

// Player of the Joints scene: walk and jump with the ordinary character motor over Box2D bodies.
public sealed class Crosser : GameBehaviour
{
    public Guid Goal, Readout, Result;
    public float Speed = 150, Jump = 320;
    private int ticks;
    private bool crossed, rodeBridge, rodePulley;
    private string shown = "";

    public override void FixedUpdate(ScriptTick tick)
    {
        var motion = tick.Objects.Motion;
        if (!motion.TryGetMotion(tick.ObjectId, out var body)) return;
        ticks++;
        motion.DriveCharacter(tick.ObjectId, tick.Direction.X * Speed, tick.Input.Pressed("Jump") ? Jump : 0);
        string stage = body.Support is not Guid support ? "Air" : support == Goal ? "Goal"
            : tick.Objects.FindByGroup("bridge").Contains(support) ? "Rope bridge"
            : tick.Objects.FindByGroup("pulley").Contains(support) ? "Pulley deck" : "Ground";
        rodeBridge |= stage == "Rope bridge";
        rodePulley |= stage == "Pulley deck";
        string text = string.Create(CultureInfo.InvariantCulture, $"X  {tick.Position.X:F1}      Y  {tick.Position.Y:F1}      {stage}");
        if (text != shown) tick.Ui.SetText(Readout, shown = text);
        if (crossed || stage != "Goal") return;
        crossed = true;
        tick.Ui.SetText(Result, rodeBridge && rodePulley ? "Crossed the bridge and the pulley" : "Reached the goal");
        var report = new GameData();
        report.Set("crossed", rodeBridge && rodePulley);
        report.Set("ticks", ticks);
        if (tick.Session.Storage.IsAvailable) tick.Session.Storage.SaveSlot(1, report);
        tick.Session.Log($"Joints crossing: bridge {rodeBridge}, pulley {rodePulley}, {ticks} ticks.");
    }
}
