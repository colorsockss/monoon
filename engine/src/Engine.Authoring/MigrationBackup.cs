using System.IO.Compression;

namespace Engine.Core;

internal static class MigrationBackup
{
    internal static string Create(string root, IEnumerable<string> affected)
    {
        var folder = Directory.CreateDirectory(Path.Combine(root, ".monoon", "migrations"));
        var target = Path.Combine(folder.FullName, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8] + ".zip");
        var temp = target + ".tmp";
        try
        {
            using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
            {
                foreach (var path in affected.Where(File.Exists).Distinct().Order(StringComparer.Ordinal))
                    zip.CreateEntryFromFile(path, Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'));
            }
            File.Move(temp, target);
            return target;
        }
        finally { File.Delete(temp); }
    }
}
