using System.Drawing;
using System.Numerics;
using Engine.Core;
using Engine.Desktop;

internal static class EffectsChecks
{
    private sealed class Canvas : ICanvas2D
    {
        internal readonly List<RectangleF> Dust=[];
        internal int Frame, Light;
        public void Clear(Color c) => Dust.Clear();
        public void FillRectangle(RectangleF r,Color c) { }
        public void DrawText(string text,float x,float y,float size,Color c) { }
        public bool DrawSprite(string p,RectangleF r)=>true;
        public bool DrawAffine(string? p,RectangleF r,Matrix3x2 m,Color tint,SpriteRegion? source=null,string layout=TileGrid.Orthogonal)=>true;
        public void DrawShader(ShaderClip shader,int frame,RectangleF local,Matrix3x2 m,Color tint,bool additive)
        {
            if (additive) Light=tint.A;
            else if (shader==ShaderClip.Puff) Dust.Add(Affine2D.Bounds(local,m));
            else Frame=frame;
        }
    }

    internal static void Run(string repository)
    {
        string project=Path.Combine(repository,"samples","PlatformerLab");
        var scene=SceneDocument.Load(Path.Combine(project,"Scenes","Platform.scene"));
        var torch=scene.Objects.Single(o=>o.Name=="Torch flame");
        var runner=scene.Objects.Single(o=>o.Name=="Runner");
        var clip=ProjectAssets.ReadManifest(project).LoadShader(project,torch.GetComponent<SpriteShader2DComponent>()!.Shader!.Value);
        using (var pixels=ShaderPixels.Bake(clip,Color.White))
        {
            var a=clip.Region(0);
            var b=clip.Region(clip.Frames/4);
            int changes=0;
            for(int y=0;y<clip.Height;y++)
            for(int x=0;x<clip.Width;x++)
                if(pixels.GetPixel(a.X+x,a.Y+y)!=pixels.GetPixel(b.X+x,b.Y+y)) changes++;
            Check(changes>20 && pixels.GetPixel(a.X,a.Y).Alpha==0,"SkSL changes the torch silhouette and keeps its transparent border");
        }
        using (var light=ShaderPixels.Bake(ShaderClip.LightFalloff,Color.White))
            Check(light.GetPixel(48,48).Alpha>240 && light.GetPixel(72,48).Alpha is >45 and <75 &&
                light.GetPixel(0,0).Alpha==0,"point light fades from its center to zero outside the radius");
        using (var puff=ShaderPixels.Bake(ShaderClip.Puff,Color.White))
            Check(puff.GetPixel(6,6).Alpha==255 && puff.GetPixel(0,0).Alpha==0,"dust has a transparent soft edge");
        // This scenario measures one dust burst on flat ground while the runner moves away.
        scene.Objects.Remove(scene.Objects.Single(o => o.Name == "Jump-through ledge"));
        using var game=new GameSession(scene);
        var canvas=new Canvas();
        const float dt=1f/60;
        void Next() { game.FixedUpdate(game.Actions.Take(),dt); game.Draw(canvas,1); }
        for (int i=0;i<5;i++) Next();
        Check(canvas.Dust.Count==0,"starting on the floor produces no landing burst");
        game.Actions.Press("Space"); Next(); game.Actions.Release("Space");
        int min=255,max=0, burst=0;
        var frames=new HashSet<int>();
        for (int i=0;i<100;i++)
        {
            Next();
            game.Current.TryGetMotion(runner.Id,out var body);
            if (!body.Grounded && canvas.Dust.Count>0) throw new Exception("Dust before contact");
            if (canvas.Dust.Count>0 && burst==0)
            {
                burst=canvas.Dust.Count;
                game.Actions.Press("Right");
            }
            min=Math.Min(min,canvas.Light);max=Math.Max(max,canvas.Light);frames.Add(canvas.Frame);
            if (burst>0 && canvas.Dust.Count>0 && i>60)
            {
                var center=canvas.Dust.Average(r=>r.X+r.Width*.5f);
                game.Current.TryGetPosition(runner.Id,out var pos);
                if (pos.X-center<25) throw new Exception("Dust followed the player");
            }
        }
        game.Actions.Release("Right");
        Check(burst==22 && canvas.Dust.Count==0,"landing emits 22 world-space particles once and they expire while the player moves");
        Check(max-min>75 && frames.Count==clip.Frames,"fixed steps cover the shader loop and vary the light intensity");
        Check(game.Current.EmitParticles(runner.Id),"scripts can emit a manual burst");
        game.Current.SetActive(runner.Id,false); game.Current.SetActive(runner.Id,true); game.Draw(canvas,1);
        Check(canvas.Dust.Count==0,"disabling the emitter clears its particles");
        var saved=ProjectYaml.Decode<SceneDocument>(ProjectYaml.Encode(scene));
        Check(saved.SchemaVersion==21 && saved.Objects.Single(o=>o.Id==torch.Id).GetComponent<PointLight2DComponent>()==torch.GetComponent<PointLight2DComponent>() &&
            saved.Objects.Single(o=>o.Id==runner.Id).GetComponent<ParticleEmitter2DComponent>()==runner.GetComponent<ParticleEmitter2DComponent>(),
            "effect settings round-trip through scene YAML");
    }
    private static void Check(bool pass,string label)
    {
        if(!pass) throw new Exception(label);
        Console.WriteLine("PASS effects: "+label);
    }
}
