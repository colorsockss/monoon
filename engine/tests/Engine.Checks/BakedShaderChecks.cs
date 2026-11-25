using System.Drawing;
using Engine.Core;
using Engine.Desktop;
using SkiaSharp;

internal static class BakedShaderChecks
{
    [Check(3100, StopAfter = "effects")] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    internal static void Run(string repository, string scratch)
    {
        string sample = Path.Combine(repository, "samples", "PlatformerLab");
        string root = Path.Combine(scratch, "shader-product", "Game");
        Directory.CreateDirectory(Path.Combine(root, "Assets"));
        const string relative = "Assets/Torch.shader";
        File.Copy(Path.Combine(sample, relative), Path.Combine(root, relative));
        var assets = ProjectAssets.ReadManifest(sample);
        // The course showcase's torch: the shader flame and the Point Light 2D beside it (which asks for the light falloff sheet).
        var course = SceneDocument.Load(Path.Combine(sample, "Scenes", "ShowcaseCourse.scene"));
        var torch = course.Objects.Single(o => o.Name == "Torch flame 1");
        var builder = new ShaderSheetBuild();
        builder.Collect(torch, sample, assets, null);
        builder.Collect(torch, sample, assets, null);
        builder.Collect(course.Objects.Single(o => o.Name == "Torch light 1"), sample, assets, null);
        var asset = assets.Entries.Single(e => e.Path == relative);
        AssetEntry[] entries = builder.Write(root, [asset], ShaderPixels.WritePng);
        if (entries.Length != 2 || entries.Single(e => e.Id == asset.Id).Hash == asset.Hash)
            throw new Exception("Shader descriptor did not replace its source or was baked more than once.");
        File.WriteAllText(Path.Combine(root, ".monoon-player"), BakedShaderSheets.Marker);
        ProjectAssets.Write(Path.Combine(root, ProjectAssets.Manifest), entries);
        var settings = new GameSettings { ContentBundles = ["Assets"] };
        ContentBundle.Pack(root, settings.ContentBundles, entries);
        using var mount = ContentBundle.Open(root, settings);
        var cooked = ProjectAssets.ReadManifest(mount.Root).LoadShader(mount.Root, asset.Id);
        var source = assets.LoadShader(sample, asset.Id);
        if (cooked.Source != null || cooked.SheetPath == null || !File.Exists(cooked.SheetPath) ||
            cooked.Width != source.Width || cooked.Height != source.Height || cooked.Fps != source.Fps || cooked.Frames != source.Frames)
            throw new Exception("Bundled shader lost its sheet, dimensions or clock.");
        ComparePixels(source, cooked.SheetPath);
        ComparePixels(ShaderClip.LightFalloff, Path.Combine(mount.Root, BakedShaderSheets.Builtins, BakedShaderSheets.Light));
        Console.WriteLine("PASS shader export: packaged descriptor and sheet retain GUID, all frame pixels, light falloff and timing after bundle mounting.");
    }

    internal static void ComparePlayers(string repository, string release, string aot)
    {
        string project = Path.Combine(repository, "samples", "PlatformerLab");
        var assets = ProjectAssets.ReadManifest(project);
        Guid id = assets.IdFor("Assets/Torch.shader")!.Value;
        var source = assets.LoadShader(project, id);
        foreach (string game in new[] { release, aot })
        {
            var cooked = ProjectAssets.ReadManifest(game).LoadShader(game, id);
            if (cooked.Source != null || cooked.SheetPath == null || cooked.Fps != source.Fps || cooked.Frames != source.Frames)
                throw new Exception("Expected a cooked torch in " + game);
            ComparePixels(source, cooked.SheetPath);
            ComparePixels(ShaderClip.LightFalloff, Path.Combine(game, BakedShaderSheets.Builtins, BakedShaderSheets.Light));
            ComparePixels(ShaderClip.Puff, Path.Combine(game, BakedShaderSheets.Builtins, BakedShaderSheets.Particle));
            Console.WriteLine($"PASS {game}: {cooked.Frames} torch cells, point light and particle sheets match the live RGBA pixels.");
        }
    }

    private static void ComparePixels(ShaderClip source, string path)
    {
        using var expected = ShaderPixels.Bake(source, Color.White);
        if (SpriteRegion.ReadPngSize(path) != new Size(expected.Width, expected.Height))
            throw new Exception("Baked PNG has the wrong dimensions: " + path);
        using var decoded = SKBitmap.Decode(path, expected.Info) ?? throw new Exception("Could not decode " + path);
        if (!expected.GetPixelSpan().SequenceEqual(decoded.GetPixelSpan()))
            throw new Exception("PNG round trip changed shader RGBA values: " + path);
    }
}
