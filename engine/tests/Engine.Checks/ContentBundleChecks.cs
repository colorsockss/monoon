using System.Security.Cryptography;
using Engine.Core;

internal static class ContentBundleChecks
{
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
        File.AppendAllText(archive, "changed");
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
