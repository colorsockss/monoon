using System.Drawing;
using System.Numerics;
using Engine.Core;

internal static class CameraChecks
{
    private static void Check(bool value,string why) { if (!value) throw new Exception("Camera: "+why); }
    private static void Near(float a,float b,string why) => Check(Math.Abs(a-b)<.002f,$"{why}: {a} vs {b}");
    private static CameraView Read(SceneGame g,Guid id) => g.TryGetView(id,out var view)?view:throw new Exception("Missing camera view");
    internal static void Run(string repository,string scratch)
    {
        CheckRenderGrid();
        var path=Path.Combine(repository,"samples","CameraLab","Scenes","CameraLab.scene");
        var scene=SceneDocument.Load(path);
        var main=scene.Camera!;var extra=scene.Objects.Single(o=>o.Name=="Partner Camera");var map=scene.Objects.Single(o=>o.Name=="Map Camera");
        var mainId=main.Id;
        var reopened=SceneDocument.Empty(new());reopened.RestoreObjects(scene.CaptureObjects());
        var lens=reopened.Camera!.GetComponent<Camera2DComponent>()!;
        Check(reopened.Camera.Id==mainId && lens==main.GetComponent<Camera2DComponent>() && lens.Follow!=null,"roundtrip camera settings");

        using (var session=new GameSession(scene))
        {
            var g=session.Current;
            Check(g.ActiveCameraId==mainId && !g.TrySetActive(map.Id) && !g.TrySetActive(extra.Id),"extra output is not a primary cut");
            session.Actions.Press("Space"); session.FixedUpdate(session.Actions.Take(),1/60f);
            var shot=Read(g,mainId);
            float alpha=(float)(1-Math.Pow(.5,1d/(60*.16)));
            Near(shot.Center.X,200+14*alpha,"one tick of post-movement tracking");
            Near(shot.Center.Y,200+8*alpha,"same-frame Y tracking");
            var recorder=new DrawCapture();g.Draw(recorder,.5f);
            Check(recorder.Clips.SequenceEqual(new[]{new RectangleF(0,0,400,400),new RectangleF(400,0,400,400),new RectangleF(584,18,200,112)}),"split, second, minimap order");
            Near(recorder.Green[0].X,184+7-7*alpha,"render interpolates camera and player at the same fraction");
            Check(recorder.TextOutside>0 && recorder.TextInside==0 && recorder.Current==null,"HUD drawn once after clip restoration");
            g.SetActive(extra.Id,false);recorder=new();g.Draw(recorder,1);
            Check(recorder.Clips.Count==2 && !g.TryGetView(extra.Id,out _),"inactive camera hidden");
            g.SetActive(extra.Id,true);
            for (int i=0;i<151;i++) session.FixedUpdate(session.Actions.Take(),1/60f);
            Check(session.State.Storage.LoadSlot(1,1)?.GetBool("pass")==true,"tracked to bottom/right limits");
            session.Actions.Release("Space");session.FixedUpdate(session.Actions.Take(),1/60f);
            session.Actions.Press("Space");
            for (int i=0;i<152;i++) session.FixedUpdate(session.Actions.Take(),1/60f);
            Check(session.State.Storage.LoadSlot(2,1)?.GetBool("pass")==true,"returned to top/left limits");
        }
        var bounded=new Camera2DComponent { Bounds=new(-100,-20,60,45),Zoom=.25f };
        var small=bounded.View(new(801,451),new(999,-999));
        Near(small.World.Right,-40,"map smaller than camera: fit horizontally"); Near(small.World.Top,-20,"clamp negative coordinates");
        Check(small.World.Bottom<=25.001f && small.World.Left>=-100.001f,"small map cannot expose outside");
        var point=new Vector2(-81,2);
        Near(Vector2.Distance(small.ScreenToWorld(small.WorldToScreen(point)),point),0,"coordinate conversions");
        var left=new Camera2DComponent { Viewport=new(0,0,.5f,1) }.View(new(801,451),default);
        var right=new Camera2DComponent { Viewport=new(.5f,0,.5f,1) }.View(new(801,451),default);
        Check(left.Viewport.Right==right.Viewport.Left && right.Viewport.Right==801,"odd split has no seam");

        var project=Directory.CreateDirectory(Path.Combine(scratch,"CameraPrefab")).FullName;
        Directory.CreateDirectory(Path.Combine(project,"Assets"));
        var group=SceneDocument.Empty(new());group.ProjectDirectory=project;
        var owner=new SceneObject { Name="Owner" };group.Objects.Add(owner);
        var follower=group.CreateCamera(new(0,0));group.Reparent(follower,owner);
        follower.GetComponent<Camera2DComponent>()!.Follow=owner.Id;
        var duplicate=group.DuplicateObject(owner);
        Check(group.Objects.Single(o=>o.Parent==duplicate).GetComponent<Camera2DComponent>()!.Follow==duplicate.Id,"duplicate remaps follow target");
        group.ExportPrefab(owner,Path.Combine(project,"Assets","Rig.prefab"));
        var instance=group.PlacePrefab("Assets/Rig.prefab",new(4,5));
        Check(group.Objects.Single(o=>o.Parent==instance).GetComponent<Camera2DComponent>()!.Follow==instance.Id,"prefab target remaps");
        using (var g=new SceneGame(group))
        {
            var id=g.Instantiate(new(owner.Prefab!.AssetId),new(100,100));g.FixedUpdate(Vector2.Zero,.1f);
            var cam=g.Objects.Single(o=>o.ParentId==id);
            Check(g.GetComponent<Camera2DComponent>(cam.Id)?.Follow==id,"runtime prefab binds own target");
            Near(Read(g,cam.Id).Center.X,120,"instant following");
        }
        var lone=SceneDocument.Empty(new());lone.Camera!.GetComponent<Camera2DComponent>()!.Output=Camera2DComponent.Minimap;
        using (var g=new SceneGame(lone)) { var r=new DrawCapture();g.Draw(r,1);Check(g.ActiveCameraId==null && r.Clips.Count==1,"minimap-only scene"); }
        foreach (var bad in new Camera2DComponent[]{new(){Viewport=new(0,0,0,1)},new(){Viewport=new(.7f,0,.4f,1)},new(){Bounds=new(0,0,-1,1)},new(){HalfLife=-1},new(){Output="unknown"}})
        {
            var doc=SceneDocument.Empty(new());var cam=doc.Camera!;cam.RemoveComponent<Camera2DComponent>();cam.AddComponent(bad);
            try { reopened.RestoreObjects(doc.CaptureObjects());throw new Exception("Invalid camera accepted"); }
            catch(InvalidDataException) { }
        }
        Console.WriteLine("PASS camera tracking, fixed/render interpolation, bounds/zoom, split/overlay/HUD order, odd viewports, reference remapping, validation and YAML");
    }
    private static void CheckRenderGrid()
    {
        var scene = SceneDocument.Empty(new());
        var camera = scene.Camera!;
        var wide = scene.CreateCamera(new(400, 225));
        wide.GetComponent<Camera2DComponent>()!.Zoom = Camera2DComponent.MinZoom;
        var root = new SceneObject { Name = "Offscreen group", X = -2400, Y = 50 };
        root.AddComponent(new SortingGroup2DComponent { SortByY = true });
        var child = new SceneObject { Name = "Child", X = 1200 };
        child.AddComponent(new SpriteRendererComponent());
        child.SetParent(root);
        scene.Objects.AddRange([root, child]);
        for (int i = 0; i < 160; i++)
            scene.Objects.Add(new SceneObject { Name = $"Distant {i}", X = 5000 + i * 64 });
        using var game = new SceneGame(scene);
        var canvas = new DrawCapture();
        void VisibleAt(float fraction, bool visible)
        {
            var expected = new List<Guid>();
            var actual = new List<Guid>();
            game.UseRenderGrid = false;
            game.PrepareCameraDraw(fraction);
            int full = game.DrawCamera(canvas, camera.Id, fraction, expected);
            game.UseRenderGrid = true;
            game.PrepareCameraDraw(fraction);
            int reduced = game.DrawCamera(canvas, camera.Id, fraction, actual);
            Check(reduced < full && actual.SequenceEqual(expected) && actual.Contains(child.Id) == visible,
                $"group visibility at interpolation {fraction}");
        }
        // The child crosses the entire lens within one tick. Its parent remains offscreen
        // when the child is visible, and both endpoints of the child's path are offscreen.
        Check(game.TryMoveStep(root.Id, new(2400, 0)), "move render group");
        VisibleAt(0, false);
        VisibleAt(.6f, true);
        VisibleAt(1, false);
        VisibleAt(.55f, true);
        game.FixedUpdate(Vector2.Zero, 1f / 60);
        VisibleAt(0, false);

        // The pool was created after indexing. A visible instance has an offscreen owner.
        var pool = game.CreateSpritePool(child.Id, 2);
        Check(pool.TrySpawn(new(84, 84), Vector2.Zero, Color.FromArgb(0x52, 0xe4, 0xb5)), "spawn pool instance");
        game.PrepareCameraDraw(1);
        game.DrawCamera(canvas, camera.Id, 1);
        Check(canvas.Green.Count == 1, "offscreen pool owner");

        // An extreme zoom falls back to the original ordered walk.
        game.PrepareCameraDraw(1);
        Check(game.DrawCamera(canvas, wide.Id, 1) == scene.Objects.Count, "large camera fallback");
    }

    private sealed class DrawCapture : ICanvas2D
    {
        public List<RectangleF> Clips=[], Green=[];
        public RectangleF? Current;
        public int TextInside,TextOutside;
        public IDisposable PushClip(RectangleF r) { Check(Current==null,"unbalanced scope");Clips.Add(r);Current=r;return new Pop(this); }
        private sealed class Pop(DrawCapture capture): IDisposable { public void Dispose()=>capture.Current=null; }
        public void Clear(Color color) {}
        public void FillRectangle(RectangleF rect,Color color) { if ((color.ToArgb()&0xffffff)==0x52e4b5) Green.Add(rect); }
        public bool DrawSprite(string path,RectangleF rect)=>true;
        public void DrawText(string text,float x,float y,float size,Color color) { if (Current==null) TextOutside++;else TextInside++; }
    }
}
