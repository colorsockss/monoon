using System.Numerics;

namespace Engine.Core;

public enum WorldPointerPhase { Press, Drag, Release, Click, Cancel }

// Opt in on a GameBehaviour to receive the primary pointer on the object's local rectangle.
public interface IWorldPointerHandler
{
    void OnWorldPointer(WorldPointerEvent pointer);
}

public readonly record struct WorldPointerEvent(WorldPointerPhase Phase, Guid ObjectId, Guid CameraId,
    Vector2 ScreenPosition, Vector2 WorldPosition, Vector2 GrabLocalPosition, IGameObjects Objects, IGameUi Ui);

public sealed partial class SceneGame
{
    private sealed class PointerCapture(Body target, Body lens, Vector2 screen, Vector2 world, Vector2 grab)
    {
        internal readonly Body Target = target, Lens = lens;
        internal readonly Vector2 Origin = screen, Grab = grab;
        internal Vector2 Screen = screen, World = world;
        internal bool Dragged;
    }

    private PointerCapture? capture;
    private float pointerFrame = 1;
    internal void SetPointerFrame(float interpolation) => pointerFrame = interpolation;

    private Body? CameraAt(Vector2 screen, float interpolation)
    {
        bool Contains(Body? lens) => lens is { Active: true } &&
            Sample(lens, interpolation).Viewport.Contains((int)MathF.Floor(screen.X), (int)MathF.Floor(screen.Y));
        if (playback == null)
            for (int i = overlays.Length - 1; i >= 0; i--)
                if (Contains(overlays[i])) return overlays[i];
        return Contains(ViewCamera) ? ViewCamera : null;
    }

    private static bool Hit(Body body, Matrix3x2 matrix, Vector2 world, out Vector2 local)
    {
        local = default;
        if (!Matrix3x2.Invert(matrix, out var inverse)) return false;
        local = Vector2.Transform(world, inverse);
        return local.X >= 0 && local.Y >= 0 && local.X < body.Size.X && local.Y < body.Size.Y;
    }

    internal bool PressWorldPointer(Vector2 screen)
    {
        if (capture != null) return true;
        if (!started || closing || Time.IsPaused || playback != null || Ui.ActiveInputMode.HasValue ||
            CameraAt(screen, pointerFrame) is not {} lens) return false;
        var worldPoint = Sample(lens, pointerFrame).ScreenToWorld(screen);
        drawOrder.Sample(pointerFrame);
        for (int i = drawOrder.Items.Length - 1; i >= 0; i--)
        {
            var item = drawOrder.Items[i];
            var body = bodies[item.Index];
            if (item.Map != null || body.Interface || !body.Active || body.PointerHandlers.Length == 0 ||
                !Hit(body, DrawMatrix(item.Index, pointerFrame), worldPoint, out var grab)) continue;
            capture = new(body, lens, screen, worldPoint, grab);
            NotifyPointer(capture, WorldPointerPhase.Press);
            return true;
        }
        return false;
    }

    private bool PointerValid(PointerCapture held) => !closing && !Time.IsPaused && playback == null &&
        Ui.ReceivesInput(held.Target.Id) && held.Target.Active && held.Lens.Active &&
        (held.Lens.Camera!.Output != Camera2DComponent.Primary || held.Lens == ViewCamera);

    internal void MoveWorldPointer(Vector2 screen)
    {
        if (capture is not {} held) return;
        if (!PointerValid(held)) { CancelWorldPointer(); return; }
        var worldPoint = Sample(held.Lens, 1).ScreenToWorld(screen);
        if (screen == held.Screen && worldPoint == held.World) return;
        held.Screen = screen;
        held.World = worldPoint;
        held.Dragged |= Vector2.DistanceSquared(screen, held.Origin) >= 16;
        NotifyPointer(held, WorldPointerPhase.Drag);
    }

    // The screen point stays fixed when only the camera moves.
    private void FollowPointerCamera()
    {
        if (capture is {} held) MoveWorldPointer(held.Screen);
    }

    internal bool ReleaseWorldPointer(Vector2 screen)
    {
        if (capture is not {} held) return false;
        MoveWorldPointer(screen);
        if (capture != held) return true;
        bool click = !held.Dragged && CameraAt(screen, 1) == held.Lens &&
            Hit(held.Target, held.Target.MatrixAt(held.Target.Position), held.World, out _);
        capture = null;
        NotifyPointer(held, WorldPointerPhase.Release);
        if (click && PointerValid(held)) NotifyPointer(held, WorldPointerPhase.Click);
        return true;
    }

    internal void CancelWorldPointer()
    {
        if (capture is not {} held) return;
        capture = null;
        NotifyPointer(held, WorldPointerPhase.Cancel);
    }

    private void NotifyPointer(PointerCapture held, WorldPointerPhase phase)
    {
        var input = new WorldPointerEvent(phase, held.Target.Id, held.Lens.Id, held.Screen, held.World, held.Grab, this, Ui);
        using var scope = EnterScene(held.Target);
        foreach (var handler in held.Target.PointerHandlers)
        {
            if (phase is WorldPointerPhase.Press or WorldPointerPhase.Drag && capture != held) break;
            if (phase != WorldPointerPhase.Cancel && !held.Target.Active) break;
            try { handler.OnWorldPointer(input); }
            catch (Exception ex) { throw ScriptFailure((GameBehaviour)handler, nameof(handler.OnWorldPointer), ex); }
        }
    }
}
