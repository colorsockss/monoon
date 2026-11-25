using System.Numerics;
using System.Text;
using Engine.Core;

// 16.12: per-action Hold/Tap/DoubleTap, input buffers and 2D vector actions, judged in game ticks.
internal static class InputInteractionChecks
{
    [Check(6800, "input-interactions")] static void Registered(CheckRun c) => Run(c.Repository);
    private static void Check(bool ok, string why) { if (!ok) throw new Exception("Input interactions: " + why); }

    internal static void Run(string repository)
    {
        var yaml = """
            actions:
            - {name: MoveLeft, controls: [Left]}
            - {name: MoveRight, controls: [Right]}
            - {name: MoveUp, controls: [Up]}
            - {name: MoveDown, controls: [Down]}
            - {name: Charge, controls: [J], interaction: Hold, ticks: 3}
            - {name: Flick, controls: [U], interaction: Tap, ticks: 3}
            - {name: Dash, controls: [O], interaction: DoubleTap, ticks: 4}
            - {name: Leap, controls: [Y, Pad.South], buffer: 2}
            - {name: AimLeft, controls: [V]}
            - {name: AimRight, controls: [B]}
            - {name: AimUp, controls: [T]}
            - {name: AimDown, controls: [G]}
            vectors:
            - {name: Walk, left: MoveLeft, right: MoveRight, up: MoveUp, down: MoveDown}
            - {name: Aim, left: AimLeft, right: AimRight, up: AimUp, down: AimDown}
            """;
        var map = ProjectYaml.Decode<InputBindings>(Encoding.UTF8.GetBytes(yaml));
        var charge = map.Actions!.Single(a => a.Name == "Charge");
        Check(charge.Interaction == ActionBinding.Hold && charge.Ticks == 3 && map.Actions!.Single(a => a.Name == "Leap").Buffer == 2 &&
              map.Vectors!.Length == 2 && map.Vectors[1] == new ActionVector("Aim", "AimLeft", "AimRight", "AimUp", "AimDown"), "YAML reads the new keys");
        var again = ProjectYaml.Decode<InputBindings>(ProjectYaml.Encode(map));
        Check(again.Actions!.SequenceEqual(map.Actions!, Comparer.Instance) && again.Vectors!.SequenceEqual(map.Vectors), "YAML round trip");
        Check(!Encoding.UTF8.GetString(ProjectYaml.Encode(new InputBindings { Actions = [new("Jump", ["Space"])] })).Contains("interaction"),
            "plain actions write no new keys");

        var input = new InputActions(map);
        var log = new List<string>();
        // Each step is one game tick: apply the presses/releases, Take, record what fired.
        GameInput Tick(params string[] changes)
        {
            foreach (var c in changes) { if (c[0] == '+') input.Press(c[1..], 0); else input.Release(c[1..]); }
            var frame = input.Take();
            foreach (var action in new[] { "Charge", "Flick", "Dash", "Leap" }) if (frame.Triggered(action)) log.Add($"{frame.Tick}:{action}");
            return frame;
        }
        Tick("+J"); Tick(); Tick(); Tick(); Tick("-J");          // ticks 1-5: Hold 3 fires on tick 3, once
        Tick("+J"); Tick("-J");                                  // 6-7: too short
        Tick("+U"); Tick(); Tick("-U");                          // 8-10: released 2 ticks after the press: fires on 10
        Tick("+U"); Tick(); Tick(); Tick("-U");                  // 11-14: 3 ticks: too long
        Tick("+O", "-O"); Tick(); Tick(); Tick("+O", "-O");      // 15-18: second press 3 ticks later fires on 18
        Tick("+O", "-O"); Tick(); Tick(); Tick(); Tick(); Tick("+O", "-O"); // 19-24: 5 ticks apart: no
        Check(log.SequenceEqual(["3:Charge", "10:Flick", "18:Dash"]), "Hold, Tap and DoubleTap ticks: " + string.Join(" ", log));
        log.Clear();
        Tick("+Y", "-Y");                                        // 25: Leap buffered for 2 more ticks
        Tick(); var third = Tick(); var late = Tick();           // 26, 27, 28
        Check(log.SequenceEqual(["25:Leap", "26:Leap", "27:Leap"]) && !late.Triggered("Leap"), "buffer 2 keeps the trigger for ticks 25-27: " + string.Join(" ", log));
        Check(!third.Consume("Leap") && !third.Triggered("Leap"), "a GameInput from an earlier tick neither sees nor consumes a trigger");
        var press = Tick("+Pad.South", "-Pad.South");
        Check(press.Consume("Leap") && !press.Triggered("Leap") && !press.Consume("Leap"), "Consume uses the trigger up for later readers in the same tick");
        Check(!Tick().Triggered("Leap"), "a consumed trigger stays used");
        Check(!Tick("+Y").Triggered("Dash") && Tick().Triggered("Flick") == false, "rules are per action");
        input.Release("Y");

        var walk = Tick("+Right", "+Up", "+V");
        Check(walk.Vector("Walk") == walk.Move && Math.Abs(walk.Move.Length() - 1) < 1e-6f && walk.Move.X > 0 && walk.Move.Y < 0,
            "Move is the first vector, clamped to length 1");
        Check(walk.Vector("Aim") == new Vector2(-1, 0) && walk.Vector("aim") == walk.Vector("Aim") && walk.Vector("Missing") == default, "named vectors");
        var ui = input.TakeUi();
        Check(ui.Vector("Aim") == new Vector2(-1, 0) && ui.Triggered("Charge") == ui.Pressed("Charge"), "UI input has vectors but no interactions");

        // Steady input: no allocation per tick once the samples stop changing.
        Tick();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { var f = input.Take(); _ = f.Triggered("Leap"); _ = f.Vector("Aim"); }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0, $"no allocation per tick with held keys ({allocated} bytes)");

        input.Rebind("Charge", "K"); input.Press("K", 0);
        var rebound = input.Take(); input.Take();
        Check(input.Take().Triggered("Charge") && !rebound.Triggered("Charge"), "Rebind keeps the rule on the new control");

        // Without vectors, Move keeps the four fixed names (existing projects).
        var legacy = new InputActions(new InputBindings { Actions = [new("MoveRight", ["D"]), new("MoveUp", ["W"]), new("Jump", ["Space"])] });
        legacy.Press("D", 0); legacy.Press("W", 0);
        var old = legacy.Take();
        Check(old.Move == Vector2.Normalize(new Vector2(1, -1)) && old.Vector("Move") == old.Move && old.Triggered("Jump") == false, "legacy Move");
        // The KeyRoom moves showcase carries the three interactions in its GameSettings; its other actions stay plain.
        var keyroom = GameSettings.Load(Path.Combine(repository, "samples", "KeyRoom"));
        ActionBinding Shipped(string name) => keyroom.Input.Actions!.Single(a => a.Name == name);
        Check(keyroom.Input.Vectors == null && Shipped("Charge") is { Interaction: ActionBinding.Hold, Ticks: 30 } &&
              Shipped("Flick") is { Interaction: ActionBinding.Tap, Ticks: 12 } && Shipped("Dash") is { Interaction: ActionBinding.DoubleTap, Ticks: 15 } &&
              keyroom.Input.Actions!.Where(a => a.Name is not ("Charge" or "Flick" or "Dash")).All(a => a.Interaction == null && a.Buffer == null),
              "KeyRoom settings load the showcase's Hold/Tap/DoubleTap moves");

        foreach (var bad in new[] {
            "actions: [{name: A, controls: [J], interaction: Press}]", "actions: [{name: A, controls: [J], ticks: 0}]",
            "actions: [{name: A, controls: [J], buffer: -1}]", "actions: [{name: A, controls: [J]}]\nvectors: [{name: V, left: A, right: A, up: A, down: B}]",
            "actions: [{name: A, controls: [J]}]\nvectors: [{name: V, left: A, right: A, up: A, down: A}, {name: v, left: A, right: A, up: A, down: A}]" })
        {
            try { new InputActions(ProjectYaml.Decode<InputBindings>(Encoding.UTF8.GetBytes(bad))); throw new Exception("Input interactions: accepted " + bad); }
            catch (InvalidDataException) { }
        }
        Console.WriteLine("PASS input interactions: Hold/Tap/DoubleTap ticks, buffer window and Consume, 2D vectors and Move, legacy Move, no allocation, YAML, validation");
    }

    private sealed class Comparer : IEqualityComparer<ActionBinding>
    {
        public static readonly Comparer Instance = new();
        public bool Equals(ActionBinding? a, ActionBinding? b) => a!.Name == b!.Name && a.Controls.SequenceEqual(b.Controls) &&
            a.Interaction == b.Interaction && a.Ticks == b.Ticks && a.Buffer == b.Buffer;
        public int GetHashCode(ActionBinding a) => a.Name.GetHashCode();
    }
}
