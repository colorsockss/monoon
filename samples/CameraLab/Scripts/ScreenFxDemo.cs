using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using Engine.Core;

// Screen effects, comfort settings, rumble and a custom cursor. Keys work while frozen (F), and "." advances one game tick.
public sealed class ScreenFxDemo : GameBehaviour
{
    public Guid Comfort, Output, Clock;
    public SpriteReference Cursor;
    private bool faded, graded;
    private string effect = "";
    private int since = -1;
    private GameData record = new();

    public override void OnCreate(ScriptLife life)
    {
        if (!life.Session.Storage.IsAvailable) return;
        try { if (life.Session.Storage.LoadSettings(1) is { } saved) life.Ui.Preferences.LoadFrom(saved); } // Comfort choices survive a restart.
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { life.Session.Log(ex.Message, GameLogLevel.Error); }
    }

    public override void UpdateUi(UiTick tick)
    {
        var input = tick.Input;
        var screen = tick.Session.Screen;
        var options = tick.Ui.Preferences;
        void Start(string name) { effect = options.ShakeScale < 1 ? name + "Reduced" : name; since = 0; }
        if (input.Pressed("Impact")) { screen.Shake(10, .5f); screen.ZoomPunch(.15f, .35f); tick.Session.Rumble(1, .5f, .4); Start("impact"); }
        if (input.Pressed("Flash")) { screen.Flash(Color.White, .45); Start("flash"); }
        if (input.Pressed("Fade")) { faded = !faded; screen.Fade(faded ? Color.Black : Color.Transparent, .5); Start(faded ? "fadeOut" : "fadeIn"); }
        if (input.Pressed("Grade")) { graded = !graded; screen.Adjust(graded ? Color.FromArgb(255, 214, 170) : Color.White, graded ? -.15f : 0, .3); Start(graded ? "grade" : "ungrade"); }
        if (input.Pressed("Buzz")) { tick.Session.Rumble(.3f, .9f, .2); Start("buzz"); }
        if (input.Pressed("Pointer"))
        {
            if (tick.Session.Cursor == null) tick.Session.SetCursor(Cursor, 8, 8);
            else tick.Session.ResetCursor();
        }
        bool changed = false;
        if (input.Pressed("Comfort")) { options.ShakeScale = options.FlashScale = options.ShakeScale == 1 ? .25f : 1; changed = true; }
        if (input.Pressed("RumbleSetting")) { options.Rumble = !options.Rumble; changed = true; }
        if (changed) Save(tick.Session, options);
        if (input.Pressed("Freeze")) tick.Time.IsPaused = !tick.Time.IsPaused;
        if (input.Pressed("Step")) tick.Time.StepOnce();
        tick.Ui.SetText(Comfort, $"Shake {options.ShakeScale:P0}   Flash {options.FlashScale:P0}");
        tick.Ui.SetText(Output, $"Rumble {(options.Rumble ? "on" : "off")}   Cursor {(tick.Session.Cursor == null ? "system" : "custom")}");
        tick.Ui.SetText(Clock, tick.Time.IsPaused ? "Frozen" : "Running");
    }

    // Writes the drawn effect values on fixed game ticks after each trigger, so runs can be compared exactly.
    public override void FixedUpdate(ScriptTick tick)
    {
        if (since < 0) return;
        since++;
        if (since is not (1 or 6 or 12 or 24)) return;
        var f = tick.Session.Screen.Current;
        var c = CultureInfo.InvariantCulture;
        record.Set($"{effect}.{since}", string.Format(c, "shake={0:F3},{1:F3} zoom={2:F4} fade={3} flash={4} tint={5:X6} brightness={6:F3} comfort={7:F2},{8:F2},{9}",
            f.Shake.X, f.Shake.Y, f.Zoom, f.Fade.A, f.Flash.A, f.Tint.ToArgb() & 0xffffff, f.Brightness,
            tick.Ui.Preferences.ShakeScale, tick.Ui.Preferences.FlashScale, tick.Ui.Preferences.Rumble));
        if (tick.Session.Storage.IsAvailable) tick.Session.Storage.SaveSlot(3, record);
        if (since == 24) since = -1;
    }

    private static void Save(SessionState session, UiPreferences options)
    {
        if (!session.Storage.IsAvailable) return;
        try
        {
            var settings = session.Storage.LoadSettings(1) ?? new GameData();
            options.SaveTo(settings);
            session.Storage.SaveSettings(settings);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { session.Log(ex.Message, GameLogLevel.Error); }
    }
}
