using System;
using System.IO;
using Engine.Core;

// AccessLab showcase: one settings widget. Action 1 language dropdown, 2 text size slider (75-200 %), 3 high contrast toggle,
// 4 screen shake slider and 5 flash slider (0-100 %) write Ui.Preferences as soon as the value changes, so every label
// re-translates and re-flows that frame and the next firework obeys the new scales. Action 6 is the save button: it writes the
// preferences and the typed name to player settings. Raises the HUD flags "lang", "scale", "contrast", "calm".
public sealed class OptionSetting : GameBehaviour
{
    public int Action;
    public Guid NameField, Language, Receipt, Form;
    private static readonly string[] Locales = ["ko-KR", "ja", "en"];

    // The save also records how it was reached (IME composition seen, save focused, form scroll) for Accessibility.Mac.
    public override void UpdateUi(UiTick tick)
    {
        if (Action == 6 && tick.Ui.Preedit.Length != 0) tick.Session.Set("sawComposition");
    }

    public override void OnUiValueChanged(UiValueChanged change)
    {
        var options = change.Ui.Preferences;
        switch (Action)
        {
            case 1: options.Locale = Locales[Math.Clamp((int)change.Value, 0, 2)]; change.Session.Set("lang"); break;
            case 2: options.TextScale = change.Value / 100f; change.Session.Set("scale"); break;
            case 3: options.HighContrast = change.Value > .5f; change.Session.Set("contrast"); break;
            case 4: options.ShakeScale = change.Value / 100f; change.Session.Set("calm"); break;
            case 5: options.FlashScale = change.Value / 100f; change.Session.Set("calm"); break;
        }
    }

    public override void OnUiClick(UiClick click)
    {
        if (Action != 6) return;
        var ui = click.Ui;
        var settings = new GameData();
        ui.Preferences.SaveTo(settings);
        string name = ui.Value(NameField) ?? "";
        settings.Set("name", name);
        settings.Set("scroll", ui.ScrollOffset(Form));
        settings.Set("saveFocused", ui.Focused == click.ObjectId);
        settings.Set("sawComposition", click.Session.Has("sawComposition"));
        try { if (click.Session.Storage.IsAvailable) click.Session.Storage.SaveSettings(settings); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { ui.SetText(Receipt, "Could not save."); return; }
        string saved = ui.Preferences.Locale switch { "ja" => "保存しました", "en" => "Saved", _ => "저장했습니다" };
        ui.SetText(Receipt, $"{saved}  ·  {name}  ·  {ui.Preferences.TextScale * 100:0}%");
    }
}
