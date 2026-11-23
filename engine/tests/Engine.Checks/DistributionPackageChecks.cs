using System.Formats.Tar;
using System.IO.Compression;
using Engine.Core;

internal static class DistributionPackageChecks
{
    internal static void Run(string repository, string scratch)
    {
        const string password = "pfx-Secret-7319!";
        var secrets = Directory.CreateDirectory(Path.Combine(scratch, "user-certs")).FullName;
        File.WriteAllText(Path.Combine(secrets, "password.txt"), password + "\n");
        File.WriteAllText(Path.Combine(secrets, "signing.yaml"),
            $"windowsPfx: {Path.Combine(secrets, "game.pfx")}\nwindowsPfxPasswordFile: {Path.Combine(secrets, "password.txt")}\n");
        var signing = SigningSettings.Load(Path.Combine(secrets, "signing.yaml"));
        if (signing.ReadPfxPassword() != password) throw new Exception("PFX password was not read from the user-specified file.");
        var logs = new List<string>();
        if (!OperatingSystem.IsWindows())
        {
            // A stand-in signtool that prints every argument it receives, then one that fails while naming them.
            DistributionPackage.RunSigntool("/bin/echo", "MoNoonPlayer.exe", signing, logs.Add);
            if (!logs.Any(l => l.Contains("********"))) throw new Exception("Signtool output was not redacted.");
            try { DistributionPackage.RunSigntool("/bin/ls", "MoNoonPlayer.exe", signing, logs.Add); throw new Exception("Failing signtool was accepted."); }
            catch (InvalidDataException ex) { logs.Add(ex.Message); }
        }

        var project = Path.Combine(repository, "samples", "KeyRoom");
        var assets = ProjectAssets.Scan(project);
        var icon = assets.Entries.First(e => SpriteAssets.IsPng(e.Path));
        var settings = GameSettings.Load(project) with { GameTitle = "KeyRoom", Icon = icon.Id };
        // On a Windows host signtool would run for real; the configured password is still covered by the stand-in above.
        var packaging = OperatingSystem.IsWindows() ? new SigningSettings() : signing;
        foreach (var rid in new[] { "linux-x64", "win-x64" })
        {
            var output = Directory.CreateDirectory(Path.Combine(scratch, "packages", rid)).FullName;
            var product = Directory.CreateDirectory(Path.Combine(output, "KeyRoom-" + rid)).FullName;
            var symbols = Directory.CreateDirectory(Path.Combine(output, "Symbols")).FullName;
            string player = Path.Combine(product, rid == "win-x64" ? "MoNoonPlayer.exe" : "MoNoonPlayer");
            File.WriteAllBytes(player, [0x7f, (byte)'E', (byte)'L', (byte)'F', 2, 1, 1, 0]);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(player, (UnixFileMode)0b111_101_101);
            Directory.CreateDirectory(Path.Combine(product, "Game"));
            File.WriteAllText(Path.Combine(product, "Game", "GameSettings"), "width: 800\n");
            var receipt = new GameData();
            receipt.Set("profile", "development");
            receipt.Set("mode", "release");
            File.WriteAllText(Path.Combine(product, "MoNoonBuild"), receipt.ToYaml());
            var build = new GameBuildResult(product, symbols);
            try { DistributionPackage.Create(project, settings, rid, build, packaging, logs.Add); throw new Exception("A development build was packaged."); }
            catch (InvalidDataException ex) when (ex.Message.Contains("distribution")) { }
            receipt.Set("profile", "distribution");
            File.WriteAllText(Path.Combine(product, "MoNoonBuild"), receipt.ToYaml());
            File.Copy(Path.Combine(product, "MoNoonBuild"), Path.Combine(symbols, "MoNoonBuild"));
            var package = DistributionPackage.Create(project, settings, rid, build, packaging, logs.Add).Single();
            if (rid == "linux-x64")
            {
                var entries = new Dictionary<string, (UnixFileMode Mode, string Text, long Length)>();
                using (var reader = new TarReader(new GZipStream(File.OpenRead(package), CompressionMode.Decompress)))
                    while (reader.GetNextEntry() is { } entry)
                        entries[entry.Name] = (entry.Mode, entry.DataStream == null ? "" : new StreamReader(entry.DataStream).ReadToEnd(), entry.Length);
                const UnixFileMode exec = (UnixFileMode)0b111_101_101, data = (UnixFileMode)0b110_100_100;
                string root = "KeyRoom-linux-x64/";
                if (entries[root + "MoNoonPlayer"].Mode != exec || entries[root + "install-desktop-entry.sh"].Mode != exec ||
                    entries[root + "Game/GameSettings"].Mode != data || entries[root + "KeyRoom.desktop"].Mode != data ||
                    !entries[root + "KeyRoom.desktop"].Text.Contains("Exec=MoNoonPlayer\nIcon=KeyRoom\n") ||
                    entries[root + "KeyRoom.png"].Length != new FileInfo(Path.Combine(project, icon.Path)).Length)
                    throw new Exception("Linux archive lost a mode, the desktop entry or the icon: " + string.Join(", ", entries.Select(e => $"{e.Key} {e.Value.Mode}")));
            }
            else
            {
                using var zip = ZipFile.OpenRead(package);
                if (!zip.Entries.Any(e => e.FullName.Replace('\\', '/') == "KeyRoom-win-x64/MoNoonPlayer.exe"))
                    throw new Exception("Windows zip does not contain KeyRoom-win-x64/MoNoonPlayer.exe.");
            }
        }
        if (!OperatingSystem.IsWindows() && !logs.Any(l => l.Contains("signtool runs only on a Windows host")))
            throw new Exception("The skipped Windows signing step left no message.");
        if (logs.FirstOrDefault(l => l.Contains(password)) is { } leak) throw new Exception("Signing password reached the build log: " + leak);

        try
        {
            DistributionPackage.RejectSecretsInside(signing with { Source = Path.Combine(project, "signing.yaml") }, project, scratch);
            throw new Exception("Signing settings inside the project were accepted.");
        }
        catch (InvalidDataException) { }
        string release = DistributionPackage.Entitlements(jit: true, adHoc: true), native = DistributionPackage.Entitlements(jit: false, adHoc: false);
        if (!release.Contains("allow-jit") || !release.Contains("disable-library-validation") || native.Contains("<key>"))
            throw new Exception("Unexpected macOS entitlements.");
        Console.WriteLine("PASS distribution packages: tar modes, desktop entry, zip layout, skipped signtool, redacted password");
    }
}
