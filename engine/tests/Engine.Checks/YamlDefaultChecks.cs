using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Engine.Core;
using VYaml.Annotations;

// VYaml's generated formatters ignore property/field initializers: a key missing from the YAML becomes default(T) unless the
// value is a [YamlConstructor] (or positional record) parameter default. For every [YamlObject] type, decoding a mapping that
// holds only the required keys must equal the type's declared defaults (`new T()`), so a hand-written or older file means the
// same as the code says.
internal static class YamlDefaultChecks
{
    [Check(7500)] static void Registered(CheckRun c) => Run();
    private static readonly MethodInfo Encode = Generic("Encode");
    private static readonly MethodInfo Decode = Generic("Decode");

    // ProjectYamlWriters adds non-generic Encode/Write overloads for the player; pick the generic formatter path.
    private static MethodInfo Generic(string name) =>
        typeof(ProjectYaml).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).Single(m => m.Name == name && m.IsGenericMethodDefinition);

    internal static void Run()
    {
        var types = new[] { typeof(SceneDocument).Assembly, typeof(BuildSettings).Assembly, typeof(YamlDefaultChecks).Assembly }
            .SelectMany(a => a.GetTypes())
            .Where(t => t.GetCustomAttribute<YamlObjectAttribute>() != null && !t.IsAbstract && !t.ContainsGenericParameters)
            .OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray();
        var failures = new List<string>();
        foreach (var type in types)
        {
            var declared = CreateDeclared(type);
            var yaml = MinimalYaml(type, declared);
            object decoded;
            try { decoded = Decode.MakeGenericMethod(type).Invoke(null, [Encoding.UTF8.GetBytes(yaml)])!; }
            catch (TargetInvocationException ex) { failures.Add($"{type.Name}: minimal YAML did not load ({ex.InnerException!.Message})"); continue; }
            var want = Lines(declared, type); var got = Lines(decoded, type);
            if (!want.SequenceEqual(got))
                failures.Add($"{type.Name}: declared [{string.Join(" | ", want.Except(got))}] read as [{string.Join(" | ", got.Except(want))}]");
        }
        if (failures.Count > 0) throw new Exception("YAML defaults differ from declared defaults:\n  " + string.Join("\n  ", failures));
        Console.WriteLine($"PASS {types.Length} YAML types read omitted keys as their declared defaults");
    }

    private static string[] Lines(object value, Type type) =>
        Encoding.UTF8.GetString((byte[])Encode.MakeGenericMethod(type).Invoke(null, [value])!).Split('\n');

    // A class's `new T()` (parameterless constructor and initializers); otherwise, and for record structs, the constructor
    // VYaml calls, with its parameter defaults.
    private static object CreateDeclared(Type type)
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        if (!type.IsValueType && type.GetConstructor(all, Type.EmptyTypes) != null) return Activator.CreateInstance(type, true)!;
        var ctors = type.GetConstructors(all).Where(c => !(c.GetParameters() is [var p] && p.ParameterType == type)).ToArray();
        if (ctors.Length == 0) return Activator.CreateInstance(type)!;
        var ctor = ctors.FirstOrDefault(c => c.GetCustomAttribute<YamlConstructorAttribute>() != null) ?? ctors.Single();
        return ctor.Invoke(ctor.GetParameters().Select(p =>
            p.HasDefaultValue && p.DefaultValue != null ? (p.ParameterType.IsEnum ? Enum.ToObject(p.ParameterType, p.DefaultValue) : p.DefaultValue)
            : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray());
    }

    // Only the required keys ([YamlRequired], C# `required`, or a setter that rejects a missing list), with the declared values
    // so they read back unchanged.
    private static string MinimalYaml(Type type, object declared)
    {
        var yaml = new StringBuilder();
        foreach (var member in type.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (member.GetCustomAttribute<YamlRequiredAttribute>() == null && member.GetCustomAttribute<RequiredMemberAttribute>() == null &&
                !(type == typeof(Tilemap2DComponent) && member.Name == nameof(Tilemap2DComponent.Chunks))) continue;
            var (memberType, value) = member switch
            {
                PropertyInfo p => (p.PropertyType, p.GetValue(declared)),
                FieldInfo f => (f.FieldType, f.GetValue(declared)),
                _ => throw new InvalidOperationException(member.Name)
            };
            var name = member.GetCustomAttribute<YamlMemberAttribute>()?.Name ?? char.ToLowerInvariant(member.Name[0]) + member.Name[1..];
            yaml.Append(name).Append(":\n");
            foreach (var line in Lines(value!, memberType).Where(l => l.Length > 0)) yaml.Append("  ").Append(line).Append('\n');
        }
        return yaml.Length == 0 ? "{}" : yaml.ToString();
    }
}
