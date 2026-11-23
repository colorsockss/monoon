namespace Engine.Core;

public sealed record AssetLink(string Source, string Field, string Target, string? Problem = null);
public sealed class ProjectCatalog
{
    public static string Kind(string path) => Path.GetExtension(path).ToLowerInvariant() switch {
        ".ttf" or ".otf" => "font", ".png" => "sprite", ".wav" or ".ogg" => "audio", ".cs" => "script", "" => "settings", var ext => ext[1..]
    };
    public static string[] Files(string root) => ProjectAssets.Enumerate(root,root)
        .Concat(new ProjectScripts(root).Locations().Values)
        .Concat(File.Exists(Path.Combine(root,GameSettings.FileName)) ? [GameSettings.FileName] : Array.Empty<string>())
        .Order(StringComparer.OrdinalIgnoreCase).ToArray();
    public static IEnumerable<string> Search(IEnumerable<string> paths, string query)
    {
        var terms=query.Split(' ',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        return paths.Where(p=>terms.All(term=>term.StartsWith("t:",StringComparison.OrdinalIgnoreCase)
            ? Kind(p).Equals(term[2..],StringComparison.OrdinalIgnoreCase) || Path.GetExtension(p).TrimStart('.').Equals(term[2..],StringComparison.OrdinalIgnoreCase)
            : p.Contains(term,StringComparison.OrdinalIgnoreCase)));
    }

    private readonly List<AssetLink> links=[];
    public IReadOnlyList<AssetLink> Links => links;
    public IEnumerable<AssetLink> Uses(string path) => links.Where(l=>l.Source==path);
    public IEnumerable<AssetLink> UsedBy(string path) => links.Where(l=>l.Target==path);
    public IEnumerable<AssetLink> Problems => links.Where(l=>l.Problem!=null);

    // Inspect saved documents, including files which are unreachable from the start scene.
    public static ProjectCatalog Inspect(string root)
    {
        var report = new ProjectCatalog();
        var assets = ProjectAssets.Scan(root);
        var byId = assets.Entries.ToDictionary(a=>a.Id,a=>a.Path);
        var scripts = new ProjectScripts(root);
        Dictionary<string,ScriptDefinition>? definitions = null;
        string? compilerError = null;
        var scriptFiles=scripts.Locations();
        string source=GameSettings.FileName;
        void Problem(string field,string target,string message) => report.links.Add(new(source,field,target,message));
        void PathLink(string field,string target,string extension, Func<string, bool>? accepts = null)
        {
            var problem = !(accepts?.Invoke(target) ?? target.EndsWith(extension,StringComparison.OrdinalIgnoreCase)) ? "Expected "+extension :
                !File.Exists(Path.Combine(root,target)) ? "Missing file" : null;
            report.links.Add(new(source,field,target,problem));
        }
        void Id(string field,Guid? id,string extension, Func<string, bool>? accepts = null)
        {
            if (id is not Guid g) return;
            if (byId.TryGetValue(g,out var path)) PathLink(field,path,extension,accepts);
            else Problem(field,g.ToString(),"Unknown asset ID");
        }
        void Objects(SceneDocument scene)
        {
            var objects=scene.Objects.ToDictionary(o=>o.Id);
            foreach (var o in scene.Objects)
            {
                string F(string name)=>o.Name+" / "+name;
                void ObjectRef(string field,Guid id,ScriptField? decl=null)
                {
                    if (!objects.TryGetValue(id,out var found) || decl?.Accepts(found)==false)
                        Problem(F(field),id.ToString(),"Missing object or component");
                }
                Id(F("Prefab"),o.Prefab?.AssetId,ProjectAssets.Prefab);
                if (o.GetComponent<SpriteRendererComponent>() is {} r)
                {
                    if (r.AssetId.HasValue) Id(F("Sprite Renderer.Sprite"),r.AssetId,".png");
                    else if (r.Asset!=null && SpriteAssets.IsReference(r.Asset)) PathLink(F("Sprite Renderer.Sprite"),r.Asset,".png");
                    Id(F("Sprite Renderer.Mask"),r.Mask,".png");
                }
                foreach (var joint in o.Components.OfType<Joint2DComponent>()) if (joint.Connected is {} endpoint) ObjectRef(joint is DistanceJoint2DComponent ? "Distance Joint.Connected" : $"{joint.Kind[..^"Component".Length]}.Connected", endpoint);
                if (o.GetComponent<Camera2DComponent>()?.Follow is {} follow) ObjectRef("Camera.Follow",follow);
                if (o.GetComponent<Tilemap2DComponent>()?.StreamTarget is {} anchor) ObjectRef("Tilemap.StreamTarget",anchor);
                Id(F("Cutscene Sequence"),o.GetComponent<CutsceneDirectorComponent>()?.Sequence,CutsceneSequence.Extension);
                Id(F("Skeleton Rig"),o.GetComponent<SkeletonAnimator2DComponent>()?.Rig,SkeletonRig.Extension);
                Id(F("Sprite Shader"),o.GetComponent<SpriteShader2DComponent>()?.Shader,ShaderClip.Extension);
                Id(F("Tilemap.Tileset"),o.GetComponent<Tilemap2DComponent>()?.Tileset,ProjectAssets.Tileset);
                Id(F("UI Image.Sprite"),o.GetComponent<UiImageComponent>()?.Sprite,".png");
                Id(F("HUD.Image"),o.GetComponent<HudComponent>()?.Image,".png");
                Id(F("Audio Source.Sound"),o.GetComponent<AudioSourceComponent>()?.Sound,"WAV or Ogg Vorbis",AudioFile.IsSupported);
                if (o.GetComponent<SpriteAnimatorComponent>() is {} anim)
                {
                    Id(F("Animator.Controller"),anim.Controller,AnimationController.Extension);
                    Id(F("Animator.Clip"),anim.Clip,ProjectAssets.Clip);
                }
                foreach (var script in o.Components.OfType<ScriptComponent>())
                {
                    var path=$"Scripts/{script.Script}.cs";
                    if (!scriptFiles.TryGetValue(script.Script, out var location)) { Problem(F("Script"),path,"Missing script"); continue; }
                    path = location;
                    PathLink(F("Script"),path,".cs");
                    if (definitions==null && compilerError==null)
                        try { definitions=scripts.Current().Components; }
                        catch (InvalidDataException e) { compilerError=e.Message; }
                    if (compilerError!=null) { Problem(F(script.Script),path,"Cannot inspect fields: "+compilerError); continue; }
                    if (!definitions!.TryGetValue(script.Script,out var def)) { Problem(F("Script"),path,"No matching component"); continue; }
                    ProjectScripts.NormalizeValues(script.Values, def);
                    foreach (var error in script.EventErrors(def, scene.Objects, definitions))
                        Problem(F(script.Script), "Event connection", error);
                    foreach(var field in def.Fields)
                    {
                        var f=F(script.Script+"."+field.Name);
                        var type=field.Type;
                        if (field.AssetExtension is string ext)
                        {
                            try
                            {
                                foreach (var (label,id) in field.AssetIds(script))
                                    if (id != Guid.Empty) Id(F(script.Script+"."+label),id,ext);
                            }
                            catch (InvalidDataException e) { Problem(f,script.Values[field.Name].GetRawText(),e.Message); }
                            continue;
                        }
                        if (field.IsObjectList)
                        {
                            try
                            {
                                var refs = script.ReadReferences(field.Name, true);
                                for (int i = 0; i < refs.Length; i++)
                                    if (refs[i] != Guid.Empty) ObjectRef($"{script.Script}.{field.Name}[{i}]", refs[i]);
                            }
                            catch (InvalidDataException e) { Problem(f,path,e.Message); }
                            continue;
                        }
                        if (type == typeof(Guid) && script.Values.TryGetValue(field.Name, out var stored))
                        {
                            if (stored.ValueKind != System.Text.Json.JsonValueKind.String || !stored.TryGetGuid(out var id))
                                Problem(f,stored.GetRawText(),"Malformed reference");
                            else if (id != Guid.Empty) ObjectRef(script.Script+"."+field.Name,id,field);
                        }
                    }
                }
            }
        }
        foreach (var path in Files(root))
        {
            source=path;
            var file=Path.Combine(root,path);
            try
            {
                switch(Path.GetExtension(path))
                {
                    case ProjectAssets.Scene: case ProjectAssets.Prefab: Objects(SceneDocument.ReadAsset(file)); break;
                    case ProjectAssets.Clip:
                        var frames=SpriteClip.OpenForEditing(file).Frames;
                        for(int i=0;i<frames.Length;i++) if (!frames[i].UseRenderer) Id($"Frame {i+1}",frames[i].Sprite,".png");
                        break;
                    case AnimationController.Extension:
                        var controller=AnimationController.Read(file); controller.Validate(path);
                        foreach(var state in controller.States) Id("State / "+state.Name,state.Clip,ProjectAssets.Clip);
                        break;
                    case ProjectAssets.Tileset: Id("Tileset.Image",Tileset.Load(file).Image,".png"); break;
                    case SpriteAtlas.Extension:
                        var atlas=SpriteAtlas.Load(file); PathLink("Packed image",atlas.ImagePath(),".png");
                        foreach(var entry in atlas.Entries) Id("Source sprite",entry.Sprite,".png");
                        break;
                    case CutsceneSequence.Extension: CutsceneSequence.Load(file); break;
                    case SkeletonRig.Extension: SkeletonRig.Load(file); break;
                    case ShaderClip.Extension: ShaderClip.Load(file); break;
                    case ProjectAssets.Data: GameData.Load(file); break;
                    case GameTable.Extension: GameTable.Load(file); break;
                    case "" when path==GameSettings.FileName:
                        var settings=GameSettings.Load(root); Id("Start scene",settings.StartScene,ProjectAssets.Scene);
                        Id("UI theme", settings.UiTheme, GameTable.Extension);
                        UiTheme.Load(root, assets, settings.UiTheme);
                        if (settings.Localization is {} text)
                        {
                            Id("Translation table", text.Table, GameTable.Extension);
                            void Font(string field, Guid id)
                            {
                                if (byId.TryGetValue(id, out var font) && GameLocalization.IsFont(font)) PathLink(field, font, Path.GetExtension(font));
                                else Problem(field, id.ToString(), "Missing font asset");
                            }
                            if (text.DefaultFont is Guid primary) Font("Default font", primary);
                            foreach (var font in text.FallbackFonts ?? []) Font("Fallback font", font);
                            GameLocalization.Load(root, assets, text);
                        }
                        foreach(var id in settings.IncludedScenes) Id("Included scene",id,ProjectAssets.Scene);
                        break;
                }
            }
            catch(Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or FormatException)
            { Problem("Document",path,e.Message); }
        }
        return report;
    }
}
