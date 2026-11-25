using System.Drawing;
using System.Numerics;

namespace Engine.Core;

public sealed partial class SceneGame : IGameObjects, IGame2D, IGameCameras, IDisposable
{
    // Play mode owns its positions. Stopping it leaves the authored scene intact.
    internal sealed class Body(SceneObject obj, string? project, ProjectAssets? assets)
    {
        public readonly SceneObject Source = obj;
        public readonly Guid Id = obj.Id;
        public Guid? SceneOwner;
        public string? SpawnedFrom;
        public SpawnOrigin? Origin;
        public readonly Camera2DComponent? Camera = obj.GetComponent<Camera2DComponent>();
        public readonly Vector2 Size = new(obj.Width, obj.Height);
        public GameBehaviour[] Behaviours = [];
        public IWorldPointerHandler[] PointerHandlers = [];
        public SpritePool2D? SpritePool;
        public SpriteRendererComponent? Renderer = obj.GetComponent<SpriteRendererComponent>();
        public SceneAppearance Appearance = SceneAppearance.From(obj, project, assets);
        public readonly CutsceneSequence? Sequence = obj.GetComponent<CutsceneDirectorComponent>()?.Sequence is Guid sequence
            ? assets?.LoadSequence(project!, sequence) ?? throw new InvalidDataException($"{obj.Name}: a cutscene requires a project.") : null;
        public readonly PositionTween? Tween = obj.GetComponent<PositionTween2DComponent>() is not {} tween ? null
            : EngineFeatures.Tweens ? new(tween) : throw EngineFeatures.Disabled(EngineFeature.Tweens, obj.Name);
        public readonly Parallax2DComponent? Parallax = obj.GetComponent<Parallax2DComponent>();
        public Vector2 CameraHome; // A camera's first view centre; parallax layers measure its travel from here.
        public readonly PathMotion? Follower = obj.GetComponent<PathFollower2DComponent>() is not {} follower ? null
            : EngineFeatures.Tweens ? new(follower) : throw EngineFeatures.Disabled(EngineFeature.Tweens, obj.Name);
        public readonly LineRenderer? Line = obj.GetComponent<Line2DComponent>() is {} line ? new(line) : null;
        public readonly TrailRenderer? Trail = obj.GetComponent<Trail2DComponent>() is {} trail ? new(trail) : null;
        public readonly WorldTextLabel? Text = obj.GetComponent<Text2DComponent>() is not {} text ? null
            : EngineFeatures.Text ? new(text) : throw EngineFeatures.Disabled(EngineFeature.Text, obj.Name);
        public readonly SkeletonAnimator2DComponent? SkeletonConfig=obj.GetComponent<SkeletonAnimator2DComponent>();
        // Roadmap 15.12: a feature's runtime object is created only behind its switch; a Native AOT game built without the
        // feature rejects the component (only unseen content can have one), so ILC drops the code it would reach.
        public readonly SkeletonPose? Skeleton=obj.GetComponent<SkeletonAnimator2DComponent>()?.Rig is not Guid rig ? null
            : !EngineFeatures.Skeletons ? throw EngineFeatures.Disabled(EngineFeature.Skeletons, obj.Name)
            : new(assets?.LoadRig(project!,rig) ?? throw new InvalidDataException($"{obj.Name}: rig requires a project."));
        public readonly ShaderClip? Shader = obj.GetComponent<SpriteShader2DComponent>()?.Shader is Guid id
            ? assets?.LoadShader(project!,id) ?? throw new InvalidDataException($"{obj.Name}: shader requires a project.") : null;
        public readonly PointLight2DComponent? Light = obj.GetComponent<PointLight2DComponent>() is not {} light ? null
            : EngineFeatures.Lighting ? light : throw EngineFeatures.Disabled(EngineFeature.Lighting, obj.Name);
        public readonly ParticleSystem2D? Particles = obj.GetComponent<ParticleEmitter2DComponent>() is not {} emitter ? null
            : !EngineFeatures.Particles ? throw EngineFeatures.Disabled(EngineFeature.Particles, obj.Name) : new(obj,emitter,
            emitter.Legacy ? emitter.LegacyEffect() : emitter.Particles is Guid fx
                ? assets?.LoadParticles(project!,fx) ?? throw new InvalidDataException($"{obj.Name}: particles require a project.") : new());
        public readonly bool Interface = ScreenUi.IsWidget(obj) || obj.GetComponent<UiCanvasComponent>() != null;
        public TileWorld? Stream;
        public Tilemap2DComponent? Map = obj.GetComponent<Tilemap2DComponent>() is not {} map ? null
            : EngineFeatures.Tilemaps ? map : throw EngineFeatures.Disabled(EngineFeature.Tilemaps, obj.Name);
        public TilePalette? Palette;
        public ParticleTiles? ParticleTiles;
        // Tile query shapes in world space (ScenePhysics.TileShape) and the pose, layout and cell size they were built for.
        public Dictionary<Point,(int Tile, Shape2D? Shape)>? TileShapes;
        public Matrix3x2 TileShapePose;
        public string? TileShapeLayout;
        public TileSize? TileShapeSize;
        public List<(GridNavigation Grid, System.Drawing.Point Origin)>? Grids;
        public string? Sound;
        public readonly AudioSourceComponent? AudioSource=obj.GetComponent<AudioSourceComponent>() is not {} source ? null
            : EngineFeatures.Audio ? source : throw EngineFeatures.Disabled(EngineFeature.Audio, obj.Name);
        public bool AutoStarted;
        public AudioPlayback? Playback;
        public AnimationPlayer? Animation;
        public ComponentPose AppliedPose, BeforeAnimation;
        public SceneAppearance DisplayAppearance
        {
            get
            {
                var result = Appearance;
                if (Animation?.Appearance is {} frame)
                    result = result.Image == null ? frame : result with { Image = frame.Image, Source = frame.Source,
                        Canvas = frame.Canvas, Color = Renderer?.Tint == null ? Color.White : result.Color };
                return AppliedPose.Tint == null && AppliedPose.Opacity == null ? result : result with { Color = AppliedPose.Paint(result.Color) };
            }
        }
        public Action<AnimationMarker>? FrameEvent;
        public readonly Collider2DComponent? ColliderConfig = obj.GetComponent<Collider2DComponent>() is not {} collider ? null
            : EngineFeatures.Physics ? collider : throw EngineFeatures.Disabled(EngineFeature.Physics, obj.Name);
        public bool ColliderEnabled = obj.GetComponent<Collider2DComponent>()?.Enabled != false;
        public long ContactRevision;
        public Shape2D EndShape;
        public RectangleF ContactBounds;
        public int SweepTick; // SceneContacts.cs: the contact sweep keeps its order across ticks.
        public bool InSweep;
        public bool Moved => translations.Count != 0 || StartAngle != Angle;
        public Collider2DComponent? Collider => ColliderEnabled ? ColliderConfig : null;
        // Without the physics feature no object has a collider, and ForObject gives default.
        private Shape2D geometry = EngineFeatures.Physics ? CollisionGeometry.ForObject(obj) : default;
        public readonly Rigidbody2DComponent? Rigid = obj.GetComponent<Rigidbody2DComponent>() is not {} rigid ? null
            : EngineFeatures.Physics ? rigid : throw EngineFeatures.Disabled(EngineFeature.Physics, obj.Name);
        public readonly CharacterMotor2DComponent? Motor = obj.GetComponent<CharacterMotor2DComponent>() is not {} motor ? null
            : EngineFeatures.Physics ? motor : throw EngineFeatures.Disabled(EngineFeature.Physics, obj.Name);
        public readonly Joint2DComponent[] Joints = obj.Components.OfType<Joint2DComponent>().ToArray() is not { Length: > 0 } joints ? []
            : EngineFeatures.Physics ? joints : throw EngineFeatures.Disabled(EngineFeature.Physics, obj.Name);
        // Box2D state (ScenePhysicsWorld.cs): the body this object owns, its exact shape inputs and last synced pose.
        public Brutal.Box2DApi.B2Body Physics;
        public Brutal.Box2DApi.B2Joint?[]? PhysicsJoints;
        public bool PhysicsEnabled;
        public ShapeKey PhysicsKey;
        public List<(Body Member, bool Solid, int Pose)>? PhysicsMembers;
        public Vector2 PhysicsPosition;
        public float PhysicsAngle;
        public int PoseRevision;
        public IReadOnlyList<WalkPoint> WalkPreview=[];
        public Body? MotionOwner, Support;
        public Vector2 GroundNormal = -Vector2.UnitY;
        public ColliderGrid? QueryGrid;
        public int ColliderOrdinal;
        public RenderGrid? RenderGrid;
        public int RenderOrdinal;
        public float Angle { get; private set; }
        public float StartAngle;
        public float? WalkSpeed;
        public Vector2 Velocity;
        public float Spin, InvMass, MinGroundCos;
        public Shape2D ShapeAt(Vector2 origin) => ShapeAt(origin, Angle);
        public Shape2D ShapeAt(Vector2 origin, float angle) => angle == 0 ? geometry.Translated(origin) : Turned(angle).Translated(origin);
        // The last turned shape: contacts, sweeps and queries ask for the same angle several times per tick.
        private float turnedAngle = float.NaN;
        private Shape2D turned;
        private Shape2D Turned(float angle)
        {
            if (BitConverter.SingleToInt32Bits(angle) != BitConverter.SingleToInt32Bits(turnedAngle))
                (turned, turnedAngle) = (EngineFeatures.Physics ? CollisionGeometry.ForObject(Source, MatrixAt(Vector2.Zero, angle)) : default, angle);
            return turned;
        }
        public float AngleAt(float time) => StartAngle + (Angle - StartAngle) * time;
        public bool Active = obj.Active, ActiveSelf = obj.Active, NotifiedActive;
        public bool Spawning; // Rented from a prefab pool during this tick.
        private Matrix3x2 Basis = obj.WorldMatrix;
        public readonly Guid? InitialParent = obj.ParentId;
        public bool EditedBasis;
        public void SetWorldPose(Matrix3x2 matrix, float angle)
        {
            Angle = angle;
            Position = matrix.Translation;
            Basis = angle == 0 ? matrix : matrix * Matrix3x2.CreateRotation(-angle);
            // Same result as ForObject without a collider, minus its component scan (tweens and clip poses call this every tick).
            geometry = default;
            if (EngineFeatures.Physics) if (ColliderConfig != null) geometry = CollisionGeometry.ForObject(Source, Basis);
            turnedAngle = float.NaN;
            PoseRevision++;
            BeginStep();
            QueryGrid?.Update(ColliderOrdinal);
            RenderGrid?.Mark(RenderOrdinal);
        }
        public Matrix3x2 MatrixAt(Vector2 origin) => MatrixAt(origin, Angle);
        public Matrix3x2 MatrixAt(Vector2 origin, float angle)
        {
            var matrix = angle == 0 ? Basis : Basis * Matrix3x2.CreateRotation(angle);
            matrix.Translation = origin;
            return matrix;
        }
        public Vector2 Position = obj.WorldPosition;
        public Vector2 CameraStart, CameraEnd, CameraTarget;
        public float CameraZoomStart, CameraZoomEnd;
        public bool CameraReady, CameraTracked;
        public SplitPair? Split;
        public int Index, SubtreeEnd;
        private Vector2 start = obj.WorldPosition;
        private readonly List<MotionPath> translations = [];
        public Vector2 Displacement;

        public void BeginStep()
        {
            start = Position;
            StartAngle = Angle;
            translations.Clear();
        }

        public void Translate(MotionPath translation, float rotation = 0)
        {
            translations.Add(translation);
            RenderGrid?.Mark(RenderOrdinal);
            Position += translation.End;
            Angle += rotation;
            if (translation.End != Vector2.Zero || rotation != 0) QueryGrid?.Update(ColliderOrdinal);
        }

        public Vector2 At(float time)
        {
            var point = start;
            foreach (var path in translations) point += path.At(time);
            return point;
        }

        public float NextBreak(float time)
        {
            float next = 1;
            foreach (var path in translations) next = Math.Min(next, path.NextBreak(time));
            return next;
        }
    }

    private Body[] bodies = [];
    private Body[] parentFirst = [];
    private Body[] colliders = [];
    private Body[] solids = [];
    private ScriptRuntime? scripts;
    private readonly bool ownsScripts;
    public event Action<Guid, Guid>? TriggerEntered;
    private Body? camera;
    private Body? defaultCamera;
    private Body? requestedCamera;
    private RenderGrid? renderGrid;
    private WorldDrawOrder drawOrder = null!;
    private Matrix3x2 DrawMatrix(int index, float interpolation)
    {
        var body = bodies[index];
        return body.MatrixAt(body.At(interpolation), body.AngleAt(interpolation));
    }
    private Body[] tileLayers = [];
    public event Action<string>? SoundRequested;
    internal void PlayEffect(Guid id)
    {
        var body = bodies.FirstOrDefault(b => b.Active && b.Id == id);
        body?.Animation?.Play();
        if (body?.Sound!=null) Play(id);
    }
    public Guid? ActiveCameraId => ViewCamera?.Id;
    public bool TrySetActive(Guid id)
    {
        var target = bodies.FirstOrDefault(b => b.Id == id && b.Active && b.Camera?.Output == Camera2DComponent.Primary);
        if (target == null || playback != null) return false;
        requestedCamera = target;
        return true;
    }
    private readonly ShaderClip particleSheet = ShaderClip.Puff, lightSheet = ShaderClip.LightFalloff;
    private ParticleTexture? particleTexture;
    private readonly GameSettings settings;
    public Size LogicalSize => settings.Resolution;
    public static readonly Color Background = Color.FromArgb(24, 33, 45);

    public ScreenUi Ui { get; private set; } = null!;
    public SessionState State { get; }
    public SceneGame(SceneDocument scene, ScriptLibrary? library = null, SessionState? state = null)
        : this(scene, library, state, null)
    {
        State.LiveWorld = this;
        State.SceneId = scene.AssetId;
    }

    internal SceneGame(SceneDocument scene, ScriptLibrary? library, SessionState? state,
        Func<ScriptComponent, GameBehaviour>? compiledScripts, ScriptRuntime? sharedScripts = null, ProjectAssets? manifest = null, bool additive = false,
        bool staged = false)
    {
        scripts = sharedScripts;
        ownsScripts = sharedScripts == null;
        State = state ?? new();
        ownsAudio=state==null;
        // Without the audio feature no sound plays, so the mixer is never configured, fed or opened (roadmap 15.12).
        if (EngineFeatures.Audio) if(ownsAudio) Mixer.Configure(scene.Settings);
        world = new SceneDocument { ProjectDirectory = scene.ProjectDirectory, Settings = scene.Settings,
            DefaultCameraId = scene.DefaultCameraId };
        var copies = scene.Objects.ToDictionary(obj => obj.Id, obj => obj.RuntimeCopy());
        foreach (var obj in scene.Objects)
        {
            var copy = copies[obj.Id];
            copy.SetParent(obj.ParentId is Guid parent ? copies[parent] : null);
            world.Objects.Add(copy);
        }
        if (scene.ProjectDirectory is {} root && File.Exists(Path.Combine(root, ".monoon-player")) &&
            File.ReadAllText(Path.Combine(root, ".monoon-player")) == BakedShaderSheets.Marker)
        {
            string directory = Path.Combine(root, BakedShaderSheets.Builtins);
            lightSheet = lightSheet with { SheetPath = Path.Combine(directory, BakedShaderSheets.Light) };
            particleSheet = particleSheet with { SheetPath = Path.Combine(directory, BakedShaderSheets.Particle) };
        }
        this.library = library;
        this.compiledScripts = compiledScripts;
        settings = scene.Settings;
        State.UseInput(settings.Input);
        assets = manifest ?? (scene.ProjectDirectory == null ? null : ProjectAssets.Scan(scene.ProjectDirectory));
        var projectAssets = assets;
        State.AssetPath = id => projectAssets?.PathFor(id) is {} relative && scene.ProjectDirectory is {} folder ? Path.Combine(folder, relative) : null;
        cameraId = scene.Camera?.Id;
        cameraMissing = cameraId == null && !additive && !scene.Cameras.Any() && !scene.IsUiOnly;
        if (staged) return; // The caller builds the bodies over several updates and disposes a partial world.
        try { Build(null); }
        catch { foreach (var stream in ownedWorlds) stream.Dispose(); if (ownsScripts) scripts?.Dispose(); throw; }
    }

    private readonly Guid? cameraId;
    private readonly bool cameraMissing;
    private int built; // Objects with bodies; -1 once the world is complete.
    internal bool IsBuilt => built < 0;

    /// <summary>
    /// Creates bodies in authored order until <paramref name="stop"/> returns true (at least one per call). A later call,
    /// or the same one if time is left, indexes the complete world in one step.
    /// </summary>
    internal bool Build(Func<bool>? stop)
    {
        if (built < 0) return true;
        while (built < world.Objects.Count)
        {
            RegisterBody(CreateBody(world.Objects[built++]));
            if (stop?.Invoke() == true) return false;
        }
        if (cameraId is { } cam) camera = defaultCamera = byId[cam];
        else if (cameraMissing) throw new InvalidOperationException("Add a camera with Scene → Create Camera before playing.");
        Ui = new ScreenUi(world, assets, State.Has, id => byId.TryGetValue(id, out var b) && b.Active, State.UiPreferences??=new(settings.Accessibility));
        Ui.InputModeChanged += Actions.ChangeInputMode;
        Ui.PointerCanceled += CancelWorldPointer;
        Reindex();
        foreach (var b in parentFirst) if (b.Source.ParentId is Guid parent && !byId[parent].Active) b.Active = false; // inactive ancestors
        UpdateTileWorlds();
        RegisterLife(parentFirst);
        built = -1;
        return true;
    }

    /// <summary>Bodies created so far and the total, for loading progress.</summary>
    internal (int Done, int Total) BuildProgress => (built < 0 ? world.Objects.Count : built, world.Objects.Count);

    private Body CreateBody(SceneObject obj)
    {
        var scene = world;
        // obj is this world's own copy. Turned-off components are left out; a collider stays, disabled (SetColliderEnabled).
        foreach (var c in obj.Components)
            if (!c.Enabled && c is not Collider2DComponent) { obj.Components = obj.Components.RemoveAll(x => !x.Enabled && x is not Collider2DComponent); break; }
        var body = new Body(obj, world.ProjectDirectory, assets);
        if (body.Sequence != null) Actions.EnableDialogueBindings();
        if (body.Rigid is {} rigid)
        {
            rigid.Validate(obj.Name);
            body.Velocity = rigid.BodyType == BodyType2D.Static ? default : new(rigid.VelocityX, rigid.VelocityY);
            body.Spin = rigid.BodyType == BodyType2D.Static || rigid.FreezeRotation || body.Motor != null ? 0 : float.DegreesToRadians(rigid.AngularVelocity);
            body.InvMass = rigid.BodyType == BodyType2D.Dynamic ? 1 / rigid.Mass : 0;
            body.MinGroundCos = MathF.Cos(float.DegreesToRadians(body.Motor?.MaxSlope ?? 60));
        }
        string? Resolve(Guid? id, string format, Func<string, bool> accepts)
        {
            if (id == null) return null;
            var path = assets?.PathFor(id.Value);
            if (path == null || !accepts(path) || !File.Exists(Path.Combine(scene.ProjectDirectory!, path)))
                throw new InvalidDataException($"{obj.Name}: missing {format} asset {id}.");
            return Path.Combine(scene.ProjectDirectory!, path);
        }
        if (EngineFeatures.Tilemaps) if (body.Map is { } map)
        {
            if (map.Tileset is Guid id)
            {
                if (scene.ProjectDirectory == null || assets == null) throw new InvalidDataException($"{obj.Name}: missing project for tileset.");
                if (!palettes.TryGetValue(id,out var palette))
                    palettes[id] = palette = TilePalette.Load(scene.ProjectDirectory,assets,id);
                body.Palette = palette;
            }
            if (map.StreamRadius.HasValue)
            {
                body.Stream = new TileWorld(map,body.Palette,scene.ProjectDirectory);
                ownedWorlds.Add(body.Stream);
                body.Map = body.Stream.Map;
            }
            else
            {
                map.ValidateTiles(body.Palette,obj.Name);
                if (body.Palette != null) body.Map = map.Resolved(body.Palette);
            }
        }
        if (obj.GetComponent<SpriteAnimatorComponent>() is {} animator && (animator.Controller != null || animator.Clip != null))
        {
            var root = scene.ProjectDirectory ?? throw new InvalidDataException($"{obj.Name}: animator needs a saved project.");
            var config = AnimationPlayer.Configuration(animator,root,assets!)!;
            body.Animation = new(config,root,assets!,imageSizes);
            if (animator.PlayOnStart) body.Animation.Play();
        }
        _ = Resolve(obj.GetComponent<UiImageComponent>()?.Sprite, ".png", SpriteAssets.IsPng);
        body.Sound = Resolve(obj.GetComponent<AudioSourceComponent>()?.Sound, "WAV or Ogg Vorbis", AudioFile.IsSupported);
        var instances = new List<GameBehaviour>();
        foreach (var script in obj.Components.OfType<ScriptComponent>())
        {
            if (compiledScripts != null) instances.Add(compiledScripts(script));
            else if (System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
            {
                scripts ??= new ScriptRuntime(scene.ResolveScripts(library));
                instances.Add(scripts.Create(script));
            }
            else throw new InvalidDataException($"No compiled factory for {script.Script}.");
        }
        body.Behaviours = instances.ToArray();
        body.PointerHandlers = instances.OfType<IWorldPointerHandler>().ToArray();
        BindFrameEvents(body);
        return body;
    }

    private void BindFrameEvents(Body body)
    {
        if (body.Animation != null && body.Behaviours.Length > 0)
            body.FrameEvent = marker =>
            {
                var frame = new ScriptAnimationEvent(body.Id,marker,State,this,Ui);
                foreach (var behaviour in body.Behaviours)
                {
                    if (!body.Active) break;
                    try { behaviour.OnAnimationEvent(frame); }
                    catch (Exception ex) { throw ScriptFailure(behaviour,nameof(GameBehaviour.OnAnimationEvent),ex); }
                }
            };
    }

    internal event Action<IEnumerable<Body>>? PreparingBodies;

    internal void VisitBodyAssets(IEnumerable<Body> items, Action<string> sprite, Action<string> sound, Action<ShaderClip>? shader = null)
    {
        foreach (var b in items)
        {
            if (b.Shader != null) shader?.Invoke(b.Shader);
            if (b.Particles?.Effect.Resolved?.Path is {} particles) sprite(particles);
            else if (b.Particles != null) shader?.Invoke(particleSheet);
            if (b.Light != null) shader?.Invoke(lightSheet);
            if (b.Appearance.Mask is {} mask) sprite(mask);
            if (b.Appearance.Image is {} image) sprite(image);
            if (b.Animation != null) foreach (var frame in b.Animation.Images) sprite(frame);
            if (b.Palette != null) sprite(b.Palette.Image);
            if (b.Source.GetComponent<UiImageComponent>()?.Sprite is Guid id && assets?.PathFor(id) is {} uiImage)
                sprite(Path.Combine(world.ProjectDirectory!, uiImage));
            foreach (var icon in b.Source.GetComponent<UiTextComponent>()?.Icons?.Values ?? Enumerable.Empty<Guid>())
                if (assets?.PathFor(icon) is {} png) sprite(Path.Combine(world.ProjectDirectory!, png));
            if (b.Sound != null) sound(b.Sound);
        }
    }

    public void PrepareAssets(Action<string> sprite, Action<string> sound, Action<ShaderClip>? shader = null)
    {
        var sprites = new HashSet<string>(StringComparer.Ordinal);
        var sounds = new HashSet<string>(StringComparer.Ordinal);
        VisitBodyAssets(bodies, p => { if (sprites.Add(p)) sprite(p); }, p => { if (sounds.Add(p)) sound(p); }, shader);
    }

    internal void Deactivate(Guid id) => SetActive(id, false);

    private ReadOnlySpan<Body> Subtree(Body body) => parentFirst.AsSpan(body.Index, body.SubtreeEnd - body.Index);

    public GameTime Time => State.Time;
    public InputActions Actions => State.UseInput(settings.Input);
    public InputBindings Bindings => settings.Input;
    public void FixedUpdate(Vector2 moveDirection, float seconds) => FixedUpdate(new GameInput(moveDirection, false), seconds);

    public void FixedUpdate(GameInput input, float seconds)
    {
        int revision = Ui.InputModeRevision;
        UpdateUi(input, seconds);
        Simulate(revision == Ui.InputModeRevision ? input : default, seconds);
    }

    private void DispatchControls(int revision)
    {
        Ui.Dispatch(id => NotifyControl(id, null), (id, value) => NotifyControl(id, value), dropHandler ??= NotifyDrop);
        void NotifyControl(Guid id, float? value)
        {
            var button = Array.Find(bodies, b => b.Id == id)!;
            using var scope = EnterScene(button);
            foreach (var behaviour in button.Behaviours)
            {
                if (!button.Active || revision != Ui.InputModeRevision) break;
                using var sample = FrameProfiler.Script(behaviour, value.HasValue ? nameof(GameBehaviour.OnUiValueChanged) : nameof(GameBehaviour.OnUiClick));
                try
                {
                    if (value.HasValue) behaviour.OnUiValueChanged(new(id, value.Value, State, Ui, this));
                    else behaviour.OnUiClick(new UiClick(id, State, Ui, this));
                }
                catch (Exception ex) { throw ScriptFailure(behaviour, value.HasValue ? nameof(GameBehaviour.OnUiValueChanged) : nameof(GameBehaviour.OnUiClick), ex); }
            }
        }
    }

    public void UpdateUi(GameInput input, double seconds)
    {
        ObjectDisposedException.ThrowIf(closing,this);
        int revision = Ui.InputModeRevision;
        Ui.ValidateInputModes();
        if (Time.IsPaused) CancelWorldPointer();
        Time.AdvanceUi(seconds);
        Ui.AdvanceReveal(seconds);
        started = true;
        FlushLife();
        UpdateDialogue(input);
        if (modalFrame) input = default;
        // The click callbacks capture locals; build them only on ticks that have control events.
        using (FrameProfiler.Sample(FrameProfiler.UiInput)) if (Ui.HasControlEvents) DispatchControls(revision);
        foreach (var body in parentFirst)
        {
            using var scope = EnterScene(body);
            foreach (var behaviour in body.Behaviours)
            {
                if (!body.Active) break;
                using var sample = FrameProfiler.Script(behaviour, nameof(GameBehaviour.UpdateUi));
                try { behaviour.UpdateUi(new UiTick(body.Id, State, Ui, RoutedInput(body, input, revision), seconds)); }
                catch (Exception ex) { throw ScriptFailure(behaviour, nameof(GameBehaviour.UpdateUi), ex); }
            }
        }
        StepRoutines(ui: true);
        if (Time.IsPaused) { CommitChanges(); SyncAudio(); }
    }

    private GameInput RoutedInput(Body body, GameInput input, int revision) =>
        !modalFrame && playback == null && revision == Ui.InputModeRevision && Ui.ReceivesInput(body.Id) ? input : default;

    public void Simulate(GameInput input, float seconds)
    {
        ObjectDisposedException.ThrowIf(closing,this);
        int revision = Ui.InputModeRevision;
        Ui.ValidateInputModes();
        Time.AdvanceGame(seconds);
        started = true;
        using (FrameProfiler.Sample(FrameProfiler.TileStreaming)) UpdateTileWorlds();
        FlushLife();
        foreach (var body in bodies) body.BeginStep();
        foreach (var body in parentFirst)
        {
            if (!body.Active || body.Spawning) continue;
            body.Displacement = Vector2.Zero;
            using var scope = EnterScene(body);
            foreach (var behaviour in body.Behaviours)
            {
                if (!body.Active) break;
                using var sample = FrameProfiler.Script(behaviour, nameof(GameBehaviour.FixedUpdate));
                try { behaviour.FixedUpdate(new ScriptTick(this, body, RoutedInput(body, input, revision), seconds)); }
                catch (Exception ex) { throw ScriptFailure(behaviour, nameof(GameBehaviour.FixedUpdate), ex); }
            }
            if (body.Active && body.Animation is {} animation)
            {
                using var sample = FrameProfiler.Sample(FrameProfiler.Animation);
                animation.Step(seconds,body.FrameEvent);
                ApplyAnimationPose(body);
            }
            if (body.Active && body.Tween is {Playing:true} tween)
            {
                var parent = body.Source.ParentId is Guid id ? byId[id] : null;
                body.Displacement+=tween.Advance(body.Position,parent?.MatrixAt(parent.Position) ?? Matrix3x2.Identity,seconds);
            }
            if (body.Active && body.Follower is {Playing:true} follower)
            {
                var parent = body.Source.ParentId is Guid id ? byId[id] : null;
                body.Displacement+=follower.Advance(body.Position,parent?.MatrixAt(parent.Position) ?? Matrix3x2.Identity,seconds);
            }
            if (!body.Active || body.Rigid != null || body.Displacement == Vector2.Zero) continue;

            var group = Subtree(body);
            MotionPath translation;
            using (FrameProfiler.Sample(FrameProfiler.Move)) translation = MoveGroup(group, body.Displacement);
            foreach (var member in group) member.Translate(translation);
        }
        if (EngineFeatures.Physics) if (rigidBodies.Length > 0) using (FrameProfiler.Sample(FrameProfiler.Rigidbodies)) StepRigidBodies(seconds);
        if (EngineFeatures.Skeletons) using (FrameProfiler.Sample(FrameProfiler.Animation)) foreach (var body in skeletons) if (body.Active) body.Skeleton!.Step(body,seconds);
        using (FrameProfiler.Sample(FrameProfiler.Effects)) { StepEffects(seconds); StepTrails(); }
        // Contacts need colliders or tile collision, both physics.
        if (EngineFeatures.Physics) using (FrameProfiler.Sample(FrameProfiler.Contacts)) DispatchContacts();
        StepRoutines(ui: false);
        CommitChanges();
        using (FrameProfiler.Sample(FrameProfiler.Cameras)) StepCameras(seconds);
        if (requestedCamera?.Active == true) camera = requestedCamera;
        requestedCamera = null;
        if (camera?.Active != true) camera = defaultCamera?.Active == true ? defaultCamera : bodies.FirstOrDefault(b => b.Active && b.Camera?.Output == Camera2DComponent.Primary);
        FollowPointerCamera();
        using (FrameProfiler.Sample(FrameProfiler.TileStreaming)) UpdateTileWorlds();
        using (FrameProfiler.Sample(FrameProfiler.Audio)) SyncAudio();
    }

    public void Draw(ICanvas2D canvas, float interpolation)
    {
        canvas.UseFonts(Ui.Language.Fonts);
        SetPointerFrame(interpolation);
        PrepareCameraDraw(interpolation);
        screen = State.Screen.Sample(Math.Max(0,Time.GameSeconds-(1-interpolation)*effectStep));
        if (screen.Adjusted) canvas.BeginColorAdjust();
        canvas.Clear(Background);
        using (FrameProfiler.Sample(FrameProfiler.RenderWorld))
        {
            if (ViewCamera is { Active:true } lens) DrawCamera(canvas,lens,interpolation);
            // A cutscene (text feature) replaces the extra views with its dialogue.
            if (!EngineFeatures.Text) DrawOverlays(canvas,interpolation);
            else if (playback is {} p) DrawDialogue(canvas,p);
            else DrawOverlays(canvas,interpolation);
        }
        if (screen.Adjusted) canvas.EndColorAdjust(screen.Multiply,screen.Lift);
        if (playback == null) using (FrameProfiler.Sample(FrameProfiler.UiDraw)) Ui.Draw(canvas);
        var all = new RectangleF(PointF.Empty, LogicalSize);
        if (screen.Fade.A > 0) canvas.FillRectangle(all,screen.Fade);
        if (screen.Flash.A > 0) canvas.FillRectangle(all,screen.Flash);
        screen = ScreenFrame.None;
    }

    private void DrawOverlays(ICanvas2D canvas, float interpolation)
    {
        foreach (var extra in overlays) if (extra.Active && !Merged(extra,interpolation)) DrawCamera(canvas,extra,interpolation);
    }

    private ScreenFrame screen = ScreenFrame.None;

    internal bool UseRenderGrid { get; set; } = true;

    internal void PrepareCameraDraw(float interpolation)
    {
        drawOrder.Sample(interpolation);
        if (UseRenderGrid) renderGrid?.Sample(interpolation);
    }

    internal int DrawCamera(ICanvas2D canvas, Guid cameraId, float interpolation, List<Guid>? spriteOrder = null)
        => DrawCamera(canvas, byId[cameraId], interpolation, spriteOrder);

    private int DrawCamera(ICanvas2D canvas, Body lens, float interpolation, List<Guid>? spriteOrder = null)
    {
        var shot=Sample(lens,interpolation);
        double effectTime=Math.Max(0,Time.GameSeconds-(1-interpolation)*effectStep);
        var own=State.Screen.Sample(lens.Id,effectTime);
        if (screen.Moves && lens.Camera!.Output!=Camera2DComponent.Minimap) shot=ScreenEffects.Move(shot,screen);
        if (own.Moves) shot=ScreenEffects.Move(shot,own);
        var port=shot.Viewport;
        bool full=port.X==0 && port.Y==0 && port.Size==LogicalSize;
        using var clip=full?null:canvas.PushClip(port);
        // An overlaid view replaces what was underneath, including empty areas of its map.
        if (!full || lens!=ViewCamera) canvas.FillRectangle(port,Background);
        var frame=shot.World;
        var view=Matrix3x2.CreateTranslation(-frame.X,-frame.Y)*Matrix3x2.CreateScale(shot.Zoom)*Matrix3x2.CreateTranslation(port.X,port.Y);
        var ranks = UseRenderGrid ? renderGrid?.Ranks(frame, drawOrder) : null;
        int count = ranks?.Count ?? drawOrder.Items.Length;
        var global = EngineFeatures.Lighting ? GlobalLight() : null;
        bool unlit = global == null;
        for (int i = 0; i < count; i++)
        {
            var item = drawOrder.Items[ranks == null ? i : ranks[i]];
            if (EngineFeatures.Lighting) if (!unlit && item.Layer >= global!.UnlitLayer) { DrawLighting(canvas,global,port,frame,view,interpolation,effectTime); unlit = true; }
            var body = bodies[item.Index];
            if (!body.Active || body.Interface) continue;
            var matrix = DrawMatrix(item.Index, interpolation);
            if (item.Map != null)
            {
                if (EngineFeatures.Tilemaps) if (body.Palette != null) body.Map!.Draw(canvas,body.Palette,matrix,frame,view,effectTime);
                continue;
            }
            if (body.Parallax != null) { DrawParallax(canvas, body, lens, shot, matrix, view); continue; }
            var local = new RectangleF(0,0,body.Size.X,body.Size.Y);
            body.SpritePool?.Draw(canvas, frame, view, interpolation);
            body.Trail?.Draw(canvas, TrailCenter(body, matrix), effectTime, view);
            if (frame.IntersectsWith(Affine2D.Bounds(local,matrix)))
            {
                if (spriteOrder != null && body.Renderer != null) spriteOrder.Add(body.Id);
                if (body.SpritePool == null) body.DisplayAppearance.Draw(canvas, local, matrix * view);
                if (body.Shader is {} shader) canvas.DrawShader(shader,shader.FrameAt(effectTime),local,matrix*view,Color.White,false);
            }
            body.Line?.Draw(canvas, matrix, view);
            if (EngineFeatures.Text) body.Text?.Draw(canvas, body.Text.Text(Ui), body.Size, matrix, view, frame);
            if (EngineFeatures.Skeletons) body.Skeleton?.Draw(canvas,matrix,view,frame,interpolation);
            if (EngineFeatures.Particles) body.Particles?.Draw(canvas,particleTexture ??= new(null,particleSheet,[particleSheet.Region(0)]),frame,matrix,view,effectTime);
        }
        if (EngineFeatures.Lighting) if (!unlit) DrawLighting(canvas,global!,port,frame,view,interpolation,effectTime);
        if (EngineFeatures.Lighting) if (global == null) foreach (var b in lights) if (b.Active)
        {
            var center=Affine2D.Transform(b.Size*.5f,b.MatrixAt(b.At(interpolation),b.AngleAt(interpolation)));
            b.Light!.Draw(canvas,lightSheet,center,frame,view,effectTime);
        }
        if(Diagnostics is {} debug && canvas is IDiagnosticLines lines) DrawDiagnostics(lines,debug,shot,interpolation);
        if (ScriptDebug.IsSupported && lens.Camera!.Output!=Camera2DComponent.Minimap) DebugShapes.Draw(this,canvas,shot);
        if (lens.Camera is { Output:Camera2DComponent.Minimap, ShowViews:true })
        {
            if (camera?.Active==true) OutlineView(camera,Color.White);
            foreach (var extra in overlays)
                if (extra.Active && extra.Camera!.Output==Camera2DComponent.Extra && !Merged(extra,interpolation)) OutlineView(extra,Color.LightSkyBlue);
        }
        // This camera's own colour adjustment, fade and flash stay inside its viewport, under the HUD.
        if (own.Adjusted) canvas.AdjustRectangle(port,own.Multiply,own.Lift);
        if (own.Fade.A > 0) canvas.FillRectangle(port,own.Fade);
        if (own.Flash.A > 0) canvas.FillRectangle(port,own.Flash);
        return count;

        void OutlineView(Body other,Color color)
        {
            var f=Sample(other,interpolation).World;
            var p=shot.WorldToScreen(new(f.Left,f.Top));
            float w=f.Width*shot.Zoom,h=f.Height*shot.Zoom;
            canvas.FillRectangle(new(p.X,p.Y,w,1),color); canvas.FillRectangle(new(p.X,p.Y+h-1,w,1),color);
            canvas.FillRectangle(new(p.X,p.Y,1,h),color); canvas.FillRectangle(new(p.X+w-1,p.Y,1,h),color);
        }
    }

    private static InvalidOperationException ScriptFailure(GameBehaviour behaviour, string method, Exception ex) =>
        new($"{behaviour.GetType().Name}.{method}: {ex.Message}", ex);

}
