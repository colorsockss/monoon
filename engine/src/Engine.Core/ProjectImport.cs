namespace Engine.Core;

public static class ProjectImport
{
    public static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name==name.Trim() &&
        !name.StartsWith('.') && !name.EndsWith('.') && !name.Any(c=>char.IsControl(c)||"<>:\"/\\|?*".Contains(c)) &&
        !System.Text.RegularExpressions.Regex.IsMatch(name,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)",System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    // Preserve identity and sheet slices when the incoming file has a MoNoon sidecar.
    // Stage and validate the entire selection before publishing any file in it.
    public static string[] Copy(string project,string folder,IEnumerable<string> sources)
    {
        var root=Path.GetFullPath(project);
        if (folder.Length>0 && !folder.Split('/').All(ValidName)) throw new InvalidDataException("Invalid project folder.");
        var assets=ProjectAssets.Scan(root);
        var result=new List<string>();
        var occupied=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ids=new HashSet<Guid>();
        var stage=Directory.CreateDirectory(Path.Combine(root,".import-"+Guid.NewGuid().ToString("N"))).FullName;
        var pending=new List<(string Temp,string Dest)>();
        var written=new List<string>();
        string Destination(string relative)
        {
            string current=root;
            foreach(var part in relative.Split('/'))
            {
                current=Path.Combine(current,part);
                if (new DirectoryInfo(current).LinkTarget!=null) throw new IOException($"Cannot import through a symbolic link: {relative}");
            }
            return current;
        }
        string Stage(string relative)
        {
            var dest=Destination(relative);
            if (!occupied.Add(relative) || Path.Exists(dest)) throw new IOException($"{relative} already exists. Rename the incoming file or choose another folder.");
            var temp=Path.Combine(stage,pending.Count.ToString());
            pending.Add((temp,dest)); return temp;
        }
        try
        {
            foreach(var input in sources.Select(Path.GetFullPath).Distinct())
            {
                var name=Path.GetFileName(input);
                bool script=name.EndsWith(".cs",StringComparison.Ordinal);
                if (!ValidName(name) || !script && !ProjectAssets.IsAsset(name)) throw new InvalidDataException($"Unsupported asset: {name}");
                var category=script?"Scripts":name.EndsWith(ProjectAssets.Scene,StringComparison.Ordinal)?"Scenes":"Assets";
                var dir=folder==category || folder.StartsWith(category+"/",StringComparison.Ordinal)?folder:category;
                var relative=dir+"/"+name;
                if (input==Destination(relative)) { result.Add(relative); continue; }
                var temp=Stage(relative); File.Copy(input,temp); result.Add(relative);
                switch(Path.GetExtension(name).ToLowerInvariant())
                {
                    case ".png": SpriteRegion.ReadPngSize(temp); break;
                    case ".wav": WaveInfo.Read(temp); break;
                    case ProjectAssets.Scene: case ProjectAssets.Prefab: SceneDocument.ReadAsset(temp); break;
                    case CutsceneSequence.Extension: CutsceneSequence.Load(temp); break;
                    case SkeletonRig.Extension: SkeletonRig.Load(temp); break;
                    case ShaderClip.Extension: ShaderClip.Load(temp); break;
                    case ProjectAssets.Data: GameData.Load(temp); break;
                    case GameTable.Extension: GameTable.Load(temp); break;
                    case ProjectAssets.Clip: SpriteClip.Load(temp); break;
                    case ProjectAssets.Tileset: Tileset.Load(temp); break;
                    case AnimationController.Extension: AnimationController.Read(temp).Validate(name); break;
                    case SpriteAtlas.Extension:
                        var packed=SpriteAtlas.Load(temp).ImagePath();
                        if (!File.Exists(Destination(packed)) && !occupied.Contains(packed))
                        {
                            var sourceDir=new FileInfo(input).Directory;
                            while(sourceDir!=null && !File.Exists(Path.Combine(sourceDir.FullName,packed))) sourceDir=sourceDir.Parent;
                            if(sourceDir==null) throw new InvalidDataException($"Missing packed image for {name}: {packed}");
                            File.Copy(Path.Combine(sourceDir.FullName,packed),Stage(packed));
                        }
                        break;
                }
                if(script) continue;
                var incoming=File.Exists(input+".meta")?ProjectYaml.Read<AssetMeta>(input+".meta"):null;
                var sidecar=Destination(relative+".meta");
                var retained=File.Exists(sidecar)?ProjectYaml.Read<AssetMeta>(sidecar):null;
                if(incoming!=null && assets.IdFor(relative) is Guid known && incoming.Id!=known)
                    throw new IOException($"Restoring {relative} requires its original asset ID.");
                if(retained!=null && incoming!=null && retained.Id!=incoming.Id)
                    throw new IOException($"Restored asset ID differs from {relative}.meta.");
                var meta=incoming??retained??new AssetMeta(assets.IdFor(relative)??Guid.NewGuid());
                if(meta.Id==Guid.Empty || !ids.Add(meta.Id) || assets.PathFor(meta.Id) is {} owner && owner!=relative && File.Exists(Path.Combine(root,owner)))
                    throw new InvalidDataException($"Asset ID conflict for {name}: {meta.Id}.");
                if(retained==null) ProjectYaml.Write(Stage(relative+".meta"),meta);
            }
            foreach(var (temp,dest) in pending)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Move(temp,dest,false); written.Add(dest);
            }
            ProjectAssets.Scan(root);
            return result.ToArray();
        }
        catch { foreach(var path in written) File.Delete(path); throw; }
        finally { Directory.Delete(stage,true); }
    }
}
