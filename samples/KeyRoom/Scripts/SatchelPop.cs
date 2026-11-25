using System.Drawing;
using System.Numerics;
using Engine.Core;
using KeyRoom.Rules;

// The satchel pops when a coin goes in: it grows around its centre with a gold flash, then settles back (script tweens).
public sealed class SatchelPop : GameBehaviour
{
    private static readonly Color Gold = Color.FromArgb(255, 214, 92);
    private bool had;
    private Vector2 home;
    private Color color;

    public override void OnCreate(ScriptLife life)
    {
        had = life.Session.Has(LootRules.CoinFlag); // a restored save does not pop
        if (life.Objects.TryGetLocalTransform(life.ObjectId, out var local)) home = local.Position;
        life.Objects.TryGetSpriteColor(life.ObjectId, out color);
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        if (had || !tick.Session.Has(LootRules.CoinFlag)) return;
        had = true;
        const float grow = 1.4f, up = .1f, down = .15f;
        // Scaling is about the local origin (top-left), so the move keeps the centre in place.
        var size = new Vector2(14, 12);
        tick.Objects.Tween(tick.ObjectId).Scale(new(grow, grow), up, TweenEase.EaseOut).Scale(Vector2.One, down, TweenEase.EaseIn);
        tick.Objects.Tween(tick.ObjectId).Move(home - size * (grow - 1) / 2, up, TweenEase.EaseOut).Move(home, down, TweenEase.EaseIn);
        tick.Objects.Tween(tick.ObjectId).Color(Gold, up, TweenEase.EaseOut).Color(color, down, TweenEase.EaseIn);
    }
}
