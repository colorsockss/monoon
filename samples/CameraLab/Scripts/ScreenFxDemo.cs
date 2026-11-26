using System;
using System.Drawing;
using System.IO;
using Engine.Core;

// Screen effects, comfort settings, rumble and a custom cursor.
public sealed class ScreenFxDemo : GameBehaviour
{
    public Guid Comfort, Output;
    public SpriteReference Cursor;
    private bool faded, graded;

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
        if (input.Pressed("Impact")) { screen.Shake(10, .5f); screen.ZoomPunch(.15f, .35f); tick.Session.Rumble(1, .5f, .4); }
        if (input.Pressed("Flash")) { screen.Flash(Color.White, .45); }
        if (input.Pressed("Fade")) { faded = !faded; screen.Fade(faded ? Color.Black : Color.Transparent, .5); }
        if (input.Pressed("Grade")) { graded = !graded; screen.Adjust(graded ? Color.FromArgb(255, 214, 170) : Color.White, graded ? -.15f : 0, .3); }
        if (input.Pressed("Buzz")) { tick.Session.Rumble(.3f, .9f, .2); }
        if (input.Pressed("Pointer"))
        {
            if (tick.Session.Cursor == null) tick.Session.SetCursor(Cursor, 8, 8);
            else tick.Session.ResetCursor();
        }
        bool changed = false;
        if (input.Pressed("Comfort")) { options.ShakeScale = options.FlashScale = options.ShakeScale == 1 ? .25f : 1; changed = true; }
        if (input.Pressed("RumbleSetting")) { options.Rumble = !options.Rumble; changed = true; }
        if (changed) Save(tick.Session, options);
        tick.Ui.SetText(Comfort, $"Shake {options.ShakeScale:P0}   Flash {options.FlashScale:P0}");
        tick.Ui.SetText(Output, $"Rumble {(options.Rumble ? "on" : "off")}   Cursor {(tick.Session.Cursor == null ? "system" : "custom")}");
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
