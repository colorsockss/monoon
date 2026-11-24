using Avalonia.Input;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private void ToggleSelection(SceneObject? obj)
    {
        if(obj==null)return;
        var selection=Viewport.Selection.ToList();
        if(!selection.Remove(obj))selection.Add(obj);
        ShowSelection(selection);
    }

    private void Select(SceneObject? obj)
    {
        rangeAnchor = obj;
        ShowSelection(obj == null ? [] : [obj]);
    }

    private void ShowSelection(IReadOnlyList<SceneObject> selection)
    {
        var obj=selection.LastOrDefault();
        inspectedAsset=null; // an object selection takes the Inspector back from a Project asset (13.16)
        Viewport.Selection=selection;
        foreach(var (owner,row) in treeItems)row.Classes.Set("groupSelected",selection.Count>1 && selection.Contains(owner));
        refreshing = true;
        if (Viewport.TileEditOwner != null && (Viewport.TileEditOwner != obj?.Id || obj?.GetComponent<Tilemap2DComponent>() == null))
            Viewport.EditTiles(null);
        Viewport.Selected = obj;
        SelectHierarchyItem(obj);
        UpdatePrefabMenus();
        refreshing = false;
        RefreshInspector();
        RefreshComponentList();
        Viewport.RefreshScene();
    }
}
