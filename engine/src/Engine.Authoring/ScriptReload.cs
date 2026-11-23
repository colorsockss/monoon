using System.Reflection;

namespace Engine.Core;

// Editor Scene Play only: swaps every live behaviour for one built from recompiled scripts and carries its field values.
// Players never load this assembly, so Release and Native AOT builds contain none of it.
internal static class ScriptReload
{
    internal static List<(GameLogLevel Level, string Message)> Apply(GameSession session, ScriptLibrary next)
    {
        if (session.Scripts is not {} shared || session.LoadingScene)
            throw new InvalidOperationException("Scripts reload once the scene has finished loading.");
        var world = session.Current;
        var bodies = world.LiveBodies.Where(b => b.Behaviours.Length > 0).ToArray();
        var runtime = new ScriptRuntime(next);
        GameBehaviour[][] fresh;
        // Build every instance first, so a script that no longer compiles to a component leaves the running code untouched.
        try { fresh = bodies.Select(b => b.Source.Components.OfType<ScriptComponent>().Select(runtime.Create).ToArray()).ToArray(); }
        catch { runtime.Dispose(); throw; }
        var swapped = new Dictionary<object, GameBehaviour>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < bodies.Length; i++)
            for (int j = 0; j < fresh[i].Length; j++) swapped[bodies[i].Behaviours[j]] = fresh[i][j];
        var notes = new List<(GameLogLevel, string)>();
        foreach (var (old, now) in swapped) Carry((GameBehaviour)old, now, swapped, notes);
        int stopped = world.StopAllRoutines();
        for (int i = 0; i < bodies.Length; i++) world.ReplaceBehaviours(bodies[i], fresh[i]);
        shared.Adopt(runtime);
        notes.Insert(0, (GameLogLevel.Info, $"Scripts reloaded: {swapped.Count} behaviours on {bodies.Length} objects kept their field values."));
        if (stopped > 0)
            notes.Add((GameLogLevel.Info, $"Stopped {stopped} running routine(s) from the previous code. Scripts start new ones as usual."));
        return notes;
    }

    // Matched by declaring type name and field name. New fields keep the new instance's value, removed ones are dropped.
    private static void Carry(GameBehaviour old, GameBehaviour now, Dictionary<object, GameBehaviour> swapped, List<(GameLogLevel, string)> notes)
    {
        var previous = Fields(old.GetType()).ToDictionary(Key);
        foreach (var field in Fields(now.GetType()))
        {
            if (!previous.TryGetValue(Key(field), out var before)) continue;
            if (Convert(before.GetValue(old), before.FieldType, field.FieldType, swapped, out var value)) field.SetValue(now, value);
            else notes.Add((GameLogLevel.Warning, $"{now.GetType().Name}.{Name(field)}: " + (before.FieldType.FullName == field.FieldType.FullName
                ? $"a project {Display(field.FieldType)} value cannot carry across the reload" : $"type changed from {Display(before.FieldType)} to {Display(field.FieldType)}") +
                "; the field was reset to its default."));
        }
    }

    private static string Key(FieldInfo f) => f.DeclaringType!.FullName + "." + f.Name;
    private static string Name(FieldInfo f) => f.Name.StartsWith('<') ? f.Name[1..f.Name.IndexOf('>')] : f.Name; // auto-property
    private static string Display(Type t) => t.IsGenericType ? t.Name[..t.Name.IndexOf('`')] + "<…>" : t.Name;

    // Delegates (events, lambdas) are skipped: they point at the old instance and the factory already wired the new one.
    private static IEnumerable<FieldInfo> Fields(Type? type)
    {
        for (; type != null && type != typeof(GameBehaviour); type = type.BaseType)
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (!typeof(Delegate).IsAssignableFrom(field.FieldType)) yield return field;
    }

    private static bool Convert(object? value, Type from, Type to, Dictionary<object, GameBehaviour> swapped, out object? result)
    {
        result = value;
        if (value != null && swapped.TryGetValue(value, out var replacement)) { result = replacement; return to.IsInstanceOfType(replacement); }
        if (from == to) return true; // An engine or .NET type is shared by both assemblies.
        if (from.FullName != to.FullName) return false;
        if (value == null) return !to.IsValueType;
        // A project enum with the same name keeps the member of the same name.
        if (to.IsEnum && Enum.TryParse(to, value.ToString(), out var member) && Enum.IsDefined(to, member)) { result = member; return true; }
        // ComponentReference<ProjectType> is only an object ID.
        if (to.IsGenericType && to.GetGenericTypeDefinition() == typeof(ComponentReference<>))
        {
            result = Activator.CreateInstance(to, from.GetProperty(nameof(ComponentReference<object>.ObjectId))!.GetValue(value));
            return true;
        }
        return false; // Other project types cannot cross assemblies.
    }
}
