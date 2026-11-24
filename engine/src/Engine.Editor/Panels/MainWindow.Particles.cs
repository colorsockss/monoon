using Avalonia.Input;
using Avalonia.Controls;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private ParticlePanel particlePanel = null!;

    // The Particle tool panel; each committed asset change is one Undo step in the scene history.
    private void InitializeParticles()
    {
        particlePanel = new ParticlePanel(() => project, () => Scene, Viewport, edit => RecordEdit(Selected?.Id, asset: edit))
        {
            NewFromPreset = preset => RunAction(() => CreateParticleEffect(preset)),
        };
        AddPanel("Particle", particlePanel);
        // Finder files dropped on the panel's asset fields (13.17).
        DragDrop.SetAllowDrop(particlePanel, true);
        particlePanel.AddHandler(DragDrop.DragOverEvent, ReferenceFileDrag);
        particlePanel.AddHandler(DragDrop.DragLeaveEvent, (_, _) => HighlightReference(null));
        particlePanel.AddHandler(DragDrop.DropEvent, ReferenceFileDrag);
        // Project + → New Particle Effect → one item per preset, after the other New asset items.
        var presets = new MenuItem { Header = "New Particle Effect" };
        foreach (var preset in ParticlePresets.All)
        {
            var item = new MenuItem { Header = preset.Name };
            item.Click += (_, _) => RunAction(() => CreateParticleEffect(preset));
            presets.Items.Add(item);
        }
        var menu = (MenuFlyout)ProjectActions.Flyout!;
        menu.Items.Insert(menu.Items.IndexOf(NewClipButton) + 1, presets);
    }

    // The Inspector's emitter button (the selected emitter's asset) and a Project double-click on a .particles asset.
    private void OpenParticlePanel(string? relative = null)
    {
        if (relative != null) particlePanel.Open(relative);
        OpenPanel("Particle");
    }

    // Writes the preset as a new asset in the open Assets folder (one Undo step removes it again) and edits it in the panel.
    private async Task CreateParticleEffect(ParticlePresets.Preset preset)
    {
        if (project == null && !await Save()) return;
        var root = project!;
        var relative = ParticlePresets.Write(root, CreationFolder("Assets"), preset);
        var path = Path.Combine(root, relative);
        ProjectAssets.Scan(root); // writes the new asset's .meta
        RecordEdit(Selected?.Id, asset: new PrefabFileEdit(path, new(null, null), PrefabFileEdit.Read(path)));
        BrowseFolder(ParentPath(relative), relative);
        OpenParticlePanel(relative);
        Status.Text = $"Created {relative}.";
    }

    private GameSession? particleSession;
    private Dictionary<string, DateTime> particleStamps = [];

    // Scene Play: a .particles file saved since the last look (by the Particle panel or another program) reloads into the
    // running emitters, as saved scripts do (ParticleSystem2D.Reload keeps or restarts their particles). The first call of a
    // Play session only records the files. A file that does not load keeps the running effect.
    private void ReloadPlayParticles()
    {
        if (project is not { } root || Viewport.PlaySession is not { } session || session.LoadingScene) return;
        var stamps = Directory.EnumerateFiles(root, "*" + ParticleEffect.Extension, SearchOption.AllDirectories)
            .ToDictionary(file => file, File.GetLastWriteTimeUtc);
        var before = particleStamps;
        bool started = session != particleSession;
        (particleSession, particleStamps) = (session, stamps);
        if (started) return;
        var changed = stamps.Where(s => !before.TryGetValue(s.Key, out var time) || time != s.Value).Select(s => s.Key).ToArray();
        if (changed.Length == 0) return;
        var assets = ProjectAssets.Scan(root);
        foreach (var file in changed)
        {
            var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            if (assets.IdFor(relative) is not Guid id) continue;
            try
            {
                var (kept, restarted) = session.Current.ReloadParticles(id);
                if (kept + restarted > 0)
                    WriteLog(new(GameLogLevel.Info, $"Particles reloaded: {relative} ({kept} emitter(s) kept their particles, {restarted} restarted).", null, 0));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                WriteLog(new(GameLogLevel.Warning, $"Particle reload failed, Play keeps the previous effect: {ex.Message}", null, 0));
            }
        }
    }
}
