using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private Border? referenceHighlight;

    // The drop place under the pointer: the Inspector, a docked tool panel such as Particle, or a torn-off panel window.
    private Border? ReferenceTarget(PointerEventArgs e) => ReferenceTarget(this.PointToScreen(e.GetPosition(this)));

    private Border? ReferenceTarget(PixelPoint screen)
    {
        foreach (var window in OwnedWindows.Where(w => w.IsVisible).Append(this))
        {
            var point = window.PointToClient(screen);
            if (!new Rect(window.Bounds.Size).Contains(point)) continue;
            var hit = window.InputHitTest(point) as Visual;
            return hit?.GetSelfAndVisualAncestors().OfType<Border>().FirstOrDefault(b => b.Tag is ReferenceDrop);
        }
        return null;
    }

    // accept false marks a field that refuses what is dragged (13.17): only the border colour changes.
    private void HighlightReference(Border? target, bool accept = true)
    {
        if (referenceHighlight == target && target?.Classes.Contains("referenceReject") == !accept) return;
        referenceHighlight?.Classes.Remove("referenceDrop");
        referenceHighlight?.Classes.Remove("referenceReject");
        referenceHighlight = target;
        target?.Classes.Add(accept ? "referenceDrop" : "referenceReject");
    }

    private Control ReferenceList(ScriptField field, ScriptComponent component)
    {
        var list = new StackPanel { Spacing = 2 };
        var extension = field.AssetExtension;
        Func<string, bool>? accepts = extension == null ? null : EndsWith(extension);
        var ids = component.ReadReferences(field.Name, field.IsObjectList).ToList();
        void Commit()
        {
            component.WriteReferences(field.Name, ids.ToArray());
            MarkDirty();
            Render();
        }

        Border Target(Control content, int index)
        {
            var border = new Border { Child = content, Classes = { "referenceRow" } };
            // Several assets dropped at once go in at this place in their order: one Undo step (13.17).
            border.Tag = new ReferenceDrop(list, index, accepts, added => { ids.InsertRange(index, added); Commit(); }, Many: true);
            return border;
        }

        void Render()
        {
            var assets = extension != null && project != null ? ProjectAssets.ReadManifest(project) : null;
            list.Children.Clear();
            var heading = new Grid { ColumnDefinitions = EditorTokens.LabelColumns(EditorTokens.FieldLabelWidth), ColumnSpacing = EditorTokens.FieldGap };
            heading.Children.Add(new TextBlock { Text = field.Name, Classes = { "fieldLabel" },
                TextTrimming = TextTrimming.CharacterEllipsis });
            string hint = extension == null ? "Drop object here" : $"Drop {extension} here";
            var append = new TextBlock { Text = $"{hint} ({ids.Count})", Classes = { "label" } };
            Grid.SetColumn(append, 1);
            heading.Children.Add(append);
            var header = Target(heading, ids.Count);
            ToolTip.SetTip(header, $"{field.Name} · Drag from {(extension == null ? "Hierarchy" : "Project")} to append");
            list.Children.Add(header);

            for (int i = 0; i < ids.Count; i++)
            {
                int index = i;
                var obj = extension == null ? (Viewport.PlayWorld?.Objects ?? Scene.Objects).FirstOrDefault(o => o.Id == ids[index]) : null;
                var path = assets?.PathFor(ids[index]);
                string name = obj?.Name ?? (path != null ? Path.GetFileName(path) :
                    ids[index] == Guid.Empty ? "None" : extension == null ? "Missing object" : "Missing asset");
                var row = new Grid { ColumnDefinitions = EditorTokens.LabelColumns(EditorTokens.FieldLabelWidth, "*,Auto"), ColumnSpacing = EditorTokens.FieldGap };
                row.Children.Add(new TextBlock { Text = (index + 1).ToString(), Classes = { "fieldLabel" } });
                var handle = new Button { HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Content = new TextBlock { Text = name, TextTrimming = TextTrimming.CharacterEllipsis } };
                AutomationProperties.SetName(handle, $"{field.Name} {index + 1}: {name}");
                ToolTip.SetTip(handle, obj == null ? path ?? name : ObjectPath(obj));
                Grid.SetColumn(handle, 1);
                row.Children.Add(handle);
                var remove = new Button { Content = "−" };
                AutomationProperties.SetName(remove, $"Remove {field.Name} {index + 1}");
                remove.Click += (_, _) => { ids.RemoveAt(index); Commit(); };
                Grid.SetColumn(remove, 2);
                row.Children.Add(remove);
                list.Children.Add(Target(row, index));

                // Keep the capture on the source until release. No scene edits occur during hover.
                handle.AddHandler(PointerPressedEvent, (_, e) =>
                {
                    if (Viewport.IsPlaying || !e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed) return;
                    handle.Focus();
                    e.Pointer.Capture(handle);
                    e.Handled = true;
                }, RoutingStrategies.Tunnel);
                handle.PointerMoved += (_, e) =>
                {
                    if (e.Pointer.Captured != handle) return;
                    var target = ReferenceTarget(e);
                    HighlightReference(target?.Tag is ReferenceDrop drop && drop.List == list ? target : null);
                };
                handle.AddHandler(PointerReleasedEvent, (_, e) =>
                {
                    if (e.Pointer.Captured != handle) return;
                    var target = ReferenceTarget(e)?.Tag as ReferenceDrop;
                    e.Pointer.Capture(null);
                    e.Handled = true;
                    if (target?.List != list) return;
                    int destination = Math.Min(target.Index, ids.Count - 1);
                    if (destination == index) return;
                    var id = ids[index];
                    ids.RemoveAt(index);
                    ids.Insert(destination, id);
                    Commit();
                }, RoutingStrategies.Tunnel);
                handle.PointerCaptureLost += (_, _) => HighlightReference(null);
            }
        }
        list.IsEnabled = !Viewport.IsPlaying;
        Render();
        return list;
    }
}
