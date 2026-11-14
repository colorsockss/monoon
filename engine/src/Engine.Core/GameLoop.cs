namespace Engine.Core;

// Shared by the SDL window and the editor. UI and simulation each consume their own input edges.
public sealed class GameLoop(IGame2D game, InputActions actions)
{
    private readonly GameTime clock = game.Time ?? new GameTime();
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
                game.Simulate(actions.Take(), (float)GameTime.FixedStep);
            return;
        }
        accumulator += Math.Min(elapsed, 0.1) * clock.Scale;
        while (accumulator + 1e-12 >= GameTime.FixedStep)
        {
            accumulator = Math.Max(0, accumulator - GameTime.FixedStep);
            game.Simulate(actions.Take(), (float)GameTime.FixedStep);
            interpolate = true;
            if (clock.PauseRevision != pauseRevision)
            {
                ResetElapsed();
                break;
            }
        }
    }
}
