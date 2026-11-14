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
        => ExportCore(project, template, rid, destination, settings, false, profile, validateShader);

    public static string ExportNativeAot(string project, string baseDirectory, string rid, string destination, GameSettings settings, Action<ShaderClip>? validateShader = null)
        => ExportNativeAot(project, baseDirectory, rid, destination, settings, BuildProfile.Distribution, validateShader).Product;

    public static GameBuildResult ExportNativeAot(string project, string baseDirectory, string rid, string destination, GameSettings settings, BuildProfile profile, Action<ShaderClip>? validateShader = null)
    {
        if (rid != System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier)
            throw new PlatformNotSupportedException($"Build the {rid} Native AOT player on a matching host.");
        var kit = Path.Combine(TemplateRoot(baseDirectory), "native-aot", "src", "Engine.Player", "Engine.Player.csproj");
        if (!File.Exists(kit))
            throw new FileNotFoundException($"Missing Native AOT build kit at {kit}. Run engine/scripts/build-native-aot-kit.sh.");
        return ExportCore(project, kit, rid, destination, settings, true, profile, validateShader);
    }

    private static GameBuildResult ExportCore(string project, string template, string rid, string destination, GameSettings settings, bool nativeAot, BuildProfile profile, Action<ShaderClip>? validateShader)
    {
        var executable = PlayerExecutable(rid);
        if (!nativeAot && !File.Exists(Path.Combine(template, executable))) throw new FileNotFoundException($"Missing {rid} player template in {template}.");
        if (settings.StartScene is not Guid start || !settings.IncludedScenes.Contains(start))
            throw new InvalidDataException("Include the start scene in the build.");
        var assets = ProjectAssets.Scan(project);
        var checkedShaders = new HashSet<Guid>();
        var dependencies = new HashSet<string>(StringComparer.Ordinal);
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
                    if (obj.GetComponent<SpriteShader2DComponent>()?.Shader is Guid shader)
                    {
                        var clip=assets.LoadShader(project,shader);
                        if (checkedShaders.Add(shader)) validateShader?.Invoke(clip);
                        Asset(shader);
                    }
                    CollectTiles(obj);
                    if (obj.GetComponent<Tilemap2DComponent>() is {StreamRadius: not null, StreamTarget: Guid anchor} && !scene.Objects.Any(o => o.Id == anchor))
                        throw new InvalidDataException($"Missing tile stream target {anchor}.");
                    Asset(obj.GetComponent<AudioSourceComponent>()?.Sound);
                    if (obj.GetComponent<UiImageComponent>()?.Sprite is Guid uiSprite)
                    {
                        if (assets.PathFor(uiSprite) is not { } path || !SpriteAssets.IsPng(path))
                            throw new InvalidDataException($"Missing UI sprite {uiSprite}.");
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
                            _ = animation.ResolveFrames(clipPath,project,assets,imageSizes);
                            foreach (var frame in animation.Frames)
                                if (frame.Source != null || !Pack(frame.Sprite)) Asset(frame.Sprite);
                        }
                    }
                    foreach (var script in obj.Components.OfType<ScriptComponent>())
                    {
                        var def = library.Get(script.Script);
                        foreach (var field in def.Fields)
                        {
                            if (field.Type == typeof(TableReference) && field.Value(script) is TableReference table && table.Id != Guid.Empty)
                            {
                                var relative = GameTable.AssetPath(assets,table);
                                GameTable.Load(Path.Combine(project,relative)); Add(relative);
                            }
                            if (field.Type == typeof(DataReference) && field.Value(script) is DataReference data && data.Id != Guid.Empty)
                            {
                                var path = GameData.AssetPath(project, assets, data);
                                GameData.Load(path);
                                Add(assets.PathFor(data.Id));
                            }
                            if (field.Type == typeof(PrefabReference) && field.Value(script) is PrefabReference reference && reference.Id != Guid.Empty)
                                CollectPrefab(reference.Id, library);
                            if (field.Type == typeof(SceneReference) && field.Value(script) is SceneReference link &&
                                !settings.IncludedScenes.Contains(link.Id))
                                throw new InvalidDataException($"{script.Script}.{field.Name} refers to a scene outside the build.");
                            if (field.Type == typeof(Guid) && field.Value(script) is Guid target && target != Guid.Empty && !scene.Objects.Any(o => o.Id == target && field.Accepts(o)))
                                throw new InvalidDataException($"{script.Script}.{field.Name} refers to a missing object or required component on {target}.");
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
            if (nativeAot) NativeAotBuild.Publish(template, rid, assembly, bin);
            else
            {
                foreach (var file in Directory.EnumerateFiles(template, "*", SearchOption.AllDirectories))
                    Copy(file, Path.Combine(bin, Path.GetRelativePath(template, file)));
            }
            if (!File.Exists(Path.Combine(bin, executable)) || new FileInfo(Path.Combine(bin, executable)).Length == 0)
                throw new InvalidDataException($"Publish did not produce {executable}. Previous build was kept.");
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
                            map.ChunkFiles = TileWorld.Bake(map, map.Tileset is Guid id ? palettes[id] : null, Path.Combine(game,TileWorld.Folder));
                            map.Chunks = [];
                        }
                    ProjectAssets.Write(path, scene);
                }
                else Copy(Path.Combine(project, relative), path);
            }
            File.WriteAllText(Path.Combine(game, ".monoon-player"), "1");
            var manifest = assets.Entries.Where(e => dependencies.Contains(e.Path)).Select(entry =>
            {
                using var input = File.OpenRead(Path.Combine(game, entry.Path));
                var hash = Convert.ToHexString(SHA256.HashData(input));
                if (!bakedDocuments.ContainsKey(entry.Path) && hash != entry.Hash)
                    throw new IOException($"Asset changed during export: {entry.Path}. Save and build again.");
                return entry with { Hash = hash };
            }).ToArray();
            ProjectAssets.Write(Path.Combine(game, ProjectAssets.Manifest), manifest);
            ContentBundle.Pack(game, settings.ContentBundles ?? [], manifest);
            GameMods.Export(project, bin, settings.ProjectId ?? start, manifest);
            (settings with { GameTitle = name, ProjectId = settings.ProjectId ?? start }).Save(game);
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
                try { Directory.Move(stage, output); }
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
            return new(output, symbolDirectory);
        }
        finally { if (Directory.Exists(work)) Directory.Delete(work, true); }
    }

    private static void Copy(string from, string to)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        File.Copy(from, to);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(to, File.GetUnixFileMode(from));
    }
}
