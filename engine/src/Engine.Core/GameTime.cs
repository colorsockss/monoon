namespace Engine.Core;

public enum TimeDomain { Game, Ui }

public sealed class GameTime
{
    public const double FixedStep = 1.0 / 60;
    public const double MaxScale = 8;
    public double GameSeconds { get; private set; }
    public double UiSeconds { get; private set; }
    public long TickCount { get; private set; }

    private double scale = 1;
    public double Scale
    {
        get => scale;
        set
        {
            if (!double.IsFinite(value) || value <= 0 || value > MaxScale)
                throw new ArgumentOutOfRangeException(nameof(value), $"Choose a speed above zero and at most {MaxScale}.");
            scale = value;
        }
    }

    private bool paused;
    internal long PauseRevision { get; private set; }
    internal int PendingSteps { get; private set; }
    public bool IsPaused
    {
        get => paused;
        set
        {
            if (paused == value) return;
            paused = value;
            PauseRevision++;
            PendingSteps = 0;
        }
    }

    public void StepOnce()
    {
        if (paused) PendingSteps++;
    }

    internal bool TakeStep()
    {
        if (PendingSteps == 0) return false;
        PendingSteps--;
        return true;
    }

    internal void AdvanceUi(double seconds) => UiSeconds += seconds;
    internal void AdvanceGame(double seconds)
    {
        GameSeconds += seconds;
        TickCount++;
    }

    public GameTimer StartTimer(double seconds, TimeDomain domain = TimeDomain.Game, bool repeat = false) =>
        new(this, seconds, domain, repeat);
}

// A polled timer owns no callback or session registration. Repeating timers preserve their cadence.
public sealed class GameTimer
{
    private readonly GameTime clock;
    private readonly TimeDomain domain;
    private readonly double interval;
    private readonly bool repeat;
    private double deadline;
    private double Now => domain == TimeDomain.Game ? clock.GameSeconds : clock.UiSeconds;
    public bool IsRunning { get; private set; } = true;
    public double Remaining => IsRunning ? Math.Max(0, deadline - Now) : 0;

    internal GameTimer(GameTime clock, double seconds, TimeDomain domain, bool repeat)
    {
        if (!double.IsFinite(seconds) || seconds < 0 || (repeat && seconds == 0))
            throw new ArgumentOutOfRangeException(nameof(seconds));
        if (domain is not (TimeDomain.Game or TimeDomain.Ui)) throw new ArgumentOutOfRangeException(nameof(domain));
        this.clock = clock;
        this.domain = domain;
        this.repeat = repeat;
        interval = seconds;
        deadline = Now + seconds;
    }

    public void Cancel() => IsRunning = false;

    public bool Consume()
    {
        var now = Now;
        if (!IsRunning || now < deadline) return false;
        if (repeat)
        {
            // A late poll coalesces missed periods into one notification.
            var periods = Math.Floor((now - deadline) / interval) + 1;
            deadline = Math.Max(Math.BitIncrement(now), deadline + periods * interval);
        }
        else IsRunning = false;
        return true;
    }
}
