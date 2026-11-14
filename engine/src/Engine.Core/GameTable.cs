using System.Text.Json;
using VYaml.Annotations;

namespace Engine.Core;

public readonly record struct TableReference(Guid Id);
[YamlObject] public sealed partial record TableColumn(string Name, string Type);
[YamlObject] public sealed partial record TableRow(string Key, JsonElement[] Cells);

// Order is part of the document: moving/renaming a column never reorders a dictionary.
[YamlObject]
public sealed partial record GameTable(int Version, TableColumn[] Columns, TableRow[] Rows)
{
    public const string Extension = ".table";
    public static readonly IReadOnlyList<string> Types = Array.AsReadOnly(new[]{"Text","Number","Boolean"});

    public GameTable Validate()
    {
        if (Version != 1 || Columns is not {Length: > 0} || Rows == null)
            throw new InvalidDataException("A version 1 table needs at least one column and a rows list.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var col in Columns)
            if (col == null || string.IsNullOrWhiteSpace(col.Name) || !names.Add(col.Name) || !Types.Contains(col.Type))
                throw new InvalidDataException("Each column needs a unique name and a supported type.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in Rows)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.Key) || !keys.Add(row.Key) || row.Cells?.Length != Columns.Length)
                throw new InvalidDataException("Each row needs a unique key and one cell per column.");
            for (int i=0; i<Columns.Length; i++)
            {
                var v = row.Cells[i];
                bool ok = Columns[i].Type switch {
                    "Text" => v.ValueKind == JsonValueKind.String,
                    "Boolean" => v.ValueKind is JsonValueKind.True or JsonValueKind.False,
                    _ => v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var n) && double.IsFinite(n)
                };
                if (!ok) throw new InvalidDataException($"{row.Key} / {Columns[i].Name}: expected {Columns[i].Type}.");
            }
        }
        return this;
    }

    public GameData Row(string key)
    {
        var row = Rows.FirstOrDefault(r => r.Key == key) ?? throw new KeyNotFoundException($"Unknown table row: {key}.");
        return new GameData { Values = Columns.Select((c,i)=>(c.Name, row.Cells[i])).ToDictionary() };
    }
    public static GameTable Parse(string yaml)
    {
        try { return ProjectYaml.Decode<GameTable>(System.Text.Encoding.UTF8.GetBytes(yaml)).Validate(); }
        catch (Exception e) when(e is VYaml.YamlException or FormatException or ArgumentException) { throw new InvalidDataException(e.Message,e); }
    }
    public static GameTable Load(string path) => ProjectYaml.Read<GameTable>(path).Validate();
    public void Save(string path, bool overwrite) => ProjectYaml.WriteBytes(path,ProjectYaml.Encode(Validate()),overwrite);
    internal static string AssetPath(ProjectAssets assets, TableReference reference) =>
        assets.PathFor(reference.Id) is {} path && path.EndsWith(Extension, StringComparison.Ordinal) ? path :
        throw new InvalidDataException($"Missing table {reference.Id}.");
}
