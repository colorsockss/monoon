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
        public SpritePool2D? SpritePool;
        public SpriteRendererComponent? Renderer = obj.GetComponent<SpriteRendererComponent>();
        public SceneAppearance Appearance = SceneAppearance.From(obj, project, assets);
        public readonly CutsceneSequence? Sequence = obj.GetComponent<CutsceneDirectorComponent>()?.Sequence is Guid sequence
            ? assets?.LoadSequence(project!, sequence) ?? throw new InvalidDataException($"{obj.Name}: a cutscene requires a project.") : null;
        public readonly PositionTween? Tween = obj.GetComponent<PositionTween2DComponent>() is {} tween ? new(tween) : null;
        public readonly SkeletonAnimator2DComponent? SkeletonConfig=obj.GetComponent<SkeletonAnimator2DComponent>();
        public readonly SkeletonPose? Skeleton=obj.GetComponent<SkeletonAnimator2DComponent>()?.Rig is Guid rig
            ? new(assets?.LoadRig(project!,rig) ?? throw new InvalidDataException($"{obj.Name}: rig requires a project.")) : null;
        public readonly FootIK2DComponent? FootIK=obj.GetComponent<FootIK2DComponent>();
        public PhysicsFilter? FootFilter;
        public readonly ShaderClip? Shader = obj.GetComponent<SpriteShader2DComponent>()?.Shader is Guid id
            ? assets?.LoadShader(project!,id) ?? throw new InvalidDataException($"{obj.Name}: shader requires a project.") : null;
        public readonly PointLight2DComponent? Light = obj.GetComponent<PointLight2DComponent>();
        public readonly ParticleBurst? Particles = obj.GetComponent<ParticleEmitter2DComponent>() is {} emitter ? new(obj.Id,emitter) : null;
        public readonly bool Interface = ScreenUi.IsWidget(obj) || obj.GetComponent<UiCanvasComponent>() != null;
        public TileWorld? Stream;
        public Tilemap2DComponent? Map = obj.GetComponent<Tilemap2DComponent>();
        public TilePalette? Palette;
        public string? Sound;
        public readonly AudioSourceComponent? AudioSource=obj.GetComponent<AudioSourceComponent>();
        public bool AutoStarted;
        public AudioPlayback? Playback;
        public AnimationPlayer? Animation;
        public Action<AnimationMarker>? FrameEvent;
        public readonly Collider2DComponent? ColliderConfig = obj.GetComponent<Collider2DComponent>();
        public bool ColliderEnabled = true;
        public long ContactRevision;
        public Shape2D EndShape;
        public RectangleF ContactBounds;
        public bool Moved => translations.Count != 0 || StartAngle != Angle;
        public Collider2DComponent? Collider => ColliderEnabled ? ColliderConfig : null;
        private readonly Shape2D geometry = CollisionGeometry.ForObject(obj);
        public readonly Rigidbody2DComponent? Rigid = obj.GetComponent<Rigidbody2DComponent>();
        public readonly CharacterMotor2DComponent? Motor = obj.GetComponent<CharacterMotor2DComponent>();
        public readonly DistanceJoint2DComponent? Joint = obj.GetComponent<DistanceJoint2DComponent>();
        public IReadOnlyList<WalkPoint> WalkPreview=[];
        public Body? MotionOwner, Support;
        public Vector2 GroundNormal = -Vector2.UnitY;
        public float Angle, StartAngle;
        public float? WalkSpeed;
        public Vector2 Velocity;
        public float Spin, InvMass, InvInertia, MinGroundCos;
        public Shape2D ShapeAt(Vector2 origin) => ShapeAt(origin, Angle);
        public Shape2D ShapeAt(Vector2 origin, float angle) => angle == 0 ? geometry.Translated(origin) :
            CollisionGeometry.ForObject(Source, MatrixAt(Vector2.Zero, angle)).Translated(origin);
        public float AngleAt(float time) => StartAngle + (Angle - StartAngle) * time;
        public bool Active = true, ActiveSelf = true, NotifiedActive;
        public readonly Matrix3x2 Basis = obj.WorldMatrix;
        public Matrix3x2 MatrixAt(Vector2 origin) => MatrixAt(origin, Angle);
        public Matrix3x2 MatrixAt(Vector2 origin, float angle)
        {
            var matrix = angle == 0 ? Basis : Basis * Matrix3x2.CreateRotation(angle);
            matrix.Translation = origin;
            return matrix;
        }
        public Vector2 Position = obj.WorldPosition;
        public Vector2 CameraStart, CameraEnd;
        public bool CameraReady;
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

        public void Translate(MotionPath translation)
        {
            translations.Add(translation);
            Position += translation.End;
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
    private readonly Body? defaultCamera;
    private Body? requestedCamera;
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
    private readonly GameSettings settings;
    public Size LogicalSize => settings.Resolution;
    public static readonly Color Background = Color.FromArgb(24, 33, 45);

    public ScreenUi Ui { get; }
    public SessionState State { get; }
    public SceneGame(SceneDocument scene, ProjectScripts? library = null, SessionState? state = null)
        : this(scene, library, state, null)
    {
        State.LiveWorld = this;
        State.SceneId = scene.AssetId;
    }

    internal SceneGame(SceneDocument scene, ProjectScripts? library, SessionState? state,
        Func<ScriptComponent, GameBehaviour>? compiledScripts, ScriptRuntime? sharedScripts = null, ProjectAssets? manifest = null, bool additive = false)
    {
        scripts = sharedScripts;
        ownsScripts = sharedScripts == null;
        State = state ?? new();
        ownsAudio=state==null;
        if(ownsAudio) Mixer.Levels=scene.Settings.Mixer;
        world = new SceneDocument { ProjectDirectory = scene.ProjectDirectory, Settings = scene.Settings,
            Objects = scene.Objects.ToList(), DefaultCameraId = scene.DefaultCameraId };
        this.library = library;
        this.compiledScripts = compiledScripts;
        settings = scene.Settings;
        State.UseInput(settings.Input);
        assets = manifest ?? (scene.ProjectDirectory == null ? null : ProjectAssets.Scan(scene.ProjectDirectory));
        try
        {
            foreach (var obj in world.Objects) byId.Add(obj.Id, CreateBody(obj));
            if (scene.Camera is { } cam) camera = defaultCamera = byId[cam.Id];
            else if (!additive && !scene.Cameras.Any() && !scene.IsUiOnly) throw new InvalidOperationException("Add a camera with Scene → Create Camera before playing.");
            Ui = new ScreenUi(world, assets, State.Has, id => byId.TryGetValue(id, out var b) && b.Active, State.UiPreferences??=new(settings.Accessibility));
            Ui.InputModeChanged += Actions.ChangeInputMode;
            Reindex();
            UpdateTileWorlds();
            RegisterLife(parentFirst);
        }
        catch { foreach (var stream in ownedWorlds) stream.Dispose(); if (ownsScripts) scripts?.Dispose(); throw; }
    }

    private Body CreateBody(SceneObject obj)
    {
        var scene = world;
        var body = new Body(obj, world.ProjectDirectory, assets);
        if (body.Sequence != null) Actions.EnableDialogueBindings();
        if (body.Rigid is {} rigid)
        {
            rigid.Validate(obj.Name);
            body.Velocity = rigid.BodyType == BodyType2D.Static ? default : new(rigid.VelocityX, rigid.VelocityY);
            body.Spin = rigid.BodyType == BodyType2D.Static || rigid.FreezeRotation || body.Motor != null ? 0 : float.DegreesToRadians(rigid.AngularVelocity);
            body.InvMass = rigid.BodyType == BodyType2D.Dynamic ? 1 / rigid.Mass : 0;
            var bounds = Affine2D.Bounds(new(0,0,obj.Width,obj.Height), obj.WorldMatrix);
            body.InvInertia = rigid.FreezeRotation || body.Motor != null ? 0 :
                12 * body.InvMass / (bounds.Width * bounds.Width + bounds.Height * bounds.Height);
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
        if (body.Map is { } map)
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
            else map.ValidateTiles(body.Palette,obj.Name);
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
            if (b.Particles != null) shader?.Invoke(ShaderClip.Puff);
            if (b.Light != null) shader?.Invoke(ShaderClip.LightFalloff);
            if (b.Appearance.Mask is {} mask) sprite(mask);
            if (b.Appearance.Image is {} image) sprite(image);
            if (b.Animation != null) foreach (var frame in b.Animation.Images) sprite(frame);
            if (b.Palette != null) sprite(b.Palette.Image);
            if (b.Source.GetComponent<UiImageComponent>()?.Sprite is Guid id && assets?.PathFor(id) is {} uiImage)
                sprite(Path.Combine(world.ProjectDirectory!, uiImage));
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

    public void UpdateUi(GameInput input, double seconds)
    {
        ObjectDisposedException.ThrowIf(closing,this);
        int revision = Ui.InputModeRevision;
        Ui.ValidateInputModes();
        Time.AdvanceUi(seconds);
        started = true;
        FlushLife();
        UpdateDialogue(input);
        if (modalFrame) input = default;
        Ui.Dispatch(id =>
        {
            var button = Array.Find(bodies, b => b.Id == id)!;
            using var scope = EnterScene(button);
            foreach (var behaviour in button.Behaviours)
            {
                if (!button.Active || revision != Ui.InputModeRevision) break;
                try { behaviour.OnUiClick(new UiClick(id, State, Ui, this)); }
                catch (Exception ex) { throw ScriptFailure(behaviour, nameof(GameBehaviour.OnUiClick), ex); }
            }
        });
        foreach (var body in parentFirst)
        {
            using var scope = EnterScene(body);
            foreach (var behaviour in body.Behaviours)
            {
                if (!body.Active) break;
                try { behaviour.UpdateUi(new UiTick(body.Id, State, Ui, RoutedInput(body, input, revision), seconds)); }
                catch (Exception ex) { throw ScriptFailure(behaviour, nameof(GameBehaviour.UpdateUi), ex); }
            }
        }
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
        UpdateTileWorlds();
        FlushLife();
        foreach (var body in bodies) body.BeginStep();
        foreach (var body in parentFirst)
        {
            if (!body.Active) continue;
            body.Displacement = Vector2.Zero;
            using var scope = EnterScene(body);
            foreach (var behaviour in body.Behaviours)
            {
                if (!body.Active) break;
                try { behaviour.FixedUpdate(new ScriptTick(this, body, RoutedInput(body, input, revision), seconds)); }
                catch (Exception ex) { throw ScriptFailure(behaviour, nameof(GameBehaviour.FixedUpdate), ex); }
            }
            if (body.Active && body.Animation is {} animation)
                animation.Step(seconds,body.FrameEvent);
            if (body.Active && body.Tween is {Playing:true} tween)
            {
                var parent = body.Source.ParentId is Guid id ? byId[id] : null;
                body.Displacement+=tween.Advance(body.Position,parent?.MatrixAt(parent.Position) ?? Matrix3x2.Identity,seconds);
            }
            if (!body.Active || body.Rigid != null || body.Displacement == Vector2.Zero) continue;

            var group = Subtree(body);
            var translation = MoveGroup(group, body.Displacement);
            foreach (var member in group) member.Translate(translation);
        }
        if (rigidBodies.Length > 0) StepRigidBodies(seconds);
        foreach (var body in skeletons) if (body.Active) body.Skeleton!.Step(this,body,seconds);
        StepEffects(seconds);
        DispatchContacts();
        CommitChanges();
        StepCameras(seconds);
        if (requestedCamera?.Active == true) camera = requestedCamera;
        requestedCamera = null;
        if (camera?.Active != true) camera = defaultCamera?.Active == true ? defaultCamera : bodies.FirstOrDefault(b => b.Active && b.Camera?.Output == Camera2DComponent.Primary);
        UpdateTileWorlds();
        SyncAudio();
    }

    public void Draw(ICanvas2D canvas, float interpolation)
    {
        canvas.UseFonts(Ui.Language.Fonts);
        drawOrder.Sample(interpolation);
        canvas.Clear(Background);
        if (ViewCamera is { Active:true } lens) DrawCamera(canvas,lens,interpolation);
        if (playback is {} p) { DrawDialogue(canvas,p); return; }
        foreach (var extra in overlays) if (extra.Active) DrawCamera(canvas,extra,interpolation);
        Ui.Draw(canvas);
    }

    private void DrawCamera(ICanvas2D canvas,Body lens,float interpolation)
    {
        var shot=Sample(lens,interpolation);
        var port=shot.Viewport;
        bool full=port.X==0 && port.Y==0 && port.Size==LogicalSize;
        using var clip=full?null:canvas.PushClip(port);
        // An overlaid view replaces what was underneath, including empty areas of its map.
        if (!full || lens!=ViewCamera) canvas.FillRectangle(port,Background);
        var frame=shot.World;
        var view=Matrix3x2.CreateTranslation(-frame.X,-frame.Y)*Matrix3x2.CreateScale(shot.Zoom)*Matrix3x2.CreateTranslation(port.X,port.Y);
        double effectTime=Math.Max(0,Time.GameSeconds-(1-interpolation)*effectStep);
        foreach (var item in drawOrder.Items)
        {
            var body = bodies[item.Index];
            if (!body.Active || body.Interface) continue;
            var matrix = DrawMatrix(item.Index, interpolation);
            if (item.Map != null)
            {
                if (body.Palette != null) body.Map!.Draw(canvas,body.Palette,matrix,frame,view);
                continue;
            }
            var local = new RectangleF(0,0,body.Size.X,body.Size.Y);
            body.SpritePool?.Draw(canvas, frame, view, interpolation);
            if (frame.IntersectsWith(Affine2D.Bounds(local,matrix)))
            {
                var appearance = body.Appearance;
                if (body.Animation?.Appearance is {} anim)
                    appearance = appearance.Image == null ? anim : appearance with { Image = anim.Image, Source = anim.Source, Canvas = anim.Canvas,
                        Color = body.Renderer?.Tint == null ? Color.White : appearance.Color };
                if (body.SpritePool == null) appearance.Draw(canvas, local, matrix * view);
                if (body.Shader is {} shader) canvas.DrawShader(shader,shader.FrameAt(effectTime),local,matrix*view,Color.White,false);
            }
            body.Skeleton?.Draw(canvas,matrix,view,frame,interpolation);
        }
        foreach (var b in emitters) if (b.Active) b.Particles!.Draw(canvas,frame,view,effectTime);
        foreach (var b in lights) if (b.Active)
        {
            var center=Vector2.Transform(b.Size*.5f,b.MatrixAt(b.At(interpolation),b.AngleAt(interpolation)));
            b.Light!.Draw(canvas,center,frame,view,effectTime);
        }
        if(Diagnostics is {} debug && canvas is IDiagnosticLines lines) DrawDiagnostics(lines,debug,shot,interpolation);
        if (lens.Camera is { Output:Camera2DComponent.Minimap, ShowViews:true })
        {
            if (camera?.Active==true) OutlineView(camera,Color.White);
            foreach (var extra in overlays)
                if (extra.Active && extra.Camera!.Output==Camera2DComponent.Extra) OutlineView(extra,Color.LightSkyBlue);
        }
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
