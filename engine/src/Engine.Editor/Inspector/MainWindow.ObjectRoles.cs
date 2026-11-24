using Avalonia.Controls;
using Avalonia.Input;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private SceneObject? roleFieldOwner;

    private void InitializeObjectRoles()
    {
        RoleSection.Foldout = new ComponentFoldout("Tags & groups");
        foreach (var box in new[] { ObjectTag, ObjectGroups })
        {
            box.PropertyChanged += (_, e) =>
            {
                if (e.Property != TextBox.TextProperty || refreshing || Viewport.IsPlaying ||
                    Inspected is not {} owner || owner != roleFieldOwner || !box.IsKeyboardFocusWithin) return;
                if (box == ObjectTag) owner.Tag = box.Text;
                else owner.Groups = [box.Text ?? ""];
                MarkDirty(box);
            };
            box.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter) { Viewport.Focus(); e.Handled = true; }
            };
            box.LostFocus += (_, _) => RefreshInspector();
        }
        RoleQuery.TextChanged += (_, _) => RefreshRoleQuery();
        RoleKindPicker.SelectionChanged += (_, _) => RefreshRoleQuery();
    }

    private void RefreshRoleFields(SceneObject owner)
    {
        bool changed = owner != roleFieldOwner;
        if (changed || !ObjectTag.IsKeyboardFocusWithin) ObjectTag.Text = owner.Tag ?? "";
        if (changed || !ObjectGroups.IsKeyboardFocusWithin) ObjectGroups.Text = string.Join(", ", owner.Groups ?? []);
        roleFieldOwner = owner;
    }

    private SceneGame? queryWorld;
    private long queryRevision;
    private string? queryName;
    private int queryKind;

    private void RefreshRoleQuery()
    {
        var world = Viewport.PlayWorld;
        var name = RoleQuery.Text?.Trim() ?? "";
        int kind = RoleKindPicker.SelectedIndex;
        if (world == queryWorld && name == queryName && kind == queryKind && world?.HierarchyRevision == queryRevision) return;
        queryWorld = world;
        queryName = name;
        queryKind = kind;
        queryRevision = world?.HierarchyRevision ?? 0;
        string result = "";
        if (world != null && name.Length != 0)
        {
            var ids = kind == 1 ? world.FindByTag(name) : world.FindByGroup(name);
            result = ids.Count == 0 ? "0" : $"{ids.Count} · {string.Join(", ", ids.Take(12).Select(world.ObjectLabel))}";
            if (ids.Count > 12) result += " …";
        }
        RoleMatches.Text = result;
        ToolTip.SetTip(RoleMatches, result);
    }
}
