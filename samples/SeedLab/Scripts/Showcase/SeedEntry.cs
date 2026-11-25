using System;
using Engine.Core;

// The seed panel's Go button: hands the typed seed to the dungeon (DungeonMaze.ApplySeed).
public sealed class SeedEntry : GameBehaviour
{
    public ComponentReference<DungeonMaze> Board;
    public Guid Field;

    public override void OnUiClick(UiClick click) =>
        Board.Resolve(click.Objects)?.ApplySeed(Board.ObjectId, click.Objects, click.Ui, click.Ui.Value(Field) ?? "");
}
