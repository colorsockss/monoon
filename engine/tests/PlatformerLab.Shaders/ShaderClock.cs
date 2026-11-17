using System;
using System.Globalization;
using System.IO;
using Engine.Core;

// Added only to the comparison fixture. The host requests absolute simulation ticks and
// waits for UpdateUi to acknowledge them before capturing the normal SDL window.
public sealed class ShaderClock : GameBehaviour
{
    private string directory;
    private int acknowledged = -1;
    public override void OnEnable(ScriptLife life)
    {
        directory = Environment.GetEnvironmentVariable("MONOON_SHADER_TEST_CLOCK");
        if (!string.IsNullOrEmpty(directory)) life.Session.Time.IsPaused = true;
    }
    public override void UpdateUi(UiTick tick)
    {
        if (string.IsNullOrEmpty(directory)) return;
        int target = int.Parse(File.ReadAllText(Path.Combine(directory, "target")), CultureInfo.InvariantCulture);
        long current = tick.Time.TickCount;
        if (current < target)
        {
            for (long i = current; i < target; i++) tick.Time.StepOnce();
        }
        else if (current == target && acknowledged != target)
        {
            string stage = Path.Combine(directory, "ready.tmp");
            File.WriteAllText(stage, target.ToString(CultureInfo.InvariantCulture));
            File.Move(stage, Path.Combine(directory, "ready"), true);
            acknowledged = target;
        }
    }
}
