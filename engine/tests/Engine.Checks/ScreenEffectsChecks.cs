using System.Drawing;
using System.Numerics;
using Engine.Core;

internal static class ScreenEffectsChecks
{
    [Check(4700, "screen-effects")] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    private static void Check(bool value, string why) { if (!value) throw new Exception("Screen effects: " + why); }

    internal static void Run(string repository, string scratch)
    {
        var scene = SceneDocument.Load(Path.Combine(repository, "samples", "CameraLab", "Scenes", "Showcase.scene"));
        var profile = Directory.CreateDirectory(Path.Combine(scratch, "ScreenEffects")).FullName;
        GameSession Open() => new(scene, null, null, new PlayerStorage(profile));
        void Tap(GameSession game, string key)
        {
            game.Actions.Press(key); game.FixedUpdate(game.Actions.Take(), 1 / 60f);
            game.Actions.Release(key); game.FixedUpdate(game.Actions.Take(), 1 / 60f);
        }
        ScreenFrame[] Impact(GameSession game)
        {
            Tap(game, "1");
            var frames = new ScreenFrame[30];
            for (int i = 0; i < frames.Length; i++) { frames[i] = game.State.Screen.Current; game.FixedUpdate(game.Actions.Take(), 1 / 60f); }
            return frames;
        }

        ScreenFrame[] full;
        using (var a = Open()) using (var b = Open())
        {
            full = Impact(a);
            Check(full.SequenceEqual(Impact(b)), "the same input on the same ticks gives the same shake and zoom (seeded, game time)");
            Check(full[0].Shake.Length() is > 0 and <= 10 && full[0].Zoom > 1.1f && !full[^1].Moves && !full[^1].Adjusted,
                "impact shakes, punches the zoom and ends");
            Check(a.State.PendingRumble == new GamepadRumble(1, .5f, .4), "impact requests rumble");

            // Shake moves the split views but not the minimap, and does not change the camera API.
            a.State.Screen.Shake(8, 1); a.FixedUpdate(a.Actions.Take(), 1 / 60f);
            var shaken = new DrawCapture(); a.Current.Draw(shaken, 1);
            var offset = a.State.Screen.Current.Shake;
            a.State.Screen.Clear();
            var still = new DrawCapture(); a.Current.Draw(still, 1);
            // The hero's picture: first in the Player Camera view, last in the minimap. The offset is in screen pixels.
            Check(offset != Vector2.Zero && Vector2.Distance(new(still.Hero[0].X - shaken.Hero[0].X, still.Hero[0].Y - shaken.Hero[0].Y), offset) < .01f &&
                  still.Hero[^1] == shaken.Hero[^1], $"views move by the shake offset; the minimap stays: {offset} {still.Hero[0]} {shaken.Hero[0]}");

            // Colour adjustment wraps the world; fade and flash cover the HUD.
            Tap(a, "4"); Tap(a, "3");
            for (int i = 0; i < 40; i++) a.FixedUpdate(a.Actions.Take(), 1 / 60f);
            var graded = new DrawCapture(); a.Current.Draw(graded, 1);
            Check(graded.Log[0] == "begin" && graded.Log.IndexOf("end 217,182,144 0") > 0 &&
                  graded.Log.IndexOf("end 217,182,144 0") < graded.Log.IndexOf("text") && graded.Log[^1] == "fill FF000000",
                "colour layer around cameras, HUD after it, black fade last");
        }

        // Comfort settings scale the same seeded motion and flashes; rumble off drops requests.
        using (var c = Open())
        {
            Tap(c, "5");
            var reduced = Impact(c);
            Check(Vector2.Distance(reduced[3].Shake, full[3].Shake * .25f) < 1e-4f && Math.Abs(reduced[3].Zoom - 1 - (full[3].Zoom - 1) * .25f) < 1e-5f,
                "reduced shake is a quarter of the same motion");
            Tap(c, "2");
            Check(c.State.Screen.Current.Flash.A == (int)Math.Round(255 * .25 * Math.Pow(1 - 2 / 60.0 / .45, 2)), "reduced flash opacity");
            c.State.PendingRumble = null;
            Tap(c, "8"); Tap(c, "6");
            Check(c.State.PendingRumble == null, "rumble off ignores requests");
            Check(c.State.Storage.LoadSlot(3, 1)?.GetText("impactReduced.6").Contains("comfort=0.25,0.25,True") == true, "the demo records drawn values on fixed ticks");
        }
        using (var d = Open())
        {
            d.FixedUpdate(d.Actions.Take(), 1 / 60f); // Scripts start on the first update.
            var options = d.Current.Ui.Preferences;
            Check(options.ShakeScale == .25f && options.FlashScale == .25f && !options.Rumble, "comfort settings load from player.settings after a restart");
            var cursor = d.State;
            Tap(d, "7");
            Check(cursor.Cursor is { HotX: 8, HotY: 8 } && cursor.Cursor.Path.EndsWith("Cursor.png") && cursor.CursorRevision == 1, "custom cursor from the project PNG");
            Tap(d, "7");
            Check(cursor.Cursor == null && cursor.CursorRevision == 2, "system cursor restored");
            var png = new SpriteReference(Guid.Parse("b3af6d15-ef36-48a2-b844-4a4a923f07f6"u8));
            foreach (var (x, y) in new[] { (-1, 0), (17, 0), (0, 17) })
                try { cursor.SetCursor(png, x, y); throw new Exception("Screen effects: hotspot outside the image accepted"); }
                catch (ArgumentOutOfRangeException) { }
            try { cursor.SetCursor(new(Guid.NewGuid()), 0, 0); throw new Exception("Screen effects: missing cursor accepted"); }
            catch (ArgumentException) { }
            try { options.ShakeScale = 2; throw new Exception("Screen effects: shake scale above 1 accepted"); }
            catch (ArgumentOutOfRangeException) { }
            var hostile = new GameData();
            hostile.Set("accessibility.shakeScale", 7); hostile.Set("accessibility.textScale", 9); hostile.Set("accessibility.locale", "");
            options.LoadFrom(hostile);
            Check(options.ShakeScale == .25f && options.TextScale == 1 && options.Locale == "en", "invalid saved values keep the current settings");
        }
        Console.WriteLine("PASS screen shake/zoom determinism, comfort scaling, colour layer order, fade/flash, rumble off, cursor validation and saved settings");
    }

    private sealed class DrawCapture : ICanvas2D
    {
        public List<RectangleF> Green = [], Hero = [];
        public List<string> Log = [];
        public IDisposable PushClip(RectangleF r) => new Pop();
        private sealed class Pop : IDisposable { public void Dispose() { } }
        public void BeginColorAdjust() => Log.Add("begin");
        public void EndColorAdjust(Color multiply, byte lift) => Log.Add($"end {multiply.R},{multiply.G},{multiply.B} {lift}");
        public void Clear(Color color) { }
        public void FillRectangle(RectangleF rect, Color color)
        {
            if ((color.ToArgb() & 0xffffff) == 0x52e4b5) Green.Add(rect);
            Log.Add($"fill {color.ToArgb():X8}");
        }
        public void DrawShader(ShaderClip shader, int frame, RectangleF local, Matrix3x2 matrix, Color tint, bool additive) { }
        public bool DrawSprite(string path, RectangleF rect) { if (path.Contains("Showcase/Hero")) Hero.Add(rect); return true; }
        public void DrawText(string text, float x, float y, float size, Color color) => Log.Add("text");
    }
}
