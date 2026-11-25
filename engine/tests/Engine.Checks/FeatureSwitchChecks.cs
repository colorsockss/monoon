using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Engine.Core;

// Roadmap 15.12: Native AOT games built without a feature switch its engine code off (MoNoon.Feature.<Name>=false). The JIT
// runs the same code with the switch turned off (EngineFeatures.SetForChecks): a scene that uses none of the switched-off features must play
// and draw exactly as with every feature on, and a component of a switched-off feature must stop with the exclusion message.
// PlayerYamlRegistration (the AOT player's formatter list) must equal the VYaml generator's and gate nothing an ungated type uses.
internal static class FeatureSwitchChecks
{
    [Check(210, "feature-switches")] static void Registered(CheckRun c) => Run(c.Repository);

    internal static void Run(string repository)
    {
        var features = Enum.GetValues<EngineFeature>();
        foreach (var feature in features)
        {
            var property = typeof(EngineFeature).Assembly.GetType("Engine.Core.EngineFeatures")!
                .GetProperty(EngineFeaturesName(feature), BindingFlags.NonPublic | BindingFlags.Static);
            Check(property?.GetCustomAttribute<FeatureSwitchDefinitionAttribute>()?.SwitchName == NativeAotBuild.FeatureSwitch(feature),
                $"the {feature} switch the build writes ({NativeAotBuild.FeatureSwitch(feature)}) is the one EngineFeatures reads");
        }

        // Every sample scene, with each feature it does not use switched off. Play() strips the scripts, so scripted showcases
        // play their authored state (the samples are showcases since 2026-11-26; few scenes are script-free).
        int scenes = 0, frames = 0;
        foreach (var file in Directory.EnumerateFiles(Path.Combine(repository, "samples"), "*.scene", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var scene = SceneDocument.Load(file);
            // Content loaded additively into another scene (CollisionLab ShowcaseStones) has no camera of its own.
            if (!scene.Objects.Any(o => o.Components.OfType<Camera2DComponent>().Any())) continue;
            var used = scene.Objects.SelectMany(o => o.Components).Select(BuildFeatures.Of).OfType<EngineFeature>().ToHashSet();
            var off = features.Where(f => !used.Contains(f)).ToArray();
            string on = Play(file, []), cut = Play(file, off);
            Check(on == cut, $"{Path.GetRelativePath(repository, file)} draws the same with {string.Join(", ", off)} switched off");
            scenes++; frames += on.Split('\n').Count(l => l == "frame");
        }
        Check(scenes >= 20, $"sample scenes were found ({scenes})");

        // A component of a switched-off feature (content the build did not see) stops with the message that names the fix.
        var unseen = new SceneDocument();
        var sparks = new SceneObject { Name = "Sparks" };
        sparks.AddComponent(new ParticleEmitter2DComponent());
        unseen.Objects.Add(sparks);
        try { Switched([EngineFeature.Particles], () => { using var game = new SceneGame(unseen); }); throw new Exception("FAIL expected the exclusion"); }
        catch (Exception ex) when (Inner(ex) is InvalidOperationException { Message: var m } && m.Contains("Sparks: this build excluded Particles") && m.Contains("GameSettings features")) { }
        Console.WriteLine($"PASS feature switches: names match the build, {scenes} sample scenes draw the same {frames} frames with their unused features off, an excluded component explains the fix");
        Registration(repository);
    }

    private static Exception Inner(Exception ex) { while (ex is TypeInitializationException or TargetInvocationException && ex.InnerException != null) ex = ex.InnerException!; return ex; }

    private static string EngineFeaturesName(EngineFeature feature) => feature.ToString();

    private static void Switched(EngineFeature[] off, Action run)
    {
        foreach (var f in off) EngineFeatures.SetForChecks(f, false);
        try { run(); }
        finally { foreach (var f in off) EngineFeatures.SetForChecks(f, true); }
    }

    // 90 ticks with input held right and a draw after each, as text.
    private static string Play(string file, EngineFeature[] off)
    {
        var log = new StringBuilder();
        Switched(off, () =>
        {
            var scene = SceneDocument.Load(file);
            foreach (var obj in scene.Objects) foreach (var script in obj.Components.OfType<ScriptComponent>().ToArray()) obj.RemoveComponent(script);
            using var game = new SceneGame(scene);
            var canvas = new Log(log);
            for (int i = 0; i < 90; i++)
            {
                game.FixedUpdate(new GameInput(new(i % 30 < 15 ? 1 : -1, 0), false), 1f / 60);
                game.Draw(canvas, .5f);
                log.Append("frame\n");
            }
        });
        return log.ToString();
    }

    private sealed class Log(StringBuilder log) : ICanvas2D
    {
        public void Clear(Color color) => log.Append($"clear {color.ToArgb():X8}\n");
        public void FillRectangle(RectangleF r, Color color) => log.Append($"fill {r} {color.ToArgb():X8}\n");
        public bool DrawSprite(string path, RectangleF r) { log.Append($"sprite {Path.GetFileName(path)} {r}\n"); return true; }
        public void DrawText(string text, float x, float y, float size, Color color) => log.Append($"text {text} {x} {y} {size}\n");
        public IDisposable PushClip(RectangleF area) { log.Append($"clip {area}\n"); return new Pop(log); }
        public void BeginColorAdjust() => log.Append("adjust\n");
        public void EndColorAdjust(Color multiply, byte lift) => log.Append($"adjusted {multiply.ToArgb():X8} {lift}\n");
        public bool DrawAffine(string? path, RectangleF r, System.Numerics.Matrix3x2 m, Color tint, SpriteRegion? source = null, string layout = TileGrid.Orthogonal)
        { log.Append($"affine {Path.GetFileName(path)} {r} {m} {tint.ToArgb():X8} {source} {layout}\n"); return true; }
        public bool DrawFiltered(SpriteFilter filter, RectangleF r, System.Numerics.Matrix3x2 m, Color tint) { log.Append($"filtered {r} {m} {tint.ToArgb():X8}\n"); return true; }
        public void DrawShader(ShaderClip shader, int frame, RectangleF r, System.Numerics.Matrix3x2 m, Color tint, bool additive) =>
            log.Append($"shader {frame} {r} {m} {tint.ToArgb():X8} {additive}\n");
        public void FillStrip(ReadOnlySpan<System.Numerics.Vector2> edges, ReadOnlySpan<Color> colors, System.Numerics.Matrix3x2 m) =>
            log.Append($"strip {string.Join(" ", edges.ToArray())} {m}\n");
        public void BeginLighting(RectangleF area, Color ambient) => log.Append($"lighting {area} {ambient.ToArgb():X8}\n");
        public void DrawLight(ShaderClip sheet, RectangleF r, System.Numerics.Matrix3x2 m, Color tint, ReadOnlySpan<System.Numerics.Vector2> shadows, System.Numerics.Matrix3x2 view) =>
            log.Append($"light {r} {m} {tint.ToArgb():X8} {shadows.Length}\n");
        public bool DrawTile(string path, RectangleF destination, SpriteRegion source, string layout) { log.Append($"tile {Path.GetFileName(path)} {destination} {source} {layout}\n"); return true; }
        private sealed class Pop(StringBuilder log) : IDisposable { public void Dispose() => log.Append("pop\n"); }
    }

    // The generator's registration (EmitCompilerGeneratedFiles in Engine.Core.csproj) and PlayerYamlRegistration.cs must list the
    // same formatters. A line behind features G is registered when any of G is on, so a type registered under G_T that uses a
    // type registered under G_R needs G_R empty, or G_T non-empty and inside G_R.
    private static void Registration(string repository)
    {
        var generated = Directory.EnumerateFiles(Path.Combine(repository, "engine", ".artifacts", "obj", "Engine.Core"), "VYamlGeneratedRegistration.g.cs", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() ?? throw new Exception("FAIL Engine.Core's generated VYaml registration was not emitted");
        static string Key(string line) => Regex.Replace(line, @"global::(VYaml\.Serialization|Engine\.Core|System\.Text\.Json|System)\.", "").Trim();
        var expected = File.ReadLines(generated).Where(l => l.Trim().EndsWith(';') && l.Contains("Register")).Select(Key).ToHashSet();
        var gates = new Dictionary<string, EngineFeature[]>();
        EngineFeature[] current = [];
        foreach (var raw in File.ReadLines(Path.Combine(repository, "engine", "src", "Engine.Core", "PlayerYamlRegistration.cs")))
        {
            var line = raw.Trim();
            if (line.StartsWith("if (EngineFeatures.", StringComparison.Ordinal))
                current = Regex.Matches(line, @"EngineFeatures\.(\w+)").Select(m => Enum.Parse<EngineFeature>(m.Groups[1].Value)).ToArray();
            else if (line == "}") current = [];
            else if (line.EndsWith(';') && line.Contains("Register") && !line.Contains("IsDynamicCodeSupported")) gates.Add(Key(line), current);
        }
        var missing = expected.Except(gates.Keys).ToArray(); var extra = gates.Keys.Except(expected).ToArray();
        Check(missing.Length == 0 && extra.Length == 0, $"PlayerYamlRegistration.cs lists the generated formatters (missing: {string.Join(" ", missing)}; extra: {string.Join(" ", extra)})");

        var core = typeof(SceneComponent).Assembly;
        Type Of(string name) => core.GetType("Engine.Core." + name) ?? Type.GetType("System." + name) ?? Type.GetType("System.Text.Json." + name + ", System.Text.Json") ?? throw new Exception("FAIL unknown type " + name);
        Type Formatted(string key)
        {
            if (Regex.Match(key, @"^(\w+)\.__RegisterVYamlFormatter\(\);$") is { Success: true } own) return Of(own.Groups[1].Value);
            var m = Regex.Match(key, @"new (\w+)<(.+)>\(\)\);$");
            var args = m.Groups[2].Value.Split(", ").Select(a => a switch { "long" => typeof(long), "string" => typeof(string), "bool" => typeof(bool), "float" => typeof(float), "int" => typeof(int), _ => Of(a) }).ToArray();
            return m.Groups[1].Value switch
            {
                "ArrayFormatter" => args[0].MakeArrayType(),
                "ListFormatter" => typeof(List<>).MakeGenericType(args),
                "DictionaryFormatter" => typeof(Dictionary<,>).MakeGenericType(args),
                "NullableFormatter" => typeof(Nullable<>).MakeGenericType(args),
                "EnumAsStringFormatter" => args[0],
                _ => throw new Exception("FAIL unknown formatter " + key)
            };
        }
        var gate = gates.ToDictionary(p => Formatted(p.Key), p => p.Value);
        IEnumerable<Type> Uses(Type t)
        {
            foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                for (var stack = new Stack<Type>([p.PropertyType]); stack.Count > 0;)
                {
                    var u = stack.Pop();
                    yield return u;
                    if (u.IsArray) stack.Push(u.GetElementType()!);
                    if (u.IsGenericType) foreach (var a in u.GetGenericArguments()) stack.Push(a);
                }
            if (t.BaseType is { } b && b != typeof(object)) foreach (var u in Uses(b)) yield return u;
        }
        int checkedLinks = 0;
        foreach (var (t, gt) in gate)
        {
            if (gt.Length > 0 && typeof(SceneComponent).IsAssignableFrom(t))
                Check(BuildFeatures.Of((SceneComponent)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(t)) is { } f && gt.Contains(f),
                    $"{t.Name} is registered only with {string.Join("/", gt)} and the build keeps that feature for it");
            if (t.IsEnum || t.IsArray || t.IsGenericType) continue;
            foreach (var u in Uses(t).Distinct())
                if (gate.TryGetValue(u, out var gu) && gu.Length > 0)
                {
                    checkedLinks++;
                    Check(gt.Length > 0 && gt.All(gu.Contains), $"{t.Name} (registered {(gt.Length == 0 ? "always" : "with " + string.Join("/", gt))}) uses {u.Name}, registered only with {string.Join("/", gu)}");
                }
        }
        Console.WriteLine($"PASS Native AOT formatter registration: {gates.Count} generated formatters, {gates.Count(g => g.Value.Length > 0)} behind feature switches, {checkedLinks} member links to gated formatters stay within their features");
    }

    private static void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL " + label); }
}
