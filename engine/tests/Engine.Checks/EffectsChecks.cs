using System.Drawing;
using System.Numerics;
using Brutal.SdlApi;
using Engine.Core;
using Engine.Desktop;

internal static class EffectsChecks
{
    [Check(2700, "particles")] static void Registered(CheckRun c) => Run(c.Repository);
    private sealed class Canvas : ICanvas2D
    {
        internal readonly List<RectangleF> Dust=[];
        internal int Frame, Light;
        public void Clear(Color c) => Dust.Clear();
        public void FillRectangle(RectangleF r,Color c) { }
        public void DrawText(string text,float x,float y,float size,Color c) { }
        public bool DrawSprite(string p,RectangleF r)=>true;
        // Landing dust is textured (Assets/Showcase/DustPuff.png), so it arrives as sprite draws, not the built-in puff shader.
        public bool DrawAffine(string? p,RectangleF r,Matrix3x2 m,Color tint,SpriteRegion? source=null,string layout=TileGrid.Orthogonal)
        {
            if (p?.EndsWith("DustPuff.png",StringComparison.Ordinal)==true) Dust.Add(Affine2D.Bounds(r,m));
            return true;
        }
        public void FillStrip(ReadOnlySpan<Vector2> edges,ReadOnlySpan<Color> colors,Matrix3x2 matrix) { }
        public void DrawShader(ShaderClip shader,int frame,RectangleF local,Matrix3x2 m,Color tint,bool additive)
        {
            if (additive) Light=tint.A;
            else if (shader==ShaderClip.Puff) Dust.Add(Affine2D.Bounds(local,m));
            else Frame=frame;
        }
    }

    // Torch light 1 flickers by 0.12 of its 0.8 intensity (alpha about 180..204 over 100 steps); parallax sets the main showcase's
    // schema to 33 and the course's capsule and edge colliders set it to 34.
    const int LightSwing=15, SchemaShowcase=33, SchemaCourse=34;
    internal static void Run(string repository)
    {
        string project=Path.Combine(repository,"samples","PlatformerLab");
        var scene=SceneDocument.Load(Path.Combine(project,"Scenes","Showcase.scene"));
        // The shader torch and its flickering light live on the course showcase; the landing dust on the main one.
        var course=SceneDocument.Load(Path.Combine(project,"Scenes","ShowcaseCourse.scene"));
        var torch=course.Objects.Single(o=>o.Name=="Torch flame 1");
        var torchLight=course.Objects.Single(o=>o.Name=="Torch light 1");
        var runner=scene.Objects.Single(o=>o.Name=="Runner");
        var camera=scene.Objects.Single(o=>o.Name=="Camera").Id;
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
        int burst=0;
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
            if (burst>0 && canvas.Dust.Count>0 && i>60)
            {
                // The canvas sees screen space; the zoomed follow camera maps it back to world x.
                game.Current.TryGetView(camera,out var view);
                var center=view.World.X+canvas.Dust.Average(r=>r.X+r.Width*.5f)/view.Zoom;
                game.Current.TryGetPosition(runner.Id,out var pos);
                if (pos.X-center<25) throw new Exception("Dust followed the player");
            }
        }
        game.Actions.Release("Right");
        Check(burst==16 && canvas.Dust.Count==0,$"landing emits 16 world-space particles once and they expire while the player moves ({burst}, {canvas.Dust.Count} left)");
        using (var lit=new GameSession(course))
        {
            var torchCanvas=new Canvas();
            int min=255,max=0;
            var frames=new HashSet<int>();
            for (int i=0;i<100;i++)
            {
                lit.FixedUpdate(lit.Actions.Take(),dt); lit.Draw(torchCanvas,1);
                min=Math.Min(min,torchCanvas.Light);max=Math.Max(max,torchCanvas.Light);frames.Add(torchCanvas.Frame);
            }
            Check(max-min>LightSwing && frames.Count==clip.Frames,$"fixed steps cover the shader loop and vary the light intensity ({min}..{max}, {frames.Count}/{clip.Frames} frames)");
        }
        Check(game.Current.EmitParticles(runner.Id),"scripts can emit a manual burst");
        game.Current.SetActive(runner.Id,false); game.Current.SetActive(runner.Id,true); game.Draw(canvas,1);
        Check(canvas.Dust.Count==0,"disabling the emitter clears its particles");
        var saved=ProjectYaml.Decode<SceneDocument>(ProjectYaml.Encode(scene));
        var savedCourse=ProjectYaml.Decode<SceneDocument>(ProjectYaml.Encode(course));
        Check(saved.SchemaVersion==SchemaShowcase && savedCourse.SchemaVersion==SchemaCourse &&
            savedCourse.Objects.Single(o=>o.Id==torchLight.Id).GetComponent<PointLight2DComponent>()==torchLight.GetComponent<PointLight2DComponent>() &&
            savedCourse.Objects.Single(o=>o.Id==torch.Id).GetComponent<SpriteShader2DComponent>()==torch.GetComponent<SpriteShader2DComponent>() &&
            saved.Objects.Single(o=>o.Id==runner.Id).GetComponent<ParticleEmitter2DComponent>()==runner.GetComponent<ParticleEmitter2DComponent>(),
            $"effect settings round-trip through scene YAML (schemas {saved.SchemaVersion}, {savedCourse.SchemaVersion})");
        DrawWithoutAllocating(clip);
    }

    // The player's SDL canvas: a sheet lookup and source cell per particle, light and torch frame without a heap object.
    private static unsafe void DrawWithoutAllocating(ShaderClip torch)
    {
        var target=Sdl.PInvoke.CreateSurface(64,64,SdlPixelFormat.Rgba32);
        if (target==null) throw new InvalidOperationException(Sdl.PInvoke.GetError());
        try
        {
            using var renderer=Sdl.Render.CreateSoftwareRenderer(target);
            using var canvas=new SdlCanvas(renderer,batchSprites:true,previewShader:ShaderPixels.Preview);
            var area=new RectangleF(0,0,8,8);
            void Draw(int i)
            {
                canvas.DrawShader(ShaderClip.Puff,0,area,Matrix3x2.Identity,Color.White,false);
                canvas.DrawShader(ShaderClip.LightFalloff,0,area,Matrix3x2.Identity,Color.White,true);
                canvas.DrawShader(torch,i%torch.Frames,area,Matrix3x2.Identity,Color.White,false);
            }
            for (int i=0;i<64;i++) Draw(i);
            canvas.FlushSprites();
            long before=GC.GetAllocatedBytesForCurrentThread();
            for (int i=0;i<1000;i++) Draw(i);
            canvas.FlushSprites();
            long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(bytes==0,$"1,000 rounds of dust, light and torch shader draws allocate {bytes} B after warm-up");
        }
        finally { Sdl.PInvoke.DestroySurface(target); }
    }
    private static void Check(bool pass,string label)
    {
        if(!pass) throw new Exception(label);
        Console.WriteLine("PASS effects: "+label);
    }
}
