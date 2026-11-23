using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using VYaml.Annotations;

namespace Engine.Core;

// Per-user signing settings. They live outside projects so certificates and passwords never enter a repository,
// a build log or a package. The PFX password comes from MONOON_WINDOWS_PFX_PASSWORD or the file named here.
[YamlObject]
public sealed partial record SigningSettings
{
    public string? MacIdentity { get; init; }
    public string? NotaryKeychainProfile { get; init; }
    public string? WindowsCertificateThumbprint { get; init; }
    public string? WindowsPfx { get; init; }
    public string? WindowsPfxPasswordFile { get; init; }
    public string? WindowsTimestampUrl { get; init; }
    [YamlIgnore] public string? Source { get; init; }

    public const string PathVariable = "MONOON_SIGNING_SETTINGS";
    public const string PasswordVariable = "MONOON_WINDOWS_PFX_PASSWORD";

    public static string DefaultPath => Environment.GetEnvironmentVariable(PathVariable) is { Length: > 0 } path ? Path.GetFullPath(path)
        : Path.Combine(Environment.GetEnvironmentVariable("MONOON_USER_DATA") is { Length: > 0 } root ? root
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MoNoon"), "signing.yaml");

    public static SigningSettings Load(string? path = null)
    {
        path ??= DefaultPath;
        return File.Exists(path) ? ProjectYaml.Read<SigningSettings>(path) with { Source = Path.GetFullPath(path) } : new();
    }

    internal string? ReadPfxPassword()
    {
        if (Environment.GetEnvironmentVariable(PasswordVariable) is { Length: > 0 } password) return password;
        return WindowsPfxPasswordFile == null ? null : File.ReadAllText(WindowsPfxPasswordFile).TrimEnd('\r', '\n');
    }
}

// Distribution steps that run after a distribution-profile export: sign, notarise and archive the product.
public static class DistributionPackage
{
    public static IReadOnlyList<string> Create(string project, GameSettings settings, string rid, GameBuildResult build, SigningSettings signing, Action<string> log)
    {
        string product = Path.GetFullPath(build.Product);
        string bin = rid == "osx-arm64" ? Path.Combine(product, "Contents", "MacOS") : product;
        var receipt = GameData.Load(Path.Combine(bin, BuildSymbols.Receipt));
        if (receipt.GetText("profile") != "distribution" || build.SymbolDirectory == null)
            throw new InvalidDataException("Packages are made from distribution-profile builds only.");
        string directory = Path.GetDirectoryName(product)!;
        RejectSecretsInside(signing, project, directory);
        bool native = receipt.GetText("mode") == "native-aot";
        string name = rid == "osx-arm64" ? Path.GetFileNameWithoutExtension(product) : Path.GetFileName(product);
        string baseName = rid == "osx-arm64" ? name + "-osx-arm64" : name;
        var packages = new List<string>();
        var work = Directory.CreateDirectory(Path.Combine(directory, ".monoon-package-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            switch (rid)
            {
                case "osx-arm64": PackageMac(product, Path.Combine(directory, baseName), native, signing, build, work, log, packages); break;
                case "win-x64":
                    SignWindows(product, signing, build, log);
                    packages.Add(Publish(Path.Combine(directory, baseName + ".zip"), temp =>
                        ZipFile.CreateFromDirectory(product, temp, CompressionLevel.Optimal, includeBaseDirectory: true)));
                    break;
                case "linux-x64":
                    packages.Add(Publish(Path.Combine(directory, baseName + ".tar.gz"), temp => WriteLinuxArchive(project, settings, product, name, temp, log)));
                    break;
                default: throw new ArgumentException("Unknown player target.", nameof(rid));
            }
        }
        finally { Directory.Delete(work, true); }
        foreach (var package in packages) log($"Package: {package}");
        return packages;
    }

    // Reject secrets stored where they could be committed or shipped: the project, its repository or the build output.
    internal static void RejectSecretsInside(SigningSettings signing, string project, string output)
    {
        var roots = new List<string> { Path.GetFullPath(project), Path.GetFullPath(output) };
        for (var dir = new DirectoryInfo(project); dir != null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) || File.Exists(Path.Combine(dir.FullName, ".git"))) { roots.Add(dir.FullName); break; }
        foreach (var (label, path) in new[] { ("Signing settings", signing.Source), ("PFX certificate", signing.WindowsPfx), ("PFX password file", signing.WindowsPfxPasswordFile) })
        {
            if (path == null) continue;
            var full = Path.GetFullPath(path);
            if (roots.Any(root => full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException($"{label} must be stored outside the project, its repository and the build output: {full}");
        }
    }

    private static void PackageMac(string app, string basePath, bool native, SigningSettings signing, GameBuildResult build, string work, Action<string> log, List<string> packages)
    {
        if (!OperatingSystem.IsMacOS())
        {
            log("macOS signing skipped: codesign, notarytool and hdiutil run only on macOS. The zip is unsigned.");
            packages.Add(Publish(basePath + ".zip", temp => ZipFile.CreateFromDirectory(app, temp, CompressionLevel.Optimal, includeBaseDirectory: true)));
            return;
        }
        bool adHoc = string.IsNullOrWhiteSpace(signing.MacIdentity);
        MarkSigned(app, Path.Combine("Contents", "MacOS"), build, adHoc ? "macos-adhoc" : "macos-developer-id");
        MoveDataToResources(app);
        var entitlements = Path.Combine(work, "entitlements.plist");
        File.WriteAllText(entitlements, Entitlements(jit: !native, adHoc));
        string[] common = ["--force", "--options", "runtime", adHoc ? "--timestamp=none" : "--timestamp", "--sign", adHoc ? "-" : signing.MacIdentity!];
        // Sign inside-out: every nested file (dylibs, managed DLLs, receipts) before the bundle seals them.
        var nested = Directory.GetFiles(Path.Combine(app, "Contents", "MacOS")).Where(f => Path.GetFileName(f) != "MoNoonPlayer"
            && new FileInfo(f).LinkTarget == null).Order(StringComparer.Ordinal).ToArray();
        if (nested.Length > 0) Require("/usr/bin/codesign", [.. common, .. nested], "codesign (nested code)");
        Require("/usr/bin/codesign", [.. common, "--entitlements", entitlements, app], "codesign");
        Require("/usr/bin/codesign", ["--verify", "--deep", "--strict", app], "codesign --verify");
        log(adHoc ? "Signed ad-hoc (no macIdentity configured); runs on this Mac, other Macs need a Developer ID signature."
                  : $"Signed with {signing.MacIdentity} (hardened runtime).");
        string zip = basePath + ".zip";
        void Zip() => packages.Add(Publish(zip, temp => Require("/usr/bin/ditto", ["-c", "-k", "--keepParent", app, temp], "ditto")));
        Zip();
        bool notarize = !adHoc && !string.IsNullOrWhiteSpace(signing.NotaryKeychainProfile);
        if (notarize)
        {
            Notarize(zip, signing.NotaryKeychainProfile!, log);
            Require("/usr/bin/xcrun", ["stapler", "staple", app], "stapler");
            packages.Remove(zip);
            Zip();
        }
        else log(adHoc ? "Notarisation skipped: ad-hoc signatures cannot be notarised. Set macIdentity and notaryKeychainProfile."
                       : "Notarisation skipped: no notaryKeychainProfile configured (create one with xcrun notarytool store-credentials).");
        var stage = Directory.CreateDirectory(Path.Combine(work, "dmg")).FullName;
        Require("/usr/bin/ditto", [app, Path.Combine(stage, Path.GetFileName(app))], "ditto");
        File.CreateSymbolicLink(Path.Combine(stage, "Applications"), "/Applications");
        string dmg = basePath + ".dmg";
        packages.Add(Publish(dmg, temp =>
        {
            Require("/usr/bin/hdiutil", ["create", "-quiet", "-volname", Path.GetFileNameWithoutExtension(app), "-srcfolder", stage, "-format", "UDZO", "-ov", temp], "hdiutil");
            if (!adHoc) Require("/usr/bin/codesign", ["--force", "--timestamp", "--sign", signing.MacIdentity!, temp], "codesign (dmg)");
            if (notarize) { Notarize(temp, signing.NotaryKeychainProfile!, log); Require("/usr/bin/xcrun", ["stapler", "staple", temp], "stapler"); }
        }, ".dmg"));
    }

    // Contents/MacOS may hold only code: folders there would be read as nested bundles. Keep their paths via relative links.
    private static void MoveDataToResources(string app)
    {
        string macos = Path.Combine(app, "Contents", "MacOS"), resources = Directory.CreateDirectory(Path.Combine(app, "Contents", "Resources")).FullName;
        foreach (var folder in Directory.GetDirectories(macos))
        {
            if (new DirectoryInfo(folder).LinkTarget != null) continue;
            string name = Path.GetFileName(folder);
            Directory.Move(folder, Path.Combine(resources, name));
            Directory.CreateSymbolicLink(folder, Path.Combine("..", "Resources", name));
        }
    }

    // CoreCLR (Release) needs JIT pages under the hardened runtime; Native AOT does not. Ad-hoc dylibs carry no team ID.
    internal static string Entitlements(bool jit, bool adHoc)
    {
        var keys = new StringBuilder();
        if (jit) keys.Append("  <key>com.apple.security.cs.allow-jit</key><true/>\n");
        if (adHoc) keys.Append("  <key>com.apple.security.cs.disable-library-validation</key><true/>\n");
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
               $"<plist version=\"1.0\">\n<dict>\n{keys}</dict>\n</plist>\n";
    }

    private static void Notarize(string file, string profile, Action<string> log)
    {
        log($"Submitting {Path.GetFileName(file)} for notarisation (keychain profile {profile})…");
        var output = Require("/usr/bin/xcrun", ["notarytool", "submit", file, "--keychain-profile", profile, "--wait", "--output-format", "json"], "notarytool");
        string? status = null;
        try { status = JsonDocument.Parse(output).RootElement.GetProperty("status").GetString(); } catch (Exception ex) when (ex is JsonException or KeyNotFoundException) { }
        if (status != "Accepted") throw new InvalidDataException($"Notarisation was not accepted ({status ?? "no status"}). Run xcrun notarytool log with the submission ID: {output}");
        log("Notarisation accepted.");
    }

    // The receipt's playerSha256 describes the unsigned player; record that signing changed it.
    private static void MarkSigned(string product, string binRelative, GameBuildResult build, string kind)
    {
        foreach (var file in new[] { Path.Combine(product, binRelative, BuildSymbols.Receipt), Path.Combine(build.SymbolDirectory!, BuildSymbols.Receipt) })
        {
            var receipt = GameData.Load(file);
            receipt.Set("codeSignature", kind);
            ProjectYaml.WriteBytes(file, Encoding.UTF8.GetBytes(receipt.ToYaml()));
        }
    }

    private static void SignWindows(string product, SigningSettings signing, GameBuildResult build, Action<string> log)
    {
        bool configured = !string.IsNullOrWhiteSpace(signing.WindowsCertificateThumbprint) || !string.IsNullOrWhiteSpace(signing.WindowsPfx);
        if (!OperatingSystem.IsWindows() || !configured)
        {
            log(!configured ? "Windows signing skipped: no windowsCertificateThumbprint or windowsPfx configured."
                            : "Windows signing skipped: signtool runs only on a Windows host. Package on Windows to sign.");
            return;
        }
        RunSigntool("signtool.exe", Path.Combine(product, "MoNoonPlayer.exe"), signing, log);
        MarkSigned(product, "", build, "windows-authenticode");
    }

    // Separate from the host check so tests can run the same argument and redaction path with a stand-in tool.
    internal static void RunSigntool(string tool, string executable, SigningSettings signing, Action<string> log)
    {
        var arguments = new List<string> { "sign", "/fd", "SHA256" };
        string? password = null;
        if (!string.IsNullOrWhiteSpace(signing.WindowsCertificateThumbprint)) arguments.AddRange(["/sha1", signing.WindowsCertificateThumbprint!]);
        else
        {
            password = signing.ReadPfxPassword();
            arguments.AddRange(["/f", signing.WindowsPfx!]);
            if (password != null) arguments.AddRange(["/p", password]);
        }
        if (!string.IsNullOrWhiteSpace(signing.WindowsTimestampUrl)) arguments.AddRange(["/tr", signing.WindowsTimestampUrl!, "/td", "SHA256"]);
        arguments.Add(executable);
        var output = Require(tool, arguments, "signtool", password);
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) log("signtool: " + line);
        log($"Signed {Path.GetFileName(executable)} with signtool.");
    }

    private static void WriteLinuxArchive(string project, GameSettings settings, string product, string name, string archive, Action<string> log)
    {
        string game = settings.GameTitle ?? new DirectoryInfo(project).Name;
        byte[]? icon = null;
        if (settings.Icon is Guid id)
        {
            var path = ProjectAssets.Scan(project).PathFor(id);
            if (path == null || !SpriteAssets.IsPng(path)) throw new InvalidDataException($"The package icon {id} is not a PNG asset.");
            icon = File.ReadAllBytes(Path.Combine(project, path));
        }
        else log("Linux desktop entry has no icon: choose a PNG for Icon in Build Game… (GameSettings icon).");
        string entryId = "monoon-" + (settings.ProjectId ?? settings.StartScene ?? Guid.Empty).ToString("N");
        string desktop = $"[Desktop Entry]\nType=Application\nName={game}\nExec=MoNoonPlayer\n{(icon != null ? $"Icon={game}\n" : "")}Terminal=false\nCategories=Game;\n";
        string installer = "#!/bin/sh\n# Adds this game to the desktop menu, pointing at this folder.\nset -eu\n" +
            "here=$(CDPATH= cd -- \"$(dirname -- \"$0\")\" && pwd)\n" +
            "target=\"${XDG_DATA_HOME:-$HOME/.local/share}/applications\"\nmkdir -p \"$target\"\n" +
            $"sed -e \"s|^Exec=.*|Exec=\\\"$here/MoNoonPlayer\\\"|\" -e \"s|^Icon=.*|Icon=$here/{game}.png|\" \"$here/{game}.desktop\" > \"$target/{entryId}.desktop\"\n" +
            $"echo \"Installed $target/{entryId}.desktop\"\n";
        const UnixFileMode executable = (UnixFileMode)0b111_101_101, data = (UnixFileMode)0b110_100_100;
        using var file = File.Create(archive);
        using var gzip = new GZipStream(file, CompressionLevel.Optimal);
        using var tar = new TarWriter(gzip, TarEntryFormat.Pax);
        foreach (var path in Directory.EnumerateFileSystemEntries(product, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            string entryName = name + "/" + Path.GetRelativePath(product, path).Replace('\\', '/');
            var info = new FileInfo(path);
            if (info.LinkTarget is { } link) tar.WriteEntry(new PaxTarEntry(TarEntryType.SymbolicLink, entryName) { LinkName = link, Mode = executable });
            else if (Directory.Exists(path)) tar.WriteEntry(new PaxTarEntry(TarEntryType.Directory, entryName + "/") { Mode = executable });
            else
            {
                using var stream = File.OpenRead(path);
                var mode = OperatingSystem.IsWindows() ? IsElf(path) ? executable : data : File.GetUnixFileMode(path);
                tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, entryName) { Mode = mode, DataStream = stream });
            }
        }
        void Add(string entry, byte[] bytes, UnixFileMode mode) =>
            tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, name + "/" + entry) { Mode = mode, DataStream = new MemoryStream(bytes) });
        Add(game + ".desktop", Encoding.UTF8.GetBytes(desktop), data);
        if (icon != null) Add(game + ".png", icon, data);
        Add("install-desktop-entry.sh", Encoding.UTF8.GetBytes(installer), executable);
    }

    // A Windows host has no Unix modes; ELF images are the player and its native helpers.
    private static bool IsElf(string path)
    {
        Span<byte> magic = stackalloc byte[4];
        using var file = File.OpenRead(path);
        return file.Read(magic) == 4 && magic.SequenceEqual("\x7fELF"u8);
    }

    // Write next to the target and replace it only after the step succeeded.
    private static string Publish(string path, Action<string> write, string extension = "")
    {
        string temp = Path.Combine(Path.GetDirectoryName(path)!, ".monoon-" + Guid.NewGuid().ToString("N") + extension);
        try { write(temp); File.Move(temp, path, true); }
        finally { File.Delete(temp); }
        return path;
    }

    // Output is redacted before it can reach a log or exception; argument lists are never printed.
    internal static string Require(string tool, IEnumerable<string> arguments, string step, string? secret = null)
    {
        var start = new ProcessStartInfo(tool) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        Process process;
        try { process = Process.Start(start) ?? throw new IOException($"Could not start {step}."); }
        catch (System.ComponentModel.Win32Exception) { throw new FileNotFoundException($"{step} was not found ({tool}). Install it or skip packaging."); }
        string Redact(string text) => string.IsNullOrEmpty(secret) ? text : text.Replace(secret, "********", StringComparison.Ordinal);
        using (process)
        {
            var error = process.StandardError.ReadToEndAsync();
            string output = Redact(process.StandardOutput.ReadToEnd());
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidDataException($"{step} failed with exit code {process.ExitCode}: {(output + Redact(error.Result)).Trim()}");
            return output;
        }
    }
}
