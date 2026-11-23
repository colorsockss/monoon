using System.Numerics;
using Engine.Core;

internal static class ContactChecks
{
    private sealed class Listener : GameBehaviour
    {
        internal readonly List<(Guid Other,string Phase)> Events = [];
        internal Vector2 Move;
        internal bool RemoveOnEnter;

        public override void FixedUpdate(ScriptTick tick) => tick.Move(Move);
        public override void OnTriggerEnter(ScriptContact c)
        {
            Events.Add((c.OtherId,"TE"));
            if (RemoveOnEnter) c.Objects.Destroy(c.OtherId);
        }
        public override void OnTriggerStay(ScriptContact c) => Events.Add((c.OtherId,"TS"));
        public override void OnTriggerExit(ScriptContact c) => Events.Add((c.OtherId,"TX"));
        public override void OnCollisionEnter(ScriptContact c) => Events.Add((c.OtherId,"CE"));
        public override void OnCollisionStay(ScriptContact c) => Events.Add((c.OtherId,"CS"));
        public override void OnCollisionExit(ScriptContact c) => Events.Add((c.OtherId,"CX"));
    }

    public static void Run(string project)
    {
        var scene = SceneDocument.Empty(new());
        SceneObject Add(string name, float x, Collider2DComponent collider)
        {
            var obj = new SceneObject { Name = name, X = x, Width = 10, Height = 10 };
            obj.AddComponent(collider);
            obj.AddComponent(new ScriptComponent { Script = "Listener" });
            scene.Objects.Add(obj);
            return obj;
        }
        var actor = Add("Actor",0,new BoxCollider2DComponent());
        var sensor = Add("Sensor",20,new CircleCollider2DComponent { IsTrigger = true });
        var wall = Add("Wall",60,new BoxCollider2DComponent());
        var masked = Add("Masked",0,new BoxCollider2DComponent { Mask = 0 });
        using var world = new SceneGame(scene,null,null,_ => new Listener());
        Listener Probe(SceneObject o) => world.GetComponent<Listener>(o.Id)!;
        void Step(float x = 0) { Probe(actor).Move = new(x,0); world.FixedUpdate(Vector2.Zero,1f/60); }
        void Expect(SceneObject self, SceneObject other, params string[] events)
        {
            var got = Probe(self).Events.Where(e => e.Other == other.Id).Select(e => e.Phase);
            if (!got.SequenceEqual(events)) throw new Exception($"{self.Name}/{other.Name}: [{string.Join(",",got)}], expected [{string.Join(",",events)}]");
        }
        Step(20); Step(); Step(30); Step(); Step(-4);
        Expect(sensor,actor,"TE","TS","TX"); Expect(actor,sensor,"TE","TS","TX");
        Expect(wall,actor,"CE","CS","CX"); Expect(actor,wall,"CE","CS","CX");
        Expect(masked,actor);
        Step(-45);
        Expect(sensor,actor,"TE","TS","TX","TE","TX"); // Fully crossed within one tick.
        Step(19); Step();
        world.SetColliderEnabled(actor.Id,false); world.SetColliderEnabled(actor.Id,true); Step();
        Expect(sensor,actor,"TE","TS","TX","TE","TX","TE","TS","TX","TE");
        world.SetActive(actor.Id,false); Step(); world.SetActive(actor.Id,true); Step();
        Expect(sensor,actor,"TE","TS","TX","TE","TX","TE","TS","TX","TE","TX","TE");
        Probe(sensor).Events.Clear();
        world.Destroy(actor.Id); world.Simulate(default,1f/60); world.Simulate(default,1f/60);
        Expect(sensor,actor,"TX");

        using var mutation = new SceneGame(scene,null,null,_ => new Listener());
        var survivor = mutation.GetComponent<Listener>(sensor.Id)!;
        survivor.RemoveOnEnter = true;
        mutation.GetComponent<Listener>(actor.Id)!.Move = new(20,0);
        mutation.FixedUpdate(Vector2.Zero,1f/60); mutation.FixedUpdate(Vector2.Zero,1f/60);
        if (!survivor.Events.SequenceEqual([(actor.Id,"TE"),(actor.Id,"TX")]))
            throw new Exception("Destroying a participant during enter must deliver exactly one exit to the survivor.");
        var secondSensor = new SceneObject { X = 20, Width = 10, Height = 10 };
        secondSensor.AddComponent(new BoxCollider2DComponent { IsTrigger = true });
        secondSensor.AddComponent(new ScriptComponent { Script = "Listener" });
        scene.Objects.Add(secondSensor);
        using var sensors = new SceneGame(scene,null,null,_ => new Listener());
        sensors.FixedUpdate(Vector2.Zero,1f/60); sensors.FixedUpdate(Vector2.Zero,1f/60);
        sensors.SetColliderEnabled(secondSensor.Id,false); sensors.FixedUpdate(Vector2.Zero,1f/60);
        foreach (var (self,other) in new[] { (sensor,secondSensor), (secondSensor,sensor) })
            if (!sensors.GetComponent<Listener>(self.Id)!.Events.SequenceEqual([(other.Id,"TE"),(other.Id,"TS"),(other.Id,"TX")]))
                throw new Exception("Two triggers should each receive one callback per phase.");
        Console.WriteLine("PASS paired solid and trigger enter/stay/exit, swept trigger crossing, masks and removal during callbacks");
        Order();
        PressureRoom(project);
    }

    private sealed class Recorder(List<string> log) : GameBehaviour
    {
        internal Vector2 Move;
        public override void FixedUpdate(ScriptTick tick) => tick.Move(Move);
        private void Add(ScriptContact c, string phase) => log.Add($"{c.ObjectId.ToString()[..8]}>{c.OtherId.ToString()[..8]}:{phase}");
        public override void OnTriggerEnter(ScriptContact c) => Add(c,"TE");
        public override void OnTriggerStay(ScriptContact c) => Add(c,"TS");
        public override void OnTriggerExit(ScriptContact c) => Add(c,"TX");
        public override void OnCollisionEnter(ScriptContact c) => Add(c,"CE");
        public override void OnCollisionStay(ScriptContact c) => Add(c,"CS");
        public override void OnCollisionExit(ScriptContact c) => Add(c,"CX");
    }

    // Callback order must not depend on how the broad phase finds pairs. Columns of equal Left, touching cells, tall
    // walls across many rows, triggers, swept crossings and an enable toggle; the hash was recorded with the plain sweep.
    private static void Order()
    {
        const string expected = "E55DF23661A36B7DC9C2E491E0CAB232ABAC6A0B614BA81A5E9F55A054A32695";
        var scene = SceneDocument.Empty(new());
        var moves = new Dictionary<Guid,Vector2>();
        int next = 1;
        Guid Add(float x, float y, float w, float h, bool trigger, bool circle = false, Vector2 move = default)
        {
            var obj = new SceneObject { Id = new Guid(next++, 0, 0, new byte[8]), X = x, Y = y, Width = w, Height = h };
            obj.AddComponent(circle ? new CircleCollider2DComponent { IsTrigger = trigger } : new BoxCollider2DComponent { IsTrigger = trigger });
            obj.AddComponent(new ScriptComponent { Script = "Recorder" });
            scene.Objects.Add(obj);
            moves[obj.Id] = move;
            return obj.Id;
        }
        for (int col = 0; col < 10; col++)
            for (int row = 0; row < 12; row++)
                Add(col * 10, row * (row % 2 == 0 ? 12 : 9), 10, 10, (col + row) % 3 == 0, circle: (col * row) % 5 == 4);
        Add(35, -20, 6, 200, false); Add(75, -20, 6, 200, true);
        Add(-30, 5, 8, 8, true, move: new(4, 1)); Add(-30, 40, 8, 8, false, move: new(3, 0));
        Add(120, 60, 8, 8, true, move: new(-25, 0)); Add(50, -40, 6, 6, true, circle: true, move: new(0, 9));
        var toggled = Add(-60, 70, 10, 10, true, move: new(6, 0));
        var log = new List<string>();
        using var world = new SceneGame(scene,null,null,_ => new Recorder(log));
        foreach (var (id, move) in moves) world.GetComponent<Recorder>(id)!.Move = move;
        for (int tick = 0; tick < 40; tick++)
        {
            if (tick == 20) world.SetColliderEnabled(toggled,false);
            if (tick == 22) world.SetColliderEnabled(toggled,true);
            world.FixedUpdate(Vector2.Zero,1f/60);
        }
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("\n",log))));
        if (!hash.StartsWith(expected)) throw new Exception($"Contact callback order changed: {log.Count} events, SHA-256 {hash}");
        Console.WriteLine($"PASS contact callback order over {log.Count} events matches the plain sweep (SHA-256 {hash[..16]})");
    }

    public static void Tiles(string project, Guid tileset)
    {
        var scene = SceneDocument.Empty(new());
        scene.ProjectDirectory = project;
        var map = new Tilemap2DComponent { Tileset = tileset };
        map.Set(new(0,0),1); map.Set(new(1,0),1);
        var wall = new SceneObject { Name = "Map", Y = 16 };
        wall.AddComponent(map);
        var pawn = new SceneObject { Name = "Pawn", X = 28, Y = 4, Width = 8, Height = 8 };
        pawn.AddComponent(new BoxCollider2DComponent());
        pawn.AddComponent(new ScriptComponent { Script = "Listener" });
        scene.Objects.AddRange([wall,pawn]);
        using var world = new SceneGame(scene,null,null,_ => new Listener());
        var probe = world.GetComponent<Listener>(pawn.Id)!;
        foreach (var move in new Vector2[] { new(0,10), new(8,0), new(0,-8) })
        {
            probe.Move = move;
            world.FixedUpdate(Vector2.Zero,1f/60);
        }
        if (!probe.Events.SequenceEqual([(wall.Id,"CE"),(wall.Id,"CS"),(wall.Id,"CX")]))
            throw new Exception("Adjacent solid tiles should report one contact with the tilemap.");
    }

    private static void PressureRoom(string project)
    {
        using var world = new SceneGame(SceneDocument.Load(Path.Combine(project,"Scenes","SecondRoom.scene")));
        Guid Id(string name) => world.Objects.Single(o => o.Name == name).Id;
        var plate = Id("Pressure Plate"); var door = Id("Pressure Door"); var player = Id("Room Two Player");
        var crate = Id("Push Crate");
        void Move(Vector2 dir,int ticks)
        {
            for (int i=0;i<ticks;i++) world.FixedUpdate(dir,1f/60);
            world.FixedUpdate(Vector2.Zero,1f/60);
        }
        void State(int count,bool open)
        {
            var image = world.GetComponent<SpriteRendererComponent>(door);
            if (world.Inspect(plate)?.Contacts != count || world.IsColliderEnabled(door) == open ||
                image?.Asset != (open ? "Assets/DoorOpen3.png" : "Assets/Door.png") ||
                (world.Physics.Raycast(new(558,210),Vector2.UnitX,24)?.Target.ObjectId == door) == open)
                throw new Exception($"Pressure Room: expected {count} contacts, door {(open ? "open" : "closed")}");
        }
        Move(Vector2.Zero,1); State(0,false);
        Move(Vector2.UnitX,34); State(1,false);
        Move(Vector2.UnitX,15); State(2,true);
        Move(-Vector2.UnitY,17); State(1,true);
        // The crate holds the door while the player walks around the plate and through the gate.
        Move(Vector2.UnitX,51); Move(Vector2.UnitY,17); Move(Vector2.UnitX,27);
        if (!world.TryGetPosition(player,out var position) || position.X < 594) throw new Exception("Open pressure door blocked player");
        State(1,true);
        Move(-Vector2.UnitX,105); // Push the crate off the left edge, then leave the plate behind it.
        State(0,false);
        if (!world.TryGetPosition(crate,out var box) || box.X + 34 > 300) throw new Exception("Crate did not leave plate");
        Console.WriteLine("PASS KeyRoom plate 0/closed, 1/closed, 2/open, crate alone/open, walk through, last leaves/closed");
    }
}
