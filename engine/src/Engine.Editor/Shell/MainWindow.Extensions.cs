using Avalonia.Controls;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private ProjectEditorExtensions? extensions;
    private string? extensionStamp;
    private readonly List<(string Id, Border Panel, NativeMenuItem Item)> extensionPanels = [];
    private readonly NativeMenu extensionMenu = new();
    private readonly NativeMenuItem extensionItem = new("Extensions");

    internal string ExtensionProject => project ?? throw new InvalidOperationException("Open a project first.");
    internal SceneDocument ExtensionScene => Scene;
    internal SceneObject? ExtensionSelection => Selected;
    internal bool ExtensionPlaying => Viewport.IsPlaying;
    internal void ExtensionLog(string message) => WriteLog(new(GameLogLevel.Info, message, null, 0));
    internal SceneObject? ExtensionPlacePrefab(string path)
    {
        try
        {
            if (Viewport.IsPlaying) throw new InvalidOperationException("Stop Play before changing the scene.");
            var center = Viewport.EditCenter;
            var placed = Scene.PlacePrefab(path, new((float)center.X, (float)center.Y));
            FinishObjectChange(placed);
            return placed;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
        { LogError(ex.Message); Status.Text = ex.Message; return null; }
    }

    // Recompiles Editor/ and rebuilds the extension menu, Inspectors and panels. A compile error leaves no extension loaded.
    private void RefreshExtensions()
    {
        extensionMenu.Items.Clear();
        UnloadExtensions();
        if (project != null)
        {
            try
            {
                extensionStamp = ProjectEditorExtensions.Stamp(project);
                extensions = ProjectEditorExtensions.Load(project);
                if (extensions != null) InstallExtensions(extensions.Registry);
            }
            catch (Exception ex)
            {
                extensionMenu.Items.Clear();
                UnloadExtensions();
                LogError(ex.Message);
                Status.Text = "Editor extensions did not load. See the Console.";
            }
        }
        RefreshComponentList();
        UpdateMenuState();
        UpdateWindowMenu();
    }

    private void InstallExtensions(EditorExtensionRegistry registry)
    {
        var context = new EditorContext(this);
        foreach (var (label, run) in registry.Commands)
        {
            var item = new NativeMenuItem(label);
            item.Click += (_, _) => RunAction(() => { run(context); Status.Text = label; return Task.CompletedTask; });
            extensionMenu.Add(item);
        }
        int at = DockPanels.Length + 1; // After the built-in panels and the separator below Inspector.
        foreach (var (title, build) in registry.Panels)
        {
            if (DockPanels.Contains(title) || Dock.Panels.Any(p => DockHost.GetPanel(p) == title))
                throw new InvalidDataException($"Panel '{title}' is already in use.");
            var content = new Border { Margin = (Avalonia.Thickness)this.FindResource("EditorPanelPadding")!, Child = build(context) };
            var panel = AddPanel(title, new EditorPanel { Title = title, Content = new ScrollViewer { Content = content } });
            panel.IsVisible = false; // Hidden until opened; the dock shows only panels in its layout.
            var item = new NativeMenuItem(title) { ToggleType = NativeMenuItemToggleType.CheckBox };
            item.Click += (_, _) => OpenPanel(title);
            windowMenu.Items.Insert(at++, item);
            panelItems[title] = item;
            extensionPanels.Add((title, panel, item));
        }
    }

    // Closing the panels first keeps the saved layout free of panels that a later session may not have.
    private void UnloadExtensions()
    {
        foreach (var (id, panel, item) in extensionPanels)
        {
            Dock.Close(id);
            Dock.Children.Remove(panel);
            windowMenu.Items.Remove(item);
            panelItems.Remove(id);
        }
        extensionPanels.Clear();
        extensions?.Dispose();
        extensions = null;
    }

    // Edit mode only: a saved Editor/ file reloads the extensions, as a saved game script refreshes the Inspector.
    private void RefreshChangedExtensions()
    {
        if (project == null || prefabSource != null) return;
        try { if (ProjectEditorExtensions.Stamp(project) != extensionStamp) RefreshExtensions(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Status.Text = ex.Message; }
    }
}
