using System.Security.Cryptography;
using Engine.Core;

internal static class ContentBundleChecks
{
    [Check(1200)] static void Registered(CheckRun c) => Run(c.Scratch);
    internal static void Run(string scratch)
    {
        string game = Directory.CreateDirectory(Path.Combine(scratch, "bundled", "Game")).FullName;
        const string folder = "Assets/Dialogue";
        const string asset = folder + "/Story.data";
        Directory.CreateDirectory(Path.Combine(game, folder));
        byte[] story = "version: 1\nvalues: {}\n"u8.ToArray();
        File.WriteAllBytes(Path.Combine(game, asset), story);
        File.WriteAllText(Path.Combine(game, ".monoon-player"), "1");
        AssetEntry[] manifest = [new(Guid.NewGuid(), asset, Convert.ToHexString(SHA256.HashData(story)))];
        ProjectAssets.Write(Path.Combine(game, ProjectAssets.Manifest), manifest);
        var settings = new GameSettings { ContentBundles = [folder] };
        ContentBundle.Pack(game, settings.ContentBundles, manifest);
        if (File.Exists(Path.Combine(game, asset))) throw new Exception("Bundled asset left loose.");
        string mount;
        using (var first = ContentBundle.Open(game, settings))
        using (var second = ContentBundle.Open(game, settings))
        {
            mount = first.Root;
            if (first.Root == second.Root || !File.ReadAllBytes(Path.Combine(first.Root, asset)).SequenceEqual(story)
                || ProjectAssets.ReadManifest(second.Root).IdFor(asset) != manifest[0].Id)
                throw new Exception("Bundle mount lost content or identity.");
        }
        if (Directory.Exists(mount)) throw new Exception("Bundle mount survived disposal.");
        using var loose = ContentBundle.Open(game, new GameSettings());
        if (loose.Root != game) throw new Exception("Loose build was mounted.");
        Reject(() => ContentBundle.ValidateFolders([folder, "Assets"]));
        Reject(() => ContentBundle.ValidateFolders(["Assets/../Scripts"]));
        string archive = Path.Combine(scratch, "bundled", "ContentBundles", folder + ".bundle");
        string cache = Path.Combine(scratch, "mount-cache");
        var samples = new List<BundleReadSample>();
        ContentBundle Cached() => ContentBundle.Open(game, settings, null, null, cache, observe: samples.Add);
        string cachedRoot;
        using (var first = Cached())
        using (var concurrent = Cached())
        {
            cachedRoot = first.Root;
            if (first.CacheStatus != "miss" || first.IsTemporary || concurrent.Root == first.Root ||
                !concurrent.IsTemporary || concurrent.CacheStatus != "busy") throw new Exception("Mount lease was shared.");
        }
        using (var hit = Cached())
            if (hit.CacheStatus != "hit" || hit.Root != cachedRoot) throw new Exception("Verified mount not reused.");
        if (!samples.Select(s => s.InflatedBytes).SequenceEqual(new long[] { story.Length, story.Length, 0 }) ||
            samples.Any(s => s.Folder != folder || s.AssetCount != 1 || s.ArchiveBytes != new FileInfo(archive).Length))
            throw new Exception("Bundle diagnostics lost the read, extraction or cache hit.");
        File.WriteAllText(Path.Combine(cachedRoot, asset), new string('x', story.Length));
        ExpectRepair();
        File.WriteAllText(Path.Combine(cachedRoot, "stale.dll"), "stale");
        ExpectRepair();
        File.WriteAllText(Path.Combine(game, ".monoon-player"), "2");
        ExpectRepair();
        File.Delete(Path.Combine(cache, "verified")); // Interrupted first mount.
        ExpectRepair();
        using (var overlay = ContentBundle.Open(game, settings, null, new() { [manifest[0].Id] = "other"u8.ToArray() }, cache))
            if (overlay.CacheStatus != "overlay" || File.ReadAllText(Path.Combine(overlay.Root, asset)) != "other")
                throw new Exception("Content override did not use an independent mount.");
        using (var original = Cached())
            if (original.CacheStatus != "hit" || !File.ReadAllBytes(Path.Combine(original.Root, asset)).SequenceEqual(story))
                throw new Exception("Overlay modified the verified mount.");
        void ExpectRepair()
        {
            using var recovered = Cached();
            if (recovered.CacheStatus != "miss" || File.Exists(Path.Combine(recovered.Root, "stale.dll")) ||
                !File.ReadAllBytes(Path.Combine(recovered.Root, asset)).SequenceEqual(story))
                throw new Exception("Dirty or partial cache was not rebuilt.");
        }
        File.AppendAllText(archive, "changed");
        Reject(() => Cached().Dispose());
        Reject(() => ContentBundle.Open(game, settings).Dispose());
        File.Delete(archive);
        try { using var missing = ContentBundle.Open(game, settings); }
        catch (FileNotFoundException ex) when (ex.FileName == archive)
        {
            Console.WriteLine("PASS content bundles: isolated mounts, GUIDs, cleanup, folder validation, damaged and missing archives.");
            return;
        }
        throw new Exception("Missing bundle was accepted.");
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new Exception("Invalid content bundle was accepted.");
    }
}
