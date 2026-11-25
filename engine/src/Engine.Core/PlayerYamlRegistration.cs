using System.Text.Json;
using VYaml.Serialization;

namespace Engine.Core;

// Native AOT players register the VYaml formatters here instead of the source generator's module initializer, which
// Engine.Player/EngineFeatures.Substitutions.xml empties in Native AOT (roadmap 15.12). The list is the generator's; a line is
// behind the features whose components and asset formats alone use it, so a game built without them constructs none of
// those formatters and ILC drops their Deserialize code and the types they create. FeatureSwitchChecks compares
// this list with the generated one and checks that no other registered type reaches a gated one.
internal static class PlayerYamlRegistration
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Register()
    {
        // JIT (editor, Release players): the generated initializer has registered everything.
        if (System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported) return;
        ActionBinding.__RegisterVYamlFormatter();
        ActionVector.__RegisterVYamlFormatter();
        AnimationCondition.__RegisterVYamlFormatter();
        AnimationController.__RegisterVYamlFormatter();
        AnimationParameter.__RegisterVYamlFormatter();
        AnimationState.__RegisterVYamlFormatter();
        AnimationTransition.__RegisterVYamlFormatter();
        AssetEntry.__RegisterVYamlFormatter();
        AssetMeta.__RegisterVYamlFormatter();
        AtlasEntry.__RegisterVYamlFormatter();
        AudioDucking.__RegisterVYamlFormatter();
        AudioSnapshot.__RegisterVYamlFormatter();
        Camera2DComponent.__RegisterVYamlFormatter();
        CameraRect.__RegisterVYamlFormatter();
        ColliderVertex.__RegisterVYamlFormatter();
        ComponentKey.__RegisterVYamlFormatter();
        ComponentTrack.__RegisterVYamlFormatter();
        CrashCause.__RegisterVYamlFormatter();
        CrashFrame.__RegisterVYamlFormatter();
        GameData.__RegisterVYamlFormatter();
        GameEventLink.__RegisterVYamlFormatter();
        GameSettings.__RegisterVYamlFormatter();
        GameTable.__RegisterVYamlFormatter();
        GridReplay.__RegisterVYamlFormatter();
        GridRunRules.__RegisterVYamlFormatter();
        InputBindings.__RegisterVYamlFormatter();
        InputReplay.__RegisterVYamlFormatter();
        Line2DComponent.__RegisterVYamlFormatter();
        LocalizationSettings.__RegisterVYamlFormatter();
        MixerLevels.__RegisterVYamlFormatter();
        ModAsset.__RegisterVYamlFormatter();
        ModDocument.__RegisterVYamlFormatter();
        NavigationRegion2DComponent.__RegisterVYamlFormatter();
        Parallax2DComponent.__RegisterVYamlFormatter();
        PathPoint.__RegisterVYamlFormatter();
        PlayerCrashReport.__RegisterVYamlFormatter();
        PrefabLink.__RegisterVYamlFormatter();
        RecordedInput.__RegisterVYamlFormatter();
        RecordedTurn.__RegisterVYamlFormatter();
        ReplayInput.__RegisterVYamlFormatter();
        SceneComponent.__RegisterVYamlFormatter();
        SceneDocument.__RegisterVYamlFormatter();
        SceneObject.__RegisterVYamlFormatter();
        ScriptComponent.__RegisterVYamlFormatter();
        ShaderClip.__RegisterVYamlFormatter();
        SheetInput.__RegisterVYamlFormatter();
        SheetSlice.__RegisterVYamlFormatter();
        SortingGroup2DComponent.__RegisterVYamlFormatter();
        SpriteAnimatorComponent.__RegisterVYamlFormatter();
        SpriteAtlas.__RegisterVYamlFormatter();
        SpriteCanvas.__RegisterVYamlFormatter();
        SpriteClip.__RegisterVYamlFormatter();
        SpriteFrame.__RegisterVYamlFormatter();
        SpriteRegion.__RegisterVYamlFormatter();
        SpriteRendererComponent.__RegisterVYamlFormatter();
        SpriteShader2DComponent.__RegisterVYamlFormatter();
        TableColumn.__RegisterVYamlFormatter();
        TableRow.__RegisterVYamlFormatter();
        Trail2DComponent.__RegisterVYamlFormatter();
        UiBorders.__RegisterVYamlFormatter();
        UiCanvasComponent.__RegisterVYamlFormatter();
        UiDefaults.__RegisterVYamlFormatter();
        UiImageComponent.__RegisterVYamlFormatter();
        UiLayoutComponent.__RegisterVYamlFormatter();
        UiRectComponent.__RegisterVYamlFormatter();
        UiScrollComponent.__RegisterVYamlFormatter();
        UpdateCatalog.__RegisterVYamlFormatter();
        GeneratedResolver.Register(new ArrayFormatter<ActionBinding>());
        GeneratedResolver.Register(new ArrayFormatter<ActionVector>());
        GeneratedResolver.Register(new ArrayFormatter<AnimationCondition>());
        GeneratedResolver.Register(new ArrayFormatter<AnimationParameter>());
        GeneratedResolver.Register(new ArrayFormatter<AnimationState>());
        GeneratedResolver.Register(new ArrayFormatter<AnimationTransition>());
        GeneratedResolver.Register(new ArrayFormatter<AssetEntry>());
        GeneratedResolver.Register(new ArrayFormatter<AtlasEntry>());
        GeneratedResolver.Register(new ArrayFormatter<AudioDucking>());
        GeneratedResolver.Register(new ArrayFormatter<AudioSnapshot>());
        GeneratedResolver.Register(new ArrayFormatter<ComponentKey>());
        GeneratedResolver.Register(new ArrayFormatter<ComponentTrack>());
        GeneratedResolver.Register(new ArrayFormatter<CrashCause>());
        GeneratedResolver.Register(new ArrayFormatter<CrashFrame>());
        GeneratedResolver.Register(new ArrayFormatter<GameEventLink>());
        GeneratedResolver.Register(new ArrayFormatter<ModAsset>());
        GeneratedResolver.Register(new ArrayFormatter<PathPoint>());
        GeneratedResolver.Register(new ArrayFormatter<RecordedInput>());
        GeneratedResolver.Register(new ArrayFormatter<RecordedTurn>());
        GeneratedResolver.Register(new ArrayFormatter<SceneComponent>());
        GeneratedResolver.Register(new ArrayFormatter<SheetInput>());
        GeneratedResolver.Register(new ArrayFormatter<SheetSlice>());
        GeneratedResolver.Register(new ArrayFormatter<SpriteFrame>());
        GeneratedResolver.Register(new ArrayFormatter<TableColumn>());
        GeneratedResolver.Register(new ArrayFormatter<TableRow>());
        GeneratedResolver.Register(new ArrayFormatter<Guid>());
        GeneratedResolver.Register(new ArrayFormatter<JsonElement>());
        GeneratedResolver.Register(new ArrayFormatter<long>());
        GeneratedResolver.Register(new ArrayFormatter<string>());
        GeneratedResolver.Register(new DictionaryFormatter<Guid, Guid>());
        GeneratedResolver.Register(new DictionaryFormatter<Guid, JsonElement>());
        GeneratedResolver.Register(new DictionaryFormatter<string, Guid>());
        GeneratedResolver.Register(new DictionaryFormatter<string, JsonElement>());
        GeneratedResolver.Register(new DictionaryFormatter<string, string>());
        GeneratedResolver.Register(new EnumAsStringFormatter<AudioBus>());
        GeneratedResolver.Register(new EnumAsStringFormatter<GridCommand>());
        GeneratedResolver.Register(new EnumAsStringFormatter<ReplayInputKind>());
        GeneratedResolver.Register(new EnumAsStringFormatter<SpriteDrawMode>());
        GeneratedResolver.Register(new ListFormatter<SceneObject>());
        GeneratedResolver.Register(new NullableFormatter<bool>());
        GeneratedResolver.Register(new NullableFormatter<float>());
        GeneratedResolver.Register(new NullableFormatter<SpriteDrawMode>());
        GeneratedResolver.Register(new NullableFormatter<Guid>());
        GeneratedResolver.Register(new NullableFormatter<int>());
        if (EngineFeatures.Text)
        {
            CutsceneDirectorComponent.__RegisterVYamlFormatter();
            CutsceneSequence.__RegisterVYamlFormatter();
            DialogueLine.__RegisterVYamlFormatter();
            HudComponent.__RegisterVYamlFormatter();
            Text2DComponent.__RegisterVYamlFormatter();
            UiButtonComponent.__RegisterVYamlFormatter();
            UiDropdownComponent.__RegisterVYamlFormatter();
            UiSliderComponent.__RegisterVYamlFormatter();
            UiTextComponent.__RegisterVYamlFormatter();
            UiTextFieldComponent.__RegisterVYamlFormatter();
            UiToggleComponent.__RegisterVYamlFormatter();
            GeneratedResolver.Register(new ArrayFormatter<DialogueLine>());
            GeneratedResolver.Register(new EnumAsStringFormatter<TextAlign2D>());
        }
        if (EngineFeatures.Audio)
        {
            AudioListener2DComponent.__RegisterVYamlFormatter();
            AudioSourceComponent.__RegisterVYamlFormatter();
        }
        if (EngineFeatures.Physics)
        {
            BoxCollider2DComponent.__RegisterVYamlFormatter();
            CapsuleCollider2DComponent.__RegisterVYamlFormatter();
            CharacterMotor2DComponent.__RegisterVYamlFormatter();
            CircleCollider2DComponent.__RegisterVYamlFormatter();
            DistanceJoint2DComponent.__RegisterVYamlFormatter();
            EdgeCollider2DComponent.__RegisterVYamlFormatter();
            PolygonCollider2DComponent.__RegisterVYamlFormatter();
            PrismaticJoint2DComponent.__RegisterVYamlFormatter();
            RevoluteJoint2DComponent.__RegisterVYamlFormatter();
            Rigidbody2DComponent.__RegisterVYamlFormatter();
            WeldJoint2DComponent.__RegisterVYamlFormatter();
            GeneratedResolver.Register(new EnumAsStringFormatter<BodyType2D>());
        }
        if (EngineFeatures.Tilemaps)
        {
            TileCell.__RegisterVYamlFormatter();
            TileChunk.__RegisterVYamlFormatter();
            TileChunkRef.__RegisterVYamlFormatter();
            TileCollision.__RegisterVYamlFormatter();
            TileDefinition.__RegisterVYamlFormatter();
            TilePage.__RegisterVYamlFormatter();
            TileSize.__RegisterVYamlFormatter();
            Tilemap2DComponent.__RegisterVYamlFormatter();
            Tileset.__RegisterVYamlFormatter();
            GeneratedResolver.Register(new ArrayFormatter<TileCell>());
            GeneratedResolver.Register(new ArrayFormatter<TileChunk>());
            GeneratedResolver.Register(new ArrayFormatter<TileChunkRef>());
            GeneratedResolver.Register(new ArrayFormatter<TileDefinition>());
        }
        if (EngineFeatures.Particles)
        {
            ParticleAlphaKey.__RegisterVYamlFormatter();
            ParticleBurst.__RegisterVYamlFormatter();
            ParticleCollision.__RegisterVYamlFormatter();
            ParticleColorKey.__RegisterVYamlFormatter();
            ParticleColors.__RegisterVYamlFormatter();
            ParticleCurve.__RegisterVYamlFormatter();
            ParticleEffect.__RegisterVYamlFormatter();
            ParticleEmitter2DComponent.__RegisterVYamlFormatter();
            ParticleGradient.__RegisterVYamlFormatter();
            ParticleKey.__RegisterVYamlFormatter();
            ParticleNoise.__RegisterVYamlFormatter();
            ParticleRange.__RegisterVYamlFormatter();
            ParticleSheet.__RegisterVYamlFormatter();
            ParticleSubEmitter.__RegisterVYamlFormatter();
            GeneratedResolver.Register(new ArrayFormatter<ParticleAlphaKey>());
            GeneratedResolver.Register(new ArrayFormatter<ParticleBurst>());
            GeneratedResolver.Register(new ArrayFormatter<ParticleColorKey>());
            GeneratedResolver.Register(new ArrayFormatter<ParticleKey>());
            GeneratedResolver.Register(new ArrayFormatter<ParticleSubEmitter>());
            GeneratedResolver.Register(new EnumAsStringFormatter<ParticleBlend>());
            GeneratedResolver.Register(new EnumAsStringFormatter<ParticleCollisionMode>());
            GeneratedResolver.Register(new EnumAsStringFormatter<ParticleEvent>());
            GeneratedResolver.Register(new EnumAsStringFormatter<ParticleShape>());
            GeneratedResolver.Register(new EnumAsStringFormatter<ParticleSpace>());
            GeneratedResolver.Register(new EnumAsStringFormatter<ParticleTrigger>());
        }
        if (EngineFeatures.Lighting)
        {
            GlobalLight2DComponent.__RegisterVYamlFormatter();
            PointLight2DComponent.__RegisterVYamlFormatter();
        }
        if (EngineFeatures.Skeletons)
        {
            BoneKey.__RegisterVYamlFormatter();
            RigBone.__RegisterVYamlFormatter();
            SkeletonAnimator2DComponent.__RegisterVYamlFormatter();
            SkeletonRig.__RegisterVYamlFormatter();
            GeneratedResolver.Register(new ArrayFormatter<BoneKey>());
            GeneratedResolver.Register(new ArrayFormatter<RigBone>());
            GeneratedResolver.Register(new EnumAsStringFormatter<SkeletonClock>());
        }
        if (EngineFeatures.Tweens)
        {
            PathFollower2DComponent.__RegisterVYamlFormatter();
            PositionTween2DComponent.__RegisterVYamlFormatter();
            GeneratedResolver.Register(new EnumAsStringFormatter<PathRepeat>());
            GeneratedResolver.Register(new EnumAsStringFormatter<TweenEase>());
            GeneratedResolver.Register(new EnumAsStringFormatter<TweenRepeat>());
        }
        if (EngineFeatures.Physics || EngineFeatures.Tilemaps)
        {
            GeneratedResolver.Register(new ArrayFormatter<ColliderVertex>());
        }
    }
}
