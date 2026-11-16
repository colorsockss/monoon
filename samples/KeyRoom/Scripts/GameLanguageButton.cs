using System;
using Engine.Core;

public sealed class GameLanguageButton : GameBehaviour
{
    public override void OnUiClick(UiClick click) => Toggle(click.Ui);
    public override void UpdateUi(UiTick tick)
    {
        if (tick.Input.Pressed("ChangeLanguage")) Toggle(tick.Ui);
    }

    private static void Toggle(IGameUi ui)
    {
        if (ui.ActiveInputMode == null) ui.ClearFocus();
        ui.Preferences.Locale = ui.Preferences.Locale.StartsWith("ko", StringComparison.OrdinalIgnoreCase) ? "en" : "ko";
    }
}
