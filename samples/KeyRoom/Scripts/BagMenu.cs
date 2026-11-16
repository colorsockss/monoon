using System;
using Engine.Core;

public sealed class BagMenu : GameBehaviour
{
    public Guid OpenPanel, ClosePanel;
    public string Shortcut = "";
    public bool EquipCompass;
    public Guid StatusLabel, QuestionLabel, QuestLabel;
    private (bool Equipped, bool HasKey, string Locale)? displayed;

    public override void UpdateUi(UiTick tick)
    {
        if (StatusLabel != Guid.Empty)
        {
            bool equipped = tick.Session.Has("compass");
            var state = (Equipped: equipped, HasKey: tick.Session.Has("key"), Locale: tick.Ui.Preferences.Locale);
            if (displayed != state)
            {
                displayed = state;
                var ui = tick.Ui;
                ui.SetText(StatusLabel, string.Format(ui.Translate(equipped ? "bag.equipped" : "bag.stowed"), ui.Translate("item.compass")));
                ui.SetTextKey(QuestionLabel, equipped ? "bag.stow" : "bag.equip");
                ui.SetText(QuestLabel, string.Format(ui.Translate(state.HasKey ? "quest.key.done" : "quest.key.active"), ui.Translate("item.key")));
            }
        }
        if (Shortcut.Length > 0 && tick.Input.Pressed(Shortcut)) Select(tick.Ui, tick.Session);
    }

    public override void OnUiClick(UiClick click) => Select(click.Ui, click.Session);

    private void Select(IGameUi ui, SessionState session)
    {
        if (OpenPanel != Guid.Empty)
        {
            if (ui.ActiveInputMode == null) ui.ClearFocus(); // Back from the bag returns the arrow keys to movement.
            ui.PushInputMode(OpenPanel);
        }
        else if (ui.PopInputMode(ClosePanel) && EquipCompass)
        {
            if (!session.Clear("compass")) session.Set("compass");
        }
    }
}
