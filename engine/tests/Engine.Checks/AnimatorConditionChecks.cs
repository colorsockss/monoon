using Engine.Core;

// Roadmap 16.5: AND conditions over Bool/Int/Float/Trigger, exit time, Any State, list priority, zero allocation per step.
// AnimatorLab.Conditions replays Guard.scene in the editor preview, Scene Play, Release and Native AOT.
internal static class AnimatorConditionChecks
{
    [Check(5600, "animator-conditions")] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
    private const double Step = 1f / 60; // the fixed step SceneGame passes (float)

    public static void Run(string repository, string scratch)
    {
        var root = Path.Combine(scratch, "AnimatorConditions");
        foreach (var file in Directory.EnumerateFiles(Path.Combine(repository, "samples", "AnimatorLab"), "*", SearchOption.AllDirectories))
        {
            var path = Path.Combine(root, Path.GetRelativePath(Path.Combine(repository, "samples", "AnimatorLab"), file));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.Copy(file, path);
        }
        var assets = ProjectAssets.Scan(root);
        var guard = AnimationController.Read(Path.Combine(root, "Assets", "Guard.animator"));
        AnimationPlayer Start()
        {
            var p = new AnimationPlayer(guard, root, assets);
            p.Play(); p.Step(Step, null);
            return p;
        }
        int Until(AnimationPlayer p, string state, int limit = 600)
        {
            for (int i = 1; i <= limit; i++) { p.Step(Step, null); if (p.State == state) return i; }
            return -1;
        }

        var player = Start();
        player.SetFloat("Speed", 1); player.SetBool("Grounded", false); player.Step(Step, null);
        Check(player.State == "Idle", "Idle -> Walk waits while one of its two conditions (Grounded) is false");
        player.SetBool("Grounded", true); player.Step(Step, null);
        Check(player.State == "Walk", "both conditions true: Walk on the next step");

        player.SetInt("Combo", 1); player.SetTrigger("Attack"); player.Step(Step, null);
        Check(player.State == "Attack", "trigger and Int Combo < 3 start Attack");
        int ticks = Until(player, "Recover");
        Check(ticks is 24 or 25, $"exit time 1 leaves the 0.4 s Attack clip after one full play ({ticks + 1} steps in the state)");
        Check(Until(player, "Idle") is 18 or 19 && Until(player, "Walk", 1) == 1, "Recover -> Idle at its end, then Speed walks again");

        player.SetTrigger("Hit"); player.SetTrigger("Attack"); player.Step(Step, null);
        Check(player.State == "Hurt", "Any State -> Hurt wins over Walk -> Attack: it is higher in the list");
        player.SetTrigger("Hit"); player.Step(Step, null);
        Check(player.State == "Hurt" && player.Frame == 0, "Any State never re-enters its own destination");
        player.SetBool("Grounded", false);
        Check(Until(player, "Idle", 120) == -1, "Hurt -> Idle needs Grounded as well as its exit time");
        player.SetBool("Grounded", true); player.Step(Step, null);
        Check(player.State == "Idle", "Grounded again: Hurt -> Idle");
        player.Step(Step, null);
        Check(player.State == "Hurt", "the Hit set while in Hurt stayed pending until Any State could consume it");

        player = Start();
        player.SetInt("Combo", 3); player.SetTrigger("Attack");
        Check(Until(player, "Attack", 60) == -1, "Combo 3 blocks Idle -> Attack (Int Less 3)");
        player.SetInt("Combo", 0); player.Step(Step, null);
        Check(player.State == "Attack", "the blocked trigger stayed set and fires once Combo drops");

        // No allocation in a steady state with parameter changes and transitions on every few steps.
        player = Start();
        player.SetFloat("Speed", 1); player.Step(Step, null);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 20000; i++)
        {
            if (i % 50 == 0) player.SetTrigger("Hit");
            if (i % 70 == 0) player.SetTrigger("Attack");
            player.SetFloat("Speed", i % 200 < 100 ? 1 : 0);
            player.Step(Step, null);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0, $"20,000 steps with transitions allocate {allocated} bytes");

        // Older controllers keep their file layout; invalid new data is rejected with a message.
        var beacon = File.ReadAllText(Path.Combine(root, "Assets", "Beacon.animator"));
        var written = System.Text.Encoding.UTF8.GetString(ProjectYaml.Encode(AnimationController.Read(Path.Combine(root, "Assets", "Beacon.animator"))));
        Check(!beacon.Contains("exitTime") && !written.Contains("exitTime") && !written.Contains("and:"), "Beacon.animator round-trips without the new keys");
        Reject(guard with { Parameters = [.. guard.Parameters, new("Half", "Int", 1.5f)] }, "Int default 1.5");
        Reject(guard with { Transitions = [new("Idle", "Walk", "Combo", "Equal", 0.5f)] }, "Int condition value 0.5");
        Reject(guard with { Transitions = [new("Idle", "Walk", "", "")] }, "a transition with no condition and no exit time");
        Reject(guard with { Transitions = [new("Idle", "Walk", "Speed", "Triggered")] }, "a trigger test on a Float");
        Reject(guard with { States = [.. guard.States, new(AnimationTransition.AnyState, guard.States[0].Clip)] }, "a state named Any State");
        Reject(guard with { Transitions = [new("Idle", "Walk", "", "") { ExitTime = -1 }] }, "a negative exit time");
    }

    private static void Reject(AnimationController controller, string what)
    {
        try { controller.Validate("fixture"); }
        catch (InvalidDataException) { Console.WriteLine($"PASS animator conditions: rejects {what}"); return; }
        throw new Exception($"Animator conditions: accepted {what}");
    }

    private static void Check(bool ok, string detail)
    {
        if (!ok) throw new Exception("Animator conditions: " + detail);
        Console.WriteLine("PASS animator conditions: " + detail);
    }
}
