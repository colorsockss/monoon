using System;
using System.Drawing;
using System.Numerics;
using Engine.Core;

public sealed class SummitWarden : GameBehaviour
{
    public Guid Player, Art, Flag, Panel, Tell, Dust, Shards;
    public Guid[] Pips = [], Embers = [], Waves = [];
    public float LeftWall = 16, RightWall = 368;

    internal enum Phase { Dormant, Arrival, Brace, Charge, Stunned, Recoil, Return, QuakeTell, Leap, Waves, EmberTell, Volley, Collapse, RaiseFlag }
    internal Phase State { get; private set; }
    internal int Health { get; private set; } = 3;
    private const float Width = 66, Floor = 176;
    private const int MovingPose = 0, TellPose = 1, HurtPose = 2;
    private static readonly Color AmberFlash = Color.FromArgb(70, 255, 209, 139);
    private readonly Vector2[] shots = new Vector2[3];
    private readonly Vector2[] velocities = new Vector2[3];
    private Vector2 home, travelFrom, flagHome, artHome;
    private int age, limit, chargeDirection = -1, facing = 1;
    private int emberMask;
    private float waveCenter;
    private TrailRunner? courier;
    private bool Enraged => Health == 1;

    public override void OnCreate(ScriptLife life)
    {
        life.Objects.TryGetPosition(life.ObjectId, out home);
        life.Objects.TryGetPosition(Flag, out flagHome);
        if (life.Objects.TryGetLocalTransform(Art, out var art)) artHome = art.Position;
        courier = life.Objects.GetComponent<TrailRunner>(Player);
        life.Ui.SetVisible(Panel, false);
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        if (!tick.Objects.TryGetPosition(Player, out var player)) return;
        if (State == Phase.Dormant)
        {
            if (player.X < 38) return;
            tick.Ui.SetVisible(Panel, true);
            tick.Session.Screen.ZoomPunch(.045f, .42);
            Warn(tick.Objects, home + new Vector2(33, -10));
            Enter(tick, Phase.Arrival, 64, "Stay on ledges", MovingPose);
            return;
        }

        age++;
        switch (State)
        {
            case Phase.Arrival:
                if (age >= limit) Brace(tick);
                break;
            case Phase.Brace:
                if ((age % 18) == 0) Pulse(tick.Objects, Dust, tick.Position + new Vector2(33, 49));
                TintTell(tick);
                if (age >= limit)
                {
                    Enter(tick, Phase.Charge, 0, "Charge / stay high", MovingPose);
                    tick.Objects.SetSpriteTint(Art, "#FFC292");
                }
                break;
            case Phase.Charge:
                Charge(tick);
                break;
            case Phase.Stunned:
                if (age >= limit) StartReturn(tick);
                break;
            case Phase.Recoil:
                Slide(tick, new(Math.Clamp(travelFrom.X - chargeDirection * 17, LeftWall, RightWall - Width), home.Y + 3));
                if (age >= limit) StartReturn(tick);
                break;
            case Phase.Return:
                Slide(tick, home);
                if (age >= limit)
                {
                    Enter(tick, Phase.QuakeTell, Enraged ? 32 : 47, "Quake / jump", TellPose);
                    Warn(tick.Objects, home + new Vector2(33, 44));
                }
                break;
            case Phase.QuakeTell:
                TintTell(tick);
                if (age >= limit) Enter(tick, Phase.Leap, Enraged ? 43 : 52, "Quake / watch feet", MovingPose);
                break;
            case Phase.Leap:
                Leap(tick);
                break;
            case Phase.Waves:
                StepWaves(tick);
                if (age >= limit)
                {
                    StopHazards(tick.Objects, Waves);
                    Enter(tick, Phase.EmberTell, Enraged ? 30 : 45, "Embers / dodge", TellPose);
                    Warn(tick.Objects, home + new Vector2(33, -15));
                }
                break;
            case Phase.EmberTell:
                TintTell(tick);
                if (age >= limit) StartVolley(tick, player);
                break;
            case Phase.Volley:
                StepVolley(tick);
                if (age >= limit)
                {
                    StopHazards(tick.Objects, Embers);
                    chargeDirection = -chargeDirection;
                    Brace(tick);
                }
                break;
            case Phase.Collapse:
                if (age >= limit) RaiseFlag(tick);
                break;
            case Phase.RaiseFlag:
                if (age >= limit)
                {
                    tick.Objects.SetColliderEnabled(Flag, true);
                    tick.Objects.Destroy(tick.ObjectId);
                }
                break;
        }
    }

    private void Brace(ScriptTick tick)
    {
        Face(tick.Objects, chargeDirection);
        Enter(tick, Phase.Brace, Enraged ? 62 : 84, "Brace / climb", TellPose);
        Warn(tick.Objects, tick.Position + new Vector2(33, 47));
    }

    private void Charge(ScriptTick tick)
    {
        float stop = chargeDirection < 0 ? LeftWall : RightWall - Width;
        float x = Approach(tick.Position.X, stop, Enraged ? 6.2f : 4.6f);
        Put(tick.Objects, tick.ObjectId, new(x, home.Y));
        if (age % 6 == 0) Pulse(tick.Objects, Dust, new(x + (chargeDirection < 0 ? 58 : 8), Floor - 5));
        if (x != stop) return;

        float wall = chargeDirection < 0 ? LeftWall : RightWall;
        Pulse(tick.Objects, Shards, new(wall - chargeDirection * 6, Floor - 32));
        tick.Session.Screen.Shake(3.1f, .20);
        courier?.WardenImpact(tick.Objects);
        Enter(tick, Phase.Stunned, Enraged ? 86 : 110, "Stun / stomp now", HurtPose);
        tick.Objects.SetSpriteTint(Art, "#90D8CF");
    }

    private void StartReturn(ScriptTick tick)
    {
        travelFrom = tick.Position;
        Enter(tick, Phase.Return, Enraged ? 54 : 65, Enraged ? "Enraged / armored" : "Armor closed", MovingPose);
        Face(tick.Objects, (int)MathF.Sign(home.X - travelFrom.X));
    }

    private void Slide(ScriptTick tick, Vector2 target)
    {
        float u = Math.Clamp(age / (float)limit, 0, 1);
        Put(tick.Objects, tick.ObjectId, travelFrom + (target - travelFrom) * u);
    }

    private void Leap(ScriptTick tick)
    {
        float u = Math.Clamp(age / (float)limit, 0, 1);
        Put(tick.Objects, tick.ObjectId, new(home.X, home.Y - 65 * 4 * u * (1 - u)));
        if (age < limit) return;

        waveCenter = home.X + Width / 2;
        Pulse(tick.Objects, Dust, new(waveCenter, Floor - 5));
        tick.Session.Screen.Shake(3.4f, .26);
        courier?.WardenImpact(tick.Objects);
        Enter(tick, Phase.Waves, Enraged ? 49 : 61, "Waves / stay high", MovingPose);
        SetWaves(tick.Objects, 26);
        foreach (Guid wave in Waves) tick.Objects.SetActive(wave, true);
    }

    private void StepWaves(ScriptTick tick) => SetWaves(tick.Objects, 26 + age * (Enraged ? 3.4f : 2.7f));

    private void SetWaves(IGameObjects objects, float radius)
    {
        Put(objects, Waves[0], new(waveCenter - radius - 21, Floor - 9), -1);
        Put(objects, Waves[1], new(waveCenter + radius - 1, Floor - 9));
    }

    private void StartVolley(ScriptTick tick, Vector2 player)
    {
        Vector2 crown = tick.Position + new Vector2(29, -15);
        float aim = player.X + 7 - crown.X;
        emberMask = 7;
        for (int i = 0; i < Embers.Length; i++)
        {
            shots[i] = crown + new Vector2((i - 1) * 7, 0);
            velocities[i] = new(Math.Clamp((aim + (i - 1) * 71) / (Enraged ? 48 : 55), -3.6f, 3.6f), -3.05f - .44f * i);
            Put(tick.Objects, Embers[i], shots[i]);
            tick.Objects.SetActive(Embers[i], true);
        }
        Pulse(tick.Objects, Dust, crown);
        Enter(tick, Phase.Volley, Enraged ? 79 : 92, "Ember volley", MovingPose);
    }

    private void StepVolley(ScriptTick tick)
    {
        for (int i = 0; i < Embers.Length; i++)
        {
            int bit = 1 << i;
            if ((emberMask & bit) == 0) continue;
            shots[i] += velocities[i];
            velocities[i].Y += Enraged ? .171f : .152f;
            if (shots[i].Y + 8 >= Floor || shots[i].X < LeftWall - 8 || shots[i].X > RightWall)
            {
                if (shots[i].Y + 8 >= Floor) Pulse(tick.Objects, Dust, shots[i] + new Vector2(4, 4));
                emberMask &= ~bit;
                tick.Objects.SetActive(Embers[i], false);
            }
            else Put(tick.Objects, Embers[i], shots[i]);
        }
    }

    public override void OnTriggerEnter(ScriptContact contact)
    {
        if (contact.OtherId != Player || courier is not { Completed: false } runner ||
            State is Phase.Dormant or Phase.Arrival or Phase.Collapse or Phase.RaiseFlag ||
            !contact.Objects.TryGetPosition(contact.ObjectId, out var warden) ||
            !contact.Objects.TryGetPosition(Player, out var player) ||
            !contact.Objects.Motion.TryGetMotion(Player, out var motion)) return;

        bool stomp = motion.Velocity.Y > 25 && player.Y + 13 < warden.Y;
        if (stomp && State == Phase.Stunned)
        {
            int direction = player.X + 7 < warden.X + Width / 2 ? -1 : 1;
            runner.BounceFromWarden(Player, contact.Objects, direction);
            Health--;
            contact.Ui.SetVisible(Pips[Health], false);
            runner.EnemyStomp(new(player.X + 7, warden.Y), contact.Objects, contact.Session, Health == 0, warden: true);
            travelFrom = warden;
            if (Health == 0) Defeat(contact, warden);
            else
            {
                SetPhase(contact.Objects, contact.Ui, Phase.Recoil, Enraged ? 30 : 38,
                    Enraged ? "Enraged / armored" : "Armor closed", HurtPose);
                contact.Objects.SetSpriteTint(Art, "#D98578");
            }
            return;
        }
        if (stomp)
        {
            // A closed shell rejects the stomp without consuming the open window.
            contact.Objects.Motion.SetVelocity(Player, new(motion.Velocity.X, -172));
            Pulse(contact.Objects, Dust, new(player.X + 7, warden.Y));
            runner.WardenImpact(contact.Objects);
            return;
        }

        // The shell moves below the deck during a charge. The raised lanes are safe.
        if ((State is Phase.Brace or Phase.Charge or Phase.Stunned or Phase.Recoil or Phase.Return) && player.Y + 20 <= 145)
            return;
        runner.Damage(Player, player, contact.Objects, contact.Ui, contact.Session);
    }

    private void Defeat(ScriptContact contact, Vector2 at)
    {
        StopHazards(contact.Objects, Waves);
        StopHazards(contact.Objects, Embers);
        contact.Objects.SetColliderEnabled(contact.ObjectId, false);
        SetPhase(contact.Objects, contact.Ui, Phase.Collapse, 44, "Warden defeated", HurtPose);
        contact.Objects.SetSpriteTint(Art, "#FFD7A3");
        Pulse(contact.Objects, Shards, at + new Vector2(33, 30));
        if (contact.Objects.TryGetLocalTransform(Art, out var art))
        {
            contact.Objects.Tween(Art).Move(art.Position + new Vector2(0, 34), .70f);
            contact.Objects.Tween(Art).Scale(new Vector2(art.Scale.X * 1.12f, .40f), .65f).Alpha(0, .08f);
        }
    }

    private void RaiseFlag(ScriptTick tick)
    {
        tick.Objects.SetActive(Art, false);
        tick.Objects.SetActive(Flag, true);
        tick.Objects.SetColliderEnabled(Flag, false);
        Put(tick.Objects, Flag, flagHome + new Vector2(0, 31));
        tick.Objects.Tween(Flag).Move(flagHome, 1);
        tick.Session.Screen.Flash(AmberFlash, .30);
        courier?.WardenSignal(tick.Objects);
        Enter(tick, Phase.RaiseFlag, 60, "Signal rising", HurtPose);
        tick.Ui.SetVisible(Panel, false);
    }

    private void Enter(ScriptTick tick, Phase next, int ticks, string label, int pose) =>
        SetPhase(tick.Objects, tick.Ui, next, ticks, label, pose);

    private void SetPhase(IGameObjects objects, IGameUi ui, Phase next, int ticks, string label, int pose)
    {
        (State, age, limit) = (next, 0, ticks);
        objects.Animator(Art)?.SetInt("Pose", pose);
        if (next is Phase.Charge or Phase.Stunned)
        {
            bool low = next == Phase.Charge;
            var position = artHome + new Vector2(0, low ? 20 : 0);
            objects.SetLocalTransform(Art, new(position, 0, new(facing, low ? .43f : 1)));
        }
        objects.SetSpriteTint(Art, null);
        ui.SetText(Tell, label);
    }

    private void TintTell(ScriptTick tick)
    {
        if ((age % 7) == 0) tick.Objects.SetSpriteTint(Art,
            tick.Ui.Preferences.FlashScale > 0 && (age / 7 & 1) != 0 ? "#FFD2A3" : "#AA887E");
    }

    private void Face(IGameObjects objects, int direction)
    {
        if (direction == 0 || direction == facing) return;
        facing = direction;
        if (objects.TryGetLocalTransform(Art, out var art))
            objects.SetLocalTransform(Art, art with { Scale = new(facing, 1) });
    }

    private void Warn(IGameObjects objects, Vector2 at) => courier?.Telegraph(objects, at);

    private static float Approach(float value, float target, float speed) =>
        value + Math.Clamp(target - value, -speed, speed);

    private static void StopHazards(IGameObjects objects, Guid[] hazards)
    {
        foreach (Guid id in hazards) objects.SetActive(id, false);
    }

    private static void Pulse(IGameObjects objects, Guid id, Vector2 at)
    {
        Put(objects, id, at);
        objects.EmitParticles(id);
    }

    private static void Put(IGameObjects objects, Guid id, Vector2 at, int facing = 1) =>
        objects.SetLocalTransform(id, new(at, 0, new(facing, 1)));
}
