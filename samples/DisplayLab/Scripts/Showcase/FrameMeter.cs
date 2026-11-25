using System;
using System.Globalization;
using System.Numerics;
using Engine.Core;

// DisplayLab showcase: counts rendered frames (UpdateUi runs once per drawn frame) and shows frames per second twice a second,
// so turning VSync off visibly lifts the rate; paces the hero across the room and flickers the torches so motion shows it too.
// Raises "window" at start, "fullscreen" and "vsync" when the standalone window's state changes (Session.Display).
public sealed class FrameMeter : GameBehaviour
{
    public Guid Hero, Shadow, Rate, Detail;
    public Guid[] Flames = [];
    public SpriteReference WalkE1, WalkE2, WalkE3, WalkE4, WalkW1, WalkW2, WalkW3, WalkW4, Flame1, Flame2, Flame3;
    private const float Left = 60, Right = 420, Speed = 80, Y = 320;
    private float x = 79, clock, window;
    private int direction = 1, frames;
    private bool? fullscreen, vsync;

    public override void UpdateUi(UiTick tick)
    {
        frames++;
        window += (float)tick.DeltaTime;
        if (window >= .5f)
        {
            tick.Ui.SetText(Rate, (frames / window).ToString("0", CultureInfo.InvariantCulture));
            tick.Ui.SetText(Detail, string.Create(CultureInfo.InvariantCulture, $"{window * 1000 / frames:0.0} ms a frame"));
            frames = 0;
            window = 0;
        }
        tick.Session.Set("window");
        if (tick.Session.Display is not {} display) return;
        if (fullscreen is {} f && f != display.Fullscreen) tick.Session.Set("fullscreen");
        if (vsync is {} v && v != display.VSync) tick.Session.Set("vsync");
        (fullscreen, vsync) = (display.Fullscreen, display.VSync);
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        clock += tick.DeltaTime;
        x += direction * Speed * tick.DeltaTime;
        if (x > Right || x < Left) { direction = -direction; x = Math.Clamp(x, Left, Right); }
        var objects = tick.Objects;
        objects.SetLocalTransform(Hero, new LocalTransform2D(new Vector2(MathF.Round(x), Y), 0, Vector2.One));
        objects.SetLocalTransform(Shadow, new LocalTransform2D(new Vector2(MathF.Round(x) + 5, Y + 54), 0, Vector2.One));
        int step = (int)(clock / .12f) % 4;
        objects.SetSprite(Hero, direction > 0 ? step switch { 0 => WalkE1, 1 => WalkE2, 2 => WalkE3, _ => WalkE4 }
                                              : step switch { 0 => WalkW1, 1 => WalkW2, 2 => WalkW3, _ => WalkW4 });
        for (int i = 0; i < Flames.Length; i++)
            objects.SetSprite(Flames[i], (((int)(clock / .11f) + i) % 3) switch { 0 => Flame1, 1 => Flame2, _ => Flame3 });
    }
}
