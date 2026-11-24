using Engine.Core;

// Project build settings (Build Game dialog and `monoon build`): defaults, round trip, CLI override precedence, and that
// the file stays out of game builds.
internal static class BuildSettingsChecks
{
    [Check(600, StopAfter = "build-settings", Templates = true)] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    internal static void Run(string repository, string scratch)
    {
        var root = Directory.CreateDirectory(Path.Combine(scratch, "BuildSettingsProject")).FullName;
        var defaults = BuildSettings.Load(root);
        Require(defaults.Target == BuildSettings.Host && defaults.Type == "release" && defaults.Profile == "distribution"
            && defaults.Content == "incremental" && !defaults.Package, "missing BuildSettings gives host Release distribution, incremental, no package");

        var saved = new BuildSettings { Target = "linux-x64", Type = "release", Profile = "distribution", Content = "full", Package = true };
        saved.Save(root);
        var text = File.ReadAllText(Path.Combine(root, BuildSettings.FileName));
        Require(text == "target: linux-x64\ntype: release\nprofile: distribution\ncontent: full\npackage: true\n", "BuildSettings YAML: " + text);
        Require(BuildSettings.Load(root) == saved, "BuildSettings round trip");
        File.WriteAllText(Path.Combine(root, BuildSettings.FileName), "type: native-aot\n");
        var partial = BuildSettings.Load(root);
        Require(partial.NativeAot && partial.Target == BuildSettings.Host && partial.BuildProfile == BuildProfile.Distribution, "absent keys use defaults");
        foreach (var bad in new[] { "target: osx-x64\n", "profile: debug\n", "profile: development\npackage: true\n", "content: some\n" })
        {
            File.WriteAllText(Path.Combine(root, BuildSettings.FileName), bad);
            try { BuildSettings.Load(root); throw new Exception("Accepted invalid BuildSettings: " + bad); }
            catch (InvalidDataException ex) when (ex.Message.Contains(BuildSettings.FileName)) { }
        }

        // Explicit arguments win for one run; everything else comes from the file, which is not rewritten.
        saved.Save(root);
        var none = BuildSettings.Override(saved, [], out var output);
        Require(none == saved && output == null, "no arguments build with the saved settings");
        var typed = BuildSettings.Override(saved, ["native-aot", "--content", "incremental", "--output", "out"], out output);
        Require(typed == saved with { Type = "native-aot", Content = "incremental" } && output == "out", "explicit type and options override saved values");
        var development = BuildSettings.Override(saved, ["--profile", "development"], out _);
        Require(development.BuildProfile == BuildProfile.Development && !development.Package && development.Target == "linux-x64",
            "--profile development turns the saved package off like the dialog");
        Require(BuildSettings.Override(saved, ["release", "--target", "osx-arm64"], out _).Target == "osx-arm64", "--target overrides the saved target");
        foreach (var bad in new[] { new[] { "--profile", "development", "--package", "on" }, ["--profile"], ["--output", "a", "--output", "b"],
                     ["--target", "osx-x64"], ["--content", "some"], ["debug"] })
        {
            try { BuildSettings.Override(saved, bad, out _); throw new Exception("Accepted invalid build arguments: " + string.Join(' ', bad)); }
            catch (ArgumentException) { }
        }
        Require(BuildSettings.Load(root) == saved, "overrides leave the saved file unchanged");

        // The file has no asset extension: the scanner skips it and the exported Game folder never contains it.
        var project = Path.Combine(repository, "samples", "KeyRoom");
        foreach (var file in Directory.EnumerateFiles(project, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(project, file);
            if (relative.Split(Path.DirectorySeparatorChar)[0] is "bin" or "obj" or ".monoon") continue;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, relative))!);
            File.Copy(file, Path.Combine(root, relative), true);
        }
        Require(!ProjectAssets.Scan(root).Entries.Any(e => e.Path == BuildSettings.FileName), "asset scanner ignores BuildSettings");
        string rid = BuildSettings.Host;
        var product = GameBuild.Export(root, GameBuild.FindPlayerTemplate(Path.Combine(repository, "engine"), rid), rid,
            Path.Combine(scratch, "BuildSettingsOutput"), GameSettings.Load(root));
        var leaked = Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(product)!, "*", SearchOption.AllDirectories)
            .Where(p => Path.GetFileName(p) == BuildSettings.FileName).ToArray();
        Require(leaked.Length == 0, "BuildSettings is not in the build output " + string.Join(", ", leaked));
        Console.WriteLine("PASS build settings defaults, round trip, CLI overrides and build output exclusion");
    }

    private static void Require(bool ok, string label)
    {
        if (!ok) throw new Exception("FAIL " + label);
    }
}
