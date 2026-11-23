using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using MoNoon.Dialogue;
using Engine.Core;
using static Engine.Editor.EditorControls;

namespace Engine.Editor;

internal sealed class DialogueWindow : EditorPanel
{
    private readonly DialogueGraph graph;
    private readonly ComboBox nodes = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly StackPanel detail = new() { Spacing = EditorTokens.Gap2 };
    private readonly TextBlock error = new() { IsVisible = false, Classes = { "error" }, TextWrapping = TextWrapping.Wrap };
    private bool refreshing;

    public DialogueWindow(string? path, string? original, Func<string, Action<string,bool>,string> write)
    {
        Title = path == null ? "Dialogue" : $"{Path.GetFileNameWithoutExtension(path)} · Dialogue"; MinWidth = 430; MinHeight = 320;
        graph = original == null ? new DialogueGraph() : DialogueGraph.Read(GameData.Parse(original));
        if (original == null) graph.Nodes.Add(new DialogueNode { Id = "start", Text = "Hello." });
        var name = Box("Asset name", path == null ? "NewDialogue" : Path.GetFileNameWithoutExtension(path));
        name.IsReadOnly = path != null;
        var layout = new Grid { Margin = EditorTokens.WindowPadding, RowDefinitions = new("Auto,Auto,*,Auto,Auto"), RowSpacing = EditorTokens.Gap2 };
        layout.Children.Add(Row("Name", name));
        var bar = new Grid { ColumnDefinitions = new("*,Auto,Auto"), ColumnSpacing = EditorTokens.Gap };
        AutomationProperties.SetName(nodes,"Dialogue node");
        nodes.SelectionChanged += (_,_) => { if (!refreshing && nodes.SelectedIndex >= 0) DrawNode(graph.Nodes[nodes.SelectedIndex]); };
        bar.Children.Add(nodes);
        Put(bar, Button("+ Node", () =>
        {
            int i = 1;
            while (graph.Nodes.Any(n => n.Id == "node" + i)) i++;
            var node = new DialogueNode { Id = "node" + i, Text = "New line." };
            graph.Nodes.Add(node); Select(node);
        }), 0, 1);
        Put(bar, Button("Remove node", () => Attempt(() =>
        {
            var node = graph.Nodes[nodes.SelectedIndex];
            if (node.Id == graph.Start || graph.Nodes.Any(n => n != node && n.Choices.Any(c => c.Next == node.Id)))
                throw new InvalidDataException("Change the start node or incoming choices before removing this node.");
            graph.Nodes.Remove(node); Select(graph.Find(graph.Start));
        })), 0, 2);
        Put(layout, Row("Node", bar), 1);
        Put(layout, new ScrollViewer { Content = detail, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, 2);
        Put(layout, error, 3);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = EditorTokens.Gap };
        var cancel = Button("Cancel", () => Close()); footer.Children.Add(cancel);
        var save = Button("Save", () => Attempt(() =>
        {
            var yaml = graph.Capture().ToYaml();
            Close(write(name.Text?.Trim() ?? "", (file, overwrite) => GameData.SaveYaml(file,yaml,overwrite)));
        }));
        save.Classes.Add("primary"); footer.Children.Add(save); Put(layout, footer, 4);
        Content = layout; Select(graph.Find(graph.Start));
    }

    private void Select(DialogueNode node)
    {
        refreshing = true;
        nodes.ItemsSource = graph.Nodes.Select(n => n.Id).ToArray();
        nodes.SelectedIndex = graph.Nodes.IndexOf(node);
        refreshing = false;
        DrawNode(node);
    }

    private void DrawNode(DialogueNode node)
    {
        detail.Children.Clear();
        var id = Box("Node ID", node.Id);
        var identity = new Grid { ColumnDefinitions = new("*,Auto,Auto"), ColumnSpacing = EditorTokens.Gap };
        identity.Children.Add(id);
        Put(identity, Button("Rename", () => Attempt(() =>
        {
            string value = id.Text?.Trim() ?? "";
            if (value.Length == 0 || graph.Nodes.Any(n => n != node && n.Id == value))
                throw new InvalidDataException("Use a unique, nonempty node ID.");
            foreach (var option in graph.Nodes.SelectMany(n => n.Choices).Where(c => c.Next == node.Id)) option.Next = value;
            if (graph.Start == node.Id) graph.Start = value;
            node.Id = value; Select(node);
        })), 0, 1);
        var start = Button(graph.Start == node.Id ? "Start node" : "Set as start", () => { graph.Start = node.Id; DrawNode(node); });
        start.IsEnabled = graph.Start != node.Id; Put(identity, start, 0, 2);
        detail.Children.Add(Row("ID", identity));
        detail.Children.Add(Row("Speaker", Box("Speaker",node.Speaker,v => node.Speaker = v)));
        var line = Box("Line",node.Text,v => node.Text = v);
        line.AcceptsReturn = true; line.TextWrapping = TextWrapping.Wrap; line.MinHeight = 48;
        detail.Children.Add(Row("Line", line));
        for (int i = 0; i < node.Choices.Count; i++)
        {
            var option = node.Choices[i];
            detail.Children.Add(new Border { Classes = { "divider" } });
            var top = new Grid { ColumnDefinitions = new("*,Auto"), ColumnSpacing = EditorTokens.Gap };
            top.Children.Add(Box($"Choice {i+1} text", option.Text, v => option.Text = v));
            Put(top,Button("Remove", () => { node.Choices.Remove(option); DrawNode(node); }), 0, 1);
            detail.Children.Add(Row($"Choice {i+1}",top));
            var options = graph.Nodes.Select(n => n.Id).ToList();
            var next = new ComboBox { ItemsSource = new[] { "(End dialogue)" }.Concat(options).ToArray(),
                SelectedIndex = options.IndexOf(option.Next) + 1, HorizontalAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetName(next, $"Choice {i+1} target");
            next.SelectionChanged += (_,_) => { if (next.SelectedIndex >= 0) option.Next = next.SelectedIndex == 0 ? "" : options[next.SelectedIndex-1]; };
            detail.Children.Add(Row("Next",next));
            detail.Children.Add(Row("Action",Box($"Choice {i+1} action", option.Action,v => option.Action = v)));
        }
        detail.Children.Add(Row("",Button("+ Choice", () => { node.Choices.Add(new() { Text = "Continue" }); DrawNode(node); })));
    }

    private void Attempt(Action action)
    {
        try { error.IsVisible = false; action(); }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
        { error.Text = e.Message; error.IsVisible = true; }
    }
    private static Control Row(string label, Control field)
    {
        var grid = new Grid { ColumnDefinitions = EditorTokens.LabelColumns(EditorTokens.DialogLabelWidth), ColumnSpacing = EditorTokens.Gap2 };
        grid.Children.Add(new TextBlock { Text = label, Classes = { "fieldLabel" } });
        Put(grid, field, 0, 1); return grid;
    }
}
