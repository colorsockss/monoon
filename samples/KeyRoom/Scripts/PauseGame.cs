using Engine.Core;

public sealed class PauseGame : GameBehaviour
{
    private GameTimer seconds;
    private double pausedAt;
    private string locale;

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
            locale = null;
        }
        if (seconds.Consume() || locale != tick.Ui.Preferences.Locale)
        {
            locale = tick.Ui.Preferences.Locale;
            tick.Ui.SetText(tick.ObjectId, string.Format(tick.Ui.Translate("time.paused"), (int)(tick.Time.UiSeconds - pausedAt)));
        }
    }
}
