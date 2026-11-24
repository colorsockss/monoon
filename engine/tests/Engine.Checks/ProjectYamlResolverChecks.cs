using System.Text.Json;
using Engine.Core;
using VYaml.Serialization;

// ProjectYaml no longer asks StandardResolver (whose BuiltinResolver keeps ~70 formatters and ~0.4 MB of Native AOT code alive;
// Engine.Player/VYaml.Substitutions.xml removes the constructor that reaches it). The player resolves scalars in
// ScalarYamlResolver and everything else in GeneratedResolver; tools add BuiltinResolver back.
// For every member and document type of Engine.Core's generated formatters, the player chain without the tools' fallback must
// give a formatter of the same type as StandardResolver, so both read and write the same YAML.
static class ProjectYamlResolverChecks
{
    // ProjectYaml's own formatter list wins before either resolver.
    static readonly Type[] Custom = [typeof(JsonElement), typeof(RecordedTurn[]), typeof(RecordedInput[]), typeof(long[]), typeof(AssetEntry[]),
        typeof(TableColumn[]), typeof(TableRow[]), typeof(JsonElement[])];

    public static void Run()
    {
        // ProjectYaml builds its options without the constructor (which stores StandardResolver); every other field must match.
        var expected = new YamlSerializerOptions { DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull };
        foreach (var field in typeof(YamlSerializerOptions).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
        {
            if (field.FieldType == typeof(IYamlFormatterResolver)) continue;
            object? a = field.GetValue(expected), b = field.GetValue(ProjectYaml.Options);
            bool same = a is VYaml.Emitter.YamlEmitOptions && b != null
                ? a.GetType().GetProperties().All(p => Equals(p.GetValue(a), p.GetValue(b))) && a.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).All(f => Equals(f.GetValue(a), f.GetValue(b)))
                : Equals(a, b);
            if (!same) throw new Exception($"ProjectYaml options field {field.Name}: {b} instead of the constructor's {a}.");
        }
        var types = new HashSet<Type> { typeof(string[]), typeof(AssetEntry[]), typeof(GameData), typeof(SceneDocument) };
        foreach (var t in typeof(SceneComponent).Assembly.GetTypes())
            if (typeof(IYamlSchemaInfoProvider).IsAssignableFrom(t) && t is { IsAbstract: false, ContainsGenericParameters: false })
            {
                var info = ((IYamlSchemaInfoProvider)Activator.CreateInstance(t)!).GetSchemaInfo();
                types.Add(info.Type);
                foreach (var member in info.Members) types.Add(member.Type);
            }
        if (types.Count < 150 || !types.Contains(typeof(Guid?)) || !types.Contains(typeof(Dictionary<string, JsonElement>)))
            throw new Exception($"YAML resolver check found only {types.Count} types; the generated schema scan is broken.");
        var check = typeof(ProjectYamlResolverChecks).GetMethod(nameof(Same), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        foreach (var type in types.Except(Custom)) check.MakeGenericMethod(type).Invoke(null, null);
        // Decode repeats YamlSerializer.DeserializeMultipleDocuments; results and errors must stay the same.
        string Outcome(Func<GameData> read) { try { return System.Text.Encoding.UTF8.GetString(ProjectYaml.Encode(read())); } catch (Exception e) { return e.GetType().Name + ": " + e.Message; } }
        string[] inputs = ["", "---\n", "~\n", "version: 2\nvalues: { a: 1 }\n", "version: 2\n---\nversion: 3\n", "--- &a\nversion: 4\n...\n",
            "version: [\n", "version: 1\n---\nversion: [\n", "values: { x: *missing }\n", "unknown: 1\n"];
        var outcomes = new HashSet<string>();
        foreach (var yaml in inputs)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(yaml);
            string before = Outcome(() =>
            {
                using var docs = YamlSerializer.DeserializeMultipleDocuments<GameData>(bytes, ProjectYaml.Options).GetEnumerator();
                if (!docs.MoveNext() || docs.Current is not { } value || docs.MoveNext())
                    throw new InvalidDataException($"Expected exactly one {nameof(GameData)} document.");
                return value;
            });
            string after = Outcome(() => ProjectYaml.Decode<GameData>(bytes));
            if (before != after) throw new Exception($"ProjectYaml.Decode of {yaml.Replace("\n", "\\n")}: {after} instead of {before}.");
            outcomes.Add(after);
        }
        if (outcomes.Count < 4) throw new Exception($"Decode inputs gave only {outcomes.Count} different outcomes.");
        Console.WriteLine($"PASS project YAML resolver: {types.Count} Engine.Core document and member types match StandardResolver without BuiltinResolver; options match the constructor's; Decode matches VYaml for {inputs.Length} inputs ({outcomes.Count} outcomes)");
    }

    static void Same<T>()
    {
        var player = (IYamlFormatter<T>?)ScalarYamlResolver.Scalar(typeof(T)) ?? GeneratedResolver.Instance.GetFormatter<T>();
        var standard = StandardResolver.Instance.GetFormatter<T>();
        if (player == null || player.GetType() != standard?.GetType())
            throw new Exception($"YAML formatter for {typeof(T)}: player {player?.GetType().FullName ?? "none"}, StandardResolver {standard?.GetType().FullName ?? "none"}.");
    }
}
