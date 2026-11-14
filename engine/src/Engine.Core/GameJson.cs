using System.Text.Json.Serialization;

namespace Engine.Core;

[JsonSourceGenerationOptions(System.Text.Json.JsonSerializerDefaults.Web, WriteIndented = true,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(System.Text.Json.Nodes.JsonNode))]
[JsonSerializable(typeof(GameSettings))]
[JsonSerializable(typeof(SceneDocument))]
[JsonSerializable(typeof(SpriteClip))]
[JsonSerializable(typeof(AssetEntry[]))]
[JsonSerializable(typeof(SceneReference))]
[JsonSerializable(typeof(PrefabReference))]
[JsonSerializable(typeof(DataReference))]
[JsonSerializable(typeof(TableReference))]
[JsonSerializable(typeof(double))]
internal partial class GameJson : JsonSerializerContext;
