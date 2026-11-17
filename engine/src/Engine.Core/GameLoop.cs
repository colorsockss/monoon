namespace Engine.Core;

// Shared by the SDL window and the editor. UI and simulation each consume their own input edges.
public sealed class GameLoop(IGame2D game, InputActions actions)
{
    private readonly GameTime clock = game.Time ?? new GameTime();
    // Invoked after the first simulation tick that consumed a mapped press, when requested by a diagnostic.
    public Action<string>? FirstActionProcessed { get; set; }
    private long pauseRevision;
    private double accumulator;
    private bool interpolate;
    public float Interpolation => clock.IsPaused || !interpolate ? 1f : (float)(accumulator / GameTime.FixedStep);

    public void ResetElapsed()
    {
        accumulator = 0;
        interpolate = false;
    }

    public void Advance(double elapsed)
    {
        if (!double.IsFinite(elapsed) || elapsed < 0) throw new ArgumentOutOfRangeException(nameof(elapsed));
        game.UpdateUi(actions.TakeUi(), elapsed);
        // Switching modes is a boundary. Wall time and input from the old mode do not carry across it.
        bool changed = clock.PauseRevision != pauseRevision;
        if (changed)
        {
            ResetElapsed();
            pauseRevision = clock.PauseRevision;
        }
        if (clock.IsPaused || changed)
        {
            actions.DiscardGameEdges();
            while (clock.IsPaused && clock.TakeStep())
                Simulate();
            return;
        }
        accumulator += Math.Min(elapsed, 0.1) * clock.Scale;
        while (accumulator + 1e-12 >= GameTime.FixedStep)
        {
            accumulator = Math.Max(0, accumulator - GameTime.FixedStep);
            Simulate();
            interpolate = true;
            if (clock.PauseRevision != pauseRevision)
            {
                ResetElapsed();
                break;
            }
        }
    }

    private void Simulate()
    {
        var input = actions.Take();
        game.Simulate(input, (float)GameTime.FixedStep);
        if (FirstActionProcessed is not {} observe || input.Samples == null) return;
        foreach (var (action, sample) in input.Samples)
        {
            if (!sample.Pressed) continue;
            FirstActionProcessed = null;
            observe(action);
            break;
        }
    }
}
