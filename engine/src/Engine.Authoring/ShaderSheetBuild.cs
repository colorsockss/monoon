using static Engine.Core.BakedShaderSheets;
using System.Security.Cryptography;
using System.Text;

namespace Engine.Core;

// Cook only dependencies reached by the normal scene/prefab export traversal.
internal sealed class ShaderSheetBuild
{
    internal const string Capability = "MoNoonShaderSheets.version";

    private readonly HashSet<Guid> shaders = [];
    private bool light, particles;
    internal bool HasEffects => light || particles || shaders.Count != 0;

    internal void Collect(SceneObject obj, string project, ProjectAssets assets, Action<ShaderClip>? validate)
    {
        light |= obj.GetComponent<PointLight2DComponent>() != null;
        particles |= obj.GetComponent<ParticleEmitter2DComponent>() is {} emitter &&
            (emitter.Particles is not Guid effect || assets.LoadParticles(project, effect).Texture == null);
        if (obj.GetComponent<SpriteShader2DComponent>()?.Shader is Guid id && shaders.Add(id))
        {
            var clip = assets.LoadShader(project, id);
            validate?.Invoke(clip);
        }
    }

    internal AssetEntry[] Write(string game, AssetEntry[] manifest, Action<ShaderClip, string> bake, ContentBuildCache? cache = null)
    {
        void WriteSheet(ShaderClip clip, string source, string output)
        {
            if (cache == null) { bake(clip, output); return; }
            string name = Path.GetRelativePath(game, output).Replace('\\', '/');
            bool standard = bake == global::Engine.Desktop.ShaderPixels.WritePng;
            var inputs = new Dictionary<string,string> { [source] = ContentBuildCache.Digest(ProjectYaml.Encode(clip)) };
            if (!standard) inputs.Add("baker", "custom");
            cache.Generate("shader " + name, inputs, output, file => bake(clip, file), reusable: standard);
        }

        var paths = manifest.Select(e => e.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ids = manifest.Select(e => e.Id).ToHashSet();
        var result = new List<AssetEntry>(manifest.Length + shaders.Count);
        foreach (var entry in manifest)
        {
            if (!shaders.Contains(entry.Id)) { result.Add(entry); continue; }
            string descriptor = Path.Combine(game, entry.Path);
            var clip = ShaderClip.Load(descriptor);
            string relative = Path.ChangeExtension(entry.Path, "shader.frames.png");
            var digest = SHA256.HashData(Encoding.UTF8.GetBytes("MoNoon shader frames/" + entry.Id.ToString("N")));
            var id = new Guid(digest.AsSpan(0, 16));
            if (!paths.Add(relative) || !ids.Add(id))
                throw new InvalidDataException($"Generated shader sheet conflicts with another build asset: {relative}");
            string sheet = Path.Combine(game, relative);
            WriteSheet(clip, entry.Path, sheet);
            ProjectYaml.Write(descriptor, clip with { Source = null, Sheet = id });
            result.Add(entry with { Hash = BundleMountCache.Digest(descriptor) });
            result.Add(new(id, relative, BundleMountCache.Digest(sheet)));
        }

        if (light || particles)
        {
            string directory = Directory.CreateDirectory(Path.Combine(game, Builtins)).FullName;
            if (light) WriteSheet(ShaderClip.LightFalloff, "built-in light", Path.Combine(directory, Light));
            if (particles) WriteSheet(ShaderClip.Puff, "built-in particles", Path.Combine(directory, Particle));
        }
        return result.OrderBy(e => e.Path, StringComparer.Ordinal).ToArray();
    }
}
