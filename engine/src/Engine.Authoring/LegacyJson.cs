using System.Text.Json.Serialization;

namespace Engine.Core;

// JSON metadata for pre-YAML projects. Only LegacyProject (migration) reads these documents, so the player does not carry it.
// Same options as the runtime GameJson context the documents were written with.
[JsonSourceGenerationOptions(System.Text.Json.JsonSerializerDefaults.Web, WriteIndented = true,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(GameSettings))]
[JsonSerializable(typeof(SceneDocument))]
[JsonSerializable(typeof(SpriteClip))]
[JsonSerializable(typeof(AssetEntry[]))]
[JsonSerializable(typeof(int))]
internal partial class LegacyJson : JsonSerializerContext;
