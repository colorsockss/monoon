using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Engine.Core;
using Engine.Desktop;
using GameColor = System.Drawing.Color;

namespace Engine.Editor;

public sealed partial class SceneViewport
{
    internal void ResetInput()
    {
        input?.Reset();SyncIme();
        gamepads?.SuppressHeld();
        EndDrag();
    }

    public void Play() => Play(null);

    private SceneDebugServer? playDebugger;
    internal void Play(SceneDebugServer? debugger)
    {
        playDebugger = System.Diagnostics.Debugger.IsAttached ? null : debugger;
        var scripts = Scene.ProjectDirectory is {} root
            ? ProjectScripts.ForScenePlay(root, playDebugger != null ? playDebugger.Instrument : null) : null;
        game = new GameSession(Scene, scripts, null, null, Preview == null ? null : LayoutSettings);
        playScriptStamp = scripts?.ChangeStamp();
        if (SessionState.SceneLoadBudgetOverride() is {} budget) game.State.SceneLoadBudgetMilliseconds = budget;
        Profile.Reset();
        Profile.Work.Install();
        game.Current.Diagnostics=PhysicsDebug;
        game.State.Logged+=EmitLog;
        sounds.Attach(game.Mixer);
        game.LoadFailed += message => PlayFailed?.Invoke(message);
        try
        {
            FrameCapture = Engine.Desktop.SdlFrameCapture.Open();
            game.UseResources(new SceneResources(path =>
            {
                if (native != null) native.Prepare(path);
                else if (!bitmaps.TryGet(path, out _)) throw new InvalidDataException($"Could not load sprite {path}.");
            }, path =>
            {
                bitmaps.Invalidate(path); variants.Invalidate(path);
                native?.InvalidateSprite(path);
            }, sounds.PreparePlayback, sounds.Release, shader =>
            {
                if (native!=null) native.PrepareShader(shader);
                else shaderSheets.Get(shader,GameColor.White,shader.Additive);
            }, shader => { shaderSheets.Release(shader); native?.ReleaseShader(shader); })
            {
                // The SDL Scene panel uploads PNGs decoded on worker threads; the Skia preview loads on the UI thread.
                Decode = native == null ? null : (path, sound) => sound ? Engine.Desktop.SdlSounds.Decode(path) : Engine.Desktop.SdlCanvas.Decode(path),
                Upload = (path, sound, data) => { if (sound) sounds.PreparePlayback(path, data); else native!.Upload(path, data); },
            });
        }
        catch { FrameCapture?.Dispose(); FrameCapture = null; sounds.Dispose(); game.Dispose(); game = null; throw; }
        input = new(game.Actions, () => game?.InputUi, game.LogicalSize, () => game?.InputUi == null ? null : game.Current);
        gamepads = new SdlGamepads();
        loop = new GameLoop(game, game.Actions);
        playDebugger?.TakePausedTicks();
        lastTick = Stopwatch.GetTimestamp();
        Focus();
        timer.Start();
        RefreshScene();
    }

    private string? playScriptStamp;
    internal GameSession? PlaySession => game;
    // A fresh Scene Play compilation when a game script changed since Play started or since the last reload attempt.
    internal ProjectScripts? ChangedPlayScripts()
    {
        if (game == null || Scene.ProjectDirectory is not {} root) return null;
        var next = ProjectScripts.ForScenePlay(root, playDebugger != null ? playDebugger.Instrument : null);
        var stamp = next.ChangeStamp();
        if (stamp == playScriptStamp) return null;
        playScriptStamp = stamp;
        return next;
    }

    public void Stop()
    {
        timer.Stop();
        Profile.Work.Uninstall();
        gamepads?.Dispose(); gamepads=null; input=null;
        if (cursorRevision != 0) ShowGameCursor(null);
        var current = game;
        game = null;
        ProbeArmed=consumingProbe=false;
        PhysicsDebug.Clear();
        loop = null;
        try { current?.Dispose(); }
        catch (InvalidOperationException ex) { PlayFailed?.Invoke(ex.Message); }
        finally { playDebugger?.EndPlay(); playDebugger = null; FrameCapture?.Dispose(); FrameCapture = null; if(current!=null)current.State.Logged-=EmitLog; sounds.Dispose(); ResetInput(); RefreshScene(); }
    }

    private int cursorRevision;
    // Scene Play shows the game's custom cursor over this panel (the Mac SDL panel or the Avalonia preview) with the player's pixels.
    private void ShowGameCursor(SessionState? state)
    {
        cursorRevision = state?.CursorRevision ?? 0;
        var previous = Cursor;
        if (state?.Cursor is not {} cursor) { Cursor = null; native?.SetGameCursor(null, 0, 0); if (state != null) FeedbackLog.Write("cursor system"); }
        else if (native != null) native.SetGameCursor(CursorPixels.Load(cursor), cursor.HotX, cursor.HotY);
        else
        {
            var image = CursorPixels.Load(cursor);
            using var bitmap = new WriteableBitmap(new PixelSize(image.Width, image.Height), new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Rgba8888, Avalonia.Platform.AlphaFormat.Unpremul);
            using (var target = bitmap.Lock())
                for (int y = 0; y < image.Height; y++)
                    Marshal.Copy(image.Pixels, y * image.Width * 4, target.Address + y * target.RowBytes, image.Width * 4);
            Cursor = new Cursor(bitmap, new PixelPoint(cursor.HotX, cursor.HotY));
        }
        previous?.Dispose();
    }

    private void Tick()
    {
        if(sounds.TakeError() is {} audioError) PlayFailed?.Invoke(audioError);
        if (input != null && IsKeyboardFocusWithin && TopLevel.GetTopLevel(this) is Window { IsActive: true }) gamepads?.Poll(input.Pad);
        var now = Stopwatch.GetTimestamp();
        // Waiting at a script breakpoint consumes wall time but must not advance game or UI clocks.
        long paused = playDebugger?.TakePausedTicks() ?? 0;
        var elapsed = Math.Max(0, now - lastTick - paused) / (double)Stopwatch.Frequency;
        lastTick = now;
        if (TopLevel.GetTopLevel(this) is not Window { IsActive: true } && KeepSimulationActive?.Invoke()!=true)
        {
            Profile.Break();loop?.ResetElapsed();
            return;
        }
        var updating=Stopwatch.GetTimestamp();
        var prior=PlayWorld;
        try { FrameCapture?.BeginUpdate(); loop?.Advance(elapsed); FrameCapture?.EndUpdate(); }
        catch (InvalidOperationException ex)
        {
            Stop();
            PlayFailed?.Invoke(ex.Message);
            return;
        }
        Profile.AddUpdate(updating);
        if (game?.State is {} played)
        {
            if (played.PendingRumble is {} rumble) { played.PendingRumble = null; gamepads?.Rumble(rumble); }
            if (played.CursorRevision != cursorRevision) ShowGameCursor(played);
        }
        // Hierarchy, Inspector and diagnostics refreshes that follow the game update.
        using var editor=FrameProfiler.Sample(FrameProfiler.Editor);
        if(game!=null && prior!=game.Current){
            ProbeArmed=false;
            EmitLog(new(GameLogLevel.Info,$"Scene loaded: {game.Current.SceneLabel}",null,0));
        }
        SyncIme();RuntimeTick?.Invoke();
        RefreshScene();
    }

    internal static string BindingKey(KeyEventArgs e) =>
        InputControls.KeyName(e.PhysicalKey == PhysicalKey.None ? e.Key.ToString() : e.PhysicalKey.ToString());

    internal bool RouteUiKey(KeyEventArgs e,bool down)
    {
        bool editingBefore=game?.InputUi?.Editing!=null;
        long? timestamp = down && input != null && OperatingSystem.IsMacOS() && Child is MacSceneHost
            ? MacSceneHost.KeyTimestamp() : null;
        bool used=input?.Key(BindingKey(e),down,e.KeyModifiers.HasFlag(KeyModifiers.Shift),timestamp)==true;
        SyncIme();
        // Let the platform turn printable key-downs into marked/committed text.
        if(editingBefore && e.Key is not (Key.Tab or Key.Enter or Key.Escape or Key.Back or Key.Delete or Key.Left or Key.Right or Key.Home or Key.End))
            return false;
        return used;
    }

    private bool consumingProbe;
    internal void GamePointer(Point point,string? control=null,bool down=false)
    {
        if (down) Focus();
        var p=ToWorld(point);
        if(control=="Mouse.Left" && !down && consumingProbe){consumingProbe=false;return;}
        if(control=="Mouse.Left" && down && ProbeArmed && PlayWorld is {} world){
            ProbeArmed=false;consumingProbe=true;
            if(Selected!=null && world.TryDebugRay(Selected.Id,new((float)p.X,(float)p.Y),out var hit))
                ProbeFinished?.Invoke(hit is {} h?$"Raycast hit {world.ObjectLabel(h.Target.ObjectId)} · {h.Distance:0.#} units":"Raycast missed.");
            else ProbeFinished?.Invoke("Choose an active object and a point inside a camera view.");
            RefreshScene();return;
        }
        input?.Pointer(new((float)p.X,(float)p.Y),control,down); SyncIme();
    }
    internal void PointerLeft() => input?.Pointer(new(-1,-1));
    internal void GameWheel(Point point,double steps)
    {
        var p=ToWorld(point); input?.Wheel(new((float)p.X,(float)p.Y),(float)steps);
    }
}
