namespace Engine.Core;

// What a routine yields. Game waits follow the fixed tick (pause and Step included); UI waits follow unscaled time.
public readonly struct ScriptWait
{
    internal enum WaitKind { GameSeconds, UiSeconds, Frames, Until }
    internal readonly WaitKind Kind;
    internal readonly double Amount;
    internal readonly Func<bool>? Condition;
    private ScriptWait(WaitKind kind, double amount, Func<bool>? condition) => (Kind, Amount, Condition) = (kind, amount, condition);

    public static ScriptWait Seconds(double seconds) => new(WaitKind.GameSeconds, Checked(seconds), null);
    public static ScriptWait UiSeconds(double seconds) => new(WaitKind.UiSeconds, Checked(seconds), null);
    public static ScriptWait Frames(int ticks) =>
        new(WaitKind.Frames, ticks >= 0 ? ticks : throw new ArgumentOutOfRangeException(nameof(ticks)), null);
    public static ScriptWait Until(Func<bool> condition) => new(WaitKind.Until, 0, condition ?? throw new ArgumentNullException(nameof(condition)));

    private static double Checked(double seconds) =>
        double.IsFinite(seconds) && seconds >= 0 ? seconds : throw new ArgumentOutOfRangeException(nameof(seconds));

    public override string ToString() => Kind switch
    {
        WaitKind.GameSeconds => $"Seconds({Amount})", WaitKind.UiSeconds => $"UiSeconds({Amount})",
        WaitKind.Frames => $"Frames({Amount})", _ => "Until(condition)"
    };
}

// The wait a routine is currently blocked on, anchored to the clock when it was yielded.
internal struct PendingWait
{
    private ScriptWait wait;
    private double deadline;
    public ScriptWait Wait => wait;
    public bool IsUi => wait.Kind == ScriptWait.WaitKind.UiSeconds;

    public void Begin(ScriptWait next, GameTime time)
    {
        wait = next;
        deadline = next.Kind switch
        {
            ScriptWait.WaitKind.GameSeconds => time.GameSeconds + next.Amount,
            ScriptWait.WaitKind.UiSeconds => time.UiSeconds + next.Amount,
            // Every wait lasts at least until the next tick, so Frames(0) behaves like Frames(1).
            ScriptWait.WaitKind.Frames => time.TickCount + Math.Max(1, next.Amount),
            _ => 0
        };
    }

    // The epsilon absorbs the rounding of repeated 1/60 additions so 0.5 s is exactly 30 ticks.
    public readonly bool Ready(GameTime time) => wait.Kind switch
    {
        ScriptWait.WaitKind.GameSeconds => time.GameSeconds + 1e-9 >= deadline,
        ScriptWait.WaitKind.UiSeconds => time.UiSeconds + 1e-9 >= deadline,
        ScriptWait.WaitKind.Frames => time.TickCount >= deadline,
        _ => wait.Condition!()
    };
}

public sealed class ScriptRoutine
{
    internal readonly SceneGame.Body Owner;
    private readonly Type iterator;
    internal IEnumerator<ScriptWait>? Steps;
    internal PendingWait Pending;
    internal bool Running;
    internal ScriptRoutine(SceneGame.Body owner, IEnumerator<ScriptWait> steps) => (Owner, Steps, iterator) = (owner, steps, steps.GetType());

    // An iterator is a nested class named <Method>d__N, or <<Outer>g__Local|0_1>d for a local function.
    internal string Name
    {
        get
        {
            var n = iterator.Name;
            int local = n.IndexOf("g__", StringComparison.Ordinal), close = n.IndexOf('>');
            var method = local >= 0 && n.IndexOf('|', local) is var bar and > 0 ? n[(local + 3)..bar] : n[0] == '<' && close > 1 ? n[1..close] : "routine";
            return $"{iterator.DeclaringType?.Name ?? n}.{method}";
        }
    }

    public bool IsRunning => Steps != null;
    public void Stop()
    {
        var steps = Steps;
        Steps = null;
        if (!Running) steps?.Dispose(); // A routine that stops itself is disposed after its step returns.
    }
}

public sealed partial class SceneGame
{
    private readonly List<ScriptRoutine> routines = [];

    public ScriptRoutine StartRoutine(Guid owner, IEnumerable<ScriptWait> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (closing || removed.Contains(owner) || !byId.TryGetValue(owner, out var body) || !body.Active)
            throw new InvalidOperationException("A routine needs an active owner object.");
        var routine = new ScriptRoutine(body, steps.GetEnumerator());
        routines.Add(routine);
        Resume(routine); // Runs to the first wait now, like a direct call.
        return routine;
    }

    private void StopRoutines(Body owner)
    {
        foreach (var routine in routines)
            if (routine.Owner == owner) routine.Stop();
    }

    // Editor script reload: every routine runs project code from the replaced assembly.
    internal int StopAllRoutines()
    {
        int running = routines.Count(r => r.IsRunning);
        foreach (var routine in routines) routine.Stop();
        routines.Clear();
        return running;
    }

    // Start order decides resume order. Routines started during this pass wait for the next one.
    private void StepRoutines(bool ui)
    {
        if (tweenCount > 0) StepTweens(ui);
        if (routines.Count == 0) return;
        int count = routines.Count;
        for (int i = 0; i < count; i++)
        {
            var routine = routines[i];
            if (routine.Steps == null || routine.Pending.IsUi != ui) continue;
            var owner = routine.Owner;
            if (!owner.Active || removed.Contains(owner.Id)) { routine.Stop(); continue; }
            bool ready;
            using (EnterScene(owner))
                try { ready = routine.Pending.Ready(Time); }
                catch (Exception ex) { routine.Stop(); throw new InvalidOperationException($"{routine.Name}: {ex.Message}", ex); }
            if (ready) Resume(routine);
        }
        routines.RemoveAll(r => r.Steps == null);
    }

    private void Resume(ScriptRoutine routine)
    {
        using var scope = EnterScene(routine.Owner);
        var steps = routine.Steps!;
        bool more;
        routine.Running = true;
        try { more = steps.MoveNext(); }
        catch (Exception ex)
        {
            routine.Running = false;
            routine.Stop();
            steps.Dispose();
            throw new InvalidOperationException($"{routine.Name}: {ex.Message}", ex);
        }
        finally { routine.Running = false; }
        if (routine.Steps == null) steps.Dispose(); // Its step disabled or removed the owner.
        else if (more) routine.Pending.Begin(steps.Current, Time);
        else routine.Stop();
    }
}
