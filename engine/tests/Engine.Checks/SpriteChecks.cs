using System.Drawing;
using System.Numerics;
using Engine.Core;
using Engine.Desktop;
using SkiaSharp;

internal static class SpriteChecks
{
    [Check(5800, Templates = true)]
    static void Registered(CheckRun c) { var root = Path.Combine(c.Scratch, "SpriteTest"); Run(root, c.Repository); Directory.Delete(root, true); }
    internal static void Prepare(string root)
    {
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any()) throw new IOException("Expected an empty sprite sample folder.");
        Directory.CreateDirectory(Path.Combine(root,"Assets")); Directory.CreateDirectory(Path.Combine(root,"Scenes"));
        void Png(string name,Func<int,int,SKColor> pixel,int w=16,int h=12)
        {
            using var b=new SKBitmap(w,h);
            for (int y=0;y<h;y++) for (int x=0;x<w;x++) b.SetPixel(x,y,pixel(x,y));
            using var data=b.Encode(SKEncodedImageFormat.Png,100);
            File.WriteAllBytes(Path.Combine(root,"Assets",name+".png"),data.ToArray());
        }
        // The arrow has asymmetric transparent borders and points right.
        Png("Arrow",(x,y)=> (x>=2 && x<=9 && y>=4 && y<=6 || x>=8 && x<=12 && Math.Abs(y-5)<=12-x)
            ? x<6 ? SKColors.White : new SKColor(240,180,100) : SKColors.Transparent);
        Png("StripeMask",(x,y)=> new SKColor(255,255,255,(byte)(x<6 ? 0 : x==6 ? 128 : 255)));
        Png("Dot",(x,y)=> x>=5 && x<=8 && y>=4 && y<=7 ? SKColors.White : SKColors.Transparent);
        var catalog=ProjectAssets.Scan(root);
        Guid Id(string name)=>catalog.IdFor("Assets/"+name)!.Value;
        var arrow=Id("Arrow.png"); var dot=Id("Dot.png"); var mask=Id("StripeMask.png");
        AtlasPacker.Pack(root,Path.Combine(root,"Assets","Icons.atlas"),[arrow,dot],false);
        ProjectAssets.Write(Path.Combine(root,"Assets","Arrow.clip"),new SpriteClip([new(arrow,4),new(dot,.7f)],true));
        var clip=ProjectAssets.Scan(root).IdFor("Assets/Arrow.clip");
        var scene=SceneDocument.Empty(new());
        void Add(string name,float x, bool effects,bool animated)
        {
            var o=new SceneObject { Name=name,X=x,Y=155.6f,Width=192,Height=144,PivotX=.5f,PivotY=.5f };
            o.AddComponent(new SpriteRendererComponent { Asset="Assets/Arrow.png",AssetId=arrow,
                Tint=effects ? "#80E0FF" : null,FlipX=effects,PixelSnap=effects,Mask=effects ? mask : null });
            if (animated) o.AddComponent(new SpriteAnimatorComponent { Clip=clip,PlayOnStart=true });
            scene.Objects.Add(o);
        }
        Add("Original",54,false,false); Add("Flipped + masked",306.4f,true,false); Add("Animated copy",558.4f,true,true);
        var file=Path.Combine(root,"Scenes","Sprites.scene");
        scene.Save(file);
        var start=ProjectAssets.Scan(root).IdFor("Scenes/Sprites.scene")!.Value;
        new GameSettings { GameTitle="SpriteLab",StartScene=start,IncludedScenes=[start],ProjectId=start }.Save(root);
    }

    internal static void Run(string root,string repository)
    {
        Prepare(root);
        var atlasFile=Path.Combine(root,"Assets","Icons.atlas");
        var sceneFile=Path.Combine(root,"Scenes","Sprites.scene");
        var assets=ProjectAssets.Scan(root);
        var arrow=assets.IdFor("Assets/Arrow.png")!.Value; var dot=assets.IdFor("Assets/Dot.png")!.Value;
        var settings=GameSettings.Load(root);
        var doc=SceneDocument.Load(sceneFile);
        var obj=doc.Objects.Single(o=>o.Name=="Flipped + masked");
        var appearance=SceneAppearance.From(obj,root,assets);
        Check(appearance.FlipX && appearance.PixelSnap && appearance.Mask!=null && appearance.Source==new SpriteRegion(1,1,11,9)
            && appearance.Canvas==new SpriteCanvas(16,12,2,1),"trim, mask and flags survive save/reopen");
        using(var img=SpritePixels.Filter(new(appearance.Image!,appearance.Source,appearance.Canvas,appearance.Mask),appearance.Color))
        {
            Check(img.GetPixel(0,4).Alpha==0 && img.GetPixel(4,4).Alpha==128 && img.GetPixel(6,4)==new SKColor(120,158,100),
                "mask alpha and multiplied tint use original untrimmed coordinates");
        }
        var sceneBytes=File.ReadAllBytes(sceneFile);
        var clipBytes=File.ReadAllBytes(Path.Combine(root,"Assets","Arrow.clip"));
        var old=appearance.Source;
        File.Move(Path.Combine(root,"Assets/Arrow.png"),Path.Combine(root,"Assets/ArrowRenamed.png"));
        File.Move(Path.Combine(root,"Assets/Arrow.png.meta"),Path.Combine(root,"Assets/ArrowRenamed.png.meta"));
        using(var larger=new SKBitmap(18,18))
        {
            larger.Erase(SKColors.White); using var bytes=larger.Encode(SKEncodedImageFormat.Png,100);
            File.WriteAllBytes(Path.Combine(root,"Assets","Dot.png"),bytes.ToArray());
        }
        AtlasPacker.Pack(root,atlasFile,[dot,arrow],true);
        var fresh=ProjectAssets.Scan(root); var current=fresh.Sprites(root).Find(arrow)!;
        Check(current.Entry.Source!=old && sceneBytes.SequenceEqual(File.ReadAllBytes(sceneFile)) &&
            clipBytes.SequenceEqual(File.ReadAllBytes(Path.Combine(root,"Assets","Arrow.clip"))),"repack moves the cell without rewriting scene or clip");
        var export=GameBuild.Export(root,GameBuild.FindPlayerTemplate(Path.Combine(repository,"engine"),"osx-arm64"),"osx-arm64",
            Path.Combine(root,"Builds"),settings);
        var game=Path.Combine(export,"Contents","MacOS","Game");
        Check(File.Exists(Path.Combine(game,current.Sheet)) && File.Exists(Path.Combine(game,"Assets/StripeMask.png")) &&
            !File.Exists(Path.Combine(game,"Assets/ArrowRenamed.png")), "export contains shared sheet and mask without original PNG");
        foreach (var target in new[]{root,game})
        {
            var reopened=SceneDocument.Load(Path.Combine(target,"Scenes","Sprites.scene")); using var world=new SceneGame(reopened);
            var canvas=new Capture(); world.Draw(canvas,1);
            var (a,r,m,t)=canvas.Masked[0]; var (b,rb,mb,tb)=canvas.Masked[1];
            Check(canvas.Masked.Count==2 && a==b && r==rb && a.Source==current.Entry.Source && a.Image==Path.Combine(target,current.Sheet) && t==tb &&
                Math.Abs(mb.M31-m.M31-252)<.01f && m.M11==-1 && m.M31==498 && m.M32==156,
                "static and animated draw from repacked sheet, flipped and snapped at logical resolution");
            for (int i=0;i<245;i++) world.FixedUpdate(Vector2.Zero,1f/60);
            canvas.Masked.Clear(); world.Draw(canvas,1);
            Check(canvas.Masked[0].Item1.Source==current.Entry.Source &&
                canvas.Masked[1].Item1.Source==fresh.Sprites(root).Find(dot)!.Entry.Source &&
                canvas.Masked[1].Item1.Mask==a.Mask && canvas.Masked[1].Item4==t,"switching animation cells preserves mask and tint");
        }
        var last=File.ReadAllBytes(atlasFile); File.Delete(Path.Combine(root,"Assets","Dot.png"));
        try { AtlasPacker.Pack(root,atlasFile,[dot,arrow],true); throw new Exception("Expected missing input."); }
        catch (IOException) { }
        Check(last.SequenceEqual(File.ReadAllBytes(atlasFile)) && File.Exists(Path.Combine(root,current.Sheet)),"failed repack keeps last published generation");
        Console.WriteLine("PASS Sprite atlas trim, padded pixels, mask alpha/tint, flip/snap, repack, static/animated export");
    }

    private sealed class Capture : ICanvas2D
    {
        public List<(SpriteFilter, RectangleF,Matrix3x2,Color)> Masked=[];
        public bool DrawFiltered(SpriteFilter f,RectangleF r,Matrix3x2 m,Color t) { Masked.Add((f,r,m,t)); return true; }
        public bool DrawAffine(string? p,RectangleF r,Matrix3x2 m,Color t,SpriteRegion? source=null,string layout=TileGrid.Orthogonal)=>true;
        public void Clear(Color c) { }
        public void FillRectangle(RectangleF r,Color c) { }
        public bool DrawSprite(string p,RectangleF r)=>true;
    }
    private static void Check(bool ok,string message) { if (!ok) throw new Exception("Sprite: "+message); }
}
