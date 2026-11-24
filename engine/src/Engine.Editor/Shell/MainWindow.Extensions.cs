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

    // 13.13: an extension's own scene changes, recorded as one Undo step (the object-diff history finds what changed).
    // refresh false: a field drawer already shows its new value, and rebuilding the Inspector would take its focus.
    internal bool ExtensionRecordEdit(bool refresh = true)
    {
        if (Viewport.IsPlaying) return false;
        CommitEdit();
        var before = history.CurrentSnapshot(); // the last recorded state, so it changes only when this records
        RecordEdit(Selected?.Id);
        bool recorded = history.CurrentSnapshot() != before;
        if (recorded && refresh) RefreshComponentList();
        return recorded;
    }

    internal void ExtensionSetField(ScriptComponent component, string name, object? value)
    {
        if (Viewport.IsPlaying) throw new InvalidOperationException("Stop Play before changing the scene.");
        if (scripts == null) throw new InvalidOperationException("Open a project first.");
        var field = scripts.Get(component.Script).Fields.FirstOrDefault(f => f.Name == name)
            ?? throw new InvalidDataException($"{component.Script} has no Inspector field '{name}'.");
        var json = value switch
        {
            null => (System.Text.Json.JsonElement?)null,
            System.Text.Json.JsonElement e => e,
            System.Numerics.Vector2 v => System.Text.Json.JsonSerializer.SerializeToElement(new { x = v.X, y = v.Y }),
            Enum e => System.Text.Json.JsonSerializer.SerializeToElement(e.ToString()),
            _ => System.Text.Json.JsonSerializer.SerializeToElement(value, value.GetType()),
        };
        var fallback = field.Default switch
        {
            null => (System.Text.Json.JsonElement?)null,
            System.Text.Json.JsonElement e => e,
            System.Numerics.Vector2 v => System.Text.Json.JsonSerializer.SerializeToElement(new { x = v.X, y = v.Y }),
            var d => System.Text.Json.JsonSerializer.SerializeToElement(d, d.GetType()),
        };
        if (json == null || fallback is { } d2 && System.Text.Json.JsonElement.DeepEquals(json.Value, d2)) component.Values.Remove(name);
        else component.Values[name] = json.Value;
        ExtensionRecordEdit(refresh: false);
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
