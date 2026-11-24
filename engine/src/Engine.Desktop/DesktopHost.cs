using System.Diagnostics;
using Brutal.SdlApi;
using Engine.Core;

namespace Engine.Desktop;

public static class DesktopHost
{
    public static void Run(string title, IGame2D game) => Run(title, game, null);

    public static void Run(string title, IGame2D game, InputRecorder? recorder) => Run(title, game, recorder, null);

    public static unsafe void Run(string title, IGame2D game, InputRecorder? recorder, PlayerStartupTrace? trace, Func<ShaderClip, RgbaImage>? previewShader = null, IGameTextRasterizer? textRasterizer = null)
    {
        Sdl.Init.Initialize(SdlInitFlags.Video);
        try
        {
            var size = game.LogicalSize;
            var state = (game as GameSession)?.State ?? (game as SceneGame)?.State;
            var preferences = SdlDesktopDisplay.Read(state, size);
            using var window = Sdl.Video.CreateWindow(title, preferences.WindowSize.Width, preferences.WindowSize.Height,
                SdlWindowFlags.Resizable | SdlWindowFlags.HighPixelDensity);
            using var renderer = Sdl.Render.CreateRenderer(window, null!);
            bool integer = ((game as GameSession)?.Current ?? game as SceneGame)?.IntegerScale == true;
            renderer.SetRenderLogicalPresentation(size.Width, size.Height,
                integer ? SdlRendererLogicalPresentation.IntegerScale : SdlRendererLogicalPresentation.Letterbox);
            // Pixel art samples PNG sprites without filtering. SdlText keeps its labels linear.
            if (integer && !renderer.SetDefaultTextureScaleMode(SdlScaleMode.Nearest)) throw new InvalidOperationException(Sdl.PInvoke.GetError());
            var clip = new SdlRect { w = size.Width, h = size.Height };
            renderer.SetRenderClipRect(&clip);
            var display = new SdlDesktopDisplay(window, renderer, state, preferences);
            display.Restore();
            if (state != null) state.Display = display;
            try { RunLoop(game, window, renderer, display, recorder, trace, previewShader, textRasterizer); }
            finally
            {
                if (state != null) state.Display = null;
                display.Save();
            }
        }
        finally
        {
            Sdl.Init.Quit();
        }
    }

    private static unsafe void RunLoop(IGame2D game, SdlWindow window, SdlRenderer renderer, SdlDesktopDisplay display, InputRecorder? recorder, PlayerStartupTrace? trace, Func<ShaderClip, RgbaImage>? previewShader, IGameTextRasterizer? textRasterizer)
    {
        using var canvas = new SdlCanvas(renderer, batchSprites: true, previewShader: previewShader, textRasterizer: textRasterizer) { LogicalSize = game.LogicalSize };
        using var sounds = new SdlSounds();
        using var resources = Resources(canvas, sounds);
        if (game is GameSession session)
        {
            sounds.Attach(session.Mixer);
            session.UseResources(resources);
        }
        else if(game is SceneGame scene)
        {
            sounds.Attach(scene.Mixer);
            scene.PrepareAssets(canvas.Prepare,sounds.PreparePlayback,canvas.PrepareShader);
        }
        var gameplay = game as GameSession;
        var actions = gameplay?.Actions ?? (game as SceneGame)?.Actions ?? new(game.Bindings);
        var loop = new GameLoop(game, actions);
        if (trace != null) loop.FirstActionProcessed = action => trace.Mark("first_action", action);
        var input = new GameInputRouter(actions,
            () => gameplay?.InputUi ?? (game as SceneGame)?.InputUi, game.LogicalSize,
            () => gameplay != null ? (gameplay.InputUi == null ? null : gameplay.Current) : game as SceneGame);
        using var capture = SdlFrameCapture.Open();
        var keyFrames = recorder != null ? Environment.GetEnvironmentVariable("MONOON_KEY_FRAME_DIR") : null;
        if (!string.IsNullOrEmpty(keyFrames)) Directory.CreateDirectory(keyFrames);
        using var pads = new SdlGamepads();
        using var cursor = new WindowCursor();
        var played = gameplay?.State ?? (game as SceneGame)?.State;
        var previousTime = Stopwatch.GetTimestamp();
        var focused = true;

        Guid? editing=null;
        System.Drawing.Rectangle bounds=default;
        ScreenUi? Ui()=>gameplay?.InputUi??(game as SceneGame)?.InputUi;
        static long EventTime(ulong stamp)
        {
            ulong now = Sdl.PInvoke.GetTicksNS();
            long received = Stopwatch.GetTimestamp();
            return stamp == 0 || stamp >= now ? received :
                received - (long)((now - stamp) * (Stopwatch.Frequency / 1_000_000_000d));
        }
        void Send(ReplayInput e, long timestamp)
        {
            recorder?.Observe(e, timestamp);
            e.Dispatch(input, loop, timestamp);
        }
        void Pad(string control, float value) =>
            Send(new(ReplayInputKind.Pad, Control: control, Value: value), Stopwatch.GetTimestamp());
        void SyncIme()
        {
            var ui=Ui();var next=focused?ui?.Editing:null;
            if(next!=editing)
            {
                if(editing!=null)window.StopTextInput();
                editing=next;bounds=default;
                if(next!=null)window.StartTextInput();
            }
            if(next is Guid id && ui!.Bounds(id) is {} b && renderer.RenderCoordinatesToWindow(b.Left,b.Top,out var x,out var y)
                && renderer.RenderCoordinatesToWindow(b.Right,b.Bottom,out var r,out var d))
            {
                var area=new System.Drawing.Rectangle((int)x,(int)y,(int)(r-x),(int)(d-y));
                if(area!=bounds){bounds=area;var rect=new SdlRect{x=area.X,y=area.Y,w=area.Width,h=area.Height};window.SetTextInputArea(&rect,0);}
            }
        }
        trace?.BeginFrames();
        while (true)
        {
            bool keyPressed = false;
            while (Sdl.Events.PollEvent(out var ev))
            {
                switch (ev.type)
                {
                    case SdlEventType.Quit:
                    case SdlEventType.WindowCloseRequested:
                    case SdlEventType.KeyDown when ev.key.scancode == SdlScancode.Escape && Ui()?.Editing==null && Ui()?.DropdownOpen!=true && actions.CaptureAction==null:
                        return;
                    case SdlEventType.WindowResized:
                        // Read live dimensions: queued resize events can predate a fullscreen transition.
                        display.RememberWindowSize();
                        break;
                    case SdlEventType.WindowFocusLost:
                    case SdlEventType.WindowFocusGained:
                        focused = ev.type == SdlEventType.WindowFocusGained;
                        Send(new(ReplayInputKind.Reset), EventTime(ev.common.timestamp));
                        pads.SuppressHeld();
                        previousTime = Stopwatch.GetTimestamp();
                        break;
                    case SdlEventType.WindowMouseLeave:
                        Send(new(ReplayInputKind.Pointer, X: -1, Y: -1), EventTime(ev.common.timestamp)); break;
                    case SdlEventType.MouseMotion when focused:
                        if (renderer.RenderCoordinatesFromWindow(ev.motion.x,ev.motion.y,out var mx,out var my))
                            Send(new(ReplayInputKind.Pointer, X: mx, Y: my), EventTime(ev.motion.timestamp));
                        break;
                    case SdlEventType.MouseButtonDown or SdlEventType.MouseButtonUp when focused:
                        var control=(int)ev.button.button switch { 1=>"Mouse.Left",2=>"Mouse.Middle",3=>"Mouse.Right",_=>null };
                        if (control!=null && renderer.RenderCoordinatesFromWindow(ev.button.x,ev.button.y,out var ux,out var uy))
                            Send(new(ReplayInputKind.Pointer, Control: control, Down: ev.type == SdlEventType.MouseButtonDown, X: ux, Y: uy), EventTime(ev.button.timestamp));
                        break;
                    case SdlEventType.MouseWheel when focused:
                        if (renderer.RenderCoordinatesFromWindow(ev.wheel.mouse_x,ev.wheel.mouse_y,out var wx,out var wy))
                            Send(new(ReplayInputKind.Wheel, X: wx, Y: wy, Value: ev.wheel.direction == SdlMouseWheelDirection.Flipped ? -ev.wheel.y : ev.wheel.y), EventTime(ev.wheel.timestamp));
                        break;
                    case SdlEventType.TextEditing when focused:
                        Send(new(ReplayInputKind.Compose, Text: System.Runtime.InteropServices.Marshal.PtrToStringUTF8((nint)ev.edit.text) ?? ""), EventTime(ev.edit.timestamp)); break;
                    case SdlEventType.TextInput when focused:
                        Send(new(ReplayInputKind.Commit, Text: System.Runtime.InteropServices.Marshal.PtrToStringUTF8((nint)ev.text.text) ?? ""), EventTime(ev.text.timestamp)); break;
                    case SdlEventType.KeyDown when focused:
                        keyPressed |= ev.key.repeat == 0;
                        Send(new(ReplayInputKind.Key, Control: InputControls.KeyName(ev.key.scancode.ToString()), Down: true, Reverse: ((int)ev.key.mod & 3) != 0 ? true : null), EventTime(ev.key.timestamp)); break;
                    case SdlEventType.KeyUp:
                        Send(new(ReplayInputKind.Key, Control: InputControls.KeyName(ev.key.scancode.ToString()), Down: false), EventTime(ev.key.timestamp)); break;
                }
                SyncIme();
            }

            if(focused) pads.Poll(Pad);
            if(sounds.TakeError() is {} audioError)Console.Error.WriteLine(audioError);
            var now = Stopwatch.GetTimestamp();
            var elapsed = Stopwatch.GetElapsedTime(previousTime, now);
            previousTime = now;
            if (focused)
            {
                capture?.BeginUpdate();
                loop.Advance(elapsed.TotalSeconds);
                recorder?.EndFrame(elapsed);
                if (played?.PendingRumble is { } rumble) { played.PendingRumble = null; pads.Rumble(rumble); }
                cursor.Sync(played);
                capture?.EndUpdate();
            }
            SyncIme();
            long calls = canvas.DrawSubmissions;
            long drawBytes = capture != null ? GC.GetAllocatedBytesForCurrentThread() : 0;
            using (FrameProfiler.Sample(FrameProfiler.Render))
            {
                game.Draw(canvas, focused ? loop.Interpolation : 1f);
                canvas.FlushSprites();
            }
            if (focused && keyPressed && !string.IsNullOrEmpty(keyFrames))
                SdlFrameCapture.SaveImage(renderer, Path.Combine(keyFrames, $"{recorder!.FrameCount - 1}.png"));
            using (FrameProfiler.Sample(FrameProfiler.Present)) renderer.RenderPresent();
            trace?.Presented();
            capture?.Frame(gameplay?.Current ?? game as SceneGame, canvas.DrawSubmissions - calls, drawBytes);
            FrameProfiler.Current?.EndFrame();
        }
    }

    /// <summary>Scene resources of a game: PNG and audio files decode on worker threads during split scene loading.</summary>
    public static SceneResources Resources(SdlCanvas canvas, SdlSounds sounds) =>
        new(canvas.Prepare, canvas.InvalidateSprite, sounds.PreparePlayback, sounds.Release, canvas.PrepareShader, canvas.ReleaseShader)
        {
            Decode = (path, sound) => sound ? SdlSounds.Decode(path) : SdlCanvas.Decode(path),
            Upload = (path, sound, data) => { if (sound) sounds.PreparePlayback(path, data); else canvas.Upload(path, data); },
        };
}
