using System.Numerics;
using Engine.Core;

internal static class ClipChecks
{
    public static void Run(string root, string sceneFile)
    {
        string file = Path.Combine(root,"Assets","Door.clip"), meta = file + ".meta";
        byte[] original = File.ReadAllBytes(file), id = File.ReadAllBytes(meta);
        var clip = SpriteClip.Load(file);
        var time = new SpriteClipTimeline(clip.Frames);
        Require(time.FrameAt(.09) == 1 && time.FrameAt(.10) == 1 && time.FrameAt(.35) == 3 &&
            time.FrameAt(.37) == 3 && time.FrameAt(.37,true) == 0, "original door and half-open intervals");
        Require(time.FrameAt(time.Duration,true) == 0 && time.FrameAt(time.Duration) == 3 &&
            time.FrameAt(time.Duration * 1000 + .1,true) == 1, "end and multiple loops");
        var decimalAxis = new SpriteClipTimeline(new[]{.1f,.2f,.3f}.Select(d => clip.Frames[0] with { Seconds = d }).ToArray());
        Require(decimalAxis.FrameAt(Math.BitDecrement(.3)) == 1 && decimalAxis.FrameAt(.3) == 2 &&
            decimalAxis.FrameAt(.6,true) == 0,"exact accumulated decimal boundary");
        var assets = ProjectAssets.Scan(root);
        var expected = new[] { "DoorOpen2.png","Door.png","DoorOpen1.png","DoorOpen3.png" };
        float[] seconds = [.25f,.20f,.30f,.15f];
        var edited = expected.Select((name,i) => new SpriteFrame(assets.IdFor("Assets/"+name)!.Value,seconds[i])).ToArray();
        try
        {
            var sheet = assets.IdFor("Assets/DoorSheet.png")!.Value;
            SpriteRegion[] regions = [new(0,0,24,94), new(24,0,24,64), new(24,64,24,32), new(0,94,1,1)];
            var slices = ProjectAssets.ReadSheetSlices(Path.Combine(root, "Assets", "DoorSheet.png"));
            Require(clip.Frames.All(f => f.Sprite == sheet) && clip.Frames.Select(f => f.Source).SequenceEqual(regions) &&
                clip.Frames.Select(f => new SheetSlice(f.Source!, f.Canvas)).SequenceEqual(slices), "picker and clip use the same packed slices");
            using (var session = new GameSession(SceneDocument.Load(sceneFile)))
            {
                const float dt = 1f / 60;
                for (int i = 0; i < 100 && !session.State.Has("key"); i++) session.FixedUpdate(-Vector2.UnitX,dt);
                Require(session.State.Has("key"),"sheet clip trigger");
                var canvas = new CaptureCanvas();
                System.Drawing.RectangleF closed = default;
                for (int tick = 0; tick < 60; tick++)
                {
                    session.Draw(canvas,1);
                    var door = canvas.Images.Single(p => Path.GetFileName(p.Path) == "DoorSheet.png");
                    int cell = Math.Min(3,(int)(tick * (double)dt / .09));
                    if (tick == 0) closed = door.Rect;
                    var region = regions[cell];
                    Require(door.Source == region && door.Rect.X == closed.X && door.Rect.Y == closed.Y &&
                        Math.Abs(door.Rect.Width - closed.Width * region.Width / 24) < .001 &&
                        Math.Abs(door.Rect.Height - closed.Height * region.Height / 94) < .001, $"aligned trimmed frame at tick {tick}");
                    session.FixedUpdate(Vector2.Zero,dt);
                }
            }
            Console.WriteLine("PASS KeyRoom sheet source, all four frames and final hold over 60 rendered ticks");
            var oldTemplate = Directory.CreateDirectory(Path.Combine(root,".old-template")).FullName;
            File.WriteAllText(Path.Combine(oldTemplate,"MoNoonPlayer"),"");
            void Reject(string reason)
            {
                try { GameBuild.Export(root,oldTemplate,"osx-arm64",Path.Combine(root,"Builds"),GameSettings.Load(root)); }
                catch (InvalidDataException ex) when (ex.Message.Contains(reason)) { return; }
                throw new Exception("Expected sheet export error: " + reason);
            }
            File.WriteAllText(Path.Combine(oldTemplate,"MoNoonContent.version"),"5");
            Reject("content version 35");
            var rulesPath = Path.Combine(root, "Assets", "KeyRoomRules.data");
            var rulesYaml = File.ReadAllText(rulesPath);
            try
            {
                File.WriteAllText(rulesPath, "values: {}");
                Reject("KeyRoomRules.data");
                File.Delete(rulesPath);
                Reject("KeyRoomRules.data");
            }
            finally { File.WriteAllText(rulesPath, rulesYaml); }
            var invalidCanvas = clip.Frames.ToArray();
            invalidCanvas[1] = invalidCanvas[1] with { Canvas = new(24, 94, 0, 50) };
            ProjectAssets.Write(file, clip with { Frames = invalidCanvas });
            Reject("Door.clip, frame 2");
            var invalid = clip.Frames.ToArray();
            invalid[1] = invalid[1] with { Source = new SpriteRegion(int.MaxValue,0,2,2), Canvas = null };
            ProjectAssets.Write(file,clip with { Frames = invalid });
            Reject("Door.clip, frame 2");
            foreach (bool loop in new[] { false, true })
            {
                ProjectAssets.Write(file,new SpriteClip(edited,loop));
                var reopened = SpriteClip.Load(file);
                Require(reopened.Frames.SequenceEqual(edited) && reopened.Loop == loop && File.ReadAllBytes(meta).SequenceEqual(id),
                    "saved clip order, durations, loop and asset identity");
                var preview = new SpriteClipTimeline(reopened.Frames);
                using var game = new GameSession(SceneDocument.Load(sceneFile));
                const float dt = 1f / 60;
                for (int i = 0; i < 100 && !game.State.Has("key"); i++) game.FixedUpdate(-Vector2.UnitX,dt);
                Require(game.State.Has("key"),"collected key to trigger authored clip");
                var canvas = new CaptureCanvas();
                for (int tick = 0; tick <= 115; tick++)
                {
                    // At tick 0 the trigger has just called PlayEffect, following the animation step.
                    double t = tick * (double)dt, local = loop ? t % .9 : t;
                    int i = local < .25 ? 0 : local < .45 ? 1 : local < .75 ? 2 : 3;
                    game.Draw(canvas,1);
                    string[] drawn = canvas.Images.Select(p => Path.GetFileName(p.Path)).Where(p => expected.Contains(p)).ToArray();
                    Require(preview.FrameAt(t,loop) == i && drawn.SequenceEqual(new[]{expected[i]}),
                        $"preview/game output at tick {tick} ({t:F4}s), loop={loop}");
                    game.FixedUpdate(Vector2.Zero,dt);
                }
                Console.WriteLine($"PASS clip preview and actual KeyRoom door draw: 116 fixed ticks, loop={loop}");
            }
        }
        finally { File.WriteAllBytes(file,original); ProjectAssets.Scan(root); }
    }
    private static void Require(bool ok,string message)
    {
        if (!ok) throw new Exception("Clip: " + message);
    }
}
