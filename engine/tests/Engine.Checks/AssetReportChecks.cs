using Engine.Core;
using Engine.Desktop;
using SkiaSharp;

// Reachability-based asset exclusion and the build's asset report (assets.yaml next to the content report). A scratch
// copy of KeyRoom gets an unreferenced large PNG/WAV and an Aseprite file; KeyRoom already has prefabs used only by
// scenes left out of the build. None of them may ship, and each must show up in the report's unreferenced list.
internal static class AssetReportChecks
{
    [Check(700, "asset-report", Templates = true)] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    // The Native AOT pass takes minutes; it runs only through --only asset-report.
    [Check(750, "asset-report", Templates = true, Full = false)] static void NativeAot(CheckRun c) => Run(c.Repository, c.Scratch, nativeAot: true);
    internal static void Run(string repository, string scratch, bool nativeAot = false)
    {
        string mode = nativeAot ? "native-aot" : "release";
        var root = Path.Combine(scratch, "AssetReport-" + mode);
        var sample = Path.Combine(repository, "samples", "KeyRoom");
        foreach (var file in Directory.EnumerateFiles(sample, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sample, file);
            if (relative.Split(Path.DirectorySeparatorChar)[0] is "bin" or "obj" or ".monoon") continue;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, relative))!);
            File.Copy(file, Path.Combine(root, relative));
        }
        var unused = Directory.CreateDirectory(Path.Combine(root, "Assets", "Unused")).FullName;
        using (var noise = new SKBitmap(1024, 1024)) // random pixels keep the PNG large (about 4 MiB)
        {
            var random = new Random(7);
            for (int y = 0; y < noise.Height; y++)
                for (int x = 0; x < noise.Width; x++) noise.SetPixel(x, y, new SKColor((uint)random.Next() | 0xFF000000));
            using var png = noise.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(Path.Combine(unused, "BigUnused.png"), png.ToArray());
        }
        WriteWav(Path.Combine(unused, "BigUnused.wav"), 2 << 20);
        WriteWav(Path.Combine(root, "Assets", "Unlock.wav"), (int)AssetBuildReport.LargeWav + 4096); // referenced by the Door
        File.Copy(Path.Combine(repository, "samples", "SpriteLab", "Assets", "Showcase", "FireflyBlink.aseprite"), Path.Combine(unused, "Art.aseprite"));

        // Roadmap 15.13: the showcase Key.png (a SpriteRenderer in Showcase.scene; UiImage keeps its PNG too) shares an atlas with eight
        // unused 128×128 noise sprites. The build must ship a repacked atlas with only Key's cell and leave the project's
        // atlas and sheet as they are.
        var props = new List<string>();
        for (int i = 0; i < 8; i++)
        {
            using var prop = new SKBitmap(128, 128);
            var random = new Random(100 + i);
            for (int y = 0; y < prop.Height; y++)
                for (int x = 0; x < prop.Width; x++) prop.SetPixel(x, y, new SKColor((uint)random.Next() | 0xFF000000));
            using var png = prop.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(Path.Combine(unused, $"Prop{i}.png"), png.ToArray());
            props.Add($"Assets/Unused/Prop{i}.png");
        }
        var catalog = ProjectAssets.Scan(root);
        var keyId = catalog.IdFor("Assets/Showcase/Key.png")!.Value;
        var atlasFile = Path.Combine(root, "Assets", "Props.atlas");
        var sourceAtlas = AtlasPacker.Pack(root, atlasFile, [keyId, .. props.Select(p => catalog.IdFor(p)!.Value)], false);
        var sourceSheet = Path.Combine(root, sourceAtlas.ImagePath());
        byte[] atlasBytes = File.ReadAllBytes(atlasFile), sheetBytes = File.ReadAllBytes(sourceSheet);

        // Keep the fixture honest: the excluded scenes really use these prefabs.
        var settings = GameSettings.Load(root);
        foreach (var (scene, prefab) in new[] { ("PrefabWorkshop", "Chest"), ("WallWorkshop", "WallSegment"), ("MovementWorkshop", "MovementPlayer") })
        {
            var sceneId = ProjectYaml.Read<AssetMeta>(Path.Combine(root, "Scenes", scene + ".scene.meta")).Id;
            var prefabId = ProjectYaml.Read<AssetMeta>(Path.Combine(root, "Assets", prefab + ".prefab.meta")).Id;
            Require(!settings.IncludedScenes.Contains(sceneId) && File.ReadAllText(Path.Combine(root, "Scenes", scene + ".scene")).Contains(prefabId.ToString()),
                $"{scene}.scene is outside the build and uses {prefab}.prefab");
        }

        string rid = BuildSettings.Host;
        var output = Path.Combine(scratch, "AssetReportOutput-" + mode);
        var result = nativeAot
            ? GameBuild.ExportNativeAot(root, Path.Combine(repository, "engine"), rid, output, settings, BuildProfile.Distribution)
            : GameBuild.Export(root, GameBuild.FindPlayerTemplate(Path.Combine(repository, "engine"), rid), rid, output, settings, BuildProfile.Distribution);
        var game = rid == "osx-arm64" ? Path.Combine(result.Product, "Contents", "MacOS", "Game") : Path.Combine(result.Product, "Game");
        var shipped = Directory.EnumerateFiles(game, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(game, f).Replace('\\', '/')).ToHashSet(StringComparer.Ordinal);

        // (a) Not reachable from an included scene or GameSettings: not in the product.
        string[] excluded = ["Assets/Unused/BigUnused.png", "Assets/Unused/BigUnused.wav", "Assets/Chest.prefab", "Assets/WallSegment.prefab",
            "Assets/MovementPlayer.prefab", "Scenes/PrefabWorkshop.scene", "Scenes/WallWorkshop.scene", "Scenes/MovementWorkshop.scene"];
        foreach (var path in excluded) Require(!shipped.Contains(path), "excluded asset is not shipped: " + path);
        Require(!shipped.Any(p => p.EndsWith(".meta", StringComparison.Ordinal) || p.EndsWith(".aseprite", StringComparison.Ordinal) || p.EndsWith(".cs", StringComparison.Ordinal)
            || p.EndsWith(".csproj", StringComparison.Ordinal) || p.EndsWith(".inputreplay", StringComparison.Ordinal) || p.StartsWith("Tests/", StringComparison.Ordinal) || p.StartsWith("Editor/", StringComparison.Ordinal)),
            "no .meta, .aseprite, scripts, Tests/ or Editor/ files ship: " + string.Join(", ", shipped));

        // The report lives in the build cache, next to the content report, never in the product.
        Require(result.AssetReport is { } reportPath && File.Exists(reportPath) && Path.GetDirectoryName(reportPath) == Path.GetDirectoryName(result.ContentReport)
            && !Directory.EnumerateFiles(result.Product, AssetBuildReport.FileName, SearchOption.AllDirectories).Any(), "asset report is next to report.yaml and outside the product");
        var report = ProjectYaml.Read<AssetBuildReport>(result.AssetReport!);

        // (b) Everything in Game/ is listed once with its bytes, and only Assets/ files that did not ship are unreferenced.
        Require(report.Shipped.Select(f => f.Path).ToHashSet(StringComparer.Ordinal).SetEquals(shipped) && report.Shipped.Length == shipped.Count,
            "report lists exactly the files in Game/");
        Require(report.Shipped.All(f => f.Bytes == new FileInfo(Path.Combine(game, f.Path)).Length) && report.ShippedBytes == report.Shipped.Sum(f => f.Bytes)
            && report.BytesByType.Values.Sum() == report.ShippedBytes, "report sizes and per-type totals");
        var unreferenced = report.Unreferenced.Select(u => u.Path).ToHashSet(StringComparer.Ordinal);
        foreach (var path in excluded.Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)))
            Require(unreferenced.Contains(path), "unreferenced list names " + path);
        Require(!unreferenced.Overlaps(shipped) && unreferenced.All(p => p.StartsWith("Assets/", StringComparison.Ordinal))
            && report.Unreferenced.First().Path == "Assets/Unused/BigUnused.png", "unreferenced list is Assets/ only, unshipped, largest first");

        // (c) Every walked asset carries the chain that pulled it in; only build outputs fall back to a generated reason.
        string Reason(string path) => report.Shipped.Single(f => f.Path == path).Reason;
        Require(Reason("Assets/Showcase/CoinPile.png") == "Scenes/Showcase.scene > Chest > Assets/Showcase/Coin.prefab > Coin > SpriteRenderer",
            "prefab chain: " + Reason("Assets/Showcase/CoinPile.png"));
        Require(Reason("Assets/Unlock.wav") == "Scenes/Showcase.scene > Door > AudioSource", "component reason: " + Reason("Assets/Unlock.wav"));
        Require(Reason("Assets/Fonts/NanumGothic-Regular.ttf") == "GameSettings localization" && Reason("Scenes/ShowcaseRoom2.scene") == "GameSettings includedScenes",
            "GameSettings reasons");
        var unexplained = report.Shipped.Where(f => (f.Path.StartsWith("Assets/", StringComparison.Ordinal) || f.Path.StartsWith("Scenes/", StringComparison.Ordinal))
            && !f.Reason.StartsWith("Scenes/", StringComparison.Ordinal) && !f.Reason.StartsWith("GameSettings ", StringComparison.Ordinal)).Select(f => f.Path).ToArray();
        Require(unexplained.Length == 0, "shipped project files without a reference chain: " + string.Join(", ", unexplained));
        Require(report.Warnings.SequenceEqual([$"Assets/Unlock.wav is {AssetBuildReport.Size(AssetBuildReport.LargeWav + 4096 + 44)} of PCM WAV; import it as Ogg Vorbis to ship less."]),
            "large WAV warning: " + string.Join(" | ", report.Warnings));
        // Roadmap 15.10: Import Assets warns with the same text; only WAV files over 1 MiB.
        Require(AssetBuildReport.ImportWarnings(root, ["Assets/Unused/BigUnused.wav", "Assets/Unused/BigUnused.png", "Assets/Unlock.wav", "Assets/Showcase/Key.png"])
            .SequenceEqual([$"Assets/Unused/BigUnused.wav is {AssetBuildReport.Size((2 << 20) + 44)} of PCM WAV; import it as Ogg Vorbis to ship less.", report.Warnings[0]]),
            "import warnings for large WAV files only");
        // Repacked atlas: same path, only the used cell, pixels inside the cell and its border identical, project files untouched.
        var shippedAtlas = SpriteAtlas.Load(Path.Combine(game, "Assets", "Props.atlas"));
        var keyCell = sourceAtlas.Entries.Single(e => e.Sprite == keyId);
        Require(shippedAtlas.Entries.Length == 1, "shipped atlas has one cell");
        var cell = shippedAtlas.Entries[0];
        Require(cell.Sprite == keyId && cell.Canvas == keyCell.Canvas
            && cell.Source.Width == keyCell.Source.Width && cell.Source.Height == keyCell.Source.Height, "shipped atlas holds only Key's cell");
        Require(!shipped.Contains(sourceAtlas.ImagePath()) && shipped.Contains(shippedAtlas.ImagePath())
            && !props.Any(shipped.Contains), "neither the source sheet nor unused sprites ship: " + string.Join(", ", shipped.Where(p => p.EndsWith(".png", StringComparison.Ordinal))));
        Require(File.ReadAllBytes(atlasFile).SequenceEqual(atlasBytes) && File.ReadAllBytes(sourceSheet).SequenceEqual(sheetBytes), "project atlas and sheet unchanged");
        using (var before = Rgba(sourceSheet))
        using (var after = Rgba(Path.Combine(game, shippedAtlas.ImagePath())))
        {
            var (a, b) = (keyCell.Source, cell.Source);
            for (int y = -1; y <= a.Height; y++)
                for (int x = -1; x <= a.Width; x++)
                    Require(before.GetPixel(a.X + x, a.Y + y) == after.GetPixel(b.X + x, b.Y + y), $"repacked Key pixel {x},{y}");
            Require(after.Width == a.Width + 2 && after.Height == a.Height + 2, $"repacked sheet is one bordered cell: {after.Width}x{after.Height}");
        }
        Require(report.Atlases is [var usage] && usage.Path == "Assets/Props.atlas" && usage.Cells == 9 && usage.UsedCells == 1 && usage.Repacked
            && usage.Usage < GameBuild.RepackBelow && usage.SheetBytes == sheetBytes.Length
            && usage.ShippedSheetBytes == new FileInfo(Path.Combine(game, shippedAtlas.ImagePath())).Length && usage.ShippedPixels == (keyCell.Source.Width + 2) * (keyCell.Source.Height + 2)
            && Reason(shippedAtlas.ImagePath()) == Reason("Assets/Props.atlas"), "report names the repacked atlas, its usage and sizes");
        Require(result.AssetSummary.Any(l => l.StartsWith("Atlas Assets/Props.atlas: 1 of 9 cells used", StringComparison.Ordinal)), "Console summary names the atlas");
        // The KeyRoom showcase draws text, plays the door sound, moves the player with colliders, lays tilemaps, burns torches
        // (particles, lights): it keeps all three native libraries and leaves skeletons and tweens (roadmap 15.12) out.
        Require(report.Features.Length == Enum.GetValues<EngineFeature>().Length && report.Features.All(f => f.Included ==
            f.Feature is not (EngineFeature.Skeletons or EngineFeature.Tweens)), "KeyRoom leaves skeletons and tweens out: " + string.Join(", ", report.Features.Select(f => $"{f.Feature} {f.Included}")));
        Require(result.AssetSummary.Count(l => l.StartsWith("  ", StringComparison.Ordinal)) <= 20 && result.AssetSummary[^1] == "Asset report: " + result.AssetReport,
            "Console summary is short and ends with the report path");
        Console.WriteLine($"PASS asset report and reachability exclusion ({mode}): {report.Shipped.Length} shipped, unreferenced {string.Join(", ", unreferenced)}; "
            + $"atlas sheet {report.Atlases[0].SheetBytes:N0} -> {report.Atlases[0].ShippedSheetBytes:N0} B, {report.Atlases[0].Pixels:N0} -> {report.Atlases[0].ShippedPixels:N0} pixels");
        // Later checks scan the whole scratch folder as one project; the fixture's atlas would point at a sheet outside it.
        Directory.Delete(root, true);
        Directory.Delete(output, true);
    }

    // 16-bit mono PCM silence; 'bytes' is the size of the sample data.
    private static void WriteWav(string path, int bytes)
    {
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write("RIFF"u8); writer.Write(36 + bytes); writer.Write("WAVEfmt "u8);
        writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(22050); writer.Write(22050 * 2); writer.Write((short)2); writer.Write((short)16);
        writer.Write("data"u8); writer.Write(bytes); writer.Write(new byte[bytes]);
    }

    private static SKBitmap Rgba(string png)
    {
        using var codec = SKCodec.Create(png);
        var bitmap = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        Require(codec.GetPixels(bitmap.Info, bitmap.GetPixels()) == SKCodecResult.Success, "decode " + png);
        return bitmap;
    }

    private static void Require(bool ok, string label)
    {
        if (!ok) throw new Exception("FAIL " + label);
    }
}
