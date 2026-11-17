namespace Engine.Core;

public sealed partial class PlayerStorage
{
    private readonly string? directory;
    public PlayerStorage(string directory) => this.directory = Path.GetFullPath(directory);
    private PlayerStorage() { }
    internal static PlayerStorage Unavailable { get; } = new();
    public bool IsAvailable => directory != null;

    internal static PlayerStorage ForGame(Guid gameId, bool editor)
    {
        if (gameId == Guid.Empty) return Unavailable;
        var root = Environment.GetEnvironmentVariable("MONOON_USER_DATA");
        if (string.IsNullOrWhiteSpace(root))
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MoNoon");
        return new(Path.Combine(root, editor ? "Play" : "Games", gameId.ToString("N")));
    }

    public static string CrashDirectory(Guid gameId) => Path.Combine(ForGame(gameId, false).DirectoryPath, "Crashes");

    internal string DirectoryPath => directory ?? throw new InvalidOperationException("Save the project before using player storage.");
    private string SlotPath(int slot)
    {
        if (slot is < 1 or > 999) throw new ArgumentOutOfRangeException(nameof(slot), "Choose a slot between 1 and 999.");
        return Path.Combine(DirectoryPath, $"slot-{slot:D3}.save");
    }

    // The latest explicitly saved run belongs to this game's existing profile.
    public void SaveReplay(GridReplay replay)
    {
        replay.Validate();
        string folder = Directory.CreateDirectory(DirectoryPath).FullName;
        using var writer = LockProfile(folder);
        ProjectYaml.Write(Path.Combine(folder, "latest.replay"), replay);
    }

    public GridReplay? LoadReplay()
    {
        try
        {
            var replay = ProjectYaml.Read<GridReplay>(Path.Combine(DirectoryPath, "latest.replay"));
            replay.Validate();
            return replay;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    public int[] ListSlots()
    {
        if (!Directory.Exists(DirectoryPath)) return [];
        return Directory.EnumerateFiles(DirectoryPath, "slot-???.save*")
            .Select(path => Path.GetFileName(path))
            .Where(name => name.Length == "slot-000.save".Length || name.EndsWith(".save.bak", StringComparison.Ordinal))
            .Select(name => int.TryParse(name.AsSpan(5, 3), out int slot) ? slot : 0)
            .Where(slot => slot is >= 1 and <= 999).Distinct().Order().ToArray();
    }

    public GameData? LoadSlot(int slot, int version, Action<int,GameData>? migrate = null) =>
        LoadSlot(slot, version, out _, migrate);

    public GameData? LoadSlot(int slot, int version, out bool recovered, Action<int,GameData>? migrate = null)
    {
        string path = SlotPath(slot);
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        recovered = false;
        if (!Directory.Exists(DirectoryPath)) return null;
        var hashes = ReadHashes(path);
        // Healthy loads (including read-only legacy profiles) need no writer lock.
        try
        {
            var current = ReadCopy(path, hashes);
            if (current != null) return Upgrade(current);
            if (hashes == null && !File.Exists(path + ".bak")) return null;
        }
        catch (SlotIntegrityException) { }

        // Re-read under the writer lock: a save could have completed since the first read.
        SlotCopy? copy;
        using (var writer = LockProfile(DirectoryPath))
        {
            hashes = ReadHashes(path);
            copy = SelectCopy(path, hashes, out recovered);
            if (copy == null) return null;
            if (recovered)
            {
                // Check version compatibility before repairing. Keep the original schema on disk.
                if (copy.Data.Version > version || copy.Data.Version < version && migrate == null)
                    return Upgrade(copy);
                ProjectYaml.WriteBytes(path, copy.Bytes);
            }
        }
        return Upgrade(copy);

        GameData Upgrade(SlotCopy source) => source.Data.UpgradeTo(version, migrate);
    }

    public void SaveSlot(int slot, GameData data)
    {
        string path = SlotPath(slot);
        byte[] bytes = ProjectYaml.Encode(data.Validate());
        Directory.CreateDirectory(DirectoryPath);
        using var writer = LockProfile(DirectoryPath);
        var hashes = ReadHashes(path);
        var previous = SelectCopy(path, hashes, out bool fromBackup);
        if (previous?.Data.Version > data.Version)
            throw new InvalidDataException($"Cannot overwrite version {previous.Data.Version} with version {data.Version}.");
        PublishSlot(path, bytes, previous, hashes, fromBackup);
    }

    public void DeleteSlot(int slot)
    {
        var path = SlotPath(slot);
        if (!Directory.Exists(DirectoryPath)) return;
        using var writer = LockProfile(DirectoryPath);
        File.Delete(path + ".bak");
        File.Delete(path);
        File.Delete(path + ".sha256");
    }
    public GameData? LoadSettings(int version, Action<int,GameData>? migrate = null) =>
        Read(Path.Combine(DirectoryPath, "player.settings"), version, migrate);
    public void SaveSettings(GameData data) => Write(Path.Combine(DirectoryPath, "player.settings"), data);

    private static GameData? Read(string path, int version, Action<int,GameData>? migrate)
    {
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
        GameData data;
        try { data = GameData.Load(path); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        return data.UpgradeTo(version, migrate);
    }

    // Keep the lock file across writes: the OS releases the handle if a player exits mid-save.
    private static FileStream LockProfile(string directory) =>
        new(Path.Combine(directory, ".writer.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

    private static void Write(string path, GameData data)
    {
        data.Validate();
        string folder = Directory.CreateDirectory(Path.GetDirectoryName(path)!).FullName;
        using var writer = LockProfile(folder);
        // Refuse to replace unreadable data or a newer format. The original stays recoverable.
        try
        {
            var previous = GameData.Load(path);
            if (previous.Version > data.Version)
                throw new InvalidDataException($"Cannot overwrite version {previous.Version} with version {data.Version}.");
        }
        catch (FileNotFoundException) { }
        ProjectYaml.Write(path, data);
    }
}
