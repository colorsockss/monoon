using System;
using System.Collections.Generic;
using Engine.Core;

public sealed class PressurePlate : GameBehaviour
{
    public Guid Player, Crate, Door, Label;
    public SpriteReference ClosedSprite, OpenSprite;
    private readonly HashSet<Guid> occupants = new();
    private bool open, changed;
    private string locale;

    public override void OnEnable(ScriptLife life) => changed = true;

    public override void OnTriggerEnter(ScriptContact contact)
    {
        if (contact.OtherId == Player || contact.OtherId == Crate)
            changed |= occupants.Add(contact.OtherId);
    }

    public override void OnTriggerExit(ScriptContact contact) => changed |= occupants.Remove(contact.OtherId);

    public override void FixedUpdate(ScriptTick tick)
    {
        if (!changed && locale == tick.Ui.Preferences.Locale) return;
        locale = tick.Ui.Preferences.Locale;
        // Evaluate the entire preceding physics tick, including simultaneous arrivals and departures.
        changed = false;
        open = occupants.Count == 2 || open && occupants.Count > 0;
        Show(tick.ObjectId, tick.Objects, tick.Ui);
    }

    public override void OnDisable(ScriptLife life)
    {
        occupants.Clear();
        open = false;
        Show(life.ObjectId, life.Objects, life.Ui);
    }

    private void Show(Guid plate, IGameObjects objects, IGameUi ui)
    {
        objects.SetColliderEnabled(Door, !open);
        objects.SetSprite(Door, open ? OpenSprite : ClosedSprite);
        objects.SetSpriteTint(plate, open ? "#3E8372" : occupants.Count == 1 ? "#A5803D" : "#485368");
        string weights = occupants.Count switch {
            2 => "plate.both", 1 => occupants.Contains(Player) ? "plate.player" : "plate.crate", _ => "plate.empty"
        };
        ui.SetText(Label, string.Format(ui.Translate("plate.status"), occupants.Count, ui.Translate(weights), ui.Translate(open ? "door.open" : "door.closed")));
    }
}
