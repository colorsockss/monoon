using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Engine.Core;

internal static class ContentUpdateChecks
{
    internal static void Run(string scratch)
    {
        const string asset = "Assets/Quotes/Greeting.data";
        var settings = new GameSettings { ProjectId = Guid.NewGuid(), ContentBundles = ["Assets/Quotes"] };
        string work = Directory.CreateDirectory(Path.Combine(scratch, "updates")).FullName;
        string game = Path.Combine(work, "Game");
        string source = Path.Combine(work, "replacements");
        string bundle = Path.Combine(work, "harvest.bundle");
        string original = Path.Combine(game, asset), replacement = Path.Combine(source, asset);
        Directory.CreateDirectory(Path.GetDirectoryName(original)!);
        Directory.CreateDirectory(Path.GetDirectoryName(replacement)!);
        var data = new GameData();
        data.Set("greeting", "original");
        File.WriteAllText(original, data.ToYaml());
        AssetEntry[] manifest = [new(Guid.NewGuid(), asset, Hash(original))];
        ProjectAssets.Write(Path.Combine(game, ProjectAssets.Manifest), manifest);
        ContentBundle.Pack(game, settings.ContentBundles, manifest);
        data.Set("greeting", "harvest");
        File.WriteAllText(replacement, data.ToYaml());
        var updates = new ContentUpdates(game, settings);
        string hash = updates.Publish(source, bundle);
        if (hash != Hash(bundle) || hash != updates.Publish(source, bundle)) throw new Exception("Nonrepeatable update.");
        string active = Path.Combine(PlayerStorage.ForGame(settings.ProjectId.Value, false).DirectoryPath, "ContentUpdates", "Active.bundle");
        void Check(string root, string expected)
        {
            string file = Path.Combine(root, asset);
            if (GameData.Load(file).GetText("greeting") != expected
                || ProjectAssets.ReadManifest(root).Entries.Single() != manifest[0] with { Hash = Hash(file) })
                throw new Exception("Unexpected mounted update or effective manifest.");
        }
        void Launch(string expected, bool fallback = false, GameMods? mods = null)
        {
            var warnings = new List<string>();
            using var content = updates.Open(mods, warnings.Add);
            Check(content.Root, expected);
            if (warnings.Any() != fallback) throw new Exception("Unexpected content fallback status.");
        }

        Reject(() => updates.Apply(bundle, new string('0', 64), _ => { }));
        Launch("original");
        updates.Apply(bundle, hash, root => Check(root, "harvest"));
        using var running = updates.Open(null, message => throw new Exception(message));
        Launch("harvest");
        // A valid download whose contents fail the game's startup check must not commit.
        data.Set("greeting", "new");
        File.WriteAllText(replacement, data.ToYaml());
        string next = Path.Combine(work, "next.bundle"), nextHash = updates.Publish(source, next);
        Reject(() => updates.Apply(next, nextHash, root => { Check(root, "new"); throw new InvalidDataException("Startup failed."); }));
        if (Hash(active) != hash) throw new Exception("Failed startup replaced the current update.");
        using (var zip = ZipFile.Open(next, ZipArchiveMode.Update))
        {
            zip.GetEntry(asset)!.Delete();
            using var output = zip.CreateEntry(asset).Open();
            output.Write(Encoding.UTF8.GetBytes(data.ToYaml() + "\n"));
        }
        Reject(() => updates.Apply(next, Hash(next), _ => { }));
        var otherGame = new ContentUpdates(game, settings with { ProjectId = Guid.NewGuid() });
        Reject(() => otherGame.Apply(bundle, hash, _ => { }));
        byte[] baseManifest = File.ReadAllBytes(Path.Combine(game, ProjectAssets.Manifest));
        File.AppendAllText(Path.Combine(game, ProjectAssets.Manifest), "\n");
        Reject(() => updates.Apply(bundle, hash, _ => { }));
        Launch("original", fallback: true);
        File.WriteAllBytes(Path.Combine(game, ProjectAssets.Manifest), baseManifest);
        Launch("harvest");

        var mods = GameMods.ForPlayer(game, settings);
        Directory.CreateDirectory(mods.UserDirectory);
        data.Set("greeting", "mod");
        ProjectAssets.Write(Path.Combine(mods.UserDirectory, "festival.mod"),
            new ModDocument(1, settings.ProjectId.Value, "Festival", [new(manifest[0].Id, data)]));
        mods.SetEnabled("festival", true);
        Launch("mod", mods: mods);
        mods.SetEnabled("festival", false);
        Launch("harvest", mods: mods);
        File.WriteAllBytes(active, "interrupted"u8.ToArray());
        Launch("original", fallback: true);
        updates.Apply(bundle, hash, root => Check(root, "harvest"));
        updates.Reset();
        Launch("original");
        Check(running.Root, "harvest");
        File.WriteAllText(Path.Combine(source, "Other.data"), data.ToYaml());
        Reject(() => updates.Publish(source, bundle));
        if (Hash(bundle) != hash) throw new Exception("Failed publication overwrote the distributable.");
        Console.WriteLine("PASS content updates: deterministic package, SHA-256, game/build compatibility, failed preflight, original and last-good content, mods, live mounts and reset.");
    }

    private static string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new Exception("Invalid content update was accepted.");
    }
}
