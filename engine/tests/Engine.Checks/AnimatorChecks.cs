using Engine.Core;

internal static class AnimatorChecks
{
    public static void Run(string folder, string sample)
    {
        Directory.CreateDirectory(folder);
        string root = Path.GetFullPath(folder);
        foreach (var file in Directory.EnumerateFiles(sample,"*",SearchOption.AllDirectories))
        {
            var path=Path.Combine(root,Path.GetRelativePath(sample,file));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.Copy(file,path);
        }
        var assets=ProjectAssets.Scan(root);
        var config=AnimationController.Read(Path.Combine(root,"Assets","Beacon.animator"));
        var first=new AnimationPlayer(config,root,assets);
        var second=new AnimationPlayer(config,root,assets);
        List<string> names=[];
        void Collect(AnimationMarker m)=>names.Add($"{m.State}/{m.Frame}/{m.Name}");
        first.Play(); second.Play(); first.Step(0,Collect); first.SetBool("Running",true);
        first.Step(.6,Collect); first.Step(2,Collect);
        Require(first.State=="Pulse" && first.Frame==1 && second.State=="Idle" && second.Frame==0,"independent instances and non-loop hold");
        first.SetBool("Running",false); first.Step(0,Collect);
        Require(names.SequenceEqual(["Idle/0/Rested","Pulse/0/Rise","Pulse/1/Lit","Idle/0/Rested"]),"entry, transition, exact frame boundary and no repeated end event");
        var clipFile=Path.Combine(root,"Assets","Pulse.clip");
        ProjectAssets.Write(clipFile,SpriteClip.Load(clipFile) with { Loop=true });
        var ordered=config with
        {
            Parameters=[new("Speed","Float"),new("Go","Trigger"),new("Reset","Bool")],
            States=[..config.States,new("Other",config.States[0].Clip)],
            Transitions=[new("Idle","Pulse","Go","Triggered"),new("Idle","Other","Speed","AtLeast",2),new("Pulse","Idle","Reset","True")]
        };
        var player=new AnimationPlayer(ordered,root,assets);
        player.SetFloat("Speed",2); player.SetTrigger("Go"); player.Play(); names.Clear();
        player.Step(2.65,Collect);
        Require(names.SequenceEqual(["Idle/0/Rested","Pulse/0/Rise","Pulse/1/Lit","Pulse/0/Rise","Pulse/1/Lit","Pulse/0/Rise","Pulse/1/Lit"]),
            "all skipped frames, complete loops and ordered transitions");
        player.SetBool("Reset",true); player.Step(0,null); player.Step(0,null);
        Require(player.State=="Other","trigger fires once and float equality meets inclusive threshold");
        try { player.SetBool("Speed",true); throw new Exception("Wrong type accepted"); } catch (ArgumentException) { }
        try { (config with { Entry="Missing" }).Validate("fixture"); throw new Exception("Invalid entry accepted"); } catch (InvalidDataException) { }

        player=new AnimationPlayer(config,root,assets);
        player.Play(); player.SetBool("Running",true);
        player.Step(2,marker => { if (marker.Name=="Lit") player.Play(); });
        Require(player.State=="Idle" && player.Frame==0,"restart inside a crossed frame event preserves the new cursor");

        var scene=SceneDocument.Load(Path.Combine(root,"Scenes","Animator.scene"));
        var body=scene.Objects.Single(o=>o.Name=="Beacon");
        using var game=new SceneGame(scene);
        var canvas=new LabelCanvas();
        game.FixedUpdate(System.Numerics.Vector2.Zero,1f/60); game.Draw(canvas,1);
        Require(canvas.Last=="Idle  /  frame 1  /  1  Rested","project script receives first-frame event");
        game.Animator(body.Id)!.SetBool("Running",true);
        game.FixedUpdate(System.Numerics.Vector2.Zero,.65f); game.Draw(canvas,1);
        Require(canvas.Last=="Pulse  /  frame 2  /  3  Lit","project script receives each event on its owner");
        Console.WriteLine("PASS controller YAML, per-instance parameters, ordered bool/float/trigger transitions, exact and skipped frame events, script callbacks");
    }

    private sealed class LabelCanvas : ICanvas2D
    {
        public string? Last;
        public void DrawText(string text,float x,float y,float size,System.Drawing.Color color) { if (text.Contains("frame")) Last=text; }
        public void Clear(System.Drawing.Color color) => Last=null;
        public void FillRectangle(System.Drawing.RectangleF rectangle,System.Drawing.Color color) {}
        public bool DrawSprite(string path,System.Drawing.RectangleF destination) => true;
    }
    private static void Require(bool ok,string message) { if (!ok) throw new Exception("Animator: "+message); }
}
