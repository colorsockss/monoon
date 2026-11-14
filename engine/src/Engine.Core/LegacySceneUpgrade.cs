using System.Text.Json.Nodes;

namespace Engine.Core;

// Removed gameplay behaviours become editable project scripts only in scenes that used them.
internal sealed class LegacySceneUpgrade(string project)
{
    private readonly ProjectScripts scripts = new(project);
    private readonly Dictionary<string, string> sources = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> Convert(JsonObject root, int version)
    {
        if (root["objects"] is not JsonArray objects) throw new InvalidDataException("Expected a scene objects list.");
        foreach (var node in objects)
        {
            if (node is not JsonObject obj) throw new InvalidDataException("Expected a scene object.");
            if (version == 1)
            {
                var list = new JsonArray();
                if (obj.Remove("moveWithArrows", out var flag) && (flag?.GetValue<bool>() ?? throw new InvalidDataException("Invalid moveWithArrows.")))
                    list.Add(new JsonObject { ["type"] = "arrowMovement" });
                obj["components"] = list;
            }
            if (obj["components"] is not JsonArray components) throw new InvalidDataException("Expected an object's components.");
            var used = components.OfType<JsonObject>().Select(c => (string?)c["script"]).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
            JsonObject? sprite = null;
            for (int i = 0; i < components.Count; i++)
            {
                if (components[i] is not JsonObject component) throw new InvalidDataException("Invalid component.");
                switch ((string?)component["type"])
                {
                    case "arrowMovement":
                        components[i] = ToScript("PlayerMovement", used, new JsonObject());
                        break;
                    case "pickup":
                        var values = new JsonObject();
                        if (component["collector"] is { } collector) values["Collector"] = collector.GetValue<Guid>();
                        if (component["opens"] is { } opens) values["Opens"] = opens.GetValue<Guid>();
                        components[i] = ToScript("Pickup", used, values);
                        break;
                    case "spriteRenderer":
                        sprite = component;
                        break;
                }
            }
            if (sprite == null && ((bool?)obj["hasRectangle"] ?? true))
            {
                sprite = new JsonObject { ["type"] = "spriteRenderer" };
                components.Insert(0, sprite);
            }
            if (sprite != null) sprite["squareColor"] = (string?)obj["tint"] ?? "#63E6C1";
            obj.Remove("tint");
            obj.Remove("hasRectangle");
        }
        return sources;
    }

    private JsonObject ToScript(string name, HashSet<string> used, JsonObject values)
    {
        using var resource = typeof(LegacySceneUpgrade).Assembly.GetManifestResourceStream($"LegacyScripts.{name}.cs")
            ?? throw new InvalidDataException($"Missing upgrade source for {name}.");
        using var reader = new StreamReader(resource);
        var source = reader.ReadToEnd();
        var files = scripts.Names().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var id = name;
        for (int index = 1; ; index++)
        {
            if (!used.Contains(id))
            {
                var ns = id.Contains('/') ? "namespace MoNoon.Legacy." + id.Split('/')[0] + ";\n" : "";
                var migrated = ns + source;
                if (!files.Contains(id)) { sources[id] = migrated; break; }
                if (File.Exists(scripts.SourcePath(id)) && scripts.Read(id).ReplaceLineEndings() == migrated.ReplaceLineEndings()) break;
            }
            id = (index == 1 ? "Migrated" : $"Migrated{index}") + "/" + name;
        }
        used.Add(id);
        return new JsonObject { ["type"] = "script", ["script"] = id, ["values"] = values };
    }
}
