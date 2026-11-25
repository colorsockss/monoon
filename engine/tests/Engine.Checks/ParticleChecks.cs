using System.Drawing;
using System.Numerics;
using Brutal.SdlApi;
using Engine.Core;
using Engine.Desktop;

// Particle runtime (P.1–P.3): module values, timing, determinism, sorting, migration and allocation.
internal static class ParticleChecks
{
    [Check(2900, "particles")] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    private const float Dt = 1f / 60;

    private static ParticleSystem2D System(ParticleEffect fx, ParticleTrigger trigger = ParticleTrigger.Start, uint seed = 7,
        float width = 20, float height = 20) =>
        new(new SceneObject { Name = "Emitter", Width = width, Height = height },
            new ParticleEmitter2DComponent { Trigger = trigger, Seed = seed }, fx);

    // Steps one tick per call at 60 Hz from t = 0 and returns the live particles.
    private sealed class Ticks(ParticleSystem2D system)
    {
        private readonly Particle2D[] buffer = new Particle2D[65536];
        private int tick;
        public Particle2D[] Step(int ticks = 1, Matrix3x2? matrix = null)
        {
            for (int i = 0; i < ticks; i++) { tick++; system.Step(Dt, tick * (double)Dt, matrix ?? Matrix3x2.Identity, false); }
            int n = system.Read(buffer, matrix ?? Matrix3x2.Identity);
            return buffer[..n];
        }
    }

    internal static void Run(string repository, string scratch)
    {
        // Start values stay inside their ranges; a fresh particle sits on its shape at age 0.
        var ranged = new ParticleEffect { Rate = 0, Looping = false, Bursts = [new(0, 2000)], MaxParticles = 2000, Lifetime = new(1, 2),
            Speed = new(10, 20), Size = new(3, 5), Color = new("#102030", "#80A0C0"), Shape = ParticleShape.Circle, Radius = 6 };
        var born = new Ticks(System(ranged)).Step();
        var center = new Vector2(10, 10);
        Check(born.Length == 2000 && born.All(p => p.Age == 0 && p.Size is >= 3 and <= 5 && Vector2.Distance(p.Position, center) <= 6.0001f &&
            p.Color.R is >= 0x10 and <= 0x80 && p.Color.G is >= 0x20 and <= 0xA0 && p.Color.B is >= 0x30 and <= 0xC0) &&
            born.Min(p => p.Size) < 3.1f && born.Max(p => p.Size) > 4.9f,
            "start size and colour stay between their two values and circle particles start inside the radius");
        var run = new Ticks(System(ranged));
        var first = run.Step().ToArray();
        var moved = run.Step();
        var speeds = first.Zip(moved, (p, q) => Vector2.Distance(p.Position, q.Position) / (q.Age - p.Age)).ToArray();
        Check(speeds.Min() > 9.99f && speeds.Max() < 20.01f && speeds.Min() < 10.2f && speeds.Max() > 19.8f,
            $"start speed spans 10–20 px/s ({speeds.Min():F2}–{speeds.Max():F2})");
        int alive = moved.Length;
        var late = run.Step(89);
        var gone = run.Step(31);
        Check(alive == 2000 && late.Length is > 0 and < 2000 && gone.Length == 0, $"lifetimes between 1 and 2 s expire each particle on its own (swap-remove): {alive} {late.Length} {gone.Length}");

        foreach (var (shape, inside) in new (ParticleShape, Func<Vector2, bool>)[]
        {
            (ParticleShape.Point, p => p == new Vector2(10, 10)),
            (ParticleShape.Edge, p => MathF.Abs(p.Y - 10) < 1e-3f && MathF.Abs(p.X - 10) <= 15),
            (ParticleShape.Box, p => MathF.Abs(p.X - 10) <= 15 && MathF.Abs(p.Y - 10) <= 4),
            (ParticleShape.Cone, p => MathF.Abs(p.Y - 10) < 1e-3f && MathF.Abs(p.X - 10) <= 6),
        })
        {
            var fx = ranged with { Shape = shape, Width = 30, Height = 8, Radius = 6, Color = new("#FFFFFF") };
            var start = new Ticks(System(fx)).Step();
            Check(start.All(p => inside(p.Position)) && start.Select(p => p.Position).Distinct().Count() > (shape == ParticleShape.Point ? 0 : 1000),
                $"{shape} shape places particles on its area");
        }

        // Bursts fire at their time inside the cycle; cycles repeat every interval.
        var bursts = new ParticleEffect { Rate = 0, Duration = 2, Looping = false, Bursts = [new(.5f, 7, 3, .25f)], Lifetime = new(5, 5) };
        var timeline = new Ticks(System(bursts));
        var counts = Enumerable.Range(0, 150).Select(_ => timeline.Step().Length).ToArray();
        var steps = Enumerable.Range(1, 149).Where(i => counts[i] != counts[i - 1]).ToArray();
        Check(counts[^1] == 21 && steps.Length == 3 && steps[0] is 29 or 30 && steps[1] - steps[0] is 14 or 15 or 16,
            $"a 3-cycle burst of 7 fires at 0.5, 0.75 and 1.0 s (ticks {string.Join(",", steps)})");
        var looping = new Ticks(System(new ParticleEffect { Rate = 0, Duration = .5f, Bursts = [new(0, 1)], Lifetime = new(60, 60) }));
        Check(looping.Step(290).Length == 10, "a looping 0.5 s cycle repeats its time-0 burst every 30 ticks");
        var rate = new Ticks(System(new ParticleEffect { Rate = 37, Lifetime = new(60, 60), MaxParticles = 1000 }));
        int emitted = rate.Step(600).Length;
        Check(emitted is 369 or 370, $"37 particles per second over 10 s of 60 Hz ticks emit {emitted}");

        // Prewarm equals running the same stream for one duration before the first tick.
        var smoke = new ParticleEffect { Rate = 30, Duration = 1, Prewarm = true, Lifetime = new(.5f, .9f), Speed = new(5, 30), Spread = 90 };
        var warm = System(smoke);
        var cold = System(smoke with { Prewarm = false });
        double now = 100;
        for (int k = 60; k > 0; k--) cold.Step(Dt, now - k * (double)Dt, Matrix3x2.Identity, false);
        warm.Step(Dt, now, Matrix3x2.Identity, false);
        cold.Step(Dt, now, Matrix3x2.Identity, false);
        var a = new Particle2D[256];
        var b = new Particle2D[256];
        int na = warm.Read(a, Matrix3x2.Identity), nb = cold.Read(b, Matrix3x2.Identity);
        Check(na > 10 && na == nb && a.AsSpan(0, na).SequenceEqual(b.AsSpan(0, nb)), "prewarm matches one duration simulated with the same seed");

        // The same seed replays; another seed differs. Local particles follow their emitter, world particles do not.
        var spray = new ParticleEffect { Rate = 120, Lifetime = new(1, 1), Speed = new(20, 40), Spread = 60, Rotation = new(0, 360) };
        Particle2D[] Trace(uint seed, ParticleSpace space, Matrix3x2 matrix) => new Ticks(System(spray with { Space = space }, seed: seed)).Step(45, matrix);
        var moveBy = Matrix3x2.CreateTranslation(100, 0);
        Check(Trace(3, ParticleSpace.World, Matrix3x2.Identity).SequenceEqual(Trace(3, ParticleSpace.World, Matrix3x2.Identity)) &&
            !Trace(3, ParticleSpace.World, Matrix3x2.Identity).SequenceEqual(Trace(4, ParticleSpace.World, Matrix3x2.Identity)),
            "a seed replays the same particles and another seed changes them");
        var localRun = new Ticks(System(spray with { Space = ParticleSpace.Local }));
        var before = localRun.Step(30).Select(p => p.Position).ToArray();
        var after = localRun.Step(0, moveBy).Select(p => p.Position).ToArray();
        var worldRun = new Ticks(System(spray));
        var worldBefore = worldRun.Step(30).Select(p => p.Position).ToArray();
        var worldAfter = worldRun.Step(0, moveBy).Select(p => p.Position).ToArray();
        Check(before.Zip(after).All(p => p.Second == p.First + new Vector2(100, 0)) && worldBefore.SequenceEqual(worldAfter),
            "local-space particles move with the emitter and world-space ones stay where they were emitted");

        var full = System(new ParticleEffect { Rate = 0, Bursts = [new(0, 50)], MaxParticles = 20 });
        full.Step(Dt, Dt, Matrix3x2.Identity, false);
        Check(full.Count == 20 && full.Dropped == 30, "a full pool keeps its particles and counts the dropped ones");
        var manual = System(new ParticleEffect { Rate = 0, Looping = false, Bursts = [new(0, 5)] }, ParticleTrigger.Manual);
        manual.Step(Dt, Dt, Matrix3x2.Identity, false);
        int idle = manual.Count;
        manual.Play();
        manual.Step(Dt, 2 * Dt, Matrix3x2.Identity, false);
        Check(idle == 0 && manual.Count == 5, "a manual emitter waits for Play and then fires its time-0 burst");

        CheckCurves();
        CheckForces();
        CheckSubEmitters();
        var lab = Path.Combine(repository, "samples", "ParticleLab", "Assets");
        var effects = Directory.GetFiles(lab, "*" + ParticleEffect.Extension).Order(StringComparer.Ordinal).Select(ParticleEffect.Load).ToArray();
        Check(effects.Length >= 16 && effects.Any(e => e.SizeOverLifetime?.Max != null) && effects.Any(e => e.ColorOverLifetime != null) &&
            effects.Any(e => e.RotationSpeed != null), $"all {effects.Length} ParticleLab effects load and validate");
        CheckSorting();
        CheckMigration(scratch);
        CheckAllocation();
        CheckEditorPreview(repository);
        CheckPresets(repository, scratch);
        CheckReload(repository, scratch);
    }

    // 14.8: the six built-in presets are the ParticleLab Presets files byte for byte, validate, need no texture, and a second
    // copy in one folder gets a new name.
    private static void CheckPresets(string repository, string scratch)
    {
        var folder = Path.Combine(repository, "samples", "ParticleLab", "Assets", "Presets");
        var presets = Engine.Editor.ParticlePresets.All;
        foreach (var preset in presets)
        {
            var file = Path.Combine(folder, preset.Name + ParticleEffect.Extension);
            var fx = ParticleEffect.Load(file);
            Check(File.ReadAllText(file) == preset.Yaml && fx.Texture == null && fx.Looping == preset.Name is not ("Dust" or "Hit"),
                $"preset {preset.Name} equals ParticleLab Assets/Presets/{preset.Name}.particles, validates, uses the built-in texture");
        }
        Check(presets.Select(p => p.Name).SequenceEqual(["Dust", "Embers", "Smoke", "Sparks", "Hit", "Rain"]), "six presets: dust, embers, smoke, sparks, hit, rain");
        var root = Directory.CreateDirectory(Path.Combine(scratch, "particle-presets")).FullName;
        string first = Engine.Editor.ParticlePresets.Write(root, "Assets", presets[0]), second = Engine.Editor.ParticlePresets.Write(root, "Assets", presets[0]);
        Check(first == "Assets/Dust.particles" && second == "Assets/Dust2.particles" &&
            File.ReadAllText(Path.Combine(root, second)) == presets[0].Yaml, $"a second preset copy in the same folder is named {second}");
    }

    // 14.8: a saved asset reloads into every running emitter that uses it, both prefabs (Torch, Brazier) included. Same pool
    // layout keeps the live particles exactly; a new Max particles restarts them; a file that fails validation changes nothing.
    // The same edits at the same ticks replay identically.
    private static void CheckReload(string repository, string scratch)
    {
        string Run(string name)
        {
            var root = Path.Combine(scratch, "particle-reload", name);
            var lab = Path.Combine(repository, "samples", "ParticleLab");
            foreach (var file in Directory.EnumerateFiles(lab, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(lab, file);
                if (relative.Split(Path.DirectorySeparatorChar)[0] is "bin" or "obj" or ".monoon" or ".monoon-compiled") continue;
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(root, relative))!);
                File.Copy(file, Path.Combine(root, relative), true);
            }
            const string embers = "Assets/Presets/Embers.particles";
            var asset = ProjectAssets.Scan(root).IdFor(embers)!.Value;
            using var session = new GameSession(SceneDocument.Load(Path.Combine(root, "Scenes", "ShowcaseWorkshop.scene")));
            var world = session.Current;
            var users = new[] { "Torch", "Brazier" }.Select(n => world.Objects.Single(o => o.Name == n).Id).ToArray();
            var buffer = new Particle2D[256];
            Particle2D[] Read(Guid id) => buffer[..world.ReadParticles(id, buffer)];
            void Tick(int n) { for (int i = 0; i < n; i++) session.FixedUpdate(Vector2.Zero, Dt); }
            Tick(90);
            var before = users.Select(Read).ToArray();
            var path = Path.Combine(root, embers);
            var original = File.ReadAllText(path);
            File.WriteAllText(path, original.Replace("color: {min: \"#FFB347\", max: \"#FF5A1F\"}", "color: {min: \"#00FF00\"}"));
            var kept = world.ReloadParticles(asset);
            var same = users.Select(Read).ToArray();
            Tick(120);
            var green = users.Select(Read).ToArray();
            Check(kept == (2, 0) && before.All(p => p.Length > 10) && before.Zip(same).All(p => p.First.SequenceEqual(p.Second)) &&
                green.All(p => p.Length > 10 && p.All(q => q.Color.R == 0 && q.Color.G == 255 && q.Color.B == 0)),
                $"a colour change reloads into both Embers prefabs ({kept}), keeps their {before[0].Length}+{before[1].Length} live particles exactly, and new ones are green");
            File.WriteAllText(path, File.ReadAllText(path).Replace("maxParticles: 96", "maxParticles: 0"));
            bool refused = false;
            try { world.ReloadParticles(asset); } catch (InvalidDataException) { refused = true; }
            Tick(1);
            Check(refused && users.All(id => Read(id).Length > 10 && Read(id).All(q => q.Color.G == 255)),
                "an asset that fails validation throws and the running emitters keep the previous effect");
            File.WriteAllText(path, File.ReadAllText(path).Replace("maxParticles: 0", "maxParticles: 80"));
            var restarted = world.ReloadParticles(asset);
            int empty = users.Sum(id => Read(id).Length);
            Tick(30);
            Check(restarted == (0, 2) && empty == 0 && users.All(id => Read(id).Length > 0),
                $"a new Max particles restarts both emitters ({restarted}): empty, then emitting again from the seed's start");
            Check(world.ReloadParticles(ProjectAssets.Scan(root).IdFor("Assets/Presets/Rain.particles")!.Value) == (1, 0) &&
                world.ReloadParticles(Guid.NewGuid()) == (0, 0), "an asset reloads only the emitters that use it");
            Tick(60);
            var hash = 14695981039346656037UL;
            foreach (var o in world.Objects.Where(o => o.Tag == "Particles"))
                foreach (var q in Read(o.Id))
                    hash = (hash ^ (uint)HashCode.Combine(q.Position.X, q.Position.Y, q.Size, q.Age, q.Color.ToArgb())) * 1099511628211UL;
            return hash.ToString("x16");
        }
        string a = Run("a"), b = Run("b");
        Check(a == b, $"the same reloads at the same ticks replay the same particles ({a})");
    }

    // P.5: the editor preview steps the player's ParticleSystem2D; scrubbing to tick t equals playing t ticks, and on the Mac
    // where the ParticleLab trace was recorded every tick of the workshop's three still Curves emitters matches Scene Play, Release
    // and Native AOT (the Curves trace of ParticleLab.Trace).
    private static void CheckEditorPreview(string repository)
    {
        var root = Path.Combine(repository, "samples", "ParticleLab");
        var scene = SceneDocument.Load(Path.Combine(root, "Scenes", "ShowcaseWorkshop.scene"));
        var assets = ProjectAssets.Scan(root);
        var tagged = scene.Objects.Where(o => o.Tag == "Particles" && o.Groups?.Contains("Curves") == true).ToList();
        var preview = new Engine.Editor.ParticlePreview();
        preview.Build(tagged, e => e.Particles is Guid id ? assets.LoadParticles(root, id) : new());
        var played = new List<(int Count, ulong Hash)>();
        for (int t = 0; t < 300; t++) { played.Add((preview.Count, preview.Hash())); preview.Advance(); }
        bool scrub = new[] { 0, 1, 59, 150, 299 }.All(t => { preview.Seek(t); return (preview.Count, preview.Hash()) == played[t]; });
        Check(scrub && played[299].Count > 0, "editor preview: scrubbing to tick t simulates the same particles as playing t ticks");
        if (!OperatingSystem.IsMacOS() || global::System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture != global::System.Runtime.InteropServices.Architecture.Arm64) return;
        var trace = File.ReadAllLines(Path.Combine(repository, "engine", "tests", "results", "particles", "osx-arm64", "curves-trace.tsv"));
        int same = 0;
        for (int t = 0; t < trace.Length && t < played.Count; t++)
        {
            var f = trace[t].Split(' ');
            if (int.Parse(f[2]) == played[t].Count && f[3] == played[t].Hash.ToString("x16")) same++;
        }
        Check(tagged.Count == 3 && trace.Length == 300 && same == 300, $"editor preview matches the Curves Scene Play/Release/AOT trace on {same} of {trace.Length} ticks");
    }

    // P.4: lifetime curves and gradients bake to 64-sample tables; old End size/alpha assets keep their exact output.
    private static void CheckCurves()
    {
        ParticleKey[] steps = [new(0, 2), new(.5f, 4), new(.5f, 8), new(1, 6)];
        Check(ParticleLut.Evaluate(steps, false, 0) == 2 && ParticleLut.Evaluate(steps, false, 1) == 6 &&
            ParticleLut.Evaluate(steps, false, .25f) == 3 && ParticleLut.Evaluate(steps, false, .5f) == 8 &&
            ParticleLut.Evaluate(steps, false, .4999f) < 4 && ParticleLut.Evaluate([new(.3f, 5)], true, .9f) == 5 &&
            ParticleLut.Evaluate([new(.2f, 1), new(.8f, 3)], false, 0) == 1 && ParticleLut.Evaluate([new(.2f, 1), new(.8f, 3)], false, 1) == 3,
            "curve ends hold their key values, a zero-length segment is a step without NaN, one key is a constant");
        var lut = ParticleLut.Bake(steps, true);
        float eased = ParticleLut.Evaluate([new(0, 0), new(1, 1)], true, .25f);
        Check(lut[0] == 2 && lut[^1] == 6 && lut.All(float.IsFinite) && MathF.Abs(eased - .15625f) < 1e-6f &&
            MathF.Abs(ParticleLut.Evaluate([new(0, 0, 0, 1), new(1, 1, 1, 0)], true, .25f) - .25f) < 1e-6f,
            "smooth keys with zero slopes ease in and out; slopes of 1 give the straight line");

        const string basis = "rate: 10\n";
        Check(ProjectYaml.Decode<ParticleEffect>(global::System.Text.Encoding.UTF8.GetBytes(basis)) is var plain && plain.SizeOverLifetime == null &&
            plain.ColorOverLifetime == null && plain.RotationSpeed == null, "effects without curves keep End size/alpha and fixed rotation");
        var yaml = global::System.Text.Encoding.UTF8.GetBytes(basis + """
            rate: 0
            sizeOverLifetime: {smooth: true, keys: [{time: 0, value: 0.2, out: 3}, {time: 0.3, value: 1}, {time: 1, value: 0}], max: [{time: 0, value: 1}]}
            colorOverLifetime: {colors: [{time: 0, color: "#FFE08A"}, {time: 1, color: "#802010"}], alphas: [{time: 0, alpha: 0}, {time: 0.1, alpha: 1}, {time: 1, alpha: 0}]}
            rotationSpeed: {keys: [{time: 0, value: 360}, {time: 1, value: -90}]}
            """);
        var parsed = ProjectYaml.Decode<ParticleEffect>(yaml);
        parsed.Validate("curves");
        var once = ProjectYaml.Encode(parsed);
        var twice = ProjectYaml.Encode(ProjectYaml.Decode<ParticleEffect>(once));
        Check(parsed.SizeOverLifetime is { Smooth: true, Keys.Length: 3, Max.Length: 1 } c && c.Keys[0].Out == 3 && c.Keys[1].In == 0 &&
            parsed.ColorOverLifetime!.Alphas!.Length == 3 && parsed.RotationSpeed!.Smooth == false && once.AsSpan().SequenceEqual(twice),
            "curves and gradients read from YAML with default slopes and save again byte for byte");
        foreach (var bad in new[] { "sizeOverLifetime: {keys: []}", "sizeOverLifetime: {keys: [{time: 0.5, value: 1}, {time: 0.2, value: 1}]}",
                     "sizeOverLifetime: {keys: [{time: 0, value: -1}]}", "colorOverLifetime: {colors: [{time: 0, color: red}]}",
                     "colorOverLifetime: {alphas: [{time: 1.5, alpha: 1}]}" })
        {
            bool rejected = false;
            try { ProjectYaml.Decode<ParticleEffect>(global::System.Text.Encoding.UTF8.GetBytes(basis + bad)).Validate("bad"); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, $"rejects {bad}");
        }

        // A size curve from 1 to End size matches the linear End size closely; the old field keeps its exact values.
        var linear = new ParticleEffect { Rate = 60, Lifetime = new(1, 1.5f), EndSize = .3f, EndAlpha = .2f, Size = new(4, 8) };
        var curved = linear with { SizeOverLifetime = new([new(0, 1), new(1, .3f)]), ColorOverLifetime = new(Alphas: [new(0, 1), new(1, .2f)]) };
        var a = new Ticks(System(linear)).Step(70);
        var b = new Ticks(System(curved)).Step(70);
        Check(a.Length == b.Length && a.Zip(b).All(p => MathF.Abs(p.First.Size - p.Second.Size) < 1e-4f && Math.Abs(p.First.Color.A - p.Second.Color.A) <= 1 &&
                p.First.Position == p.Second.Position) && a.Any(p => p.Age > .5f),
            "a size curve and alpha gradient equal to End size/alpha give the same particles");

        var random = new Ticks(System(new ParticleEffect { Rate = 200, Lifetime = new(2, 2), MaxParticles = 512, Size = new(10, 10),
            SizeOverLifetime = new([new(0, 1), new(1, 1)], [new(0, 3), new(1, 3)]) })).Step(30);
        Check(random.All(p => p.Size is >= 10 and <= 30) && random.Min(p => p.Size) < 12 && random.Max(p => p.Size) > 28,
            "random between two curves gives each particle its own size between them");

        var gradient = new Ticks(System(new ParticleEffect { Rate = 0, Bursts = [new(0, 4)], Lifetime = new(1, 1), Color = new("#FF8040"),
            ColorOverLifetime = new([new(0, "#FFFFFF"), new(1, "#000000")], [new(0, 0), new(.5f, 1), new(1, 1)]) }));
        var early = gradient.Step()[0];
        var middle = gradient.Step(29)[0];
        Check(early.Color.A == 0 && early.Color.R == 255 && middle.Color.A is >= 240 && middle.Color.R is >= 125 and <= 135 && middle.Color.B is >= 30 and <= 34,
            $"a gradient multiplies the start colour and alpha over lifetime ({early.Color} {middle.Color})");

        // Rotation speed turns the drawn quad: 90°/s for one second from a start rotation of 0.
        var spin = System(new ParticleEffect { Rate = 0, Looping = false, Bursts = [new(0, 1)], Lifetime = new(2, 2), Size = new(10, 10), RotationSpeed = new([new(0, 90)]) });
        for (int i = 1; i <= 61; i++) spin.Step(Dt, i * (double)Dt, Matrix3x2.Identity, false);
        var quads = new Quads();
        spin.Draw(quads, new ParticleTexture(null, ShaderClip.Puff, [ShaderClip.Puff.Region(0)]), new(-1000, -1000, 2000, 2000),
            Matrix3x2.Identity, Matrix3x2.Identity, 61 * (double)Dt);
        var right = quads.Last.Right;
        Check(MathF.Abs(right.X) < .01f && MathF.Abs(right.Y - 5) < .05f, $"90°/s rotation speed turns a particle a quarter turn in its first second ({right})");
    }

    // P.8: velocity curves, force, drag and noise integrate per tick; tile collision bounces off solids, slopes and one-way faces.
    private static void CheckForces()
    {
        Particle2D One(ParticleEffect fx, int ticks, ParticleWorld? world = null, float x = 10, float y = 10)
        {
            var system = new ParticleSystem2D(new SceneObject { Name = "Emitter", Width = 2 * x, Height = 2 * y },
                new ParticleEmitter2DComponent { Trigger = ParticleTrigger.Start, Seed = 7 }, fx with { Rate = 0, Looping = false, Bursts = [new(0, 1)], Lifetime = new(10, 10) })
                { World = world };
            var run = new Ticks(system);
            return run.Step(ticks)[0];
        }
        var still = new ParticleEffect { Speed = new(0, 0) };
        var wind = One(still with { VelocityX = new([new(0, 50)]) }, 61);
        var pushed = One(still with { ForceX = 30 }, 61);
        Check(MathF.Abs(wind.Position.X - 60) < .01f && wind.Position.Y == 10 && MathF.Abs(pushed.Position.X - 25) < 1e-3f,
            $"a 50 px/s velocity curve moves 50 px in a second and a 30 px/s² force follows ½at² ({wind.Position.X}, {pushed.Position.X})");
        var free = One(new ParticleEffect { Speed = new(100, 100), Direction = 0, Drag = .001f }, 61);
        var dragged = One(new ParticleEffect { Speed = new(100, 100), Direction = 0, Drag = 2 }, 61);
        Check(free.Position.X > 109.9f && dragged.Position.X - 10 is > 40 and < 45, $"linear drag slows particles ({free.Position.X - 10:F1} vs {dragged.Position.X - 10:F1} px)");

        var corner = ParticleNoise2D.Sample(3, -2, 11);
        var between = ParticleNoise2D.Sample(3.5f, -1.25f, 11);
        Check(corner == ParticleNoise2D.Sample(3, -2, 11) && corner != ParticleNoise2D.Sample(3, -2, 12) && MathF.Abs(corner.X) <= 1 && MathF.Abs(corner.Y) <= 1 &&
            MathF.Abs(between.X) <= 1 && corner != between, "value noise is a seeded lattice hash in −1…1");
        var gusty = new ParticleEffect { Rate = 300, Lifetime = new(1, 1), Speed = new(0, 0), MaxParticles = 512, Shape = ParticleShape.Box, Width = 200, Height = 200, Noise = new(400, .05f, .5f) };
        var gustA = new Ticks(System(gusty)).Step(40);
        var gustB = new Ticks(System(gusty)).Step(40);
        Check(gustA.SequenceEqual(gustB) && gustA.Select(p => p.Position).Distinct().Count() > gustA.Length / 2 &&
            !gustA.SequenceEqual(new Ticks(System(gusty, seed: 8)).Step(40)), "noise moves particles apart, replays with the seed and changes with another seed");

        // A 16 px grid: solid floor on row 10, a slope at column 6 and a one-way ledge at column 2 one row up.
        var region = new SpriteRegion(0, 0, 16, 16);
        var palette = new TilePalette("tiles.png", new Dictionary<int, TileDefinition>
        {
            [1] = new(1, region, true),
            [2] = new(2, region, true) { Collision = new([new(0, 1), new(1, 0), new(1, 1)]) },
            [3] = new(3, region, false) { Collision = new([new(0, 0), new(1, 0)], OneWay: true) },
        });
        var map = new Tilemap2DComponent { CellSize = new(16, 16) };
        for (int column = 0; column < 20; column++) { map.Set(new(column, 10), column == 6 ? 2 : 1); map.Set(new(column, 11), 1); }
        map.Set(new(2, 9), 3);
        var tiles = new ParticleTiles(map, palette) { Matrix = Matrix3x2.Identity };
        var world = new ParticleWorld([tiles], null);
        var drop = new ParticleEffect { Speed = new(0, 0), Gravity = 600, Collision = new(Bounce: .6f) };
        var fall = new Ticks(new ParticleSystem2D(new SceneObject { Name = "Ball", Width = 2 * 80, Height = 2 * 100 },
            new ParticleEmitter2DComponent { Trigger = ParticleTrigger.Start, Seed = 7 }, drop with { Rate = 0, Looping = false, Bursts = [new(0, 1)], Lifetime = new(10, 10) }) { World = world });
        var path = Enumerable.Range(0, 90).Select(_ => fall.Step()[0].Position.Y).ToArray();
        float lowest = path.Max(), after = path[Enumerable.Range(1, path.Length - 1).First(i => path[i] < path[i - 1])..].Min();
        Check(lowest <= 160 && lowest > 150 && after < lowest - 10, $"a falling particle stops at the floor top and bounces up ({lowest:F2}, then {after:F2})");
        var slide = One(drop, 60, world, 104, 100);
        Check(slide.Position.X < 100 && slide.Position.Y <= 176, $"the slope tile turns a falling particle down its face ({slide.Position})");
        var ledge = One(drop with { Collision = new(Bounce: 0) }, 60, world, 40, 100);
        var jump = One(new ParticleEffect { Speed = new(300, 300), Direction = -90, Collision = new() }, 30, world, 40, 156);
        Check(MathF.Abs(ledge.Position.Y - 144) < 3 && jump.Position.Y < 100, $"a one-way ledge stops falling particles and lets rising ones through ({ledge.Position.Y:F1}, {jump.Position.Y:F1})");
        var kill = new ParticleEffect { Rate = 0, Looping = false, Bursts = [new(0, 20)], Lifetime = new(10, 10), Speed = new(0, 0), Gravity = 600,
            Shape = ParticleShape.Edge, Width = 100, Direction = 90, Collision = new(LifetimeLoss: 1) };
        var doomed = new ParticleSystem2D(new SceneObject { Name = "Rain", Width = 200, Height = 200 },
            new ParticleEmitter2DComponent { Trigger = ParticleTrigger.Start, Seed = 7 }, kill) { World = world };
        var rain = new Ticks(doomed);
        int before = rain.Step(20).Length;
        int gone = rain.Step(60).Length;
        Check(before == 20 && gone == 0 && doomed.Collisions == 20, $"lifetime loss 1 kills each particle on its first hit ({before} → {gone}, {doomed.Collisions} hits)");

        var storm = new ParticleSystem2D(new SceneObject { Name = "Storm", Width = 320, Height = 100 }, new ParticleEmitter2DComponent { Trigger = ParticleTrigger.Start, Seed = 3 },
            new ParticleEffect { Rate = 4000, Lifetime = new(1, 1.2f), MaxParticles = 4096, Shape = ParticleShape.Edge, Width = 300, Direction = 90, Speed = new(80, 120),
                Gravity = 300, Drag = .5f, Noise = new(200), VelocityX = new([new(0, -20), new(1, 40)], [new(0, 0)]), Collision = new(Bounce: .3f, LifetimeLoss: .5f) }) { World = world };
        var stormTicks = new Ticks(storm);
        stormTicks.Step(200);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 120; i++) storm.Step(Dt, (201 + i) * (double)Dt, Matrix3x2.Identity, false);
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Check(storm.Count > 2000 && storm.Collisions > 1000 && allocated == 0,
            $"{storm.Count:N0} particles with noise, drag, a velocity curve and tile collision step with {allocated} B allocated ({storm.Collisions:N0} hits)");

        // Colliders mode raycasts at most Max queries particles per tick (the scene's Box2D queries in play; a plane here).
        var plane = new Plane(200);
        var cast = new ParticleSystem2D(new SceneObject { Name = "Sparks", Width = 200, Height = 200 }, new ParticleEmitter2DComponent { Trigger = ParticleTrigger.Start, Seed = 7 },
            kill with { Collision = new(ParticleCollisionMode.Colliders, Bounce: .5f, MaxQueries: 4) }) { World = new([], plane) };
        var sparks = new Ticks(cast);
        int most = 0;
        for (int i = 0; i < 120; i++) { plane.Calls = 0; sparks.Step(); most = Math.Max(most, plane.Calls); }
        Check(most == 4 && cast.Collisions > 0,
            $"Colliders mode casts at most 4 rays per tick ({most}) and bounces particles off what they hit ({cast.Collisions} hits)");
    }

    // P.9: Birth/Death/Collision sub-emitters emit exactly Count per event, drop what a full target cannot take, stay at depth 1.
    private static void CheckSubEmitters()
    {
        var burstId = Guid.NewGuid();
        var rocket = new ParticleEffect { Rate = 0, Duration = .5f, Bursts = [new(0, 3)], Lifetime = new(.7f, .9f), Speed = new(200, 250), Gravity = 200,
            Color = new("#FF8040"), SubEmitters = [new(ParticleEvent.Death, burstId, 40, .5f, true)] };
        var spark = new ParticleEffect { Rate = 0, Bursts = [], Lifetime = new(.4f, .6f), Speed = new(50, 90), Spread = 360, MaxParticles = 4096, Color = new("#FFFFFF"),
            SubEmitters = [new(ParticleEvent.Birth, burstId, 1)] };
        var parent = System(rocket);
        var target = System(spark, ParticleTrigger.Manual, seed: 9);
        parent.BindSubEmitters([(rocket.SubEmitters[0], target)]);
        target.BindSubEmitters([(spark.SubEmitters[0], target)]);
        long died = 0, sparks = 0, rockets = 0;
        var buffer = new Particle2D[4096];
        bool tinted = false;
        for (int tick = 1; tick <= 240; tick++)
        {
            parent.BeginTick(); target.BeginTick();
            parent.Step(Dt, tick * (double)Dt, Matrix3x2.Identity, false);
            target.Step(Dt, tick * (double)Dt, Matrix3x2.Identity, false);
            died += parent.Events.Died; rockets += parent.Events.Born; sparks += target.Events.Born;
            int n = target.Read(buffer, Matrix3x2.Identity);
            tinted |= n > 0 && buffer[..n].Any(p => p.Age == 0 && p.Color.R == 255 && p.Color.G == 128 && p.Color.B == 64);
        }
        Check(rockets is 24 or 27 && died > 15 && sparks == 40 * died && target.Dropped == 0 && tinted,
            $"each of {died} rocket deaths emits exactly 40 sparks in the rocket colour ({sparks}, {rockets} rockets, tinted {tinted}); sparks never fire their own sub-emitter");

        var small = System(spark with { MaxParticles = 50, SubEmitters = null }, ParticleTrigger.Manual, seed: 9);
        var flood = System(rocket with { Bursts = [new(0, 4)], Lifetime = new(.1f, .1f), Looping = false });
        flood.BindSubEmitters([(rocket.SubEmitters[0], small)]);
        for (int tick = 1; tick <= 20; tick++) { flood.BeginTick(); small.BeginTick(); flood.Step(Dt, tick * (double)Dt, Matrix3x2.Identity, false); small.Step(Dt, tick * (double)Dt, Matrix3x2.Identity, false); }
        Check(small.Count == 50 && small.Dropped == 4 * 40 - 50, $"a full target keeps 50 and counts {small.Dropped} dropped sub-emitted particles");

        // Collision and birth events, and no allocation with sub-emitters running.
        var region = new SpriteRegion(0, 0, 16, 16);
        var map = new Tilemap2DComponent { CellSize = new(16, 16) };
        for (int column = -4; column < 30; column++) map.Set(new(column, 10), 1);
        var world = new ParticleWorld([new ParticleTiles(map, new("t.png", new Dictionary<int, TileDefinition> { [1] = new(1, region, true) })) { Matrix = Matrix3x2.Identity }], null);
        var splashId = Guid.NewGuid();
        var drops = new ParticleSystem2D(new SceneObject { Name = "Rain", Width = 400, Height = 100 }, new ParticleEmitter2DComponent { Trigger = ParticleTrigger.Start, Seed = 5 },
            new ParticleEffect { Rate = 600, MaxParticles = 1024, Lifetime = new(1.5f, 1.5f), Shape = ParticleShape.Edge, Width = 380, Direction = 90, Speed = new(200, 260), Gravity = 300,
                Collision = new(Bounce: .2f, LifetimeLoss: .5f), SubEmitters = [new(ParticleEvent.Collision, splashId, 2, .3f), new(ParticleEvent.Birth, splashId, 1)] }) { World = world };
        var splash = System(new ParticleEffect { Rate = 0, MaxParticles = 8192, Lifetime = new(.3f, .3f), Speed = new(20, 40), Spread = 180 }, ParticleTrigger.Manual);
        drops.BindSubEmitters([(drops.Effect.SubEmitters![0], splash), (drops.Effect.SubEmitters[1], splash)]);
        long hits = 0, splashes = 0, births = 0, allocated = 0;
        for (int tick = 1; tick <= 300; tick++)
        {
            if (tick == 201) allocated = GC.GetAllocatedBytesForCurrentThread();
            drops.BeginTick(); splash.BeginTick();
            drops.Step(Dt, tick * (double)Dt, Matrix3x2.Identity, false);
            splash.Step(Dt, tick * (double)Dt, Matrix3x2.Identity, false);
            hits += drops.Events.Collided; births += drops.Events.Born; splashes += splash.Events.Born;
        }
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Check(hits > 1000 && splashes == 2 * hits + births && splash.Dropped == 0 && allocated == 0,
            $"collisions ({hits}) and births ({births}) emit 2 and 1 splashes each ({splashes}) with {allocated} B allocated over 100 ticks");
    }

    private sealed class Plane(float top) : IPhysics2D
    {
        internal int Calls;
        public PhysicsQueryDiagnostics QueryDiagnostics { get; } = new();
        public IReadOnlyList<PhysicsTarget> Overlap(Shape2D shape, PhysicsFilter? filter = null) => [];
        public PhysicsHit? Sweep(Shape2D shape, Vector2 displacement, PhysicsFilter? filter = null) => null;
        public PhysicsHit? Raycast(Vector2 origin, Vector2 direction, float distance, PhysicsFilter? filter = null)
        {
            Calls++;
            float end = origin.Y + direction.Y / direction.Length() * distance;
            if (origin.Y > top || end < top) return null;
            float f = (top - origin.Y) / (end - origin.Y);
            return new(new(Guid.Empty, 0, false, null), f, f * distance, new(origin.X, top), new(0, -1));
        }
    }

    private sealed class Quads : ICanvas2D
    {
        internal ParticleQuad Last;
        public void Clear(Color c) { }
        public void FillRectangle(RectangleF r, Color c) { }
        public bool DrawSprite(string p, RectangleF r) => true;
        public void DrawParticles(ParticleTexture texture, ReadOnlySpan<ParticleQuad> quads, Matrix3x2 view, ParticleBlend blend) => Last = quads[^1];
    }

    // P.2: an emitter takes its own sorting slot between other renderers.
    private static void CheckSorting()
    {
        var scene = SceneDocument.Empty(new());
        void Add(string name, int order, SceneComponent component)
        {
            var obj = new SceneObject { Name = name, X = 100, Y = 100, Width = 40, Height = 40 };
            obj.AddComponent(component);
            scene.Objects.Add(obj);
        }
        Add("Front", 2, new SpriteRendererComponent { SquareColor = "#0000FF", Order = 2 });
        Add("Sparks", 1, new ParticleEmitter2DComponent { Trigger = ParticleTrigger.Start, Order = 1 });
        Add("Back", 0, new SpriteRendererComponent { SquareColor = "#FF0000", Order = 0 });
        using var game = new GameSession(scene);
        var canvas = new Order();
        for (int i = 0; i < 30; i++) game.FixedUpdate(Vector2.Zero, Dt);
        game.Draw(canvas, 1);
        Check(string.Join(",", canvas.Calls) == "back,particles,front", $"particles draw inside the sorting order ({string.Join(",", canvas.Calls)})");
    }

    private sealed class Order : ICanvas2D
    {
        internal readonly List<string> Calls = [];
        public void Clear(Color c) => Calls.Clear();
        public void FillRectangle(RectangleF r, Color c) { }
        public bool DrawSprite(string p, RectangleF r) => true;
        public void DrawSquareSprite(RectangleF r, Color c) => Calls.Add(c.B == 255 ? "front" : "back");
        public bool DrawAffine(string? p, RectangleF r, Matrix3x2 m, Color tint, SpriteRegion? source = null, string layout = TileGrid.Orthogonal)
        {
            if (tint.B == 255 && tint.R == 0) Calls.Add("front");
            else if (tint.R == 255 && tint.B == 0) Calls.Add("back");
            return true;
        }
        public void DrawParticles(ParticleTexture texture, ReadOnlySpan<ParticleQuad> quads, Matrix3x2 view, ParticleBlend blend) => Calls.Add("particles");
    }

    // Schema 31: `monoon migrate` turns inline settings into a .particles asset with the same look.
    private static void CheckMigration(string scratch)
    {
        var root = Directory.CreateDirectory(Path.Combine(scratch, "particle-migration")).FullName;
        Directory.CreateDirectory(Path.Combine(root, "Scenes"));
        File.WriteAllText(Path.Combine(root, "Scenes", "Old.scene"), """
            schemaVersion: 21
            objects:
            - id: 0d1f1e44-8d5a-4f39-8f69-2f3b8e7a1c01
              name: Hero
              x: 10
              y: 20
              width: 22
              height: 30
              components:
              - type: particleEmitter2D
                trigger: Landing
                count: 22
                lifetime: 0.62
                speed: 104
                gravity: 174
                size: 5
                color: "#E6C79B"
            - id: 0d1f1e44-8d5a-4f39-8f69-2f3b8e7a1c02
              name: Twin
              x: 50
              y: 20
              width: 22
              height: 30
              components:
              - type: particleEmitter2D
                trigger: Landing
                count: 22
                lifetime: 0.62
                speed: 104
                gravity: 174
                size: 5
                color: "#E6C79B"
            """);
        var legacy = SceneDocument.ReadAsset(Path.Combine(root, "Scenes", "Old.scene"));
        var expected = legacy.Objects[0].GetComponent<ParticleEmitter2DComponent>()!.LegacyEffect();
        Check(legacy.SchemaVersion == 19 && expected.Bursts![0].Count == 22 && expected.Gravity == 174,
            "an unmigrated emitter still plays its inline settings (schema 19)");
        LegacyProject.Upgrade(root);
        var assets = ProjectAssets.Scan(root);
        var scene = SceneDocument.Load(Path.Combine(root, "Scenes", "Old.scene"));
        var emitters = scene.Objects.Select(o => o.GetComponent<ParticleEmitter2DComponent>()!).ToArray();
        var effect = assets.LoadParticles(root, emitters[0].Particles!.Value);
        Check(scene.SchemaVersion == 31 && emitters.All(e => !e.Legacy && e.Particles == emitters[0].Particles && e.Trigger == ParticleTrigger.Landing &&
                e.OffsetY == 13) && assets.Entries.Count(e => e.Path.EndsWith(ParticleEffect.Extension)) == 1 &&
            ProjectYaml.Encode(effect).AsSpan().SequenceEqual(ProjectYaml.Encode(expected)),
            "migrate writes one shared .particles asset per distinct setting and schema 31 emitters that reference it");
        var bytes = File.ReadAllBytes(Path.Combine(root, "Scenes", "Old.scene"));
        LegacyProject.Upgrade(root);
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(root, "Scenes", "Old.scene"))), "a second migrate changes nothing");
    }

    // 10,240 live particles stepped and drawn through the player's SDL canvas allocate nothing after warm-up.
    private static unsafe void CheckAllocation()
    {
        var target = Sdl.PInvoke.CreateSurface(64, 64, SdlPixelFormat.Rgba32);
        if (target == null) throw new InvalidOperationException(Sdl.PInvoke.GetError());
        try
        {
            using var renderer = Sdl.Render.CreateSoftwareRenderer(target);
            using var canvas = new SdlCanvas(renderer, batchSprites: true, previewShader: ShaderPixels.Preview);
            var fx = new ParticleEffect { Rate = 5120, Lifetime = new(2, 2), MaxParticles = 10240, Speed = new(0, 1), Size = new(1, 2),
                Spread = 360, Rotation = new(0, 360), Blend = ParticleBlend.Additive };
            var shade = System(fx with { Blend = ParticleBlend.Multiply, Rate = 60, MaxParticles = 256,
                SizeOverLifetime = new([new(0, 1), new(1, 0)], [new(0, 2)], true), RotationSpeed = new([new(0, 90), new(1, -90)]),
                ColorOverLifetime = new([new(0, "#FFFFFF"), new(1, "#203040")], [new(0, 0), new(1, 1)]) });
            var system = System(fx);
            var frame = new RectangleF(0, 0, 64, 64);
            var puff = new ParticleTexture(null, ShaderClip.Puff, [ShaderClip.Puff.Region(0)]);
            double now = 0;
            void Frame()
            {
                now += Dt;
                system.Step(Dt, now, Matrix3x2.Identity, false);
                shade.Step(Dt, now, Matrix3x2.Identity, false);
                system.Draw(canvas, puff, frame, Matrix3x2.Identity, Matrix3x2.Identity, now);
                shade.Draw(canvas, puff, frame, Matrix3x2.Identity, Matrix3x2.Identity, now);
                canvas.FlushSprites();
            }
            for (int i = 0; i < 200; i++) Frame();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 120; i++) Frame();
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(system.Count >= 10200 && bytes == 0, $"{system.Count:N0} additive and 120 multiply particles (size curve, gradient, rotation speed) step and draw through SdlCanvas with {bytes} B allocated over 120 frames");
        }
        finally { Sdl.PInvoke.DestroySurface(target); }
    }

    private static void Check(bool pass, string label)
    {
        if (!pass) throw new Exception(label);
        Console.WriteLine("PASS particles: " + label);
    }
}
