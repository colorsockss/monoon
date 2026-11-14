using Engine.Core;

public sealed class PauseGame : GameBehaviour
{
    private GameTimer seconds;
    private double pausedAt;

    public override void UpdateUi(UiTick tick)
    {
        if (tick.Input.Pressed("PauseGame")) tick.Time.IsPaused = !tick.Time.IsPaused;
        if (!tick.Time.IsPaused)
        {
            tick.Session.Clear("paused");
            seconds = null;
            return;
        }

        if (seconds == null)
        {
            tick.Session.Set("paused");
            seconds = tick.Time.StartTimer(1, TimeDomain.Ui, repeat: true);
            pausedAt = tick.Time.UiSeconds;
            tick.Ui.SetText(tick.ObjectId, "Paused  0s");
        }
        else if (seconds.Consume())
        {
            tick.Ui.SetText(tick.ObjectId, $"Paused  {(int)(tick.Time.UiSeconds - pausedAt)}s");
        }
    }
}
