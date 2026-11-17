using System.Security.Cryptography;

namespace Engine.Core;

// One reusable mount per game. Holding the lease for the whole session also protects
// open streaming audio handles on Windows. A second player can always mount privately.
internal sealed class BundleMountCache : IDisposable
{
    private readonly FileStream lease;
    private readonly string receipt;
    internal DirectoryInfo Mount { get; }

    private BundleMountCache(string path, FileStream lease)
    {
        this.lease = lease;
        receipt = Path.Combine(path, "verified");
        Mount = new DirectoryInfo(Path.Combine(path, "Game"));
    }

    internal static BundleMountCache? TryAcquire(string directory)
    {
        Directory.CreateDirectory(directory);
        FileStream lease;
        try { lease = new(Path.Combine(directory, "session.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { return null; }
        return new(directory, lease);
    }

    internal static string Digest(string file)
    {
        using var input = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(input));
    }

    internal bool Matches(string game, string key, IEnumerable<AssetEntry> bundled)
    {
        var marker = new FileInfo(receipt);
        if (!marker.Exists || marker.Length != key.Length || marker.LinkTarget != null ||
            File.ReadAllText(receipt) != key || !Directory.Exists(Mount.FullName)) return false;
        try
        {
            // The private mount includes code and other loose files outside the asset manifest.
            // Recheck both the set of paths and every byte before executing anything from it.
            var files = ContentBundle.Files(Mount.FullName).ToHashSet(StringComparer.Ordinal);
            foreach (var path in ContentBundle.Files(game))
            {
                if (!files.Remove(path) || Digest(Path.Combine(game, path)) != Digest(Path.Combine(Mount.FullName, path))) return false;
            }
            foreach (var asset in bundled)
            {
                if (!files.Remove(asset.Path) || Digest(Path.Combine(Mount.FullName, asset.Path)) != asset.Hash) return false;
            }
            return files.Count == 0;
        }
        catch (InvalidDataException) { return false; } // Unexpected link in the mount.
    }

    internal void Reset()
    {
        File.Delete(receipt);
        Mount.Refresh();
        if (Mount.Exists) Mount.Delete(true);
        Mount.Create();
    }

    internal void Commit(string key) => ProjectYaml.WriteBytes(receipt, System.Text.Encoding.ASCII.GetBytes(key));

    public void Dispose() => lease.Dispose();
}
