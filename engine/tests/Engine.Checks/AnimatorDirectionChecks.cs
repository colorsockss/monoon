using System.Numerics;
using Engine.Core;

// Roadmap 16.5: 4/8-direction clip sets picked by a Vector2 parameter, with hysteresis, no allocation per step, and older files
// unchanged. AnimatorLab.Directions replays Scout.scene in the editor preview, Scene Play, Release and Native AOT.
internal static class AnimatorDirectionChecks
{
    [Check(5700, "animator-directions")] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    private const double Step = 1f / 60;

    public static void Run(string repository, string scratch)
    {
        var root = Path.Combine(scratch, "AnimatorDirections");
        foreach (var file in Directory.EnumerateFiles(Path.Combine(repository, "samples", "AnimatorLab"), "*", SearchOption.AllDirectories))
        {
            var path = Path.Combine(root, Path.GetRelativePath(Path.Combine(repository, "samples", "AnimatorLab"), file));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.Copy(file, path, true);
        }
        var assets = ProjectAssets.Scan(root);
        var scout = AnimationController.Read(Path.Combine(root, "Assets", "Scout.animator"));
        var walk = scout.States.Single(s => s.Name == "Walk").Directions!;
        var idle = scout.States.Single(s => s.Name == "Idle").Directions!;
        Check(walk.Length == 8 && idle.Length == 4, "Scout.animator: Walk has 8 direction clips, Idle 4");

        var player = new AnimationPlayer(scout, root, assets);
        player.Play(); player.Step(Step, null);
        Check(player.State == "Idle" && player.Facing == 4, "a zero vector at start faces S");
        int Turn(float degrees, int steps = 1)
        {
            // Screen angles: 0 = E, 90 = S (world +Y is down). MathF.Sin/Cos separately, as simulation code must.
            float r = float.DegreesToRadians(degrees);
            player.SetVector2("Move", new(MathF.Cos(r), MathF.Sin(r)));
            for (int i = 0; i < steps; i++) player.Step(Step, null);
            return player.Facing;
        }
        player.SetFloat("Speed", 1);
        Check(Turn(0, 2) == 2 && player.State == "Walk", "Walk picks E for 0 degrees");
        Check(Turn(25) == 2 && Turn(29) == 2, "hysteresis: E is kept at 25 and 29 degrees, past the 22.5 degree midpoint");
        Check(Turn(31) == 3, "at 31 degrees (more than 30 from E) Walk turns to SE");
        Check(Turn(20) == 3 && Turn(14) == 2, "turning back: SE is kept at 20 degrees, E returns at 14");
        foreach (var (degrees, facing) in new[] { (-90f, 0), (-45f, 1), (90f, 4), (135f, 5), (180f, 6), (-135f, 7) })
            Check(Turn(degrees) == facing, $"{degrees} degrees faces {AnimationState.Compass[facing]}", quiet: true);
        Check(true, "a jump to any compass point picks it at once");
        player.SetVector2("Move", Vector2.Zero); player.Step(Step, null);
        Check(player.State == "Walk" && player.Facing == 7, "a zero vector keeps the facing");

        // Idle has 4 directions: a diagonal facing shows the axis before it clockwise; its own hysteresis is 60 degrees.
        player.SetFloat("Speed", 0); player.Step(Step, null);
        Check(player.State == "Idle" && player.Facing == 6, "Walk NW, then Idle with a zero vector shows W");
        Check(Turn(140) == 6 && Turn(235) == 6 && Turn(245) == 0, "Idle keeps W at 40 and 55 degrees away, then picks N at 65");

        // The clip follows the facing; time carries over so the walk cycle does not restart on a turn.
        player.SetFloat("Speed", 1); Turn(0, 2);
        var east = player.Appearance;
        Turn(90);
        Check(player.Appearance != east && player.Appearance?.Source?.X == 4 * 32, "the S walk clip shows its own sheet column");
        int frame = player.Frame;
        Turn(180);
        Check(player.Frame == frame || player.Frame == (frame + 1) % 2, "turning keeps the walk cycle position");

        // No allocation while turning and switching states.
        player = new AnimationPlayer(scout, root, assets);
        player.Play(); player.Step(Step, null);
        var circle = new Vector2[72];
        for (int i = 0; i < circle.Length; i++) circle[i] = new(MathF.Cos(i * MathF.Tau / 72), MathF.Sin(i * MathF.Tau / 72));
        for (int pass = 0; pass < 2; pass++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 20000; i++)
            {
                player.SetVector2("Move", i % 300 < 20 ? Vector2.Zero : circle[i % 72]);
                player.SetFloat("Speed", i % 300 < 20 ? 0 : 1);
                player.Step(Step, null);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (pass == 1) Check(allocated == 0, $"20,000 steps turning through 72 angles allocate {allocated} bytes");
        }

        // Older controllers keep their layout; invalid sets are rejected.
        foreach (var name in new[] { "Beacon.animator", "Guard.animator" })
        {
            var text = File.ReadAllText(Path.Combine(root, "Assets", name));
            var written = System.Text.Encoding.UTF8.GetString(ProjectYaml.Encode(AnimationController.Read(Path.Combine(root, "Assets", name))));
            Check(!text.Contains("direction") && !written.Contains("direction"), $"{name} round-trips without direction keys");
        }
        var walkState = scout.States[1];
        Reject(scout with { States = [scout.States[0], walkState with { Directions = walk[..3] }] }, "3 direction clips");
        Reject(scout with { States = [scout.States[0], walkState with { Direction = "Speed" }] }, "a Float direction parameter");
        Reject(scout with { States = [scout.States[0], walkState with { Direction = null }] }, "clips without a parameter");
        Reject(scout with { States = [scout.States[0], walkState with { Clip = walk[1] }] }, "a state clip other than the first direction");
        Reject(scout with { Parameters = [new("Move", "Vector2", 1), scout.Parameters[1]] }, "a nonzero Vector2 default");
        Reject(scout with { Transitions = [new("Idle", "Walk", "Move", "Greater", 0)] }, "a condition on a Vector2");
        Expect(() => player.SetVector2("Speed", Vector2.One), "SetVector2 on a Float parameter");
        Expect(() => player.SetVector2("Move", new(float.NaN, 0)), "a NaN vector");
    }

    private static void Expect(Action action, string what)
    {
        try { action(); }
        catch (ArgumentException) { Console.WriteLine($"PASS animator directions: rejects {what}"); return; }
        throw new Exception($"Animator directions: accepted {what}");
    }

    private static void Reject(AnimationController controller, string what)
    {
        try { controller.Validate("fixture"); }
        catch (InvalidDataException) { Console.WriteLine($"PASS animator directions: rejects {what}"); return; }
        throw new Exception($"Animator directions: accepted {what}");
    }

    private static void Check(bool ok, string detail, bool quiet = false)
    {
        if (!ok) throw new Exception("Animator directions: " + detail);
        if (!quiet) Console.WriteLine("PASS animator directions: " + detail);
    }
}
