using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace Engine.Core;

public enum BuildProfile { Development, Distribution }

public sealed record GameBuildResult(string Product, string? SymbolDirectory)
{
    public string? ContentReport { get; init; }
    public IReadOnlyList<string> ContentDecisions { get; init; } = [];
    public string? AssetReport { get; init; }
    public IReadOnlyList<string> AssetSummary { get; init; } = [];
}

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
            if (rid == "osx-arm64")
            {
                var uuid = MacUuid(Path.Combine(bin, "MoNoonPlayer"));
                if (uuid != MacUuid(file.FullName)) throw new InvalidDataException("Player and dSYM UUIDs differ.");
                receipt.Set("nativeUuid", uuid.ToString("D"));
            }
        }
        else
        {
            foreach (var dll in new[] { "MoNoonPlayer.dll", "Engine.Core.dll", "Engine.Desktop.dll", ScriptAssembly(bin) })
            {
                string path = Path.Combine(bin, dll);
                string key = dll.Replace(Path.DirectorySeparatorChar, '/');
                receipt.Set("pdbId/" + key, VerifyPortablePdb(path).ToString("D"));
                using var stream = File.OpenRead(path);
                using var pe = new PEReader(stream);
                var metadata = pe.GetMetadataReader();
                receipt.Set("mvid/" + key, metadata.GetGuid(metadata.GetModuleDefinition().Mvid).ToString("D"));
            }
        }

        if (archive != null) MoveSymbols(bin, archive);
    }

    // A per-game Release publish links the scripts next to the player; a template-copy Release loads them from Game/.
    internal static string ScriptAssembly(string bin) => File.Exists(Path.Combine(bin, ProjectScripts.AssemblyFile))
        ? ProjectScripts.AssemblyFile : Path.Combine("Game", ".monoon-compiled", ProjectScripts.AssemblyFile);

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

    // Both files produced by the osx-arm64 publisher are thin little-endian Mach-O images.
    internal static Guid MacUuid(string path)
    {
        using var file = File.OpenRead(path);
        using var reader = new BinaryReader(file);
        if (reader.ReadUInt32() != 0xfeedfacf || reader.ReadUInt32() != 0x0100000c)
            throw new InvalidDataException($"Expected an arm64 Mach-O image: {path}");
        file.Position = 16;
        uint count = reader.ReadUInt32(), length = reader.ReadUInt32();
        file.Position = 32;
        long end = file.Position + length;
        if (end > file.Length) throw new InvalidDataException($"Invalid Mach-O header: {path}");
        for (uint i = 0; i < count && file.Position + 8 <= end; i++)
        {
            long start = file.Position;
            uint command = reader.ReadUInt32(), size = reader.ReadUInt32();
            if (size < 8 || start + size > end) break;
            if (command == 0x1b && size == 24) return new Guid(reader.ReadBytes(16), bigEndian: true);
            file.Position = start + size;
        }
        throw new InvalidDataException($"Missing Mach-O UUID: {path}");
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
