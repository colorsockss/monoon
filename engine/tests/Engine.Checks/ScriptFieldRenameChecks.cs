using System.Text.Json;
using Engine.Core;

internal static class ScriptFieldRenameChecks
{
    [Check(6300)] static void Registered(CheckRun c) => Run(c.Scratch);
    internal static void Run(string scratch)
    {
        string root = Path.Combine(scratch, "FieldNames");
        string file = Path.Combine(Directory.CreateDirectory(Path.Combine(root,"Scripts")).FullName,"Mover.cs");
        const string source = """
            using Engine.Core;
            public sealed class Mover : GameBehaviour
            {
                [FormerlySerializedAs("Speed"), FormerlySerializedAs("Rate")] public float MoveSpeed = 1;
                [FormerlySerializedAs("Art")] public SpriteReference[] Frames = [];
                [FormerlySerializedAs("Peer")] public ComponentReference<Mover> Target;
            }
            """;
        File.WriteAllText(file,source);
        var project = new ProjectScripts(root);
        var desc = project.Get("Mover");
        var component = new ScriptComponent { Script = "Mover" };
        var sprite = Guid.NewGuid();
        var target = Guid.NewGuid();
        component.WriteReferences("Art",[sprite]);
        component.Values["Peer"] = JsonSerializer.SerializeToElement(target);
        component.Values["Rate"] = JsonSerializer.SerializeToElement(175);
        component.Values["Unrelated"] = JsonSerializer.SerializeToElement("retain");
        using var runtime = new ScriptRuntime(project);
        var instance = runtime.Create(component);
        object? Read(GameBehaviour behaviour, string field) => behaviour.GetType().GetField(field)!.GetValue(behaviour);
        static void Require(bool ok, string label) { if (!ok) throw new InvalidDataException(label); }
        Require((float)Read(instance,"MoveSpeed")! == 175 &&
            (Read(instance,"Frames") as SpriteReference[])!.Single().Id == sprite &&
            Read(instance,"Target")!.GetType().GetProperty("ObjectId")!.GetValue(Read(instance,"Target"))!.Equals(target),
            "A generated factory must read old scalar, asset array and component-reference names.");
        component.Values["Speed"] = JsonSerializer.SerializeToElement(210);
        Require((float)Read(runtime.Create(component),"MoveSpeed")! == 210, "Newest declared alias must win.");
        component.Values["MoveSpeed"] = JsonSerializer.SerializeToElement(320);
        Require((float)Read(runtime.Create(component),"MoveSpeed")! == 320, "Canonical value must win.");
        Require(ProjectScripts.NormalizeValues(component.Values,desc) &&
            !ProjectScripts.NormalizeValues(component.Values,desc) &&
            component.Values.Keys.ToHashSet().SetEquals(["Frames","Target","MoveSpeed","Unrelated"]) &&
            component.Values["MoveSpeed"].GetSingle() == 320 && component.ReadReferences("Frames",false).Single() == sprite &&
            component.Values["Target"].GetGuid() == target, "Normalize once, preserving other fields and reference IDs.");
        foreach (string reserved in new[]{"Speed","MoveSpeed"})
        {
            File.WriteAllText(file, source.Replace("public SpriteReference[] Frames", $"[FormerlySerializedAs(\"{reserved}\")] public SpriteReference[] Frames"));
            try { _ = project.Get("Mover"); throw new Exception("Accepted conflicting serialized field name"); }
            catch (InvalidDataException ex) { Require(ex.Message.Contains("claimed by") && ex.Message.Contains(reserved), ex.Message); }
        }
        Console.WriteLine("PASS script field renames: aliases, current-key precedence, reference IDs, stable canonical keys and conflicting declarations");
    }
}
