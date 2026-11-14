#nullable enable
using System;
using Engine.Core;

namespace MoNoon.Dialogue;

public sealed class DialogueSession
{
    private readonly DialogueGraph graph;
    private bool choosing;
    public DialogueNode? Current { get; private set; }
    public string Position => Current?.Id ?? "";

    public DialogueSession(DialogueGraph graph) => this.graph = graph;
    public void Begin() => Current = graph.Find(graph.Start);
    public void Close() => Current = null;
    public void Restore(string id) => Current = id == "" ? null : graph.Find(id);

    public bool Choose(int index, Func<string, bool>? tryAction = null)
    {
        if (choosing || Current == null || (uint)index >= (uint)Current.Choices.Count) return false;
        var option = Current.Choices[index];
        var next = option.Next.Length == 0 ? null : graph.Find(option.Next);
        choosing = true;
        try
        {
            if (option.Action.Length > 0 && (tryAction == null || !tryAction(option.Action))) return false;
            Current = next;
            return true;
        }
        finally { choosing = false; }
    }
}

public sealed class DialoguePlayer : GameBehaviour
{
    public DataReference Story;
    public DialogueSession Session { get; private set; } = null!;
    public override void OnCreate(ScriptLife life) =>
        Session = new DialogueSession(DialogueGraph.Read(life.Objects.ReadData(Story)));
}
