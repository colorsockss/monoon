using System.Reflection;
using VYaml.Annotations;

namespace Engine.Core;

[YamlObject]
internal sealed partial record FeatureDecision(EngineFeature Feature, bool Included, string Reason);

// Roadmap 15.11: a game ships SDL_ttf, SDL_mixer and Box2D (and their notices) only for the features it uses.
// The managed BRUTAL bindings stay; only the native files are left out, so nothing loads them unless a feature runs.
// Roadmap 15.12: Native AOT builds also switch each left-out feature's engine code off (NativeAotBuild.FeatureSwitch).
internal static class BuildFeatures
{
    // Native files in the Release template or the Native AOT publish output, per RID naming, and the BRUTAL notice.
    internal static string[] Files(EngineFeature feature) => feature switch
    {
        EngineFeature.Text => ["libSDL3_ttf.dylib", "libSDL3_ttf.so", "SDL3_ttf.dll", "Licenses/BRUTAL/BRUTAL-SDL-TTF-THIRD-PARTY-NOTICES.txt"],
        EngineFeature.Audio => ["libSDL3_mixer.dylib", "libSDL3_mixer.so", "SDL3_mixer.dll", "Licenses/BRUTAL/BRUTAL-SDL-MIXER-THIRD-PARTY-NOTICES.txt"],
        EngineFeature.Physics => ["libbox2d.dylib", "libbox2d.so", "box2d.dll", "Licenses/BRUTAL/BRUTAL-BOX2D-THIRD-PARTY-NOTICES.txt"],
        _ => [] // engine code only (roadmap 15.12): left out of Native AOT players by a feature switch
    };

    internal static EngineFeature? Of(SceneComponent component)
    {
        for (var type = component.GetType(); type != null; type = type.BaseType)
            if (type.GetCustomAttribute<EngineFeatureAttribute>(false) is { } marked) return marked.Feature;
        return null;
    }

    // 'uses' holds the first reason found per feature (forced, scenes and prefabs, GameSettings, then scripts).
    internal static FeatureDecision[] Decide(IReadOnlyDictionary<EngineFeature, string> uses) =>
        Enum.GetValues<EngineFeature>().Select(f => uses.TryGetValue(f, out var why) ? new FeatureDecision(f, true, why)
            : new FeatureDecision(f, false, f switch
            {
                EngineFeature.Text => "no UI text, control, HUD or cutscene, localization font, or script text API",
                EngineFeature.Audio => "no Audio Source, audio asset, or script audio API",
                EngineFeature.Physics => "no collider, rigid body, joint, character motor, tile collision, or script physics API",
                EngineFeature.Tilemaps => "no Tilemap 2D or script tile API",
                EngineFeature.Particles => "no Particle Emitter 2D or script particle API",
                EngineFeature.Lighting => "no Point Light 2D or Global Light 2D",
                EngineFeature.Skeletons => "no Skeleton Animator 2D, Foot IK 2D or script bone API",
                _ => "no Position Tween 2D, Path Follower 2D or PlayTween"
            })).ToArray();

    internal static HashSet<string> Excluded(IEnumerable<FeatureDecision> decisions) =>
        decisions.Where(d => !d.Included).SelectMany(d => Files(d.Feature)).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
