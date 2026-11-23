using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private sealed record EventReceiver(SceneObject Object, string Script, string Method)
    {
        public override string ToString() => $"{Script}.{Method}";
    }

    private void AddGameEvents(StackPanel panel, ScriptDefinition definition, ScriptComponent component)
    {
        var links = component.EventLinks ?? [];
        void Commit(GameEventLink[] updated)
        {
            if (links.SequenceEqual(updated)) return;
            component.EventLinks = updated.Length == 0 ? null : updated;
            MarkDirty();
            RefreshComponentList();
        }
        foreach (var name in definition.Events.Union(links.Select(l => l.Event)))
        {
            bool declared = definition.Events.Contains(name);
            var header = new Grid { ColumnDefinitions = new("*,Auto"), ColumnSpacing = 5 };
            var title = new TextBlock { Text = declared ? name : $"{name} (missing event)",
                TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            header.Children.Add(title);
            ToolTip.SetTip(title, name);
            var add = new Button { Content = "Connect…", IsEnabled = declared && !Viewport.IsPlaying };
            AutomationProperties.SetName(add, $"Connect {name}");
            add.Click += (_, _) => RunAction(async () =>
            {
                if (await ChooseGameEventReceiver(name, links, null) is {} link) Commit([..links, link]);
            });
            Grid.SetColumn(add, 1);
            header.Children.Add(add);
            panel.Children.Add(header);
            foreach (var link in links.Where(l => l.Event == name))
            {
                var owner = Scene.Objects.FirstOrDefault(o => o.Id == link.Target);
                string label = $"{owner?.Name ?? "Missing object"} · {link.Script}.{link.Method}";
                var row = new Grid { ColumnDefinitions = new("*,Auto"), ColumnSpacing = 5, IsEnabled = !Viewport.IsPlaying };
                var edit = new Button { Content = new TextBlock { Text = label, TextTrimming = TextTrimming.CharacterEllipsis },
                    HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, IsEnabled = declared };
                ToolTip.SetTip(edit, owner == null ? label : $"{ObjectPath(owner)} · {link.Script}.{link.Method}");
                AutomationProperties.SetName(edit, label);
                edit.Click += (_, _) => RunAction(async () =>
                {
                    if (await ChooseGameEventReceiver(name, links, link) is {} replacement)
                        Commit(links.Select(l => l == link ? replacement : l).ToArray());
                });
                var remove = new Button { Content = "−" };
                AutomationProperties.SetName(remove, $"Disconnect {name} from {label}");
                remove.Click += (_, _) => Commit(links.Where(l => l != link).ToArray());
                row.Children.Add(edit);
                Grid.SetColumn(remove, 1);
                row.Children.Add(remove);
                panel.Children.Add(row);
            }
        }
    }

    private Task<GameEventLink?> ChooseGameEventReceiver(string name, GameEventLink[] links, GameEventLink? editing)
    {
        var definitions = scripts!.Current().Components;
        var receivers = (from obj in Scene.Objects
                         from script in obj.Components.OfType<ScriptComponent>()
                         where definitions.ContainsKey(script.Script)
                         from method in definitions[script.Script].Handlers
                         let link = new GameEventLink(name, obj.Id, script.Script, method)
                         where link == editing || !links.Contains(link)
                         select new EventReceiver(obj, script.Script, method)).ToArray();
        var grid = new Grid { ColumnDefinitions = EditorTokens.LabelColumns(EditorTokens.FieldLabelWidth), RowDefinitions = new("Auto,Auto,Auto"),
            RowSpacing = 6, ColumnSpacing = 5, Margin = new Thickness(10) };
        var objects = receivers.Select(r => r.Object).Distinct().Select(o => new ObjectReference(o.Id, ObjectPath(o))).ToArray();
        var target = new ComboBox { ItemsSource = objects, PlaceholderText = "No available receivers", HorizontalAlignment = HorizontalAlignment.Stretch };
        var handler = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(target, "Receiver object");
        AutomationProperties.SetName(handler, "Receiver method");
        void Put(Control control, int r, int c) { Grid.SetRow(control,r); Grid.SetColumn(control,c); grid.Children.Add(control); }
        Put(new TextBlock { Text = "Object", Classes = { "fieldLabel" } },0,0); Put(target,0,1);
        Put(new TextBlock { Text = "Method", Classes = { "fieldLabel" } },1,0); Put(handler,1,1);
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        var confirm = new Button { Content = "Connect", IsEnabled = false };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 5, Children = { cancel, confirm } };
        Put(buttons,2,1);
        var window = new Window { Title = name, Width = 340, MinWidth = 270, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = grid };
        target.SelectionChanged += (_, _) =>
        {
            var methods = receivers.Where(r => r.Object.Id == (target.SelectedItem as ObjectReference)?.Id).ToArray();
            handler.ItemsSource = methods;
            handler.SelectedItem = methods.FirstOrDefault(r => r.Script == editing?.Script && r.Method == editing.Method) ?? methods.FirstOrDefault();
        };
        handler.SelectionChanged += (_, _) => confirm.IsEnabled = handler.SelectedItem != null;
        target.SelectedItem = objects.FirstOrDefault(o => o.Id == editing?.Target) ?? objects.FirstOrDefault();
        cancel.Click += (_, _) => window.Close();
        confirm.Click += (_, _) => { if (handler.SelectedItem is EventReceiver r) window.Close(new GameEventLink(name, r.Object.Id, r.Script, r.Method)); };
        return window.ShowDialog<GameEventLink?>(this);
    }
}
