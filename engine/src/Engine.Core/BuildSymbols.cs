using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace Engine.Core;

public enum BuildProfile { Development, Distribution }

public sealed record GameBuildResult(string Product, string? SymbolDirectory);

internal static class BuildSymbols
{
    internal const string Receipt = "MoNoonBuild";

    // Only examine compiler outputs, before project assets are included in the product.
    internal static void Collect(string bin, bool native, string rid, string? archive, GameData receipt)
    {
        using (var player = File.OpenRead(Path.Combine(bin, rid == "win-x64" ? "MoNoonPlayer.exe" : "MoNoonPlayer")))
            receipt.Set("playerSha256", Convert.ToHexString(SHA256.HashData(player)));
        if (native)
        {
            string symbol = rid switch
            {
                "osx-arm64" => "MoNoonPlayer.dSYM/Contents/Resources/DWARF/MoNoonPlayer",
                "linux-x64" => "MoNoonPlayer.dbg",
                "win-x64" => "MoNoonPlayer.pdb",
                _ => throw new ArgumentOutOfRangeException(nameof(rid))
            };
            var file = new FileInfo(Path.Combine(bin, symbol));
            if (!file.Exists || file.Length == 0) throw new InvalidDataException($"Native publish did not produce {symbol}.");
        }
        else
        {
            foreach (var dll in new[] { "MoNoonPlayer.dll", "Engine.Core.dll", "Engine.Desktop.dll",
                Path.Combine("Game", ".monoon-compiled", ProjectScripts.AssemblyFile) })
                receipt.Set("pdbId/" + dll.Replace('\\', '/'), VerifyPortablePdb(Path.Combine(bin, dll)).ToString("D"));
        }

        if (archive != null) MoveSymbols(bin, archive);
    }

    internal static Guid VerifyPortablePdb(string assembly, string? path = null)
    {
        path ??= Path.ChangeExtension(assembly, ".pdb");
        using var input = File.OpenRead(assembly);
        using var pe = new PEReader(input);
        using var symbols = File.OpenRead(path);
        using var provider = MetadataReaderProvider.FromPortablePdbStream(symbols);
        var id = new BlobContentId(provider.GetMetadataReader().DebugMetadataHeader!.Id);
        if (!pe.ReadDebugDirectory().Any(entry => entry.Type == DebugDirectoryEntryType.CodeView &&
                entry.Stamp == id.Stamp && pe.ReadCodeViewDebugDirectoryData(entry).Guid == id.Guid))
            throw new InvalidDataException($"Debug symbols do not match {assembly}: {path}");
        return id.Guid;
    }

    private static void MoveSymbols(string bin, string archive)
    {
        foreach (var path in Directory.GetFileSystemEntries(bin))
        {
            string target = Path.Combine(archive, Path.GetFileName(path));
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (Directory.Exists(path))
            {
                if (extension == ".dsym")
                {
                    Directory.CreateDirectory(archive);
                    Directory.Move(path, target);
                }
                else MoveSymbols(path, target);
            }
            else if (extension is ".pdb" or ".dbg" or ".debug" or ".dwarf" or ".map")
            {
                Directory.CreateDirectory(archive);
                File.Move(path, target);
            }
        }
    }
}
