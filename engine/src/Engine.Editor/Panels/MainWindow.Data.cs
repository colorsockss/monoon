using Engine.Core;
using MoNoon.Dialogue;

namespace Engine.Editor;

public partial class MainWindow
{
    private Task EditData(string? relative) => EditValues(relative,false);
    private async Task EditValues(string? relative,bool table,bool dialogue = false)
    {
        if (project==null && !await Save()) return;
        var root=project!;
        var original=relative==null?null:File.ReadAllText(Path.Combine(root,relative));
        if (!table && original != null)
        {
            try { dialogue = GameData.Parse(original).GetText("format") == DialogueGraph.Format; }
            catch (InvalidDataException) { /* The generic data editor can repair malformed YAML. */ }
        }
        var folder=relative==null?CreationFolder("Assets"):ParentPath(relative);
        PrefabFiles? before = null;
        string Write(string stem,Action<string,bool> write)
        {
            if (!ProjectImport.ValidName(stem)) throw new InvalidDataException("Enter a valid asset name.");
            var target=relative ?? folder+"/"+stem+(table?GameTable.Extension:ProjectAssets.Data);
            var path=Path.Combine(root,target);
            if (original!=null && (!File.Exists(path) || File.ReadAllText(path)!=original))
                throw new IOException("This file changed on disk. Reopen it before saving.");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            before = PrefabFileEdit.Read(path);
            write(path,original!=null);
            return target;
        }
        string kind = dialogue ? "Dialogue" : "Data";
        if (!await ReplaceEditor(kind, relative)) return;
        EditorPanel editor = dialogue ? new DialogueWindow(relative,original,Write) : new ValuesWindow(relative,original,table) { Write = Write };
        editor.Asset = relative;
        ShowEditor<string>(kind, editor, saved => Saved(root, saved, before!));
    }

    private void Saved(string root, string saved, PrefabFiles before)
    {
        var assets = ProjectAssets.Scan(root);
        if (saved == assets.PathFor(Scene.Settings.Localization?.Table ?? Guid.Empty) ||
            saved == assets.PathFor(Scene.Settings.UiTheme ?? Guid.Empty))
            Viewport.InvalidateUi();
        var savedPath = Path.Combine(root, saved);
        var edit = new PrefabFileEdit(savedPath, before, PrefabFileEdit.Read(savedPath));
        if (edit.HasChanges) RecordEdit(Selected?.Id, asset: edit);
        BrowseFolder(ParentPath(saved),saved);
        RefreshComponentList();
        Status.Text=$"Saved {saved}.";
    }
}
