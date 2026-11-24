using System;
using Engine.Core;

// A 4×5 bag (UI Layout Grid with draggable UI Button slots, roadmap 16.13). Dropping one slot on another swaps their contents,
// whether the slot was dragged with the mouse or picked up and put down with Enter / Space / pad South.
public sealed class BagGrid : GameBehaviour
{
    public Guid Status;

    public override void OnUiDrop(UiDrop drop)
    {
        var ui = drop.Ui;
        string from = ui.TextOf(drop.Source) ?? "", to = ui.TextOf(drop.Target) ?? "";
        ui.SetText(drop.Source, to);
        ui.SetText(drop.Target, from);
        ui.SetText(Status, from.Length == 0 && to.Length == 0 ? "빈 칸끼리는 바뀌는 것이 없습니다." :
            to.Length == 0 ? $"{from}을(를) 빈 칸으로 옮겼습니다." : from.Length == 0 ? $"{to}을(를) 빈 칸으로 옮겼습니다." : $"{from}과(와) {to}의 자리를 바꿨습니다.");
    }
}
