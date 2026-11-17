using System.Security.Cryptography;

namespace Engine.Core;

public sealed partial class PlayerStorage
{
    private sealed record SlotCopy(byte[] Bytes, string Hash, GameData Data);
    private sealed class SlotIntegrityException(string message, Exception? inner = null) : Exception(message, inner);

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static string[]? ReadHashes(string path)
    {
        try
        {
            var hashes = ProjectYaml.Read<string[]>(path + ".sha256");
            if (hashes.Length is < 1 or > 3 || hashes.Any(hash => hash is not { Length: 64 } || !hash.All(Uri.IsHexDigit)))
                throw new InvalidDataException($"Invalid slot checksums: {path}");
            return hashes;
        }
        catch (FileNotFoundException) { return null; }
    }

    private static SlotCopy? ReadCopy(string path, string[]? hashes)
    {
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (FileNotFoundException) { return null; }
        string hash = Hash(bytes);
        if (hashes != null && !hashes.Contains(hash, StringComparer.OrdinalIgnoreCase))
            throw new SlotIntegrityException($"Checksum mismatch: {path}");
        using var reader = new StreamReader(new MemoryStream(bytes, writable: false));
        try { return new(bytes, hash, GameData.Parse(reader.ReadToEnd())); }
        catch (InvalidDataException ex) { throw new SlotIntegrityException($"Invalid slot data: {path}", ex); }
    }

    private static SlotCopy? SelectCopy(string path, string[]? hashes, out bool fromBackup)
    {
        fromBackup = false;
        SlotIntegrityException? failure = null;
        try
        {
            if (ReadCopy(path, hashes) is {} primary) return primary;
        }
        catch (SlotIntegrityException ex) { failure = ex; }
        try
        {
            if (ReadCopy(path + ".bak", hashes) is {} backup)
            {
                fromBackup = true;
                return backup;
            }
        }
        catch (SlotIntegrityException ex) { throw new InvalidDataException($"No valid copy of {path}.", ex); }
        if (failure != null) throw new InvalidDataException(failure.Message, failure);
        return null;
    }

    private static void PublishSlot(string path, byte[] bytes, SlotCopy? previous, string[]? known, bool fromBackup)
    {
        var allowed = new List<string>(3) { Hash(bytes) };
        if (previous != null) allowed.Add(previous.Hash);
        // If publication stops after the checksum update, the untouched files still verify.
        if (previous != null && !fromBackup && File.Exists(path + ".bak"))
        {
            string oldBackup = Hash(File.ReadAllBytes(path + ".bak"));
            if (known?.Contains(oldBackup, StringComparer.OrdinalIgnoreCase) == true) allowed.Add(oldBackup);
        }

        string staged = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }
            ProjectYaml.Write(path + ".sha256", allowed.Distinct().ToArray());
            if (previous != null && !fromBackup) File.Replace(staged, path, path + ".bak");
            else File.Move(staged, path, overwrite: true);
        }
        finally { File.Delete(staged); }
    }
}
