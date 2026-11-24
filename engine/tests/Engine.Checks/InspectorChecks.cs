using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;
using System.Text.Json;
using Engine.Core;

// Roadmap 13.2–13.14 Inspector model checks (no UI). Called from HistoryChecks.Run; the coordinator may give it its own group.
internal static class InspectorChecks
{
    private static void Require(bool ok, string message)
    {
        if (!ok) throw new Exception("Inspector: " + message);
    }

    internal static void Run(string repository, string scratch)
    {
        ComponentStateChecks.Run(repository);
        ScriptAttributes(repository, scratch);
        ScriptFieldTypeChecks.Run(repository, scratch);
        ComponentMenu();
    }

    // 13.4: every built-in component has an Add Component group; "col" finds the five colliders; Paste Values keeps the
    // target's place and on/off state and refuses another type.
    private static void ComponentMenu()
    {
        var choices = Engine.Editor.ComponentChoice.Registered.Values.ToArray();
        var groups = choices.GroupBy(c => c.Category).ToDictionary(g => g.Key, g => g.Count());
        Require(groups.Keys.All(k => Engine.Editor.ComponentChoice.Categories.Contains(k)) &&
            Engine.Editor.ComponentChoice.Categories[..^1].All(groups.ContainsKey), "categories " + string.Join(",", groups));
        var col = choices.Where(c => c.Name.Contains("col", StringComparison.OrdinalIgnoreCase)).Select(c => c.Type).Order(Comparer<Type>.Create((a, b) => string.CompareOrdinal(a.Name, b.Name))).ToArray();
        Require(col.SequenceEqual([typeof(BoxCollider2DComponent), typeof(CapsuleCollider2DComponent), typeof(CircleCollider2DComponent), typeof(EdgeCollider2DComponent), typeof(PolygonCollider2DComponent)]), "col search");

        var door = new SceneObject { Name = "Door" };
        door.AddComponent(new SpriteRendererComponent());
        door.AddComponent(new BoxCollider2DComponent { IsTrigger = true, Layer = 3, Mask = 5 });
        var box = new SceneObject { Name = "Box" };
        box.AddComponent(new BoxCollider2DComponent { Enabled = false });
        box.AddComponent(new SpriteRendererComponent());
        var copied = Engine.Editor.ComponentEdits.Copy(door.Components[1]);
        Require(!Engine.Editor.ComponentEdits.CanPasteValues(box.Components[1], copied), "paste values refuses another type");
        Require(Engine.Editor.ComponentEdits.PasteValues(box, box.Components[0], copied) && box.Components[0] is BoxCollider2DComponent { IsTrigger: true, Layer: 3, Mask: 5, Enabled: false },
            "paste values copies the fields and keeps the target's on/off state");
        Require(!Engine.Editor.ComponentEdits.PasteValues(box, box.Components[0], copied), "pasting the same values changes nothing");
        Require(Engine.Editor.ComponentEdits.Reset(box, box.Components[0]) && box.Components[0] is BoxCollider2DComponent { IsTrigger: false, Layer: 0, Mask: uint.MaxValue, Enabled: false }, "reset");
        Require(Engine.Editor.ComponentEdits.Move(box, box.Components[1], -1) && box.Components[0] is SpriteRendererComponent && !Engine.Editor.ComponentEdits.Move(box, box.Components[0], -1), "move");
        Console.WriteLine($"PASS component menu: {choices.Length} built-ins in {groups.Count} groups, \"col\" finds the 3 colliders, Paste Values/Reset/Move");
    }

    // 13.3: Unity-named field attributes are [Conditional]: the editor reads them from Roslyn source symbols, the game
    // assembly has none, and a [SerializeField] private field is written by the generated factory without reflection.
    private static void ScriptAttributes(string repository, string scratch)
    {
        var root = Path.Combine(scratch, "inspector-attributes");
        TileChecks.Prepare(Path.Combine(repository, "samples", "KeyRoom"), root);
        File.WriteAllText(Path.Combine(root, "Scripts", "AttributeProbe.cs"), """
            using Engine.Core;
            public sealed class AttributeProbe : GameBehaviour
            {
                [Space(12), Multiline(4)] public string Notes = "a";
                public float Later = System.Environment.ProcessorCount;
                public System.Collections.Generic.HashSet<int> Items = new();
                private int hidden = 3;
                [SerializeField] protected int shownProtected = 4;
                [HideInInspector] public int Secret = 5;
                public const int Constant = 1;
                public static int Shared;
                public readonly int Fixed = 1;
                public int Total => hidden + shownProtected + Secret + Fixed;
            }
            """);
        var scripts = new ProjectScripts(root);
        var speed = scripts.Inspector("PlayerMovement")!.Fields["MoveSpeed"];
        Require(speed is { Header: "Movement", Min: 60, Max: 480, Tooltip: "Walking speed in pixels per second.", Hidden: false }, $"MoveSpeed {speed}");
        var reach = scripts.Inspector("PlayerMovement")!.Fields["coinReach"];
        Require(reach is { Tooltip: "How far a coin can be picked up or dropped, in pixels.", Min: 16, Max: null }, $"coinReach {reach}");
        Require(scripts.Get("PlayerMovement").Fields.Any(f => f.Name == "coinReach" && (float)f.Default! == 128), "[SerializeField] private field is an Inspector field");

        var probe = scripts.Inspector("AttributeProbe")!;
        var names = scripts.Get("AttributeProbe").Fields.Select(f => f.Name).ToArray();
        Require(names.SequenceEqual(["Notes", "shownProtected", "Secret"]), "probe fields " + string.Join(",", names));
        Require(probe.Fields["Secret"].Hidden && probe.Fields["Notes"] is { Space: 12, Lines: 4 }, "HideInInspector, Space, Multiline");
        var skipped = probe.Skipped.ToDictionary(n => n.Name);
        Require(skipped["Later"] is { Expected: true, Reason: var later } && later.StartsWith("set at run time", StringComparison.Ordinal) &&
            skipped["Items"] is { Expected: true, Reason: "unsupported type" } && skipped["hidden"] is { Expected: false, Initializer: "3" } &&
            skipped["Fixed"].Expected && !skipped["Constant"].Expected && !skipped["Shared"].Expected,
            "skipped field reasons " + string.Join("; ", probe.Skipped));

        var build = scripts.Current();
        using (var pe = new PEReader(new MemoryStream(build.Bytes)))
        {
            var md = pe.GetMetadataReader();
            string[] ours = [nameof(TooltipAttribute), nameof(RangeAttribute), nameof(MinAttribute), nameof(HeaderAttribute), nameof(SpaceAttribute),
                nameof(MultilineAttribute), nameof(HideInInspectorAttribute), nameof(SerializeFieldAttribute)];
            var referenced = md.TypeReferences.Select(h => md.GetString(md.GetTypeReference(h).Name)).Intersect(ours).ToArray();
            Require(referenced.Length == 0, "game assembly references " + string.Join(",", referenced));
        }
        var context = new AssemblyLoadContext("inspector-attributes", isCollectible: true);
        try
        {
            var create = context.LoadFromStream(new MemoryStream(build.Bytes)).GetType("MoNoon.Generated.ProjectScriptFactory")!.GetMethod("Create")!;
            var component = new ScriptComponent { Script = "PlayerMovement" };
            component.Values["coinReach"] = JsonSerializer.SerializeToElement(96f);
            var player = create.Invoke(null, [component])!;
            float value = (float)player.GetType().GetField("coinReach", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(player)!;
            var protectedComponent = new ScriptComponent { Script = "AttributeProbe" };
            protectedComponent.Values["shownProtected"] = JsonSerializer.SerializeToElement(9);
            var probeInstance = create.Invoke(null, [protectedComponent])!;
            int total = (int)probeInstance.GetType().GetProperty("Total")!.GetValue(probeInstance)!;
            Require(value == 96 && total == 3 + 9 + 5 + 1, $"factory writes private fields ({value}, {total})");
        }
        finally { context.Unload(); }
        Console.WriteLine("PASS script field attributes: Header/Range/Min/Tooltip/Space/Multiline/HideInInspector read from source, " +
            "0 attribute references in the game assembly, [SerializeField] private fields set by the factory");
    }
}
