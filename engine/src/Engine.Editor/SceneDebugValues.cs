using System.Globalization;
using System.Reflection;

namespace Engine.Editor;

// Inspect stored values only. Expanding an object or hovering over a name never runs user code.
internal sealed class SceneDebugValues(string[] names, object?[] values)
{
    private readonly Dictionary<int, object> children = [];
    private int nextId = 2;

    private static bool Leaf(object? value) => value == null || value is string or decimal or Guid ||
        value.GetType().IsPrimitive || value.GetType().IsEnum;

    private static string Display(object? value) => value switch {
        null => "null", string s => System.Text.Json.JsonSerializer.Serialize(s), bool b => b ? "true" : "false",
        Guid id => id.ToString(), _ when Leaf(value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
        _ => "{" + value.GetType().Name + "}"
    };

    private static IEnumerable<(string Name, object? Value)> Members(object obj)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (Type? type = obj.GetType(); type != null; type = type.BaseType)
        foreach (var field in type.GetFields(flags))
        {
            string name = field.Name.EndsWith(">k__BackingField", StringComparison.Ordinal) && field.Name.StartsWith('<')
                ? field.Name[1..field.Name.IndexOf('>')] : field.Name;
            if (seen.Add(name) && !field.FieldType.IsPointer && !field.FieldType.IsFunctionPointer && !field.FieldType.IsByRefLike)
                yield return (name, field.GetValue(obj));
        }
    }

    private int Reference(object? value)
    {
        if (Leaf(value)) return 0;
        int id = nextId++;
        children[id] = value!;
        return id;
    }

    public object[] Variables(int handle)
    {
        IEnumerable<(string,object?)> entries = handle == 1 ? names.Zip(values) :
            children.TryGetValue(handle, out var obj) ? Members(obj) : [];
        return entries.Select(pair => (object)new { name = pair.Item1, value = Display(pair.Item2),
            type = pair.Item2?.GetType().Name, variablesReference = Reference(pair.Item2) }).ToArray();
    }

    public object Evaluate(string path)
    {
        var parts = path.Trim().Split('.');
        int i = Array.IndexOf(names, parts[0]);
        object? value;
        int start;
        if (i >= 0) { value = values[i]; start = 1; }
        else
        {
            int self = Array.IndexOf(names, "this");
            if (self < 0) throw new InvalidDataException("Variable is outside the current scope.");
            value = values[self];
            start = 0;
        }
        foreach (var part in parts.Skip(start))
        {
            if (value == null) throw new InvalidDataException("Cannot inspect a null value.");
            var member = Members(value).FirstOrDefault(m => m.Name == part);
            if (member.Name == null) throw new InvalidDataException("Enter a variable or a dot-separated field name.");
            value = member.Value;
        }
        return new { result = Display(value), type = value?.GetType().Name, variablesReference = Reference(value) };
    }
}
