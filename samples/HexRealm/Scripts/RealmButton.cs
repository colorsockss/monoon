using System;
using System.Drawing;
using System.Numerics;
using Engine.Core;

public sealed class RealmButton : GameBehaviour
{
    public string Action = "";
    public Guid Caption, Key, KeyCaption, Seal, SelectionMark;
    public int Width, Height;
    public string[] Titles = [], Keys = [];
    public float[] TextMetrics = [], KeyMetrics = [];

    private IGameObjects objects = null!;
    private IGameUi ui = null!;
    private Guid id;
    private int option;
    private bool enabled = true, selected;
    private float lastScale;

    public override void OnCreate(ScriptLife life)
    {
        objects = life.Objects;
        ui = life.Ui;
        id = life.ObjectId;
        ApplyOption();
    }

    public override void OnUiClick(UiClick click)
    {
        click.Session.Set(Action);
        click.Ui.ClearFocus();
    }

    internal void SetOption(int index, bool interactable, bool active = false)
    {
        if (option == index && enabled == interactable && selected == active) return;
        (option, enabled, selected) = (index, interactable, active);
        if (ui != null) ApplyOption();
    }

    private void ApplyOption()
    {
        ui.SetText(Caption, Titles[option]);
        ui.SetInteractable(id, enabled);
        if (SelectionMark != Guid.Empty) ui.SetVisible(SelectionMark, selected);
        if (Key != Guid.Empty)
        {
            ui.SetText(KeyCaption, Keys[option]);
            ui.SetVisible(Key, Keys[option].Length != 0);
        }
        Layout(ui.Preferences.TextScale);
    }

    public override void UpdateUi(UiTick tick)
    {
        float scale = tick.Ui.Preferences.TextScale;
        if (scale != lastScale) Layout(scale);
    }

    private void Layout(float scale)
    {
        lastScale = scale;
        Vector2 caption = new Vector2(TextMetrics[2 * option], TextMetrics[2 * option + 1]) * scale;
        Vector2 key = new Vector2(KeyMetrics[2 * option], KeyMetrics[2 * option + 1]) * scale;
        float keyWidth = key.X == 0 ? 0 : Math.Max(24, MathF.Ceiling((key.X + 16) / 4) * 4);
        float keyHeight = 24 + 10 * (scale - 1);
        float iconWidth = Seal == Guid.Empty ? 0 : 32;
        float groupWidth = iconWidth + caption.X + (keyWidth == 0 ? 0 : keyWidth + 8);
        float x = (Width - groupWidth) / 2;
        if (Seal != Guid.Empty)
            Place(Seal, new(x, (Height - 24) / 2f, 0, 0));
        x += iconWidth;
        Place(Caption, new(x, Height / 2f - caption.Y, -x, 0));
        if (keyWidth == 0) return;
        Place(Key, new(x + caption.X + 8, (Height - keyHeight) / 2, keyWidth - 24, keyHeight - 24));
        Place(KeyCaption, new((keyWidth - key.X) / 2, keyHeight / 2 - key.Y, keyWidth - 152, 0));
    }

    private void Place(Guid widget, RectangleF offset) =>
        objects.Tween(widget, TweenClock.Ui).UiRect(offset, 0);
}
