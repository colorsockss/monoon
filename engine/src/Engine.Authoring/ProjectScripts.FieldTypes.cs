using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;

namespace Engine.Core;

// Roadmap 13.8: long, uint, colours, flag enums, primitive arrays, List<T>, dictionaries and one-level [Serializable] structs.
// The Inspector default is the initializer evaluated from the source operations (constants only). The generated factory reads
// the saved JSON with ScriptValues; it starts from the field's own value, so what the initializer computed stays when nothing
// is saved. Statement templates go into ScriptInspectorInfo.Reads: (key expression, member expression) -> C# statement.
public sealed partial class ProjectScripts
{
    private const string Values = "global::Engine.Core.ScriptValues.";

    private static string Qualified(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static Type? Scalar(ITypeSymbol type) => type.SpecialType switch
    {
        SpecialType.System_Int32 => typeof(int), SpecialType.System_Int64 => typeof(long), SpecialType.System_UInt32 => typeof(uint),
        SpecialType.System_Single => typeof(float), SpecialType.System_Double => typeof(double),
        SpecialType.System_Boolean => typeof(bool), SpecialType.System_String => typeof(string), _ => null
    };

    private static bool IsColor(ITypeSymbol type) => type.ToDisplayString() == "System.Drawing.Color";
    private static bool IsFlags(ITypeSymbol type) => type.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "System.FlagsAttribute");

    // The generated expression reading one scalar, enum, Vector2 or Color from JsonElement `e` with `initial` as the fallback.
    private static string? ScalarRead(ITypeSymbol type, string e, string initial)
    {
        string? method = type.SpecialType switch
        {
            SpecialType.System_Int32 => "Int32", SpecialType.System_Int64 => "Int64", SpecialType.System_UInt32 => "UInt32",
            SpecialType.System_Single => "Single", SpecialType.System_Double => "Double", SpecialType.System_Boolean => "Boolean",
            SpecialType.System_String => "String", _ => null
        };
        if (method != null) return $"{Values}{method}({e}, {initial})";
        if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum }) return $"{Values}Enum<{Qualified(type)}>({e}, {initial}{(IsFlags(type) ? ", true" : "")})";
        if (type.ToDisplayString() == "System.Numerics.Vector2") return $"{Values}Vector({e}, {initial})";
        return IsColor(type) ? $"{Values}Color({e}, {initial})" : null;
    }

    private static IOperation? Unwrap(IOperation? op)
    {
        while (op is IConversionOperation { IsImplicit: true } or IParenthesizedOperation)
            op = op is IConversionOperation c ? c.Operand : ((IParenthesizedOperation)op).Operand;
        return op;
    }

    private static JsonNode? Zero(Type scalar) => scalar == typeof(string) ? null : Json(Activator.CreateInstance(scalar)!);

    private static JsonNode Json(object value) => value switch
    {
        int i => JsonValue.Create(i), long l => JsonValue.Create(l), uint u => JsonValue.Create(u), float f => JsonValue.Create(f),
        double d => JsonValue.Create(d), bool b => JsonValue.Create(b), string s => JsonValue.Create(s),
        _ => throw new ArgumentException(value.GetType().Name)
    };

    // A compile-time constant (or `default`, or no initializer) of a scalar type.
    private static bool Constant(IOperation? op, Type scalar, out JsonNode? json)
    {
        json = null;
        op = Unwrap(op);
        if (op is null or IDefaultValueOperation) { json = Zero(scalar); return true; }
        if (!op.ConstantValue.HasValue) return false;
        if (op.ConstantValue.Value is not { } value) return scalar == typeof(string);
        if (scalar == typeof(float) && value is float f && !float.IsFinite(f) || scalar == typeof(double) && value is double d && !double.IsFinite(d)) return false;
        json = Json(Convert.ChangeType(value, scalar, CultureInfo.InvariantCulture));
        return true;
    }

    private static string? ColorHex(IOperation? op)
    {
        op = Unwrap(op);
        System.Drawing.Color? color = op switch
        {
            null or IDefaultValueOperation => default(System.Drawing.Color),
            IPropertyReferenceOperation { Property: { IsStatic: true } p } when IsColor(p.ContainingType) =>
                typeof(System.Drawing.Color).GetProperty(p.Name)?.GetValue(null) as System.Drawing.Color?,
            IInvocationOperation { TargetMethod: { Name: "FromArgb", IsStatic: true } m, Arguments: { Length: 3 or 4 } args } when IsColor(m.ContainingType) &&
                args.All(a => a.Parameter?.Type.SpecialType == SpecialType.System_Int32 && a.Value.ConstantValue is { HasValue: true, Value: int v } && v is >= 0 and <= 255) =>
                args.Length == 3
                    ? System.Drawing.Color.FromArgb((int)args[0].Value.ConstantValue.Value!, (int)args[1].Value.ConstantValue.Value!, (int)args[2].Value.ConstantValue.Value!)
                    : System.Drawing.Color.FromArgb((int)args[0].Value.ConstantValue.Value!, (int)args[1].Value.ConstantValue.Value!, (int)args[2].Value.ConstantValue.Value!, (int)args[3].Value.ConstantValue.Value!),
            _ => null
        };
        return color is { } c ? Hex(c) : null;
    }

    internal static string Hex(System.Drawing.Color c) => c.A == 255 ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : $"#{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}";

    private static ulong Bits(object? constant) => constant switch
    {
        null => 0, ulong u => u, uint u => u, ushort u => u, byte b => b,
        _ => unchecked((ulong)Convert.ToInt64(constant, CultureInfo.InvariantCulture))
    };

    // The names a flag enum's Inspector checks: the non-zero members.
    private static string[] FlagNames(INamedTypeSymbol enumeration) =>
        enumeration.GetMembers().OfType<IFieldSymbol>().Where(m => m.HasConstantValue && Bits(m.ConstantValue) != 0).Select(m => m.Name).ToArray();

    // An enum constant as its declared name, or a flag value as its names ("Fire, Ice"; "" when no flag is set).
    private static string? EnumText(INamedTypeSymbol enumeration, object? constant)
    {
        var members = enumeration.GetMembers().OfType<IFieldSymbol>().Where(m => m.HasConstantValue).ToArray();
        ulong bits = Bits(constant);
        if (!IsFlags(enumeration)) return members.FirstOrDefault(m => Bits(m.ConstantValue) == bits)?.Name;
        if (bits == 0) return ""; // no flag: the Inspector check list holds the non-zero names only
        var names = new List<string>();
        ulong rest = bits;
        foreach (var m in members)
        {
            ulong flag = Bits(m.ConstantValue);
            if (flag != 0 && (bits & flag) == flag && (rest & flag) != 0) { names.Add(m.Name); rest &= ~flag; }
        }
        return rest == 0 ? string.Join(", ", names) : null; // bits no declared name covers: keep the script's value
    }

    // A struct member's default as JSON: scalars, enums (names), Vector2 ({x,y}) and colours (hex).
    private static bool MemberJson(ITypeSymbol type, IOperation? op, out JsonNode? json)
    {
        json = null;
        if (Scalar(type) is { } scalar) return Constant(op, scalar, out json);
        if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum } e)
        {
            var value = Unwrap(op) is { } o and not IDefaultValueOperation ? o.ConstantValue : new Optional<object?>(0);
            if (!value.HasValue || EnumText(e, value.Value) is not { } text) return false;
            json = JsonValue.Create(text);
            return true;
        }
        if (type.ToDisplayString() == "System.Numerics.Vector2")
        {
            if (VectorDefault(Unwrap(op)) is not { } v) return false;
            json = new JsonObject { ["x"] = v.X, ["y"] = v.Y };
            return true;
        }
        if (IsColor(type) && ColorHex(op) is { } hex) { json = JsonValue.Create(hex); return true; }
        return false;
    }

    private static IEnumerable<IOperation>? Items(IOperation? op)
    {
        switch (Unwrap(op))
        {
            case IArrayCreationOperation { Initializer: { } init }: return init.ElementValues;
            case IArrayInitializerOperation init: return init.ElementValues;
            case IArrayCreationOperation { DimensionSizes: [{ ConstantValue: { HasValue: true, Value: int n } }] } when n is >= 0 and <= 1024:
                return Enumerable.Repeat<IOperation>(null!, n);
            case ICollectionExpressionOperation c when c.Elements.All(e => e is not ISpreadOperation): return c.Elements;
            case IObjectCreationOperation { Arguments.Length: 0 } o:
                var adds = o.Initializer?.Initializers ?? [];
                return adds.All(i => i is IInvocationOperation { TargetMethod.Name: "Add", Arguments.Length: 1 })
                    ? adds.Select(i => ((IInvocationOperation)i).Arguments[0].Value) : null;
            default: return null;
        }
    }

    private static bool IsNull(IOperation? op) => Unwrap(op) is null or IDefaultValueOperation || Unwrap(op)!.ConstantValue is { HasValue: true, Value: null };

    // Adds a 13.8 field; false when the type is not one of them. A supported type whose initializer is not constant adds nothing.
    private static bool AddMoreField(IFieldSymbol field, IOperation? initializer, Action<ScriptField> add, ScriptInspectorInfo info)
    {
        var type = field.Type;
        string Q(string s) => SymbolDisplay.FormatLiteral(s, true);
        void Read(Func<string, string, string> statement) => info.Reads[field.Name] = statement;
        // T[] and List<T> of a scalar.
        var elementType = type is IArrayTypeSymbol { Rank: 1 } arr ? arr.ElementType : type is INamedTypeSymbol { IsGenericType: true } l &&
            l.ConstructedFrom.ToDisplayString() == "System.Collections.Generic.List<T>" ? l.TypeArguments[0] : null;
        if (elementType != null && Scalar(elementType) is { } element)
        {
            JsonNode? json = null;
            if (!IsNull(initializer))
            {
                if (Items(initializer) is not { } items) return true;
                var values = new JsonArray();
                foreach (var item in items)
                {
                    if (!Constant(item, element, out var v)) return true;
                    values.Add(v);
                }
                json = values;
            }
            bool isList = type is not IArrayTypeSymbol;
            add(new(field.Name, typeof(JsonElement), JsonSerializer.SerializeToElement(json, GameJson.Default.JsonNode)) { Element = element, IsList = isList });
            Read((key, member) => $"{{ if (component.TryValue({key}, out var __v)) {member} = {Values}{(isList ? "List" : "Array")}(__v, {member}, static (__e, __d) => {ScalarRead(elementType, "__e", "__d")}); }}");
            return true;
        }
        if (type is INamedTypeSymbol { IsGenericType: true } map && map.ConstructedFrom.ToDisplayString() == "System.Collections.Generic.Dictionary<TKey, TValue>")
        {
            var keyType = map.TypeArguments[0].SpecialType switch { SpecialType.System_String => typeof(string), SpecialType.System_Int32 => typeof(int), _ => null };
            if (keyType == null || Scalar(map.TypeArguments[1]) is not { } value) return false;
            JsonNode? json = null;
            if (!IsNull(initializer))
            {
                if (Unwrap(initializer) is not IObjectCreationOperation { Arguments.Length: 0 } creation) return true;
                var entries = new JsonObject();
                foreach (var entry in creation.Initializer?.Initializers ?? [])
                {
                    (IOperation k, IOperation v) = entry switch
                    {
                        IInvocationOperation { TargetMethod.Name: "Add", Arguments: [var a, var b] } => (a.Value, b.Value),
                        ISimpleAssignmentOperation { Target: IPropertyReferenceOperation { Arguments: [var a] }, Value: var b } => (a.Value, b),
                        _ => (null!, null!)
                    };
                    if (k?.ConstantValue is not { HasValue: true, Value: { } keyValue } || !Constant(v, value, out var item)) return true;
                    string name = Convert.ToString(keyValue, CultureInfo.InvariantCulture)!;
                    if (entries.ContainsKey(name)) return true;
                    entries[name] = item;
                }
                json = entries;
            }
            add(new(field.Name, typeof(JsonElement), JsonSerializer.SerializeToElement(json, GameJson.Default.JsonNode)) { Element = value, Key = keyType });
            string parse = keyType == typeof(int) ? $"{Values}Key32(__k)" : "__k";
            Read((key, member) => $"{{ if (component.TryValue({key}, out var __v)) {member} = {Values}Dictionary(__v, {member}, static __k => {parse}, static (__e, __d) => {ScalarRead(map.TypeArguments[1], "__e", "__d")}); }}");
            return true;
        }
        if (type.SpecialType is SpecialType.System_Int64 or SpecialType.System_UInt32 || IsColor(type))
        {
            ScriptField description;
            if (IsColor(type))
            {
                if (ColorHex(initializer) is not { } hex) return true;
                bool alpha = field.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == typeof(ColorUsageAttribute).FullName) is not { } usage ||
                    usage.ConstructorArguments is not [{ Value: false }];
                description = new(field.Name, typeof(string), hex) { ColorAlpha = alpha };
            }
            else
            {
                var scalar = Scalar(type)!;
                if (!Constant(initializer, scalar, out var json)) return true;
                description = new(field.Name, scalar, Clr(json, scalar));
            }
            add(description);
            Read((key, member) => $"{{ if (component.TryValue({key}, out var __v)) {member} = {ScalarRead(type, "__v", member)}; }}");
            return true;
        }
        if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum } flags && IsFlags(flags))
        {
            var value = Unwrap(initializer) is { } o and not IDefaultValueOperation ? o.ConstantValue : new Optional<object?>(0);
            if (!value.HasValue || EnumText(flags, value.Value) is not { } text) return true;
            var names = FlagNames(flags);
            add(new(field.Name, typeof(string), text, EnumNames: names) { Flags = true });
            Read((key, member) => $"{member} = component.ReadFlags({key}, {member});");
            return true;
        }
        if (type is INamedTypeSymbol { TypeKind: TypeKind.Struct, IsGenericType: false } shape && shape.DeclaringSyntaxReferences.Length > 0 &&
            shape.SpecialType == SpecialType.None)
        {
            if (!shape.IsSerializable && !HasAttribute(shape, typeof(SerializableAttribute))) return false;
            var members = shape.GetMembers().OfType<IFieldSymbol>().Where(m => !m.IsStatic && !m.IsConst && !m.IsReadOnly && !m.IsImplicitlyDeclared &&
                m.DeclaredAccessibility == Accessibility.Public && ScalarRead(m.Type, "", "") != null).ToArray();
            if (members.Length == 0 || shape.InstanceConstructors.Any(c => c.Parameters.Length == 0 && !c.IsImplicitlyDeclared)) return false;
            var assigned = new Dictionary<string, IOperation>(StringComparer.Ordinal);
            switch (Unwrap(initializer))
            {
                case null or IDefaultValueOperation: break;
                case IObjectCreationOperation { Arguments.Length: 0 } creation:
                    foreach (var init in creation.Initializer?.Initializers ?? [])
                        if (init is ISimpleAssignmentOperation { Target: IFieldReferenceOperation target, Value: var v }) assigned[target.Field.Name] = v;
                        else return true;
                    break;
                default: return true;
            }
            var json = new JsonObject();
            var described = new List<ScriptField>();
            foreach (var m in members)
            {
                if (!MemberJson(m.Type, assigned.GetValueOrDefault(m.Name), out var v)) return true;
                json[m.Name] = v;
                described.Add(Describe(m.Name, m.Type, v));
            }
            add(new(field.Name, typeof(JsonElement), JsonSerializer.SerializeToElement(json, GameJson.Default.JsonNode)) { Members = [.. described] });
            Read((key, member) => $"{{ if (component.TryValue({key}, out var __v) && __v.ValueKind == global::System.Text.Json.JsonValueKind.Object) {{ var __s = {member}; global::System.Text.Json.JsonElement __m; " +
                string.Concat(members.Select(m => $"if (__v.TryGetProperty({Q(m.Name)}, out __m)) __s.@{m.Name} = {ScalarRead(m.Type, "__m", "__s.@" + m.Name)}; ")) +
                $"{member} = __s; }} }}");
            return true;
        }
        return false;
    }

    // A struct member as the Inspector sees it (no saved key of its own: the struct's JSON holds it).
    private static ScriptField Describe(string name, ITypeSymbol type, JsonNode? json)
    {
        if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum } e)
            return new(name, typeof(string), json!.GetValue<string>(), EnumNames: IsFlags(e) ? FlagNames(e) : e.GetMembers().OfType<IFieldSymbol>().Where(m => m.HasConstantValue).Select(m => m.Name).ToArray()) { Flags = IsFlags(e) };
        if (type.ToDisplayString() == "System.Numerics.Vector2")
            return new(name, typeof(System.Numerics.Vector2), new System.Numerics.Vector2(json!["x"]!.GetValue<float>(), json["y"]!.GetValue<float>()));
        if (IsColor(type)) return new(name, typeof(string), json!.GetValue<string>()) { ColorAlpha = true };
        var scalar = Scalar(type)!;
        return new(name, scalar, Clr(json, scalar));
    }

    private static object? Clr(JsonNode? json, Type scalar) => json == null ? null : scalar == typeof(int) ? json.GetValue<int>() :
        scalar == typeof(long) ? json.GetValue<long>() : scalar == typeof(uint) ? json.GetValue<uint>() : scalar == typeof(float) ? json.GetValue<float>() :
        scalar == typeof(double) ? json.GetValue<double>() : scalar == typeof(bool) ? json.GetValue<bool>() : json.GetValue<string>();

    private static bool SupportedMore(ITypeSymbol type) =>
        type is IArrayTypeSymbol { Rank: 1 } a && Scalar(a.ElementType) != null || IsColor(type) ||
        type.SpecialType is SpecialType.System_Int64 or SpecialType.System_UInt32 ||
        type is INamedTypeSymbol { IsGenericType: true } g && (g.ConstructedFrom.ToDisplayString() == "System.Collections.Generic.List<T>" && Scalar(g.TypeArguments[0]) != null ||
            g.ConstructedFrom.ToDisplayString() == "System.Collections.Generic.Dictionary<TKey, TValue>" &&
            g.TypeArguments[0].SpecialType is SpecialType.System_String or SpecialType.System_Int32 && Scalar(g.TypeArguments[1]) != null) ||
        type is INamedTypeSymbol { TypeKind: TypeKind.Struct, IsGenericType: false, IsSerializable: true } s && s.DeclaringSyntaxReferences.Length > 0;
}
