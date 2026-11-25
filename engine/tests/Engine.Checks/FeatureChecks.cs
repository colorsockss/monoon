using System.Diagnostics;
using Engine.Core;

// Roadmap 15.11: builds leave out SDL_ttf, SDL_mixer and Box2D when nothing shipped uses text, audio or physics.
// SpriteLab copies (no text, audio or physics) are exported for the host as Release: plain, with a script that only
// plays sound, with GameSettings forcing physics, and with a script that needs Box2D whose library is then removed.
internal static class FeatureChecks
{
    [Check(200, StopAfter = "features", Templates = true)] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    internal static void Run(string repository, string scratch)
    {
        // Later checks scan the scratch folder as a project; dot folders are not content.
        scratch = Directory.CreateDirectory(Path.Combine(scratch, ".features")).FullName;
        string rid = BuildSettings.Host;
        string template = GameBuild.FindPlayerTemplate(Path.Combine(repository, "engine"), rid);
        string Library(EngineFeature feature) => BuildFeatures.Files(feature).Single(f => rid switch
        {
            "osx-arm64" => f.EndsWith(".dylib", StringComparison.Ordinal),
            "linux-x64" => f.EndsWith(".so", StringComparison.Ordinal),
            _ => f.EndsWith(".dll", StringComparison.Ordinal)
        });
        (string Bin, AssetBuildReport Report) Export(string name, Action<string>? edit = null, Func<GameSettings, GameSettings>? settings = null)
        {
            var root = Path.Combine(scratch, "Features-" + name);
            Copy(Path.Combine(repository, "samples", "SpriteLab"), root);
            // The SpriteLab showcase labels its signs (Text 2D) and HUD (UI Text); without text it uses no optional engine feature.
            var yard = Path.Combine(root, "Scenes", "ShowcaseYard.scene");
            var scene = SceneDocument.Load(yard);
            foreach (var obj in scene.Objects) while (obj.RemoveComponent<Text2DComponent>() || obj.RemoveComponent<UiTextComponent>()) { }
            scene.Save(yard);
            edit?.Invoke(root);
            var result = GameBuild.Export(root, template, rid, Path.Combine(scratch, "FeaturesOutput-" + name),
                (settings ?? (s => s))(GameSettings.Load(root)), BuildProfile.Distribution);
            string bin = rid == "osx-arm64" ? Path.Combine(result.Product, "Contents", "MacOS") : result.Product;
            Require(result.AssetSummary.Count(l => l.StartsWith("Engine feature ", StringComparison.Ordinal)) == Enum.GetValues<EngineFeature>().Length, name + ": summary names every feature");
            return (bin, ProjectYaml.Read<AssetBuildReport>(result.AssetReport!));
        }
        void Expect(string name, (string Bin, AssetBuildReport Report) build, params EngineFeature[] included)
        {
            foreach (var decision in build.Report.Features)
            {
                bool want = included.Contains(decision.Feature);
                Require(decision.Included == want, $"{name}: {decision.Feature} {(want ? "included" : "left out")} ({decision.Reason})");
                if (BuildFeatures.Files(decision.Feature).Length == 0) continue; // engine code only (roadmap 15.12)
                Require(File.Exists(Path.Combine(build.Bin, Library(decision.Feature))) == want
                    && File.Exists(Path.Combine(build.Bin, BuildFeatures.Files(decision.Feature)[^1])) == want, $"{name}: {decision.Feature} library and notice shipped only when included");
            }
            var receipt = GameData.Load(Path.Combine(build.Bin, BuildSymbols.Receipt)).GetText("features");
            Require(receipt == string.Join(",", included.Order().Select(f => f.ToString().ToLowerInvariant())), $"{name}: receipt features '{receipt}'");
            Require(File.Exists(Path.Combine(build.Bin, "Brutal.Box2D.dll")) && File.Exists(Path.Combine(build.Bin, "Brutal.Sdl.Mixer.dll")) && File.Exists(Path.Combine(build.Bin, "Brutal.Sdl.Ttf.dll")),
                $"{name}: managed BRUTAL bindings stay");
        }

        var plain = Export("plain");
        Expect("plain SpriteLab (showcase without text)", plain);
        var smoke = Player(plain.Bin, "--smoke-test", Path.Combine(scratch, "features-smoke"));
        Require(smoke.Code == 0, "SpriteLab without SDL_ttf, SDL_mixer and Box2D starts: " + smoke.Output);

        var chime = Export("script-audio", root => File.WriteAllText(Path.Combine(root, "Scripts", "Chime.cs"), """
            using Engine.Core;
            public sealed class Chime : GameBehaviour
            {
                public override void FixedUpdate(ScriptTick tick) => tick.Objects.Audio.Play(tick.ObjectId);
            }
            """));
        Expect("script-only audio", chime, EngineFeature.Audio);
        var audio = chime.Report.Features.Single(f => f.Feature == EngineFeature.Audio).Reason;
        Require(audio.StartsWith("Scripts/Chime.cs:4: ", StringComparison.Ordinal) && audio.Contains("Play"), "script audio reason: " + audio);

        var forced = Export("forced", settings: s => s with { Features = ["physics"] });
        Expect("forced physics", forced, EngineFeature.Physics);
        Require(forced.Report.Features.Single(f => f.Feature == EngineFeature.Physics).Reason == "GameSettings features", "forced reason");
        Require(!File.ReadAllText(Path.Combine(forced.Bin, "Game", GameSettings.FileName)).Contains("features"), "the shipped GameSettings has no features list");

        // A feature reached anyway (here a deleted library) fails with the exclusion message, not a bare DllNotFoundException.
        var probe = Export("missing-physics", root =>
        {
            File.WriteAllText(Path.Combine(root, "Scripts", "Prober.cs"), """
                using System.Drawing;
                using Engine.Core;
                public sealed class Prober : GameBehaviour
                {
                    public override void OnCreate(ScriptLife life) => Shape2D.Box(new RectangleF(0, 0, 4, 4));
                }
                """);
            var path = Path.Combine(root, "Scenes", "ShowcaseYard.scene");
            var scene = SceneDocument.Load(path);
            scene.Objects[0].AddComponent(new ScriptComponent { Script = "Prober" });
            scene.Save(path);
        });
        Expect("script physics", probe, EngineFeature.Physics);
        File.Delete(Path.Combine(probe.Bin, Library(EngineFeature.Physics)));
        var missing = Player(probe.Bin, "--smoke-test", Path.Combine(scratch, "features-missing"));
        Require(missing.Code == 1 && missing.Output.Contains("This build excluded physics (Box2D)") && missing.Output.Contains("GameSettings features"),
            "missing Box2D explains the exclusion: " + missing.Output);
        Console.WriteLine("PASS engine feature libraries: plain SpriteLab ships none and starts; script-only audio, forced physics and the missing-library message");
    }

    private static (int Code, string Output) Player(string bin, params string[] args)
    {
        var start = new ProcessStartInfo(Path.Combine(bin, OperatingSystem.IsWindows() ? "MoNoonPlayer.exe" : "MoNoonPlayer"))
            { WorkingDirectory = bin, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output.Result + error);
    }

    private static void Copy(string from, string to)
    {
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(from, file);
            if (relative.Split(Path.DirectorySeparatorChar)[0] is "bin" or "obj" or ".monoon") continue;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(to, relative))!);
            File.Copy(file, Path.Combine(to, relative));
        }
        Directory.CreateDirectory(Path.Combine(to, "Scripts"));
    }

    private static void Require(bool ok, string label)
    {
        if (!ok) throw new Exception("FAIL " + label);
    }
}
