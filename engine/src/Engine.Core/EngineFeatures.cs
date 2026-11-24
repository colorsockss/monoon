using System.Diagnostics.CodeAnalysis;

namespace Engine.Core;

/// <summary>Engine features a game build can leave out when nothing it ships uses them. Text, Audio and Physics also drop
/// their native library (SDL_ttf, SDL_mixer, Box2D); in Native AOT every feature also drops its engine code.</summary>
public enum EngineFeature { Text, Audio, Physics, Tilemaps, Particles, Lighting, Skeletons, Tweens }

/// <summary>Components and script API that need a feature. The builder keeps the feature when a shipped scene or prefab has
/// such a component or a project script names such a type or member.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Method, Inherited = false)]
public sealed class EngineFeatureAttribute(EngineFeature feature) : Attribute
{
    public EngineFeature Feature { get; } = feature;
}

internal static class EngineFeatures
{
    // Feature switches (roadmap 15.12). A Native AOT build writes every MoNoon.Feature.<Name> into the game's publish props
    // (false for a feature it left out, true otherwise) with Trim="true", so ILC folds each property to a constant and drops
    // the branches behind a false one and the code only they reach. Same mechanism as MoNoon.ExportedGameOnly. Release and
    // the editor get no switch: the values are read once, because some of these sit on per-object draw paths.
    internal const string SwitchPrefix = "MoNoon.Feature.";
    private static readonly bool[] enabled = [On("MoNoon.Feature.Text"), On("MoNoon.Feature.Audio"), On("MoNoon.Feature.Physics"),
        On("MoNoon.Feature.Tilemaps"), On("MoNoon.Feature.Particles"), On("MoNoon.Feature.Lighting"), On("MoNoon.Feature.Skeletons"),
        On("MoNoon.Feature.Tweens")];
    [FeatureSwitchDefinition("MoNoon.Feature.Text")] internal static bool Text => enabled[0];
    [FeatureSwitchDefinition("MoNoon.Feature.Audio")] internal static bool Audio => enabled[1];
    [FeatureSwitchDefinition("MoNoon.Feature.Physics")] internal static bool Physics => enabled[2];
    [FeatureSwitchDefinition("MoNoon.Feature.Tilemaps")] internal static bool Tilemaps => enabled[3];
    [FeatureSwitchDefinition("MoNoon.Feature.Particles")] internal static bool Particles => enabled[4];
    [FeatureSwitchDefinition("MoNoon.Feature.Lighting")] internal static bool Lighting => enabled[5];
    [FeatureSwitchDefinition("MoNoon.Feature.Skeletons")] internal static bool Skeletons => enabled[6];
    [FeatureSwitchDefinition("MoNoon.Feature.Tweens")] internal static bool Tweens => enabled[7];
    // FeatureSwitchChecks plays scenes in the JIT with features switched off as a Native AOT build would.
    internal static void SetForChecks(EngineFeature feature, bool on) => enabled[(int)feature] = on;
    private static bool On(string name) => !AppContext.TryGetSwitch(name, out bool on) || on;

    internal static string Name(EngineFeature feature) => feature switch
    {
        EngineFeature.Text => "Text",
        EngineFeature.Audio => "Audio",
        EngineFeature.Physics => "Physics",
        EngineFeature.Tilemaps => "Tilemaps",
        EngineFeature.Particles => "Particles",
        EngineFeature.Lighting => "Lighting",
        EngineFeature.Skeletons => "Skeletons",
        _ => "Tweens"
    };

    // A binding loads its library on first P/Invoke or in its type initializer (Brutal.Box2D).
    internal static bool Missing(Exception error) => error is DllNotFoundException or TypeInitializationException { InnerException: DllNotFoundException };

    // Thrown where a feature first loads its library; a missing library otherwise surfaces as a bare DllNotFoundException.
    // Names are spelled out: Enum.ToString would root enum reflection in Native AOT players.
    internal static InvalidOperationException Excluded(EngineFeature feature, Exception missing)
    {
        var (name, setting, library) = feature switch
        {
            EngineFeature.Text => ("text", "Text", "SDL_ttf"),
            EngineFeature.Audio => ("audio", "Audio", "SDL_mixer"),
            _ => ("physics", "Physics", "Box2D")
        };
        return new($"This build excluded {name} ({library}): no shipped scene, prefab or script used it. " +
            $"Add {setting} to GameSettings features and build again to include it.", missing);
    }

    // A scene or prefab the build did not see (a mod, a content update) has a component of a feature this Native AOT player
    // was built without.
    internal static InvalidOperationException Disabled(EngineFeature feature, string owner) =>
        new($"{owner}: this build excluded {Name(feature)}: no shipped scene, prefab or script used it. " +
            $"Add {Name(feature)} to GameSettings features and build again to include it.");
}
