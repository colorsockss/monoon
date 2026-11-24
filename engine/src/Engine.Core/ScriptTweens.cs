using System.Drawing;
using System.Numerics;

namespace Engine.Core;

// Which clock a script tween follows: game time (pause, Step and time scale apply) or unscaled UI time.
public enum TweenClock { Game, Ui }

/// <summary>
/// A script tween on one object from <c>IGameObjects.Tween(id)</c>. Each call appends a step; steps play in order from the
/// value the property has when the step starts. A handle whose tween ended or whose object went away ignores further calls.
/// </summary>
public readonly struct ScriptTween
{
    private readonly SceneGame? game;
    private readonly int slot, generation;
    internal ScriptTween(SceneGame game, int slot, int generation) => (this.game, this.slot, this.generation) = (game, slot, generation);

    // Local position, scale and rotation (degrees) of a world object, as in SetLocalTransform.
    public ScriptTween Move(Vector2 to, float seconds, TweenEase ease = TweenEase.EaseInOut) => Add(TweenKind.Move, new(to, 0, 0), seconds, ease);
    public ScriptTween Scale(Vector2 to, float seconds, TweenEase ease = TweenEase.EaseInOut) => Add(TweenKind.Scale, new(to, 0, 0), seconds, ease);
    public ScriptTween Rotate(float degrees, float seconds, TweenEase ease = TweenEase.EaseInOut) => Add(TweenKind.Rotate, new(degrees, 0, 0, 0), seconds, ease);
    // The sprite's drawn RGB and opacity (0–1), like Tint and Opacity keys of a clip.
    public ScriptTween Color(System.Drawing.Color to, float seconds, TweenEase ease = TweenEase.EaseInOut) => Add(TweenKind.Color, new(to.R, to.G, to.B, 0), seconds, ease);
    public ScriptTween Alpha(float to, float seconds, TweenEase ease = TweenEase.EaseInOut) =>
        to is >= 0 and <= 1 ? Add(TweenKind.Alpha, new(to, 0, 0, 0), seconds, ease) : throw new ArgumentOutOfRangeException(nameof(to));
    // A UI element's laid-out box plus an offset: X/Y move it, Width/Height grow it. Ending at RectangleF.Empty restores the layout.
    public ScriptTween UiRect(RectangleF offset, float seconds, TweenEase ease = TweenEase.EaseInOut) =>
        Add(TweenKind.UiRect, new(offset.X, offset.Y, offset.Width, offset.Height), seconds, ease);
    // Any value: apply receives from…to on every pass. Cache the delegate to keep the tween allocation-free.
    public ScriptTween Float(float from, float to, float seconds, Action<float> apply, TweenEase ease = TweenEase.EaseInOut) =>
        Add(TweenKind.Float, new(from, to, 0, 0), seconds, ease, apply ?? throw new ArgumentNullException(nameof(apply)));
    public ScriptTween Delay(float seconds) => Add(TweenKind.Delay, default, seconds, TweenEase.Linear);

    public bool IsPlaying => game?.TweenPlaying(slot, generation) == true;
    public void Stop() => game?.StopTween(slot, generation);

    private ScriptTween Add(TweenKind kind, Vector4 to, float seconds, TweenEase ease, Action<float>? apply = null)
    {
        if (!float.IsFinite(seconds) || seconds < 0 || !float.IsFinite(to.X) || !float.IsFinite(to.Y) || !float.IsFinite(to.Z) ||
            !float.IsFinite(to.W) || !Enum.IsDefined(ease)) throw new ArgumentOutOfRangeException(nameof(seconds), "Tween steps need finite values and a duration of 0 or more.");
        game?.AddTweenStep(slot, generation, new() { Kind = kind, Seconds = seconds, Ease = ease, To = to, Apply = apply, Next = -1 });
        return this;
    }
}

internal enum TweenKind : byte { Delay, Move, Scale, Rotate, Color, Alpha, UiRect, Float }

internal struct TweenStep
{
    public TweenKind Kind;
    public TweenEase Ease;
    public float Seconds;
    public Vector4 From, To;
    public Action<float>? Apply;
    public int Next;
}

internal struct TweenTrack
{
    public SceneGame.Body? Owner;
    public int Head, Tail, Generation;
    public double Start; // clock time when the head step started
    public bool Ui, Began;
}

public sealed partial class SceneGame
{
    // Struct pools: a running tween allocates nothing; the arrays only grow to the most tweens and steps alive at once.
    private TweenTrack[] tweens = new TweenTrack[8];
    private TweenStep[] tweenSteps = new TweenStep[16];
    private int tweenCount, freeTweenStep = -1, tweenStepCount;

    public ScriptTween Tween(Guid id, TweenClock clock = TweenClock.Game)
    {
        if (closing || removed.Contains(id) || !byId.TryGetValue(id, out var body) || !body.Active || !Enum.IsDefined(clock))
            throw new InvalidOperationException("A tween needs an active object.");
        int slot = 0;
        while (slot < tweenCount && tweens[slot].Owner != null) slot++;
        if (slot == tweens.Length) Array.Resize(ref tweens, slot * 2);
        if (slot == tweenCount) tweenCount++;
        ref var track = ref tweens[slot];
        track = new() { Owner = body, Head = -1, Tail = -1, Generation = track.Generation + 1, Ui = clock == TweenClock.Ui, Start = Now(clock == TweenClock.Ui) };
        return new(this, slot, track.Generation);
    }

    private double Now(bool ui) => ui ? Time.UiSeconds : Time.GameSeconds;
    private bool Current(int slot, int generation) => slot < tweenCount && tweens[slot].Owner != null && tweens[slot].Generation == generation;
    internal bool TweenPlaying(int slot, int generation) => Current(slot, generation) && tweens[slot].Head >= 0;

    internal void AddTweenStep(int slot, int generation, TweenStep step)
    {
        if (!Current(slot, generation)) return;
        int index = freeTweenStep;
        if (index >= 0) freeTweenStep = tweenSteps[index].Next;
        else
        {
            if (tweenStepCount == tweenSteps.Length) Array.Resize(ref tweenSteps, tweenStepCount * 2);
            index = tweenStepCount++;
        }
        tweenSteps[index] = step;
        ref var track = ref tweens[slot];
        if (track.Head < 0) { track.Head = index; track.Start = Now(track.Ui); track.Began = false; }
        else tweenSteps[track.Tail].Next = index;
        track.Tail = index;
    }

    internal void StopTween(int slot, int generation) { if (Current(slot, generation)) FreeTween(slot); }

    private void FreeTween(int slot)
    {
        ref var track = ref tweens[slot];
        for (int step = track.Head; step >= 0;)
        {
            int next = tweenSteps[step].Next;
            tweenSteps[step] = new() { Next = freeTweenStep };
            freeTweenStep = step;
            step = next;
        }
        track.Owner = null;
        track.Head = track.Tail = -1;
    }

    // Runs with the routines of the same clock (StepRoutines): game tweens each tick, UI tweens each frame. A step that ends
    // in this pass hands its remaining time to the next one, so sequences keep exact durations.
    private void StepTweens(bool ui)
    {
        for (int slot = 0; slot < tweenCount; slot++)
        {
            if (tweens[slot].Owner is not { } owner || tweens[slot].Ui != ui) continue;
            if (!owner.Active || removed.Contains(owner.Id)) { FreeTween(slot); continue; }
            int generation = tweens[slot].Generation;
            double now = Now(ui);
            while (Current(slot, generation) && tweens[slot].Head is var head and >= 0)
            {
                if (!tweens[slot].Began) { tweenSteps[head].From = TweenValue(owner, tweenSteps[head]); tweens[slot].Began = true; }
                var step = tweenSteps[head];
                // The epsilon absorbs the rounding of repeated 1/60 additions, as in PendingWait.
                double t = step.Seconds == 0 ? 1 : Math.Min(1, (now - tweens[slot].Start + 1e-9) / step.Seconds);
                ApplyTween(owner, step, (float)Eased(step.Ease, t));
                if (t < 1 || !Current(slot, generation)) break;
                ref var track = ref tweens[slot]; // after Apply: a Float callback may have started tweens and grown the array
                track.Start += step.Seconds;
                track.Head = step.Next;
                track.Began = false;
                tweenSteps[head] = new() { Next = freeTweenStep };
                freeTweenStep = head;
                if (track.Head < 0) { track.Tail = -1; track.Owner = null; }
            }
        }
    }

    private static double Eased(TweenEase ease, double t) => ease switch
    {
        TweenEase.EaseIn => t * t, TweenEase.EaseOut => t * (2 - t),
        TweenEase.EaseInOut => .5 - .5 * Math.Cos(t * Math.PI), _ => t
    };

    private Vector4 TweenValue(Body owner, in TweenStep step)
    {
        TryGetLocalTransform(owner.Id, out var local);
        var color = owner.DisplayAppearance.Color;
        return step.Kind switch
        {
            TweenKind.Move => new(local.Position, 0, 0),
            TweenKind.Scale => new(local.Scale, 0, 0),
            TweenKind.Rotate => new(local.Rotation, 0, 0, 0),
            TweenKind.Color => new(color.R, color.G, color.B, 0),
            TweenKind.Alpha => new(color.A / 255f, 0, 0, 0),
            TweenKind.UiRect => Ui.BoxOffset(owner.Id) is var r ? new(r.X, r.Y, r.Width, r.Height) : default,
            _ => default
        };
    }

    private void ApplyTween(Body owner, in TweenStep step, float t)
    {
        var value = Affine2D.Lerp(step.From, step.To, t);
        switch (step.Kind)
        {
            case TweenKind.Move or TweenKind.Scale or TweenKind.Rotate:
                if (!TryGetLocalTransform(owner.Id, out var local)) return;
                SetLocalTransform(owner.Id, step.Kind switch
                {
                    TweenKind.Move => local with { Position = new(value.X, value.Y) },
                    TweenKind.Scale => local with { Scale = new(value.X, value.Y) },
                    _ => local with { Rotation = value.X }
                });
                break;
            case TweenKind.Color:
                owner.AppliedPose = owner.AppliedPose with { Tint = System.Drawing.Color.FromArgb(Channel(value.X), Channel(value.Y), Channel(value.Z)) };
                break;
            case TweenKind.Alpha:
                owner.AppliedPose = owner.AppliedPose with { Opacity = Math.Clamp(value.X, 0, 1) };
                break;
            case TweenKind.UiRect:
                Ui.SetBoxOffset(owner.Id, new(value.X, value.Y, value.Z, value.W));
                break;
            case TweenKind.Float:
                using (EnterScene(owner))
                    try { step.Apply!(step.To.X + (step.To.Y - step.To.X) * t); }
                    catch (Exception ex) { throw new InvalidOperationException($"{owner.Source.Name}: tween callback failed: {ex.Message}", ex); }
                break;
        }
        static int Channel(float v) => Math.Clamp((int)MathF.Round(v), 0, 255);
    }
}
