using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Framework.Utilities;

using var game = new BenchmarkGame();
game.Run();
return game.Completed ? 0 : 1;

internal sealed class BenchmarkGame : Game
{
    private readonly SpriteBenchmark benchmark = new();
    private readonly (Rectangle Bounds, Color Tint)[] sprites = SpriteBenchmark.CreateSprites((x, y, r, g, b) =>
        (new Rectangle(x, y, SpriteBenchmark.SpriteSize, SpriteBenchmark.SpriteSize), new Color(r, g, b)));
    private SpriteBatch batch = null!;
    private Texture2D square = null!;
    private RenderTarget2D? verificationTarget;
    public bool Completed => benchmark.Finished;

    public BenchmarkGame()
    {
        _ = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = SpriteBenchmark.Width,
            PreferredBackBufferHeight = SpriteBenchmark.Height,
            SynchronizeWithVerticalRetrace = false,
            PreferredDepthStencilFormat = DepthFormat.None
        };
        IsFixedTimeStep = false;
        InactiveSleepTime = TimeSpan.Zero;
        Window.Title = "MonoGame sprite benchmark";
    }

    protected override void LoadContent()
    {
        var parameters = GraphicsDevice.PresentationParameters;
        if (parameters.PresentationInterval != PresentInterval.Immediate)
            throw new InvalidOperationException("MonoGame did not select immediate presentation.");
        SpriteBenchmark.ReportConfiguration($"MonoGame {PlatformInfo.GraphicsBackend}", parameters.BackBufferWidth, parameters.BackBufferHeight);
        batch = new SpriteBatch(GraphicsDevice);
        using var file = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "EngineAssets", "Square.png"));
        square = Texture2D.FromStream(GraphicsDevice, file);
        // DesktopVK 3.8.6.1 can lose the device when reading a swapchain image before Present.
        if (PlatformInfo.GraphicsBackend == GraphicsBackend.Vulkan)
            verificationTarget = new RenderTarget2D(GraphicsDevice, parameters.BackBufferWidth, parameters.BackBufferHeight);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (benchmark.FirstWarmup && verificationTarget != null)
        {
            GraphicsDevice.SetRenderTarget(verificationTarget);
            DrawSprites();
            GraphicsDevice.SetRenderTarget(null);
        }
        benchmark.BeginDraw();
        DrawSprites();
        benchmark.EndDraw();
        if (benchmark.FirstWarmup && verificationTarget == null) VerifyPixel();
    }

    private void DrawSprites()
    {
        GraphicsDevice.Clear(Color.Black);
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        foreach (var sprite in sprites.AsSpan(0, benchmark.Count))
            batch.Draw(square, sprite.Bounds, sprite.Tint);
        batch.End();
    }

    private void VerifyPixel()
    {
        var last = sprites[benchmark.Count - 1];
        var point = last.Bounds.Center;
        var region = new Rectangle(point.X, point.Y, 1, 1);
        var pixel = new Color[1];
        if (verificationTarget == null) GraphicsDevice.GetBackBufferData(region, pixel, 0, 1);
        else verificationTarget.GetData(0, region, pixel, 0, 1);
        if (pixel[0] != last.Tint)
            throw new InvalidOperationException($"Incorrect rendered pixel at {point}, expected {last.Tint}, got {pixel[0]}.");
    }

    protected override void EndDraw()
    {
        base.EndDraw();
        if (benchmark.FirstWarmup && verificationTarget != null) VerifyPixel();
        benchmark.EndPresent();
        if (benchmark.Finished) Exit();
    }

    protected override void UnloadContent()
    {
        verificationTarget?.Dispose();
        square.Dispose();
        batch.Dispose();
    }
}
