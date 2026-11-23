using System.Text.Json;

namespace Engine.Editor;

// Per-user editor view state beside editor-layout.json. Nothing here is written into the project, so scene files and game builds never see it.
internal sealed class EditorUserState
{
    public int Version { get; set; } = 1;
    // Inspector fold state by component type (scripts by script name).
    public Dictionary<string, bool> Folds { get; set; } = [];
    // Scene-view-only hide and pick lock, by scene file path.
    public Dictionary<string, List<Guid>> Hidden { get; set; } = [];
    public Dictionary<string, List<Guid>> Locked { get; set; } = [];
    public bool ProjectGrid { get; set; }

    internal static string FilePath => Path.Combine(Path.GetDirectoryName(MainWindow.LayoutPath)!, "editor-state.json");

    private static EditorUserState? current;
    public static EditorUserState Current => current ??= Load();

    private static EditorUserState Load()
    {
        try { return JsonSerializer.Deserialize<EditorUserState>(File.ReadAllText(FilePath)) ?? new(); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }

    // View state only: a failed write keeps the value for this session.
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    public static HashSet<Guid> Marks(Dictionary<string, List<Guid>> marks, string? scene) =>
        scene != null && marks.TryGetValue(scene, out var ids) ? [.. ids] : [];

    public void SetMarks(Dictionary<string, List<Guid>> marks, string? scene, IEnumerable<Guid> ids)
    {
        if (scene == null) return;
        var list = ids.ToList();
        if (list.Count == 0) marks.Remove(scene); else marks[scene] = list;
        Save();
    }
}
