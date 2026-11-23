using System.Drawing;
using System.Security.Cryptography;
using Engine.Core;
using Engine.Desktop;

namespace Engine.Editor;

internal sealed record ImportedPng(string Asset, Size Size);

internal static class PngImporter
{
    public static ImportedPng Import(string project, string source, string assetFolder)
    {
        if (!SpriteAssets.IsPng(source)) throw new InvalidDataException("Choose a PNG file.");
        string relative = Path.GetRelativePath(project, source).Replace(Path.DirectorySeparatorChar, '/');
        if (SpriteAssets.IsReference(relative))
            return new ImportedPng(relative, SdlCanvas.ReadPngSize(source));

        var stem = Path.GetFileNameWithoutExtension(source);
        if (!SpriteAssets.IsReference($"{assetFolder}/{stem}.png"))
            throw new InvalidDataException("The PNG filename contains characters unsupported by the project.");
        var folder = Directory.CreateDirectory(Path.Combine(project, assetFolder)).FullName;
        var temporary = Path.Combine(folder, $".{Guid.NewGuid():N}.tmp");
        try
        {
            File.Copy(source, temporary);
            Size size;
            try { size = SdlCanvas.ReadPngSize(temporary); }
            catch (InvalidDataException ex) { throw new InvalidDataException($"Cannot import '{Path.GetFileName(source)}' as a PNG.", ex); }
            byte[] hash;
            using (var incoming = File.OpenRead(temporary)) hash = SHA256.HashData(incoming);
            for (int i = 1; ; i++)
            {
                var name = i == 1 ? $"{stem}.png" : $"{stem}-{i}.png";
                var destination = Path.Combine(folder, name);
                try { File.Move(temporary, destination); }
                catch (IOException) when (File.Exists(destination))
                {
                    using var existing = File.OpenRead(destination);
                    if (!SHA256.HashData(existing).AsSpan().SequenceEqual(hash)) continue;
                }
                return new ImportedPng($"{assetFolder}/{name}", size);
            }
        }
        finally { File.Delete(temporary); }
    }

}
