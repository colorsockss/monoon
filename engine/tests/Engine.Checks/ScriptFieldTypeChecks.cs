using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Engine.Core;

// Roadmap 13.8: long, uint, Color and [ColorUsage] strings, flag enums, primitive arrays, List<T>, dictionaries and one-level
// [Serializable] structs. The Inspector defaults come from the initializers; the generated factory reads saved JSON through
// ScriptValues (no ReadOverride/JsonTypeInfo, no reflection) and keeps the initializer when nothing is saved; saved values
// survive a scene save and load byte-exact. Called from InspectorChecks.Run.
internal static class ScriptFieldTypeChecks
{
    private static void Require(bool ok, string message)
    {
        if (!ok) throw new Exception("Script field types: " + message);
    }

    internal const string Probe = """
        using System;
        using System.Collections.Generic;
        using System.Drawing;
        using System.Numerics;
        using Engine.Core;

        [Flags] public enum Element { None = 0, Fire = 1, Ice = 2, Wind = 4 }
        public enum Rarity { Common, Rare }
        [Serializable] public struct Wave { public int Count; public float Delay; public Rarity Kind; public Vector2 Offset; public string Label; public Color Tint; public readonly int Fixed; public int[] Skipped; }
        public struct Plain { public int A; }

        public sealed class FieldKinds : GameBehaviour
        {
            public long Big = 5000000000;
            public uint Mask = 7;
            public Color Tint = Color.FromArgb(255, 200, 100);
            [ColorUsage(false)] public string Hex = "#123456";
            public Element Elements = Element.Fire | Element.Wind;
            public int[] Waves = { 1, 2, 3 };
            public float[] Delays = new float[2];
            public string[] Names = ["a", "b"];
            public bool[] Switches;
            public long[] Bigs = [];
            public List<int> Counts = new() { 4, 5 };
            public List<string> Tags = [];
            public Dictionary<string, int> Prices = new() { ["herb"] = 3, ["gem"] = 10 };
            public Dictionary<int, string> Labels = new() { { 1, "one" } };
            public Wave First = new Wave { Count = 2, Delay = 0.5f, Kind = Rarity.Rare };
            public Wave Second;
            public Plain NotShown;
            public List<int> Later = new(Environment.ProcessorCount);
            [SerializeField] private Wave hiddenWave = new() { Count = 9 };
            public int HiddenCount => hiddenWave.Count;
        }
        """;

    internal static void Run(string repository, string scratch)
    {
        var root = Path.Combine(scratch, "script-field-types");
        TileChecks.Prepare(Path.Combine(repository, "samples", "KeyRoom"), root);
        File.WriteAllText(Path.Combine(root, "Scripts", "FieldKinds.cs"), Probe);
        var scripts = new ProjectScripts(root);
        var fields = scripts.Get("FieldKinds").Fields.ToDictionary(f => f.Name);
        string J(string name) => ((JsonElement)fields[name].Default!).GetRawText().Replace("\r", "").Replace("\n", "").Replace(" ", "");
        Require(fields["Big"] is { Type: var big, Default: 5000000000L } && big == typeof(long), "long " + fields["Big"]);
        Require(fields["Mask"] is { Default: 7u }, "uint");
        Require(fields["Tint"] is { Type: var t, Default: "#FFC864", ColorAlpha: true } && t == typeof(string), "Color " + fields["Tint"]);
        Require(fields["Hex"] is { Default: "#123456", ColorAlpha: false }, "[ColorUsage(false)] string");
        Require(fields["Elements"] is { Flags: true, Default: "Fire, Wind", EnumNames: ["Fire", "Ice", "Wind"] }, "flags " + fields["Elements"]);
        Require(J("Waves") == "[1,2,3]" && fields["Waves"].Element == typeof(int) && !fields["Waves"].IsList, "int[] " + J("Waves"));
        Require(J("Delays") == "[0,0]" && J("Names") == "[\"a\",\"b\"]" && J("Switches") == "null" && J("Bigs") == "[]", "arrays");
        Require(J("Counts") == "[4,5]" && fields["Counts"].IsList && J("Tags") == "[]", "lists " + J("Counts"));
        Require(J("Prices") == "{\"herb\":3,\"gem\":10}" && fields["Prices"].Key == typeof(string) && J("Labels") == "{\"1\":\"one\"}" &&
            fields["Labels"].Key == typeof(int), "dictionaries " + J("Prices") + J("Labels"));
        Require(J("First") == "{\"Count\":2,\"Delay\":0.5,\"Kind\":\"Rare\",\"Offset\":{\"x\":0,\"y\":0},\"Label\":null,\"Tint\":\"#00000000\"}" &&
            fields["First"].Members!.Select(m => m.Name).SequenceEqual(["Count", "Delay", "Kind", "Offset", "Label", "Tint"]), "struct " + J("First"));
        Require(J("Second") == "{\"Count\":0,\"Delay\":0,\"Kind\":\"Common\",\"Offset\":{\"x\":0,\"y\":0},\"Label\":null,\"Tint\":\"#00000000\"}", "default struct");
        Require(J("hiddenWave").StartsWith("{\"Count\":9,", StringComparison.Ordinal), "[SerializeField] struct");
        var skipped = scripts.Inspector("FieldKinds")!.Skipped.ToDictionary(n => n.Name);
        Require(skipped["NotShown"].Reason.Contains("Serializable", StringComparison.Ordinal) && skipped["Later"].Reason.StartsWith("set at run time", StringComparison.Ordinal),
            "skipped " + string.Join("; ", skipped.Values));

        var build = scripts.Current();
        Require(!build.Factory.Contains("ReadOverride(\"Big\"", StringComparison.Ordinal) && !build.Factory.Contains("System.Reflection", StringComparison.Ordinal) &&
            build.Factory.Contains("ScriptValues.Dictionary(", StringComparison.Ordinal), "the factory uses ScriptValues readers");

        // Saved values: each kind changed, the struct only partly (the rest keeps the initializer).
        var saved = new Dictionary<string, string>
        {
            ["Big"] = "-9000000000", ["Mask"] = "4294967295", ["Tint"] = "\"#10203040\"", ["Hex"] = "\"#ABCDEF\"", ["Elements"] = "\"Ice, Fire\"",
            ["Waves"] = "[9,8]", ["Delays"] = "[0.25]", ["Names"] = "[]", ["Switches"] = "[true,false,true]", ["Bigs"] = "[1,-2]",
            ["Counts"] = "[]", ["Tags"] = "[\"x\",\"y\"]", ["Prices"] = "{\"key\":1,\"herb\":4}", ["Labels"] = "{\"-3\":\"minus\",\"7\":null}",
            ["First"] = "{\"Count\":5,\"Kind\":\"Common\",\"Offset\":{\"x\":1.5,\"y\":-2},\"Label\":\"wave\",\"Tint\":\"#FF0000\"}",
            ["hiddenWave"] = "{\"Count\":11}",
        };
        var component = new ScriptComponent { Script = "FieldKinds" };
        foreach (var (k, v) in saved) component.Values[k] = JsonDocument.Parse(v).RootElement.Clone();
        var expected = "Big=-9000000000|Mask=4294967295|Tint=16,32,48,64|Hex=#ABCDEF|Elements=Fire, Ice|Waves=9,8|Delays=0.25|Names=|Switches=True,False,True|" +
            "Bigs=1,-2|Counts=|Tags=x,y|Prices=key:1,herb:4|Labels=-3:minus,7:|First=5,0.5,Common,<1.5, -2>,wave,255,0,0,255|Second=0,0,Common,<0, 0>,,0,0,0,0|Hidden=11";
        var initial = "Big=5000000000|Mask=7|Tint=255,200,100,255|Hex=#123456|Elements=Fire, Wind|Waves=1,2,3|Delays=0,0|Names=a,b|Switches=null|" +
            "Bigs=|Counts=4,5|Tags=|Prices=herb:3,gem:10|Labels=1:one|First=2,0.5,Rare,<0, 0>,,0,0,0,0|Second=0,0,Common,<0, 0>,,0,0,0,0|Hidden=9";
        var context = new AssemblyLoadContext("script-field-types", isCollectible: true);
        try
        {
            var create = context.LoadFromStream(new MemoryStream(build.Bytes)).GetType("MoNoon.Generated.ProjectScriptFactory")!.GetMethod("Create")!;
            string Dump(ScriptComponent c)
            {
                var o = create.Invoke(null, [c])!;
                object? F(string n) => o.GetType().GetField(n)!.GetValue(o);
                static string Seq(object? v) => v == null ? "null" : string.Join(",", ((System.Collections.IEnumerable)v).Cast<object?>().Select(x =>
                    x is System.Collections.DictionaryEntry e ? $"{e.Key}:{e.Value}" : x?.GetType().Name.StartsWith("KeyValuePair", StringComparison.Ordinal) == true
                        ? $"{x.GetType().GetProperty("Key")!.GetValue(x)}:{x.GetType().GetProperty("Value")!.GetValue(x)}" : Convert.ToString(x, System.Globalization.CultureInfo.InvariantCulture)));
                static string Wave(object w)
                {
                    object? M(string n) => w.GetType().GetField(n)!.GetValue(w);
                    var c = (System.Drawing.Color)M("Tint")!;
                    return string.Join(",", Convert.ToString(M("Count"), System.Globalization.CultureInfo.InvariantCulture), Convert.ToString(M("Delay"), System.Globalization.CultureInfo.InvariantCulture),
                        M("Kind"), ((System.Numerics.Vector2)M("Offset")!).ToString(null, System.Globalization.CultureInfo.InvariantCulture), M("Label"), c.R, c.G, c.B, c.A);
                }
                var tint = (System.Drawing.Color)F("Tint")!;
                return string.Join("|", $"Big={F("Big")}", $"Mask={F("Mask")}", $"Tint={tint.R},{tint.G},{tint.B},{tint.A}", $"Hex={F("Hex")}", $"Elements={F("Elements")}",
                    $"Waves={Seq(F("Waves"))}", $"Delays={Seq(F("Delays"))}", $"Names={Seq(F("Names"))}", $"Switches={Seq(F("Switches"))}", $"Bigs={Seq(F("Bigs"))}",
                    $"Counts={Seq(F("Counts"))}", $"Tags={Seq(F("Tags"))}", $"Prices={Seq(F("Prices"))}", $"Labels={Seq(F("Labels"))}",
                    $"First={Wave(F("First")!)}", $"Second={Wave(F("Second")!)}", $"Hidden={o.GetType().GetProperty("HiddenCount")!.GetValue(o)}");
            }
            var got = Dump(component);
            Require(got == expected, $"factory values\n  got  {got}\n  want {expected}");
            var none = Dump(new ScriptComponent { Script = "FieldKinds" });
            Require(none == initial, $"initializers kept\n  got  {none}\n  want {initial}");
            // A wrong JSON kind keeps the initializer (the field type may have changed); an undeclared flag name is a data error.
            var wrong = new ScriptComponent { Script = "FieldKinds" };
            wrong.Values["Waves"] = JsonDocument.Parse("\"text\"").RootElement.Clone();
            wrong.Values["Big"] = JsonDocument.Parse("true").RootElement.Clone();
            Require(Dump(wrong) == initial, "wrong kinds keep the initializer");
            wrong.Values["Elements"] = JsonDocument.Parse("\"Fire, Lava\"").RootElement.Clone();
            try { Dump(wrong); Require(false, "undeclared flag accepted"); }
            catch (TargetInvocationException ex) when (ex.InnerException is InvalidDataException) { }
        }
        finally { context.Unload(); }

        // The editor's view of a struct merges a partial save over the defaults; the scene keeps the saved JSON byte-exact.
        var first = (JsonElement)fields["First"].Value(new ScriptComponent { Script = "FieldKinds", Values = { ["First"] = JsonDocument.Parse("{\"Count\":7}").RootElement.Clone() } })!;
        Require(first.GetProperty("Count").GetInt32() == 7 && first.GetProperty("Kind").GetString() == "Rare", "struct view merges defaults");
        var scenePath = Path.Combine(root, "Scenes", "KeyRoom.scene");
        var scene = SceneDocument.Load(scenePath);
        var owner = new SceneObject { Name = "Kinds" };
        owner.AddComponent(component);
        scene.Objects.Add(owner);
        scene.Save(scenePath);
        var bytes = File.ReadAllBytes(scenePath);
        SceneDocument.Load(scenePath).Save(scenePath);
        Require(bytes.SequenceEqual(File.ReadAllBytes(scenePath)), "scene save -> load -> save byte-exact");
        var reloaded = SceneDocument.Load(scenePath).Objects.Single(o => o.Name == "Kinds").GetComponent<ScriptComponent>()!;
        Require(saved.All(p => JsonElement.DeepEquals(reloaded.Values[p.Key], component.Values[p.Key])), "saved values round-trip");
        Console.WriteLine($"PASS script field types: long/uint/Color/flags/arrays/List/Dictionary/struct defaults, factory reads {saved.Count} saved kinds " +
            "through ScriptValues and keeps initializers, scene round-trip byte-exact");
    }
}
