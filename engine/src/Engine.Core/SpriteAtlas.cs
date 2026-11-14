using System.Drawing;
using VYaml.Annotations;

namespace Engine.Core;

// Scene/clip references always point to the original PNG ID. Only the atlas owns packed coordinates.
[YamlObject]
public sealed partial record AtlasEntry(Guid Sprite, SpriteRegion Source, SpriteCanvas Canvas);

[YamlObject]
public sealed partial record SpriteAtlas(string ImageHash, AtlasEntry[] Entries)
{
    public const string Extension = ".atlas";
    public string ImagePath() => ".monoon-atlas/" + ImageHash + ".png";

    public static SpriteAtlas Load(string file)
    {
        var atlas = ProjectYaml.Read<SpriteAtlas>(file);
        if (atlas.ImageHash is not { Length: 64 } hash || !hash.All(char.IsAsciiHexDigit) ||
            atlas.Entries is not { Length: > 0 } || atlas.Entries.Any(e => e == null || e.Sprite == Guid.Empty ||
                e.Source == null || e.Canvas?.Fits(e.Source) != true) || atlas.Entries.DistinctBy(e => e.Sprite).Count() != atlas.Entries.Length)
            throw new InvalidDataException($"Invalid sprite atlas: {file}.");
        return atlas;
    }

    public void Save(string file, bool overwrite) => ProjectYaml.WriteBytes(file, ProjectYaml.Encode(this), overwrite);
}

public sealed record PackedSprite(string Atlas, string Sheet, AtlasEntry Entry);

public sealed class SpriteLibrary
{
    private readonly Dictionary<Guid,PackedSprite> packed = [];
    public PackedSprite? Find(Guid? id) => id.HasValue ? packed.GetValueOrDefault(id.Value) : null;

    public SpriteLibrary(string root, ProjectAssets assets)
    {
        foreach (var asset in assets.Entries.Where(e => e.Path.EndsWith(SpriteAtlas.Extension, StringComparison.Ordinal)))
        {
            var file = Path.Combine(root,asset.Path);
            if (!File.Exists(file)) continue; // The authoring manifest retains deleted asset IDs.
            var atlas = SpriteAtlas.Load(file);
            var sheet = atlas.ImagePath();
            Size size = SpriteRegion.ReadPngSize(Path.Combine(root,sheet));
            foreach (var cell in atlas.Entries)
            {
                if (!cell.Source.Fits(size.Width,size.Height) || !packed.TryAdd(cell.Sprite,new(asset.Path,sheet,cell)))
                    throw new InvalidDataException($"{asset.Path}: invalid or duplicate packed sprite {cell.Sprite}.");
            }
        }
    }
}
