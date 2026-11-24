using Engine.Core;

// 11.2: Scene Play script reload keeps live field values; 11.10: Editor/ stays out of the game scripts.
internal static class ScriptReloadChecks
{
    [Check(6500)] static void Registered(CheckRun c) => Run(c.Scratch);
    internal static void Run(string scratch)
    {
        string root = Directory.CreateDirectory(Path.Combine(scratch, "Reload Project")).FullName;
        string file = Path.Combine(Directory.CreateDirectory(Path.Combine(root, "Scripts")).FullName, "Counter.cs");
        File.WriteAllText(file, """
            using Engine.Core;
            public enum Pace { Slow, Fast }
            public sealed class Counter : GameBehaviour
            {
                public int Ticks;
                public float Speed = 2;
                public Pace Pace = Pace.Slow;
                public Counter Peer;
                public ComponentReference<Counter> Self;
                private int hidden = 5;
                public int Step = 1;
                public override void OnCreate(ScriptLife life) { Peer = this; Self = new(life.ObjectId); }
                public override void FixedUpdate(ScriptTick tick) { Ticks += Step; hidden++; }
            }
            """);
        Directory.CreateDirectory(Path.Combine(root, ProjectScripts.EditorFolder));
        File.WriteAllText(Path.Combine(root, ProjectScripts.EditorFolder, "Tool.cs"), "this would not compile as a game script");
        var library = ProjectScripts.ForScenePlay(root);
        Check(library.Locations().Keys.SequenceEqual(["Counter"]), "Editor/ C# files are not game scripts");

        var scene = SceneDocument.Empty(new()); scene.ProjectDirectory = root;
        var obj = new SceneObject { Name = "Counter" };
        obj.AddComponent(new ScriptComponent { Script = "Counter" });
        scene.Objects.Add(obj);
        using var session = new GameSession(scene, library);
        object Live() => session.Current.GetComponent<GameBehaviour>(obj.Id)!;
        object? Value(string name) => Live().GetType().GetField(name, System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(Live());
        for (int i = 0; i < 5; i++) session.FixedUpdate(default(GameInput), 1f / 60);
        Live().GetType().GetField("Pace")!.SetValue(Live(), Enum.Parse(Live().GetType().GetField("Pace")!.FieldType, "Fast"));
        IEnumerable<ScriptWait> Forever() { while (true) yield return ScriptWait.Frames(1); }
        var routine = session.Current.StartRoutine(obj.Id, Forever());
        var before = Live();
        Check((int)Value("Ticks")! == 5, "v1 counts one per tick");

        // v2: Step changes to 10 by code (kept at 1 live), Speed becomes an int, Label is new, hidden is removed.
        File.WriteAllText(file, """
            using Engine.Core;
            public enum Pace { Slow, Fast }
            public sealed class Counter : GameBehaviour
            {
                public int Ticks;
                public int Speed = 3;
                public Pace Pace = Pace.Slow;
                public Counter Peer;
                public ComponentReference<Counter> Self;
                public string Label = "v2";
                public int Step = 1;
                public override void FixedUpdate(ScriptTick tick) { Ticks += Step * 100; }
            }
            """);
        var next = ProjectScripts.ForScenePlay(root);
        var notes = ScriptReload.Apply(session, next);
        Check(!ReferenceEquals(Live(), before) && Live().GetType().Assembly != before.GetType().Assembly, "behaviours come from the new assembly");
        Check((int)Value("Ticks")! == 5 && (int)Value("Step")! == 1 && Value("Pace")!.ToString() == "Fast",
            "int and project enum fields keep their live values");
        Check(ReferenceEquals(Value("Peer"), Live()), "a reference to a reloaded behaviour points at its replacement");
        Check(Value("Self")!.GetType().GetProperty("ObjectId")!.GetValue(Value("Self")) is Guid self && self == obj.Id &&
            !notes.Any(n => n.Message.Contains("Counter.Self")), "ComponentReference<ProjectType> keeps its object");
        Check((string?)Value("Label") == "v2" && Value("hidden") == null, "new fields get defaults and removed fields are dropped");
        Check((int)Value("Speed")! == 3 && notes.Any(n => n.Level == GameLogLevel.Warning && n.Message.Contains("Counter.Speed") && n.Message.Contains("Single")),
            "a field whose type changed resets with a warning");
        Check(!routine.IsRunning && notes.Any(n => n.Message.Contains("Stopped 1 running routine")), "running routines stop with a Console note");
        session.FixedUpdate(default(GameInput), 1f / 60);
        Check((int)Value("Ticks")! == 105, "the next tick runs the new code");

        File.WriteAllText(file, File.ReadAllText(file).Replace("Ticks += Step * 100;", "Ticks += Step * 100"));
        var broken = ProjectScripts.ForScenePlay(root);
        try { broken.Current(); throw new Exception("Reload: the syntax error compiled."); }
        catch (InvalidDataException ex) when (ex.Message.Contains("Counter.cs(12,") && ex.Message.Contains("CS1002"))
        {
            session.FixedUpdate(default(GameInput), 1f / 60);
            Check((int)Value("Ticks")! == 205, "a compile error names file:line and the previous code keeps running");
        }

        File.WriteAllText(file, "using Engine.Editor; using Engine.Core; public sealed class Counter : GameBehaviour { }");
        try { ProjectScripts.ForScenePlay(root).Current(); throw new Exception("Reload: a game script referenced the editor."); }
        catch (InvalidDataException ex) when (ex.Message.Contains("CS0234") || ex.Message.Contains("CS0246")) { Check(true, "game scripts cannot reference the editor API or Editor/ types"); }
    }

    private static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception("Script reload: " + label);
        Console.WriteLine("PASS script reload: " + label);
    }
}
