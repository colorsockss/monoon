using System;
using Engine.Core;

public sealed class ApplySeed : GameBehaviour
{
    public ComponentReference<MazeRun> Board;
    public Guid Field;

    public override void OnUiClick(UiClick click) =>
        Board.Resolve(click.Objects)?.ApplySeed(Board.ObjectId, click.Objects, click.Ui, click.Ui.Value(Field) ?? "");
}
