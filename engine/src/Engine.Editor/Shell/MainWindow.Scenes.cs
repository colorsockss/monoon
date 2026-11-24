using Avalonia.Platform.Storage;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private static string? FindSample()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "engine", "MoNoonEngine.slnx")) &&
                Directory.Exists(Path.Combine(folder.FullName, "samples", "KeyRoom")))
                return Path.Combine(folder.FullName, "samples", "KeyRoom");
        }
        return null;
    }

    private async Task ChooseProject(bool create)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = create ? "Choose an empty folder for the new project" : "Open project folder"
        });
        if (folders.FirstOrDefault()?.TryGetLocalPath() is not { } root || !await CanLeave()) return;
        if (create)
        {
            CreateProject(root, SceneDocument.Starter(), "Main");
        }
        else LoadProject(root);
    }

    private static string? FirstScene(string root)
    {
        var folder = Path.Combine(root, "Scenes");
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.ReparsePoint };
        return Directory.EnumerateFiles(folder, "*" + Suffix, options)
            .Select(path => Path.GetRelativePath(folder, path)[..^Suffix.Length].Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(name => name.Contains('/')).ThenBy(name => name, StringComparer.Ordinal).FirstOrDefault();
    }

    private void LoadProject(string root)
    {
        if (!Directory.Exists(Path.Combine(root, "Scenes")))
            throw new InvalidDataException("Choose a project folder containing a Scenes directory.");
        PrepareMigration(root);
        var settings = GameSettings.Load(root);
        var start = settings.StartScene is Guid id ? ProjectAssets.Scan(root).PathFor(id) : null;
        var name = start?.StartsWith("Scenes/") == true && start.EndsWith(Suffix) && File.Exists(Path.Combine(root, start))
            ? start[7..^Suffix.Length] : FirstScene(root);
        var scene = name == null ? SceneDocument.Empty(GameSettings.Load(root)) : SceneDocument.OpenForEditing(Path.Combine(root, "Scenes", name + Suffix));
        OpenScene(root, name ?? "Untitled", scene);
        if (migrationReport != null) Status.Text = $"Migration backup: {migrationReport}";
        if (SceneRecovery.Pending(root, LogError).Any())
            DispatcherPostRecovery(root);
    }

    private void DispatcherPostRecovery(string root)
    {
        void Offer()
        {
            if (project == root) Avalonia.Threading.Dispatcher.UIThread.Post(() => RunAction(PromptRecovery));
        }
        if (IsVisible) Offer();
        else Opened += OnOpened;
        void OnOpened(object? sender, EventArgs args) { Opened -= OnOpened; Offer(); }
    }

    private void CreateProject(string root, SceneDocument document, string name, bool savingCurrent = false)
    {
        if (Directory.EnumerateFileSystemEntries(root).Any(path => Path.GetFileName(path) != ".DS_Store"))
            throw new IOException("Choose an empty folder to create a project.");
        var folder = Directory.CreateDirectory(Path.Combine(root, "Scenes"));
        Directory.CreateDirectory(Path.Combine(root, "Scripts"));
        Directory.CreateDirectory(Path.Combine(root, SpriteAssets.Folder));
        document.Settings = document.Settings with { ProjectId = Guid.NewGuid() };
        document.Settings.Save(root);
        document.Save(Path.Combine(folder.FullName, name + Suffix));
        OpenScene(root, name, document, savingCurrent);
    }

    private async Task SwitchScene(string name)
    {
        if (project == null || !await CanLeave()) return;
        var next = SceneDocument.OpenForEditing(Path.Combine(project, "Scenes", name + Suffix));
        OpenScene(project, name, next);
    }

    private async Task NewScene()
    {
        if (project == null || !await CanLeave()) return;
        var folder = CreationFolder("Scenes");
        var name = "Scene";
        for (int i = 2; File.Exists(Path.Combine(project, folder, name + Suffix)); i++) name = $"Scene {i}";
        var scene = SceneDocument.Empty(Scene.Settings);
        var relative = $"{folder}/{name}{Suffix}";
        scene.Save(Path.Combine(project, relative));
        OpenScene(project, relative["Scenes/".Length..^Suffix.Length], scene);
        BrowseFolder(folder, relative);
    }

    private void OpenScene(string? root, string name, SceneDocument document, bool savingCurrent = false)
    {
        EndFileDrag();
        if (project != root)
        {
            Viewport.SetUiPreview(null);
            browserFolder = "";
            AssetSearch.Text="";
            HierarchySearch.Text = "";
            FolderTree.Items.Clear();
            WatchCatalog(root);
        }
        scripts = root == null ? null : document.OpenScripts(root);
        project = root;
        sceneName = name;
        document.ProjectDirectory = root;
        if (!savingCurrent) Viewport.EditTiles(null);
        Viewport.EndDrag();
        Viewport.Scene = document;
        if (!savingCurrent) { Viewport.Selected = null; Viewport.FrameCamera(); }
        UpdateResolution();
        Viewport.ClearSprites();
        if (!savingCurrent) LoadSceneMarks();
        if (!savingCurrent)
        {
            foldouts.Clear();
            pendingEdit = null; // The edited fields belonged to the replaced scene.
            history.Reset(document);
        }
        RefreshProjectFiles(force: true);
        scriptRefresh.IsEnabled = scripts != null;
        RebuildHierarchy(savingCurrent ? Selected : document.Objects.FirstOrDefault());
        UpdateTitle();
        SetControls();
        if (prefabSource == null) StartWorkspace(root);
        Status.Text = document.LoadNotice ?? "";
    }

    private async Task RunGame()
    {
        if (!Scene.Cameras.Any() && !Scene.IsUiOnly) throw new InvalidOperationException("Add a camera with Scene → Create Camera before playing.");
        if (!await Save()) return;
        if (scripts != null)
            foreach (var name in Scene.Objects.SelectMany(obj => obj.Components).OfType<ScriptComponent>()
                         .Select(component => component.Script).Distinct(StringComparer.Ordinal))
                scripts.Get(name);
        var executable = Environment.ProcessPath ?? throw new IOException("Cannot locate the game host.");
        var start = new System.Diagnostics.ProcessStartInfo(executable) { UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(typeof(EditorApp).Assembly.Location);
        start.ArgumentList.Add("--run-scene");
        start.ArgumentList.Add(ScenePath);
        using var process = System.Diagnostics.Process.Start(start);
        Status.Text = $"Opened {sceneName} in the SDL runtime.";
    }
}
