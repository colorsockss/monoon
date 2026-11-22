using VYaml.Annotations;
using System.Collections.Immutable;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Engine.Core;

[YamlObject]
public sealed partial class SceneDocument
{
    [Obsolete("Use Settings.Width for a project's game resolution.")]
    public const int Width = 800;
    [Obsolete("Use Settings.Height for a project's game resolution.")]
    public const int Height = 450;
    internal const int CurrentVersion = 28;
    internal ScriptLibrary ResolveScripts(ScriptLibrary? library) => library ??
        ScriptLibrary.Open(ProjectDirectory ?? throw new InvalidDataException("Save the project before running scripts."));

    [JsonRequired, YamlRequired] public int SchemaVersion { get; set; } = CurrentVersion;
    [JsonRequired, YamlRequired] public List<SceneObject> Objects { get; set; } = [];
    [JsonIgnore, YamlIgnore] public string? ProjectDirectory { get; set; }

    [JsonIgnore, YamlIgnore] public Guid? AssetId { get; private set; }
    [JsonIgnore, YamlIgnore] public GameSettings Settings { get; set; } = new();
    [JsonIgnore, YamlIgnore] public IEnumerable<SceneObject> Cameras => Objects.Where(obj => obj.GetComponent<Camera2DComponent>() != null);
    private Guid? startCamera;
    public Guid? DefaultCameraId
    {
        get => Camera?.Id;
        set => startCamera = value;
    }
    [JsonIgnore, YamlIgnore] public SceneObject? Camera => PrimaryCameras.FirstOrDefault(obj => obj.Id == startCamera) ?? PrimaryCameras.FirstOrDefault();
    private IEnumerable<SceneObject> PrimaryCameras => Cameras.Where(obj => obj.GetComponent<Camera2DComponent>()!.Output==Camera2DComponent.Primary);
    [JsonIgnore, YamlIgnore] public string? LoadNotice { get; private set; }
    public void NormalizeCamera() => startCamera = Camera?.Id;

    public string CaptureObjects() => System.Text.Encoding.UTF8.GetString(ProjectYaml.Encode(this));

    public void RestoreObjects(string snapshot)
    {
        var restored = ProjectYaml.Decode<SceneDocument>(System.Text.Encoding.UTF8.GetBytes(snapshot));
        restored.Validate();
        Objects = restored.Objects;
        DefaultCameraId = restored.DefaultCameraId;
        NormalizeScriptFields();
    }

    public SceneObject CreateCamera(Vector2 center)
    {
        var camera = new SceneObject { Name = Camera == null ? "Main Camera" : $"Camera {Cameras.Count() + 1}", X = center.X, Y = center.Y };
        camera.AddComponent(new Camera2DComponent());
        Objects.Add(camera);
        return camera;
    }

    public static SceneDocument Empty(GameSettings settings)
    {
        var scene = new SceneDocument { Settings = settings };
        scene.CreateCamera(new Vector2(settings.Width / 2f, settings.Height / 2f));
        return scene;
    }

    private static string ProjectForScene(string path)
    {
        var directory = new FileInfo(path).Directory!;
        for (var folder = directory; folder?.Parent != null; folder = folder.Parent)
            if (folder.Name.Equals("Scenes", StringComparison.OrdinalIgnoreCase)) return folder.Parent.FullName;
        return directory.FullName;
    }

    public static SceneDocument Starter()
    {
        var player = new SceneObject { Name = "Player", X = 380, Y = 205 };
        player.AddComponent(new SpriteRendererComponent { SquareColor = "#63E6C1" });
        var scene = Empty(new GameSettings());
        scene.Objects.Insert(0, player);
        return scene;
    }

    public static SceneDocument Load(string path)
    {
        return Load(path, ProjectAssets.Scan(ProjectForScene(path)));
    }

    // A missing prefab must not lock the author out of the Project panel needed to restore it.
    public static SceneDocument OpenForEditing(string path) => Load(path, ProjectAssets.Scan(ProjectForScene(path)), editing:true);

    internal static SceneDocument Load(string path, ProjectAssets assets,bool editing=false)
    {
        var project = ProjectForScene(path);
        var scene = ProjectYaml.Read<SceneDocument>(path);
        scene.ProjectDirectory = project;
        scene.Settings = GameSettings.Load(project);
        scene.Validate();
        scene.AssetId = assets.IdFor(Path.GetRelativePath(project, path).Replace(Path.DirectorySeparatorChar, '/'));
        // Export already resolved prefab instances against their source assets.
        if (!File.Exists(Path.Combine(project, ".monoon-player")))
        {
            try { scene.RefreshPrefabs(assets); }
            catch (Exception ex) when (editing && ex is (InvalidDataException or FileNotFoundException or DirectoryNotFoundException))
            {
                // Refresh can have updated earlier siblings. Restore the complete saved snapshot.
                scene=ReadAsset(path);scene.ProjectDirectory=project;scene.Settings=GameSettings.Load(project);
                scene.AssetId=assets.IdFor(Path.GetRelativePath(project,path).Replace(Path.DirectorySeparatorChar,'/'));
                scene.LoadNotice=$"Opened saved instances. Project → + → Check Missing References…: {ex.Message}";
            }
        }
        scene.ResolveAssets(assets);
        if (scene.startCamera != scene.DefaultCameraId)
            scene.LoadNotice ??= $"Restored start camera: {scene.Camera?.Name ?? "none"}.";
        scene.NormalizeCamera();
        return scene;
    }

    internal bool MigratedScriptFields { get; set; }

    internal void NormalizeScriptFields()
    {
        if (ProjectDirectory != null) ScriptLibrary.Open(ProjectDirectory).NormalizeFields(this);
    }

    public void Save(string path)
    {
        Validate();
        var project = ProjectForScene(path);
        ScriptLibrary.Open(project).NormalizeFields(this);
        ResolveAssets(ProjectAssets.Scan(project));
        ProjectYaml.Write(path, this);
        MigratedScriptFields = false;
        ProjectDirectory = project;
    }

    public IEnumerable<SceneObject> DrawOrder() => WorldItems().Where(i => i.Map == null).Select(i => i.Owner);

    public static SceneDocument ReadAsset(string path)
    {
        var doc = ProjectYaml.Read<SceneDocument>(path);
        doc.Validate();
        return doc;
    }

    public IEnumerable<(SceneObject Owner, Tilemap2DComponent? Map, int Layer, int Order)> WorldItems()
    {
        var sorted = new WorldDrawOrder(Objects, (index, _) => Objects[index].WorldMatrix);
        sorted.Sample(1);
        foreach (var item in sorted.Items)
            yield return (Objects[item.Index], item.Map, item.Layer, item.Order);
    }

    public void CheckReferences() => CheckReferences(null);
    internal void CheckReferences(ProjectAssets? assets)
    {
        foreach (var obj in Objects)
        {
            if (obj.GetComponent<SpriteRendererComponent>() is { Asset: {} png } sprite && !File.Exists(Path.Combine(ProjectDirectory!, png)) &&
                (assets ??= ProjectAssets.Scan(ProjectDirectory!)).Sprites(ProjectDirectory!).Find(sprite.AssetId) == null)
                throw new InvalidDataException($"{obj.Name}: missing sprite {png}.");
            if (obj.Prefab is { } prefab && ((assets ??= ProjectAssets.Scan(ProjectDirectory!)).PathFor(prefab.AssetId) is not { } source || !File.Exists(Path.Combine(ProjectDirectory!, source))))
                throw new InvalidDataException($"{obj.Name}: missing prefab {prefab.AssetId}.");
        }
    }

    // Read the former HUD only at the file boundary. There is no legacy paint path.
    private void UpgradeHud()
    {
        foreach (var obj in Objects)
        {
            if (obj.GetComponent<HudComponent>() is not { } hud) continue;
            obj.RemoveComponent(hud);
            obj.AddComponent(new UiRectComponent { ShowIf = hud.RequiredFlag });
            if (hud.Image.HasValue) obj.AddComponent(new UiImageComponent { Sprite = hud.Image });
            obj.AddComponent(new UiTextComponent { Text = hud.Text, InsetX = hud.Image.HasValue ? obj.Width + 6 : 0 });
        }
    }

    private static bool InvalidPin(float? value) => value.HasValue && !float.IsFinite(value.Value);

    internal SceneDocument WithSettings(GameSettings settings)
    {
        var view = (SceneDocument)MemberwiseClone();
        view.Settings = settings;
        return view;
    }

    internal void Validate()
    {
        if (SchemaVersion is < 6 or > CurrentVersion || Objects is null)
            throw new InvalidDataException($"Expected a version {CurrentVersion} scene with an objects list.");
        ResolveHierarchy();
        foreach(var obj in Objects)
            if(obj.Prefab is {} link && (link.Instances==null || link.Instances.Count==0 ||
                !link.Instances.ContainsValue(obj.Id) || link.Instances.Values.Distinct().Count()!=link.Instances.Count ||
                link.Baseline?.Keys.Any(id=>!link.Instances.ContainsKey(id))==true))
                throw new InvalidDataException($"{obj.Name} has an invalid prefab object map.");
        foreach (var script in Objects.SelectMany(o => o.Components).OfType<ScriptComponent>())
            if (script.EventLinks is {} links && (links.Any(l => l == null || string.IsNullOrWhiteSpace(l.Event) ||
                l.Target == Guid.Empty || !ScriptLibrary.ValidReference(l.Script) || string.IsNullOrWhiteSpace(l.Method)) ||
                links.Distinct().Count() != links.Length))
                throw new InvalidDataException($"{script.Script}: invalid or duplicate game event connection.");
        UpgradeHud();
        // Older readers must reject transformed scenes instead of silently flattening them.
        SchemaVersion = Objects.Any(o => o.GetComponent<UiRectComponent>() is { HasPins: true }) ? CurrentVersion :
            Objects.Any(o => o.Tag != null || o.Groups != null) ? 27 :
            Objects.Any(o => o.GetComponent<UiControlComponent>() is UiSliderComponent or UiToggleComponent or UiDropdownComponent) ? 26 :
            Objects.Any(o => o.GetComponent<UiImageComponent>()?.Borders != null || o.GetComponent<UiButtonComponent>()?.Style != null ||
            o.GetComponent<UiRectComponent>()?.ScaleHorizontalWithText == true) ? 25 :
            Objects.SelectMany(o => o.Components).OfType<ScriptComponent>().Any(s => s.EventLinks is { Length: > 0 }) ? 24 :
            Objects.Any(o => o.GetComponent<UiTextComponent>()?.TextKey is { Length: > 0 }) ? 23 :
            Objects.SelectMany(o => o.Components).Any(c => c is SortingGroup2DComponent or
                SpriteRendererComponent { SortByY: true } or SpriteRendererComponent { FootOffset: not 0 }) ? 22 :
            Objects.Any(o => o.GetComponent<CutsceneDirectorComponent>() != null) ? 21 :
            Objects.SelectMany(o=>o.Components).Any(c=>c is PositionTween2DComponent or SkeletonAnimator2DComponent or FootIK2DComponent) ? 20 :
            Objects.SelectMany(o=>o.Components).Any(c=>c is ParticleEmitter2DComponent or PointLight2DComponent or SpriteShader2DComponent) ? 19 :
            Objects.Any(o=>o.GetComponent<NavigationRegion2DComponent>()!=null) ? 18 :
            Objects.Any(o => o.GetComponent<Tilemap2DComponent>()?.StreamRadius != null) ? 17 :
            Objects.SelectMany(o => o.Components).Any(c => c is Rigidbody2DComponent or DistanceJoint2DComponent or CharacterMotor2DComponent) ? 16 :
            Objects.Any(o=>o.Prefab?.Baseline!=null) ? 15 :
            Objects.SelectMany(o=>o.Components).Any(c=>c is UiLayoutComponent or UiScrollComponent or UiTextFieldComponent or UiTextComponent {Translations:not null} or UiTextComponent {Wrap:true}) ? 14 :
            Objects.SelectMany(o=>o.Components).Any(c=>c is AudioListener2DComponent or AudioSourceComponent { Extended:true }) ? 13 :
            Cameras.Any(o=>o.GetComponent<Camera2DComponent>()!.Extended) ? 12 : Objects.SelectMany(o => o.Components).Any(c => c is CircleCollider2DComponent or PolygonCollider2DComponent
            or Collider2DComponent { Layer: not 0 } or Collider2DComponent { Mask: not uint.MaxValue }
            or Tilemap2DComponent { CollisionLayer: not 0 } or Tilemap2DComponent { CollisionMask: not uint.MaxValue })
            ? 11 : Objects.Any(o => o.UsesAffine) ? 10 : 9;

        foreach (var obj in Objects)
        {
            if (string.IsNullOrWhiteSpace(obj.Name)) throw new InvalidDataException("Each object needs a name.");
            if (!float.IsFinite(obj.Rotation) || obj.PivotX is not (>= 0 and <= 1) || obj.PivotY is not (>= 0 and <= 1) ||
                !Matrix3x2.Invert(obj.WorldMatrix, out var inverse) || !Affine2D.Finite(inverse))
                throw new InvalidDataException($"'{obj.Name}' needs an invertible Transform 2D and pivots between 0 and 1.");
            var world = obj.WorldPosition;
            if (!float.IsFinite(obj.X) || !float.IsFinite(obj.Y) ||
                !float.IsFinite(world.X) || !float.IsFinite(world.Y) ||
                !float.IsFinite(obj.Width) || !float.IsFinite(obj.Height) || obj.Width < 1 || obj.Height < 1)
                throw new InvalidDataException($"'{obj.Name}' needs finite coordinates and positive dimensions.");
            var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var component in obj.Components)
            {
                if (component is null || !types.Add(component.Kind))
                    throw new InvalidDataException($"'{obj.Name}' must have one entry per attached component type.");
                if (component is UiControlComponent control) control.Validate(obj.Name);
                if (component is CutsceneDirectorComponent && obj.GetComponent<Camera2DComponent>() is { Output: not Camera2DComponent.Primary })
                    throw new InvalidDataException($"{obj.Name}: a cutscene shot requires a primary camera.");
                if (component is PositionTween2DComponent tween) tween.Validate(obj);
                if (component is SkeletonAnimator2DComponent skeleton) skeleton.Validate(obj.Name);
                if (component is FootIK2DComponent feet) feet.Validate(obj.Name);
                if (component is ParticleEmitter2DComponent emitter) emitter.Validate(obj.Name);
                if (component is PointLight2DComponent light) light.Validate(obj.Name);
                if (component is NavigationRegion2DComponent region) region.Validate(obj.Name);
                if (component is Rigidbody2DComponent rigid) rigid.Validate(obj.Name);
                if (component is DistanceJoint2DComponent joint && (joint.Connected == obj.Id || !float.IsFinite(joint.Length) || joint.Length < 0))
                    throw new InvalidDataException($"{obj.Name}: invalid joint length or self connection.");
                if (component is CharacterMotor2DComponent motor && (motor.MaxSlope is not (>= 0 and < 90) || !float.IsFinite(motor.GroundSnap) || motor.GroundSnap < 0))
                    throw new InvalidDataException($"{obj.Name}: invalid walkable slope or ground snap.");
                if (component is AudioSourceComponent audio) audio.Validate(obj.Name);
                if (component is Collider2DComponent collider)
                    CollisionGeometry.Validate(collider, obj.Name);
                if (component is Tilemap2DComponent tm && (tm.StreamRadius is < 1 or > 16 ||
                    tm.ChunkFiles != null && (tm.StreamRadius == null || tm.OccupiedCells().Any())))
                    throw new InvalidDataException($"{obj.Name}: expected stream radius 1–16 and one source of tile cells.");
                if (component is Tilemap2DComponent { CollisionLayer: < 0 or > 31 })
                    throw new InvalidDataException($"{obj.Name}: collision layer must be 0–31.");
                if (component is Camera2DComponent camera) camera.Validate(obj.Name);
                if (component is SpriteRendererComponent sr && !float.IsFinite(sr.FootOffset) ||
                    component is SortingGroup2DComponent group && !float.IsFinite(group.FootOffset))
                    throw new InvalidDataException($"{obj.Name}: sprite sorting foot offset must be finite.");
                if (component is SpriteRendererComponent sprite && (!SceneObject.TryColor(sprite.SquareColor, out _) || sprite.Tint != null && !SceneObject.TryColor(sprite.Tint,out _)))
                    throw new InvalidDataException($"'{obj.Name}' needs a sprite color in #RRGGBB format.");
                if (component is SpriteRendererComponent { Asset: { } asset } && !SpriteAssets.IsReference(asset))
                    throw new InvalidDataException($"'{obj.Name}' has an invalid PNG asset path: {asset}");
                if (component is Tilemap2DComponent grid && !TileGrid.IsSupported(grid.Layout))
                    throw new InvalidDataException($"{obj.Name}: unknown tile layout '{grid.Layout}'.");
                if (component is Tilemap2DComponent map && map.CellSize is not { Width: > 0, Height: > 0 })
                    throw new InvalidDataException($"{obj.Name}: tile cell size must be positive.");
                if(component is UiLayoutComponent l && (l.Axis is not ("Vertical" or "Horizontal") || l.Gap is not (>=0 and <=16384) || l.Padding is not (>=0 and <=16384)) ||
                    component is UiTextFieldComponent f && (f.MaxLength is <1 or >4096 || f.Value==null || f.Value.Contains('\n')) ||
                    component is UiTextComponent { Translations:{} translations } && translations.Any(p=>string.IsNullOrWhiteSpace(p.Key)||p.Value==null))
                    throw new InvalidDataException($"{obj.Name}: invalid UI layout, text field or translation.");
                if (component is UiImageComponent { Borders: {} edges } panel && (panel.Sprite == null ||
                    edges.Left < 0 || edges.Right < 0 || edges.Top < 0 || edges.Bottom < 0))
                    throw new InvalidDataException($"{obj.Name}: 9-slice needs a PNG and nonnegative borders.");
                if (component is UiRectComponent rect)
                {
                    ScreenUi.AnchorIndex(rect.Anchor);
                    if (InvalidPin(rect.Left) || InvalidPin(rect.Right) || InvalidPin(rect.Top) || InvalidPin(rect.Bottom))
                        throw new InvalidDataException($"{obj.Name}: UI edge offsets must be finite.");
                }
                if (component is UiImageComponent image && !SceneObject.TryColor(image.Color, out _) ||
                    component is UiTextComponent label && (!SceneObject.TryColor(label.Color, out _) || !float.IsFinite(label.Size) ||
                    label.Size is <= 0 or > 1024 || !float.IsFinite(label.InsetX) || !float.IsFinite(label.InsetY)))
                    throw new InvalidDataException($"'{obj.Name}' has an invalid UI color, size or inset.");
                if (component is ScriptComponent script && (!ScriptLibrary.ValidReference(script.Script) || script.Values == null))
                    throw new InvalidDataException($"'{obj.Name}' has an invalid script component.");
            }
        }
    }
}

[YamlObject]
public sealed partial class SceneObject
{
    public SceneObject() { }
    [YamlConstructor] public SceneObject(string name = "GameObject", float width = 40, float height = 40, float scaleX = 1, float scaleY = 1)
        => (Name, Width, Height, ScaleX, ScaleY) = (name, width, height, scaleX, scaleY);

    [JsonRequired, YamlRequired] public Guid Id { get; init; } = Guid.NewGuid();
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ParentId { get; internal set; }
    [JsonIgnore, YamlIgnore] public SceneObject? Parent { get; private set; }

    internal void SetParent(SceneObject? parent)
    {
        Parent = parent;
        ParentId = parent?.Id;
    }

    public bool IsWithin(SceneObject ancestor)
    {
        for (var node = this; node != null; node = node.Parent)
            if (node == ancestor) return true;
        return false;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public PrefabLink? Prefab { get; set; }
    public string Name { get; set; } = "GameObject";
    private string? tag;
    private string[]? groups;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Tag
    {
        get => tag;
        set => tag = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Groups
    {
        get => groups;
        set
        {
            var names = value?.SelectMany(name => (name ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                .Distinct(StringComparer.Ordinal).ToArray();
            groups = names is { Length: > 0 } ? names : null;
        }
    }

    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; } = 40;
    public float Height { get; set; } = 40;
    [JsonInclude, JsonRequired]
    [YamlIgnore]
    public ImmutableArray<SceneComponent> Components { get; internal set; } = [];

    [YamlMember("components"), YamlRequired]
    internal SceneComponent[] StoredComponents { get => Components.ToArray(); set => Components = (value ?? throw new InvalidDataException("Expected components.")).ToImmutableArray(); }

    public T? GetComponent<T>() where T : SceneComponent => Components.OfType<T>().FirstOrDefault();

    public bool AddComponent(SceneComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (Components.Any(existing => StringComparer.OrdinalIgnoreCase.Equals(existing.Kind, component.Kind))) return false;
        Components = Components.Add(component);
        return true;
    }

    public bool RemoveComponent<T>() where T : SceneComponent
    {
        return GetComponent<T>() is { } component && RemoveComponent(component);
    }

    public bool RemoveComponent(SceneComponent component)
    {
        int index = Components.IndexOf(component);
        if (index < 0) return false;
        Components = Components.RemoveAt(index);
        return true;
    }

    [JsonIgnore, YamlIgnore] public RectangleF Bounds => Affine2D.Bounds(new(0, 0, Width, Height), WorldMatrix);

    public static bool TryColor(string? value, out Color color)
    {
        color = default;
        if (value is not { Length: 7 } || value[0] != '#' ||
            !int.TryParse(value.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
            return false;
        color = Color.FromArgb(unchecked((int)0xff000000) | rgb);
        return true;
    }

    public override string ToString() => Name;
}
