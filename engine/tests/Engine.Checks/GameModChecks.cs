using System.Security.Cryptography;
using Engine.Core;

internal static class GameModChecks
{
    [Check(1300)] static void Registered(CheckRun c) => Run(c.Scratch);
    internal static void Run(string scratch)
    {
        var gameId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var project = Path.Combine(scratch, "mod-project");
        var bin = Path.Combine(scratch, "mod-product");
        var game = Path.Combine(bin, "Game");
        var profile = Path.Combine(scratch, "mod-profile");
        const string asset = "Assets/Story.data";
        string installed = Path.Combine(game, asset);
        Directory.CreateDirectory(Path.GetDirectoryName(installed)!);
        byte[] original = "version: 1\nvalues: {name: original}\n"u8.ToArray();
        File.WriteAllBytes(installed, original);
        AssetEntry[] entries = [new(assetId, asset, Convert.ToHexString(SHA256.HashData(original)))];
        ProjectAssets.Write(Path.Combine(game, ProjectAssets.Manifest), entries);
        var source = Directory.CreateDirectory(Path.Combine(project, "Mods")).FullName;
        var data = new GameData();
        data.Set("name", "modified");
        var mod = new ModDocument(1, gameId, "Sample", [new(assetId, data)]);
        ProjectAssets.Write(Path.Combine(source, "sample.mod"), mod);
        GameMods.Export(project, bin, gameId, entries);
        var mods = new GameMods(game, gameId, profile);
        var settings = new GameSettings { ProjectId = gameId };
        mods.SetEnabled("sample", true);
        using (var mounted = ContentBundle.Open(game, settings, new GameMods(game, gameId, profile)))
        {
            var effective = Path.Combine(mounted.Root, asset);
            if (GameData.Load(effective).GetText("name") != "modified" ||
                ProjectAssets.ReadManifest(mounted.Root).Entries.Single() != entries[0] with
                    { Hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(effective))) })
                throw new Exception("Mounted mod data or identity did not match.");
        }
        // Third-party mods live in the user profile and can also be disabled after removal.
        string userMod = Path.Combine(mods.UserDirectory, "external.mod");
        ProjectAssets.Write(userMod, mod with { GameId = Guid.NewGuid() });
        Reject(() => mods.SetEnabled("external", true));
        ProjectAssets.Write(userMod, mod with { Replacements = [new(Guid.NewGuid(), data)] });
        Reject(() => mods.SetEnabled("external", true));
        ProjectAssets.Write(userMod, mod);
        Reject(() => mods.SetEnabled("external", true)); // Both replace the story.
        Reject(() => mods.SetEnabled("../sample", true));
        if (!mods.Enabled().SequenceEqual(["sample"])) throw new Exception("Failed activation changed selected mods.");
        mods.SetEnabled("sample", false);
        mods.SetEnabled("external", true);
        File.Delete(userMod);
        mods.SetEnabled("external", false);
        using var vanilla = ContentBundle.Open(game, settings, mods);
        if (vanilla.Root != game || !File.ReadAllBytes(installed).SequenceEqual(original))
            throw new Exception("Turning off mods did not restore loose content.");
        Console.WriteLine("PASS data mods: persisted selection, third-party files, GUID and hash, conflicts, rollback and original content.");
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new Exception("Invalid mod activation was accepted.");
    }
}
