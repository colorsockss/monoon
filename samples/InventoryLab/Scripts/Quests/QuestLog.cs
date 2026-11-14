using Engine.Core;

namespace MoNoon.Quests;

public sealed class QuestLog : GameBehaviour
{
    public TableReference Catalog;
    public QuestJournal Journal { get; private set; }

    public override void OnCreate(ScriptLife life) => Journal = new QuestJournal(life.Objects.ReadTable(Catalog));
}
