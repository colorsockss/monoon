using System;
using System.IO;
using System.Numerics;
using Engine.Core;

public sealed class ListeningWalk : GameBehaviour
{
    public Guid Music, Beacon, Levels, Readout;
    public Guid MusicSlider, EffectsList, MasterMute;
    private bool beacon, music = true;
    private GameData settings;
    private float musicPercent, masterGain;
    private int effectsPreset;
    private bool muted;
    private string lastStatus = "", lastLevels = "";

    public override void OnCreate(ScriptLife life)
    {
        var mixer = life.Objects.Audio;
        masterGain = mixer.Levels.Master;
        if (life.Session.Storage.IsAvailable)
        {
            try { settings = life.Session.Storage.LoadSettings(1) ?? new GameData(); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            { life.Session.Log(ex.Message, GameLogLevel.Error); }
        }
        life.Ui.SetSliderValue(MusicSlider, (float)(settings?.GetNumber("musicPercent", mixer.Levels.Music * 100) ?? mixer.Levels.Music * 100));
        life.Ui.SetSelectedIndex(EffectsList, (int)Math.Clamp(settings?.GetNumber("effectsPreset", 1) ?? 1, 0, 2));
        life.Ui.SetToggleValue(MasterMute, settings?.GetBool("muted") ?? false);
        ApplyWidgets(life.Ui, mixer, life.Session, false);
    }

    private void ApplyWidgets(IGameUi ui, IGameAudio mixer, SessionState session, bool save)
    {
        float volume = ui.SliderValue(MusicSlider) ?? 80;
        int preset = ui.SelectedIndex(EffectsList) ?? 1;
        bool mute = ui.ToggleValue(MasterMute) == true;
        if (save && (volume, preset, mute) == (musicPercent, effectsPreset, muted)) return;
        (musicPercent, effectsPreset, muted) = (volume, preset, mute);
        mixer.Levels = new MixerLevels(mute ? 0 : masterGain, volume / 100f, preset switch { 0 => .375f, 2 => 1f, _ => .75f });
        var applied = mixer.Levels;
        session.Log(FormattableString.Invariant($"Mixer: music={applied.Music:0.###} effects={applied.Effects:0.###} master={applied.Master:0.###}"));
        if (!save || settings == null) return;
        settings.Set("musicPercent", volume);
        settings.Set("effectsPreset", preset);
        settings.Set("muted", mute);
        try { session.Storage.SaveSettings(settings); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { session.Log(ex.Message, GameLogLevel.Error); }
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        var audio = tick.Objects.Audio;
        if (tick.Input.Pressed("FocusMixer"))
        {
            if (tick.Ui.Focused.HasValue) tick.Ui.ClearFocus();
            else tick.Ui.FocusWidget(MusicSlider);
        }
        if (tick.Input.Pressed("Interact"))
        {
            beacon = !beacon;
            if (beacon) audio.Play(Beacon); else audio.Stop(Beacon);
        }
        if (tick.Input.Pressed("Fade"))
        {
            music = !music;
            if (music) audio.Play(Music); else audio.Stop(Music);
        }
        if (tick.Input.Pressed("MoveUp")) tick.Ui.SetSliderValue(MusicSlider, musicPercent == 20 ? 80 : 20);
        if (tick.Input.Pressed("MoveDown")) tick.Ui.SetSelectedIndex(EffectsList, effectsPreset == 0 ? 1 : 0);
        if (tick.Input.Pressed("MasterMute")) tick.Ui.SetToggleValue(MasterMute, !muted);
        ApplyWidgets(tick.Ui, audio, tick.Session, true);
        var levels = audio.Levels;
        var x = Math.Clamp(tick.Position.X + tick.Direction.X * 600 * tick.DeltaTime, 144, 624);
        tick.Move(new Vector2(x - tick.Position.X, 0));
        var status = $"Listener X  {x + 16:0}     |     Beacon  {(beacon ? "on" : "off")}     |     Music  {(music ? "playing" : "fading / stopped")}";
        var line = $"Master  {levels.Master:P0}       Music  {levels.Music:P0}       Effects  {levels.Effects:P0}";
        if (lastStatus != status) { lastStatus = status; tick.Ui.SetText(Readout, status); }
        if (lastLevels != line) { lastLevels = line; tick.Ui.SetText(Levels, line); }
    }
}
