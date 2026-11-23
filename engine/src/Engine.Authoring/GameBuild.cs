using System.Xml.Linq;
using System.Security.Cryptography;

namespace Engine.Core;

public static class GameBuild
{
    public static string DefaultOutputDirectory(string project)
    {
        var directory = new DirectoryInfo(project);
        var parent = directory.Parent ?? directory;
        if (parent.Name == "samples" && parent.Parent is { } workspace &&
            File.Exists(Path.Combine(workspace.FullName, "engine", "MoNoonEngine.slnx")))
            parent = workspace;
        return Path.Combine(parent.FullName, "game-builds", directory.Name);
    }

    public static string FindPlayerTemplate(string baseDirectory, string rid)
    {
        var root = TemplateRoot(baseDirectory);
        var template = Path.Combine(root, rid);
        var executable = PlayerExecutable(rid);
        if (File.Exists(Path.Combine(template, executable))) return template;
        throw new FileNotFoundException($"Missing {rid} player template in {template}. Copy the {rid} template there or run engine/scripts/build-players.sh {rid} from the source checkout.");
    }

    private static string TemplateRoot(string baseDirectory)
    {
        var configured = Environment.GetEnvironmentVariable("MONOON_PLAYERS_DIR");
        return string.IsNullOrWhiteSpace(configured) ? DefaultTemplateRoot(baseDirectory) : Path.GetFullPath(configured);
    }

    private static string DefaultTemplateRoot(string baseDirectory)
    {
        var location = new DirectoryInfo(baseDirectory);
        for (var dir = location; dir?.Parent != null; dir = dir.Parent)
        {
            if (dir.Extension.Equals(".app", StringComparison.OrdinalIgnoreCase))
            {
                location = dir.Parent;
                break;
            }
            if (File.Exists(Path.Combine(dir.FullName, "MoNoonEngine.slnx")))
                return Path.Combine(dir.Parent.FullName, "player-templates");
        }
        var adjacent = Path.Combine(location.FullName, "player-templates");
        if (location.Name == "editor" && location.Parent != null && !Directory.Exists(adjacent))
            return Path.Combine(location.Parent.FullName, "player-templates");
        return adjacent;
    }

    private static string PlayerExecutable(string rid) => rid switch
    {
        "win-x64" => "MoNoonPlayer.exe",
        "osx-arm64" or "linux-x64" => "MoNoonPlayer",
        _ => throw new ArgumentException("Unknown player target.", nameof(rid))
    };

    public static string Export(string project, string template, string rid, string destination, GameSettings settings, Action<ShaderClip>? validateShader = null)
        => Export(project, template, rid, destination, settings, BuildProfile.Distribution, validateShader).Product;

    public static GameBuildResult Export(string project, string template, string rid, string destination, GameSettings settings, BuildProfile profile, Action<ShaderClip>? validateShader = null)
        => Export(project, template, rid, destination, settings, profile, validateShader, null);

    public static GameBuildResult Export(string project, string template, string rid, string destination, GameSettings settings, BuildProfile profile, Action<ShaderClip>? validateShader, Action<ShaderClip, string>? bakeShader)
        => Export(project, template, rid, destination, settings, profile, validateShader, bakeShader, false);

    public static GameBuildResult Export(string project, string template, string rid, string destination, GameSettings settings, BuildProfile profile, Action<ShaderClip>? validateShader, Action<ShaderClip, string>? bakeShader, bool fullContent)
        => ExportCore(project, template, rid, destination, settings, false, profile, validateShader, bakeShader, fullContent);

    public static string ExportNativeAot(string project, string baseDirectory, string rid, string destination, GameSettings settings, Action<ShaderClip>? validateShader = null)
        => ExportNativeAot(project, baseDirectory, rid, destination, settings, BuildProfile.Distribution, validateShader).Product;

    public static GameBuildResult ExportNativeAot(string project, string baseDirectory, string rid, string destination, GameSettings settings, BuildProfile profile, Action<ShaderClip>? validateShader = null)
        => ExportNativeAot(project, baseDirectory, rid, destination, settings, profile, validateShader, null);

    public static GameBuildResult ExportNativeAot(string project, string baseDirectory, string rid, string destination, GameSettings settings, BuildProfile profile, Action<ShaderClip>? validateShader, Action<ShaderClip, string>? bakeShader)
        => ExportNativeAot(project, baseDirectory, rid, destination, settings, profile, validateShader, bakeShader, false);

    public static GameBuildResult ExportNativeAot(string project, string baseDirectory, string rid, string destination, GameSettings settings, BuildProfile profile, Action<ShaderClip>? validateShader, Action<ShaderClip, string>? bakeShader, bool fullContent)
    {
        if (rid != System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier)
            throw new PlatformNotSupportedException($"Build the {rid} Native AOT player on a matching host.");
        var kit = Path.Combine(TemplateRoot(baseDirectory), "native-aot", "src", "Engine.Player", "Engine.Player.csproj");
        if (!File.Exists(kit))
            throw new FileNotFoundException($"Missing Native AOT build kit at {kit}. Run engine/scripts/build-native-aot-kit.sh.");
        return ExportCore(project, kit, rid, destination, settings, true, profile, validateShader, bakeShader, fullContent);
    }

    private static GameBuildResult ExportCore(string project, string template, string rid, string destination, GameSettings settings, bool nativeAot, BuildProfile profile, Action<ShaderClip>? validateShader, Action<ShaderClip, string>? bakeShader, bool fullContent)
    {
        var executable = PlayerExecutable(rid);
        if (!nativeAot && !File.Exists(Path.Combine(template, executable))) throw new FileNotFoundException($"Missing {rid} player template in {template}.");
        if (settings.StartScene is not Guid start || !settings.IncludedScenes.Contains(start))
            throw new InvalidDataException("Include the start scene in the build.");
        var assets = SpriteSheetImporter.Scan(project);
        var sheets = new ShaderSheetBuild();
        bool needsComponentKeys = false;
        var dependencies = new HashSet<string>(StringComparer.Ordinal);
        var audioAssets = new HashSet<Guid>();
        var theme = UiTheme.Load(project, assets, settings.UiTheme, dependencies);
        GameLocalization.Load(project, assets, settings.Localization, dependencies); // adds the string tables and fonts to the build
        var bakedDocuments = new Dictionary<string, SceneDocument>();
        var projectScripts = new ProjectScripts(project);
        var imageSizes = new Dictionary<string, System.Drawing.Size>(StringComparer.Ordinal);
        var palettes = new Dictionary<Guid,TilePalette>();
        void CollectTiles(SceneObject owner)
        {
            if (owner.GetComponent<Tilemap2DComponent>() is not { } map) return;
            TilePalette? palette = null;
            if (map.Tileset is Guid id && !palettes.TryGetValue(id,out palette))
            {
                palettes[id] = palette = TilePalette.Load(project,assets,id);
                dependencies.Add(assets.PathFor(id)!);
                dependencies.Add(Path.GetRelativePath(project,palette.Image).Replace(Path.DirectorySeparatorChar,'/'));
            }
            map.ValidateTiles(palette,owner.Name);
        }
        var checkedPrefabs = new HashSet<Guid>();
        void CollectPrefab(Guid id, ProjectScripts library)
        {
            if (!checkedPrefabs.Add(id)) return;
            var relative = assets.PathFor(id);
            if (relative == null || !relative.EndsWith(ProjectAssets.Prefab)) throw new InvalidDataException($"Missing prefab {id}.");
            try
            {
                var prefab = SceneDocument.ReadResolvedPrefab(project,assets,id);
                if (prefab.Objects.Count(o => o.Parent == null) != 1) throw new InvalidDataException("Expected one prefab root.");
                prefab.ResolveAssets(assets);
                dependencies.Add(relative);
                Collect(prefab, library);
                foreach(var obj in prefab.Objects)obj.Prefab=null;
                bakedDocuments[relative]=prefab;
            }
            catch (Exception ex) { throw new InvalidDataException($"{relative}: {ex.Message}", ex); }
        }
        void Collect(SceneDocument scene, ProjectScripts library)
        {
            foreach (var obj in scene.Objects)
            {
                try
                {
                    void Add(string? path)
                    {
                        if (path == null || !File.Exists(Path.Combine(project, path))) throw new InvalidDataException($"Missing asset {path ?? "(unknown ID)"}.");
                        dependencies.Add(path);
                    }
                    void Asset(Guid? id) { if (id is Guid guid) Add(assets.PathFor(guid)); }
                    bool Pack(Guid? id)
                    {
                        if (assets.Sprites(project).Find(id) is not {} packed) return false;
                        Add(packed.Atlas); Add(packed.Sheet); return true;
                    }
                    if (obj.GetComponent<SpriteRendererComponent>() is {} renderer)
                    {
                        if (renderer.Asset is {} png && !Pack(renderer.AssetId)) Add(png);
                        if (renderer.Mask is Guid mask && !SpriteAssets.IsPng(assets.PathFor(mask) ?? ""))
                            throw new InvalidDataException($"Missing PNG mask {mask}.");
                        Asset(renderer.Mask);
                    }
                    if (obj.Prefab is { } prefabLink) CollectPrefab(prefabLink.AssetId, library);
                    if (obj.GetComponent<CutsceneDirectorComponent>()?.Sequence is Guid sequence)
                    {
                        assets.LoadSequence(project, sequence);
                        Asset(sequence);
                    }
                    if (obj.GetComponent<SkeletonAnimator2DComponent>()?.Rig is Guid rig)
                    {
                        assets.LoadRig(project,rig);
                        Asset(rig);
                    }
                    sheets.Collect(obj, project, assets, validateShader);
                    Asset(obj.GetComponent<SpriteShader2DComponent>()?.Shader);
                    CollectTiles(obj);
                    if (obj.GetComponent<Tilemap2DComponent>() is {StreamRadius: not null, StreamTarget: Guid anchor} && !scene.Objects.Any(o => o.Id == anchor))
                        throw new InvalidDataException($"Missing tile stream target {anchor}.");
                    if (obj.GetComponent<AudioSourceComponent>()?.Sound is Guid sound && audioAssets.Add(sound))
                    {
                        var path = assets.PathFor(sound);
                        if (path == null || !AudioFile.IsSupported(path)) throw new InvalidDataException($"Expected WAV or Ogg Vorbis for audio asset {sound}.");
                        Add(path);
                        AudioFile.Read(Path.Combine(project, path));
                    }
                    if (obj.GetComponent<UiControlComponent>() is {} button) theme.Find(button.Style);
                    if (obj.GetComponent<UiImageComponent>() is { Sprite: Guid uiSprite } image)
                    {
                        if (assets.PathFor(uiSprite) is not { } path || !SpriteAssets.IsPng(path))
                            throw new InvalidDataException($"Missing UI sprite {uiSprite}.");
                        image.Borders?.Validate(SpriteRegion.ReadPngSize(Path.Combine(project, path)));
                        Add(path);
                    }
                    foreach (var (name, icon) in obj.GetComponent<UiTextComponent>()?.Icons ?? [])
                    {
                        if (assets.PathFor(icon) is not { } path || !SpriteAssets.IsPng(path))
                            throw new InvalidDataException($"Missing PNG for text icon '{name}' ({icon}).");
                        Add(path);
                    }
                    if (obj.GetComponent<SpriteAnimatorComponent>() is {} animator &&
                        AnimationPlayer.Configuration(animator,project,assets) is {} controller)
                    {
                        Asset(animator.Controller);
                        foreach (var clip in controller.States.Select(s => s.Clip).Distinct())
                        {
                            Asset(clip);
                            var clipPath = assets.PathFor(clip)!;
                            if (!clipPath.EndsWith(ProjectAssets.Clip)) throw new InvalidDataException($"Expected a clip at {clipPath}.");
                            var animation = SpriteClip.Load(Path.Combine(project,clipPath));
                            needsComponentKeys |= animation.Tracks is { Length: > 0 } || animation.Frames.Any(f => f.UseRenderer);
                            _ = animation.ResolveFrames(clipPath,project,assets,imageSizes);
                            foreach (var frame in animation.Frames)
                                if (!frame.UseRenderer && (frame.Source != null || !Pack(frame.Sprite))) Asset(frame.Sprite);
                        }
                    }
                    foreach (var script in obj.Components.OfType<ScriptComponent>())
                    {
                        var def = library.Get(script.Script);
                        if (script.EventLinks != null && script.EventErrors(def, scene.Objects, library.Current().Components).FirstOrDefault() is string error)
                            throw new InvalidDataException(error);
                        foreach (var field in def.Fields)
                        {
                            if (field.EnumNames != null && script.Values.TryGetValue(field.Name, out var enumValue) &&
                                (enumValue.ValueKind != System.Text.Json.JsonValueKind.String || !field.EnumNames.Contains(enumValue.GetString())))
                                throw new InvalidDataException($"{script.Script}.{field.Name} has an unknown enum value.");
                            if (field.Type == typeof(System.Numerics.Vector2)) _ = field.Value(script);
                            if (field.AssetExtension is string extension)
                                foreach (var (label, id) in field.AssetIds(script))
                                {
                                    if (id == Guid.Empty) continue;
                                    var path = assets.PathFor(id);
                                    if (path == null || !path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                                        throw new InvalidDataException($"{script.Script}.{label}: missing {extension} asset {id}.");
                                    switch (extension)
                                    {
                                        case ".png":
                                            if (!SpriteAssets.IsReference(path))
                                                throw new InvalidDataException($"{script.Script}.{label}: invalid PNG path {path}.");
                                            if (!Pack(id)) Add(path);
                                            break;
                                        case ProjectAssets.Prefab: CollectPrefab(id, library); break;
                                        case ProjectAssets.Scene:
                                            if (!settings.IncludedScenes.Contains(id))
                                                throw new InvalidDataException($"{script.Script}.{label} refers to a scene outside the build.");
                                            break;
                                        case GameTable.Extension: GameTable.Load(Path.Combine(project, path)); Add(path); break;
                                        case ProjectAssets.Data: GameData.Load(Path.Combine(project, path)); Add(path); break;
                                    }
                                }
                            if (field.Type == typeof(Guid) && field.Value(script) is Guid target && target != Guid.Empty && !scene.Objects.Any(o => o.Id == target && field.Accepts(o)))
                                throw new InvalidDataException($"{script.Script}.{field.Name} refers to a missing object or required component on {target}.");
                            if (field.IsObjectList)
                            {
                                var ids = script.ReadReferences(field.Name, true);
                                for (int i = 0; i < ids.Length; i++)
                                    if (ids[i] != Guid.Empty && !scene.Objects.Any(o => o.Id == ids[i]))
                                        throw new InvalidDataException($"{script.Script}.{field.Name}[{i}] refers to a missing object {ids[i]}.");
                            }
                        }
                    }
                }
                catch (Exception ex) { throw new InvalidDataException($"{obj.Name}: {ex.Message}", ex); }
            }
        }
        foreach (var sceneId in settings.IncludedScenes.Distinct())
        {
            var relative = assets.PathFor(sceneId);
            if (relative?.EndsWith(ProjectAssets.Scene) != true) throw new InvalidDataException($"Missing scene {sceneId}.");
            try
            {
                var scene = SceneDocument.Load(Path.Combine(project, relative));
                if (sceneId == start && !scene.Cameras.Any() && !scene.IsUiOnly) throw new InvalidDataException("Add a start camera.");
                var library = projectScripts;
                dependencies.Add(relative);
                Collect(scene, library);
                scene.CheckReferences(assets);
                foreach(var obj in scene.Objects)obj.Prefab=null;
                bakedDocuments.Add(relative, scene);
            }
            catch (Exception ex) { throw new InvalidDataException($"{relative}: {ex.Message}", ex); }
        }

        var markerRoot = nativeAot ? Path.GetDirectoryName(template)! : template;
        var versionFile = Path.Combine(markerRoot, "MoNoonContent.version");
        string? version = File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : null;
        const int minimum = 35; // Player crash reports carry build and frame identities.
        if (!int.TryParse(version, out var supplied) || supplied != minimum)
            throw new InvalidDataException($"This project requires content version {minimum}. Update the {rid} player template or Native AOT kit at {markerRoot}.");
        var runtimeManifest = Path.Combine(markerRoot, nativeAot ? "PlayerDistribution.targets" : "MoNoonPlayer.files");
        if (!File.Exists(runtimeManifest) || !File.Exists(Path.Combine(markerRoot, "MoNoonSdlPlayer.version")))
            throw new InvalidDataException($"Update the {rid} player template or Native AOT kit at {markerRoot} to export a runtime-only game.");
        if (dependencies.Any(path => path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)) &&
            !File.Exists(Path.Combine(markerRoot, "MoNoonVorbis.version")))
            throw new InvalidDataException($"Update the player template or Native AOT kit at {markerRoot} to stream Ogg Vorbis.");
        if (!File.Exists(Path.Combine(markerRoot, "MoNoonUiControls.version")) &&
            bakedDocuments.Values.SelectMany(scene => scene.Objects).Any(o => o.GetComponent<UiControlComponent>() is UiSliderComponent or UiToggleComponent or UiDropdownComponent))
            throw new InvalidDataException($"Update the player template or Native AOT kit at {markerRoot} to use UI value controls.");
        if (bakedDocuments.Values.Any(scene => scene.Objects.Any(o => o.Tag != null || o.Groups != null)) && !File.Exists(Path.Combine(markerRoot, "MoNoonObjectGroups.version")))
            throw new InvalidDataException($"Update the player template or Native AOT kit at {markerRoot} to use object tags and groups.");
        if (bakedDocuments.Values.Any(scene => scene.SchemaVersion >= 28) && !File.Exists(Path.Combine(markerRoot, "MoNoonUiPins.version")))
            throw new InvalidDataException($"Update the player template or Native AOT kit at {markerRoot} to use pinned UI edges.");
        if (needsComponentKeys && !File.Exists(Path.Combine(markerRoot, "MoNoonComponentKeys.version")))
            throw new InvalidDataException($"Update the player template or Native AOT kit at {markerRoot} to use component animation keys.");
        if (palettes.Values.Any(p => p.Tiles.Values.Any(t => t.Collision != null)) && !File.Exists(Path.Combine(markerRoot, "MoNoonTileCollision.version")))
            throw new InvalidDataException($"Update the player template or Native AOT kit at {markerRoot} to use tile collision shapes.");
        // Rigid bodies run on Box2D since roadmap 11.3; an older player would simulate them with the previous solver.
        if (bakedDocuments.Values.Any(scene => scene.Objects.Any(o => o.Components.Any(c => c is Rigidbody2DComponent or Joint2DComponent or CharacterMotor2DComponent)))
            && !File.Exists(Path.Combine(markerRoot, "MoNoonBox2D.version")))
            throw new InvalidDataException($"Update the player template or Native AOT kit at {markerRoot} to simulate rigid bodies and joints with Box2D.");
        if (bakedDocuments.Values.Any(scene => scene.SchemaVersion >= 30) && !File.Exists(Path.Combine(markerRoot, "MoNoonLines.version")))
            throw new InvalidDataException($"Update the player template or Native AOT kit at {markerRoot} to draw lines and trails and follow paths.");
        bool cookShaders = sheets.HasEffects;
        bakeShader ??= global::Engine.Desktop.ShaderPixels.WritePng;
        if (cookShaders && !File.Exists(Path.Combine(markerRoot, ShaderSheetBuild.Capability)))
            throw new InvalidDataException($"Update the player template or Native AOT kit at {markerRoot} to export baked shader sheets.");
        // Project scripts are compiled against this engine. An older template may support the
        // same on-disk content format but lack methods referenced by the compiled scripts.
        if (!nativeAot && System.Reflection.AssemblyName.GetAssemblyName(Path.Combine(template, "Engine.Core.dll")).Version
            != typeof(GameBehaviour).Assembly.GetName().Version)
            throw new InvalidDataException($"Incompatible scripting API in {rid} player template. Rebuild the template at {template}.");
        var name = settings.GameTitle ?? new DirectoryInfo(project).Name;
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidDataException("Invalid game title.");
        string buildId = Guid.NewGuid().ToString("N");
        string mode = nativeAot ? "native-aot" : "release";
        string? symbolDirectory = profile switch
        {
            BuildProfile.Distribution => Path.Combine(destination, "Symbols", name, rid, mode, buildId),
            BuildProfile.Development => null,
            _ => throw new ArgumentOutOfRangeException(nameof(profile))
        };
        var cache = new ContentBuildCache(destination, settings.ProjectId ?? start, rid, mode, profile, fullContent);
        var assetHashes = assets.Entries.ToDictionary(e => e.Path, e => e.Hash, StringComparer.Ordinal);
        if (profile == BuildProfile.Development) destination = Path.Combine(destination, "Development");
        if (nativeAot) destination = Path.Combine(destination, "NativeAOT");
        Directory.CreateDirectory(destination);
        var output = Path.Combine(destination, rid == "osx-arm64" ? name + ".app" : name + "-" + rid);
        var marker = rid == "osx-arm64" ? Path.Combine("Contents", "MacOS", "Game", ".monoon-player") : Path.Combine("Game", ".monoon-player");
        var work = Directory.CreateDirectory(Path.Combine(destination, ".monoon-" + buildId)).FullName;
        var stage = Directory.CreateDirectory(Path.Combine(work, "product")).FullName;
        var symbolStage = symbolDirectory == null ? null : Path.Combine(work, "symbols");
        try
        {
            var bin = rid == "osx-arm64" ? Path.Combine(stage, "Contents", "MacOS") : stage;
            var assembly = projectScripts.Current();
            if (nativeAot)
            {
                NativeAotBuild.Publish(template, rid, assembly, bin, profile);
                foreach (var markerFile in TemplateMarkers) File.Delete(Path.Combine(bin, markerFile));
            }
            else
            {
                var files = File.ReadLines(runtimeManifest).Select(path => path.Replace('\\', '/'))
                    .Distinct(rid == "win-x64" ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
                foreach (var relative in files)
                {
                    if (!relative.Split('/').All(ProjectAssets.ValidName))
                        throw new InvalidDataException($"Invalid player template entry: {relative}");
                    if (!TemplateMarkers.Contains(relative))
                        Copy(Path.Combine(template, relative), Path.Combine(bin, relative));
                }
            }
            if (!File.Exists(Path.Combine(bin, executable)) || new FileInfo(Path.Combine(bin, executable)).Length == 0)
                throw new InvalidDataException($"Publish did not produce {executable}. Previous build was kept.");
            if (rid == "osx-arm64" && profile == BuildProfile.Distribution && !OperatingSystem.IsWindows())
                ShareIdenticalSdl(bin);
            var game = Directory.CreateDirectory(Path.Combine(bin, "Game")).FullName;
            if (!nativeAot)
            {
                var dir = Directory.CreateDirectory(Path.Combine(game, ".monoon-compiled")).FullName;
                File.WriteAllBytes(Path.Combine(dir, ProjectScripts.AssemblyFile), assembly.Bytes);
                File.WriteAllBytes(Path.Combine(dir, Path.ChangeExtension(ProjectScripts.AssemblyFile, ".pdb")), assembly.Symbols);
            }
            var receipt = new GameData();
            receipt.Set("buildId", buildId);
            receipt.Set("projectId", (settings.ProjectId ?? start).ToString("D"));
            receipt.Set("game", name);
            receipt.Set("target", rid);
            receipt.Set("mode", mode);
            receipt.Set("profile", profile == BuildProfile.Development ? "development" : "distribution");
            BuildSymbols.Collect(bin, nativeAot, rid, symbolStage, receipt);
            var yaml = receipt.ToYaml();
            File.WriteAllText(Path.Combine(bin, BuildSymbols.Receipt), yaml);
            if (symbolStage != null) File.WriteAllText(Path.Combine(symbolStage, BuildSymbols.Receipt), yaml);
            foreach (var relative in dependencies)
            {
                var path = Path.Combine(game, relative);
                if (bakedDocuments.TryGetValue(relative, out var scene))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    foreach (var obj in scene.Objects)
                        if (obj.GetComponent<Tilemap2DComponent>() is {StreamRadius:not null} map)
                        {
                            map.ChunkFiles = TileChunkBuild.Write(cache, $"{relative}/{obj.Id}", map,
                                map.Tileset is Guid id ? palettes[id] : null,
                                map.Tileset is Guid set ? assetHashes[assets.PathFor(set)!] : "", Path.Combine(game,TileWorld.Folder));
                            map.Chunks = [];
                        }
                    ProjectAssets.Write(path, scene);
                }
                else Copy(Path.Combine(project, relative), path);
            }
            File.WriteAllText(Path.Combine(game, ".monoon-player"), cookShaders ? BakedShaderSheets.Marker : "1");
            var manifest = assets.Entries.Where(e => dependencies.Contains(e.Path)).Select(entry =>
            {
                using var input = File.OpenRead(Path.Combine(game, entry.Path));
                var hash = Convert.ToHexString(SHA256.HashData(input));
                if (!bakedDocuments.ContainsKey(entry.Path) && hash != entry.Hash)
                    throw new IOException($"Asset changed during export: {entry.Path}. Save and build again.");
                return entry with { Hash = hash };
            }).ToArray();
            if (cookShaders) manifest = sheets.Write(game, manifest, bakeShader, cache);
            ProjectAssets.Write(Path.Combine(game, ProjectAssets.Manifest), manifest);
            ContentBundle.Pack(game, settings.ContentBundles ?? [], manifest, (folder, members, archive, write) =>
                cache.Generate("bundle " + folder, members.ToDictionary(p => p.Key, p => p.Value.Hash, StringComparer.Ordinal), archive, write));
            GameMods.Export(project, bin, settings.ProjectId ?? start, manifest);
            (settings with { GameTitle = name, ProjectId = settings.ProjectId ?? start, Icon = null }).Save(game);
            if (rid == "osx-arm64")
            {
                var plist = new XElement("dict");
                void Entry(string key, string value) => plist.Add(new XElement("key", key), new XElement("string", value));
                Entry("CFBundleName", name); Entry("CFBundleDisplayName", name); Entry("CFBundleExecutable", executable);
                Entry("CFBundlePackageType", "APPL"); Entry("CFBundleIdentifier", "local.monoon.game." + start.ToString("N"));
                new XDocument(new XElement("plist", new XAttribute("version", "1.0"), plist)).Save(Path.Combine(stage, "Contents", "Info.plist"));
            }
            // Keep the archive before making its identifying receipt visible in the new product.
            using var publishLock = new FileStream(Path.Combine(destination, "." + Path.GetFileName(output) + ".publish-lock"), FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            if (File.Exists(output) || Directory.Exists(output) && !File.Exists(Path.Combine(output, marker)))
                throw new IOException($"The output already exists and is not a MoNoon game build: {output}");
            string? previous = null;
            if (symbolDirectory != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(symbolDirectory)!);
                Directory.Move(symbolStage!, symbolDirectory);
            }
            try
            {
                if (Directory.Exists(output))
                {
                    previous = Path.Combine(destination, ".monoon-previous-" + buildId);
                    Directory.Move(output, previous);
                }
                try
                {
                    Directory.Move(stage, output);
                    try { cache.Publish(); }
                    catch { Directory.Move(output, stage); throw; }
                }
                catch
                {
                    if (previous != null) Directory.Move(previous, output);
                    throw;
                }
            }
            catch
            {
                if (symbolDirectory != null) Directory.Delete(symbolDirectory, true);
                throw;
            }
            if (previous != null) Directory.Delete(previous, true);
            return new(output, symbolDirectory) { ContentReport = cache.Report, ContentDecisions = cache.Decisions };
        }
        finally { if (Directory.Exists(work)) Directory.Delete(work, true); }
    }

    private static void ShareIdenticalSdl(string bin)
    {
        var sdl = new FileInfo(Path.Combine(bin, "libSDL3.0.dylib"));
        var alias = new FileInfo(Path.Combine(bin, "libSDL3.dylib"));
        if (!sdl.Exists || !alias.Exists || sdl.LinkTarget != null || alias.LinkTarget != null || sdl.Length != alias.Length)
            return;
        using (var versioned = sdl.OpenRead())
        using (var unversioned = alias.OpenRead())
            if (!SHA256.HashData(versioned).AsSpan().SequenceEqual(SHA256.HashData(unversioned))) return;

        // Keep both loader names valid when the staged app is moved or archived.
        alias.Delete();
        File.CreateSymbolicLink(alias.FullName, sdl.Name);
    }

    private static readonly string[] TemplateMarkers = ["MoNoonSdlPlayer.version", "MoNoonContent.version", "MoNoonVorbis.version", "MoNoonUiControls.version", "MoNoonObjectGroups.version", "MoNoonUiPins.version", "MoNoonComponentKeys.version", "MoNoonTileCollision.version", "MoNoonBox2D.version", "MoNoonLines.version", ShaderSheetBuild.Capability];

    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        File.Copy(from, to);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(to, File.GetUnixFileMode(from));
    }
}
