using System;
using System.Numerics;
using Engine.Core;

public sealed partial class Expedition
{
    private const float GuardianReach = 48;
    private const int FirstDeathPuff = 16;
    private const int GuardianWindup = 66;
    private static readonly Vector2 AboveFeet = new(0, -15);

    private void FindEncounter(ScriptTick tick)
    {
        if (fighting) return;
        for (int i = 0; i < foes.Length; i++)
        {
            if (EnemyGroups[i] != group || foes[i].Health <= 0 ||
                !Near(CaptainFeet, foes[i].Feet, group == 2 && Zone == 3 ? 93 : 72))
                continue;
            fighting = true;
            guardianClock = 80;
            tick.Ui.SetVisible(BossPanel, GuardianActive);
            UpdateObjective(tick.Ui);
            if (GuardianActive)
            {
                Fill(tick.Objects, BossFill, 186, 1);
                tick.Ui.SetText(BossTell, "Warden");
                tick.Ui.SetVisible(ChapterPanel, false);
                PlaySound(tick, Sound.Windup);
            }
            RefreshHud(tick.Objects, tick.Ui);
            return;
        }
    }

    private void CheckEncounter(ScriptTick tick)
    {
        if (party[0].Health <= 0) return;
        for (int i = 0; i < foes.Length; i++)
            if (EnemyGroups[i] == group && foes[i].Health > 0) return;
        fighting = false;
        windup = 0;
        HideWarning(tick.Objects);
        tick.Ui.SetVisible(BossPanel, false);
        if (defeated != foes.Length)
        {
            group++;
            RefreshHud(tick.Objects, tick.Ui);
            PlaySound(tick, Sound.Crown);
            Notice(tick.Ui, "Wave cleared", "Follow the lanterns.");
            return;
        }
        cleared = true;
        progress.Set("route", Math.Max(Zone, Read("route")));
        if (Zone == Routes.Length)
        {
            progress.Set("finalLevel", level);
            progress.Set("finalBand", PartyCount);
        }
        Save(tick.Session);
        PlaySound(tick, Sound.Clear);
        RefreshHud(tick.Objects, tick.Ui);
        if (measureParticles)
        {
            long dropped = 0;
            foreach (Guid emitter in CombatEmitters)
                dropped += tick.Objects.ReadParticleEvents(emitter).Dropped;
            Console.WriteLine($"IsoBand particles {ZoneName}: peak={peakParticles} dropped={dropped}");
        }
        Notice(tick.Ui, Zone == 3 ? "Guardian defeated" : "Gate unsealed", "", gate: true);
        tick.Session.Log(FormattableString.Invariant($"IsoBand zone {Zone}: {defeated} defeated, {xp} XP, {coins} crowns."));
    }

    private void ActAlly(ScriptTick tick, int index)
    {
        ref var ally = ref party[index];
        if (ally.Health <= 0) return;
        if (ally.Cooldown > 0) ally.Cooldown--;
        if (ally.Strike > 0 && --ally.Strike == 0)
        {
            ResolveAllyStrike(tick, ref ally);
            return;
        }
        if (ally.Cooldown > 0 || ally.Hurt > 0) return;
        if (ally.Role == Role.Healer)
        {
            int target = -1;
            float fraction = .90f;
            for (int i = 0; i < PartyCount; i++)
            {
                float value = party[i].Health / (float)party[i].Max;
                if (party[i].Health > 0 && value < fraction && Near(ally.Feet, party[i].Feet, 80))
                {
                    target = i;
                    fraction = value;
                }
            }
            if (target >= 0) BeginStrike(ref ally, target, party[target].Feet, 91);
            return;
        }
        float reach = AttackRange(ally.Role);
        int foe = TargetFoe(ally.Feet, reach);
        if (foe < 0) return;
        int cooldown = ally.Role switch { Role.Archer => 47, Role.Shield => 43, _ => 36 };
        BeginStrike(ref ally, foe, foes[foe].Feet, cooldown);
    }

    private static float AttackRange(Role role) => role switch { Role.Archer => 91, Role.Shield => 32, _ => 36 };

    private void ResolveAllyStrike(ScriptTick tick, ref Actor ally)
    {
        int slot = ally.Target;
        if (ally.Role == Role.Healer)
        {
            ref var target = ref party[slot];
            if (target.Health <= 0) return;
            int restored = Math.Min(3 + level / 3, target.Max - target.Health);
            if (restored <= 0) return;
            target.Health += restored;
            WorldBar(tick.Objects, ref target);
            ShowHeal(tick, target.Feet, slot);
            FloatNumber(tick, target.Feet, ~slot, restored, heal: true);
            PlaySound(tick, Sound.Heal);
            RefreshHud(tick.Objects, tick.Ui);
            return;
        }
        ref var foe = ref foes[slot];
        if (foe.Health <= 0) return;
        if (ally.Role == Role.Archer)
        {
            LaunchArrow(tick, ally.Feet + AboveFeet, slot, gear + 1);
            return;
        }
        if (!Near(ally.Feet, foe.Feet, AttackRange(ally.Role) + 7) || !area.Clear(ally.Feet, foe.Feet)) return;
        if (ally.Role == Role.Captain) ShowSaber(tick, ally.Feet, foe.Feet);
        else
        {
            ShowShield(tick, foe.Feet);
            PushFoe(ref foe, ally.Feet, foe.Role == Role.Guardian ? .45f : 1.45f);
        }
        HitFoe(tick, slot, gear + (ally.Role == Role.Captain ? 4 : 2));
    }

    private int TargetFoe(Vector2 position, float range)
    {
        int closest = -1;
        float best = range * range;
        for (int i = 0; i < foes.Length; i++)
        {
            if (foes[i].Health <= 0 || EnemyGroups[i] != group) continue;
            float distance = Vector2.DistanceSquared(position, foes[i].Feet);
            if (distance >= best || !area.Clear(position, foes[i].Feet)) continue;
            best = distance;
            closest = i;
        }
        return closest;
    }

    private static void BeginStrike(ref Actor actor, int target, Vector2 position, int cooldown)
    {
        actor.Attack = 16;
        actor.Cooldown = cooldown;
        actor.Strike = 7;
        actor.Target = target;
        Face(ref actor, position);
    }

    private static void Face(ref Actor actor, Vector2 target)
    {
        float dx = target.X - actor.Feet.X;
        if (MathF.Abs(dx) >= .1f) actor.FacingLeft = dx < 0;
    }

    private void ActEnemies(ScriptTick tick)
    {
        for (int i = 0; i < foes.Length; i++)
        {
            ref var foe = ref foes[i];
            foe.Moving = false;
            if (foe.Health <= 0 || EnemyGroups[i] != group) continue;
            if (foe.PushTicks > 0)
            {
                Place(tick.Objects, ref foe, Slide(foe.Feet, foe.Push * (foe.PushTicks / 6f)));
                foe.PushTicks--;
            }
            if (foe.Role == Role.Guardian)
            {
                ActGuardian(tick, ref foe);
                continue;
            }
            int target = TargetParty(foe.Feet);
            ref var mark = ref party[target];
            if (foe.Cooldown > 0) foe.Cooldown--;
            if (foe.Strike > 0)
            {
                if (--foe.Strike == 0 && Near(foe.Feet, party[foe.Target].Feet, 29))
                    HitAlly(tick, foe.Target, Zone + 1);
                continue;
            }
            if (foe.Hurt > 2) continue;
            if (Near(foe.Feet, mark.Feet, 23))
            {
                if (foe.Cooldown == 0) BeginStrike(ref foe, target, mark.Feet, 69 + i % 4 * 6);
            }
            else
                Chase(tick, i, ref foe, mark.Feet);
        }
    }

    private int TargetParty(Vector2 position)
    {
        int target = 0;
        float best = Vector2.DistanceSquared(CaptainFeet, position);
        for (int i = 1; i < PartyCount; i++)
        {
            if (party[i].Health <= 0) continue;
            float distance = Vector2.DistanceSquared(party[i].Feet, position);
            if (party[i].Role == Role.Shield) distance *= .57f;
            if (distance >= best) continue;
            target = i;
            best = distance;
        }
        return target;
    }

    private void Chase(ScriptTick tick, int index, ref Actor foe, Vector2 target)
    {
        Vector2 direction = target - foe.Feet;
        if (!area.Clear(foe.Feet, target))
        {
            Span<Vector2> steps = stackalloc Vector2[16];
            if (area.FindPath(foe.Feet, target, steps) > 1) direction = steps[1] - foe.Feet;
        }
        // The half-diamond rafts leave enough lateral space for two attackers to pass.
        for (int j = 0; j < foes.Length; j++)
        {
            if (j == index || foes[j].Health <= 0 || EnemyGroups[j] != group) continue;
            var away = foe.Feet - foes[j].Feet;
            float gap = away.LengthSquared();
            if (gap is > .05f and < 320) direction += away * ((320 - gap) / 260);
        }
        AdvanceFoe(tick, ref foe, direction, 23 + Zone * 3);
    }

    private void AdvanceFoe(ScriptTick tick, ref Actor foe, Vector2 direction, float speed)
    {
        float len = direction.Length();
        if (len > .06f)
            Place(tick.Objects, ref foe, Slide(foe.Feet, direction * MathF.Min(1, speed * tick.DeltaTime / len)));
    }

    private void ActGuardian(ScriptTick tick, ref Actor foe)
    {
        if (windup > 0)
        {
            if (windup == 13) foe.Attack = 18;
            if (--windup > 0) return;
            HideWarning(tick.Objects);
            PlaceAccent(tick.Objects, Cracks, guardianTarget, new(104));
            tick.Objects.Tween(Cracks).Alpha(1, 0).Delay(.19f).Alpha(0, .47f);
            Burst(tick.Objects, Effect.Stone, guardianTarget);
            for (int i = 0; i < PartyCount; i++)
                if (Near(guardianTarget, party[i].Feet, GuardianReach)) HitAlly(tick, i, foe.Health * 2 > foe.Max ? 11 : 15);
            tick.Session.Screen.Shake(3, .22);
            PlaySound(tick, Sound.Hurt);
            guardianClock = foe.Health * 2 > foe.Max ? 140 : 105;
            tick.Ui.SetText(BossTell, foe.Health * 2 > foe.Max ? "Warden" : "Enraged");
            return;
        }
        if (guardianClock > 0) guardianClock--;
        if (guardianClock == 0 && Near(foe.Feet, CaptainFeet, 108))
        {
            windup = GuardianWindup;
            guardianTarget = CaptainFeet;
            Face(ref foe, guardianTarget);
            PlaceAccent(tick.Objects, Warning, guardianTarget, new(96));
            warningShown = true;
            warningFade = tick.Objects.Tween(Warning).Alpha(.56f, 0).Alpha(1, .24f).Alpha(.76f, .28f).Alpha(1, .35f);
            tick.Ui.SetText(BossTell, "Evade");
            PlaySound(tick, Sound.Windup);
            return;
        }
        if (!Near(foe.Feet, CaptainFeet, 31))
            Chase(tick, foes.Length - 1, ref foe, CaptainFeet);
    }

    private void CastPulse(ScriptTick tick)
    {
        skill = PulseInterval;
        PulseHud(tick.Ui);
        StartRing(tick, CaptainFeet);
        PlaySound(tick, Sound.Pulse);
        tick.Session.Screen.Shake(3.2f, .18);
        tick.Session.Screen.Flash(System.Drawing.Color.FromArgb(23, 255, 213, 125), .11);
        for (int i = 0; i < foes.Length; i++)
        {
            if (!fighting || EnemyGroups[i] != group || foes[i].Health <= 0 || !Near(CaptainFeet, foes[i].Feet, 98)) continue;
            PushFoe(ref foes[i], CaptainFeet, foes[i].Role == Role.Guardian ? .8f : 3.2f);
            HitFoe(tick, i, 8 + gear * 2, sound: false, emphasized: true);
        }
    }

    private static void PushFoe(ref Actor foe, Vector2 from, float strength)
    {
        var heading = foe.Feet - from;
        float length = heading.Length();
        foe.Push = length > .1f ? heading * (strength / length) : new(strength, 0);
        foe.PushTicks = 6;
    }

    private void HitFoe(ScriptTick tick, int slot, int damage, bool sound = true, bool emphasized = false)
    {
        ref var foe = ref foes[slot];
        if (foe.Health <= 0) return;
        foe.Health = Math.Max(0, foe.Health - damage);
        foe.Hurt = 9;
        var impact = foe.Feet - new Vector2(0, foe.Role == Role.Guardian ? 27 : 12);
        FoeImpact(tick.Objects, EnemyKinds[slot], impact);
        FloatNumber(tick, foe.Feet, slot, damage, emphasized: emphasized);
        if (sound && ticks - lastImpact > 7)
        {
            PlaySound(tick, Sound.Strike);
            lastImpact = ticks;
        }
        if (foe.Role == Role.Guardian) Fill(tick.Objects, BossFill, 186, foe.Health / (float)foe.Max);
        WorldBar(tick.Objects, ref foe);
        if (foe.Health > 0) return;

        foe.Fall = 27;
        Burst(tick.Objects, CombatEmitters[FirstDeathPuff + Math.Min(EnemyKinds[slot], 2)], impact + new Vector2(0, 6));
        RiseSoul(tick.Objects, impact);
        TossLoot(tick, foe.Feet);
        defeated++;
        xp += foe.Role == Role.Guardian ? 12 : 3;
        coins += foe.Role == Role.Guardian ? 12 : 2;
        int rank = 1 + xp / 12;
        if (rank != level)
        {
            level = rank;
            for (int i = 0; i < PartyCount; i++)
            {
                ref var member = ref party[i];
                int old = member.Max;
                member.Max = MaxHealth(member.Role);
                if (member.Health > 0) member.Health = Math.Min(member.Max, member.Health + member.Max - old + 5);
                WorldBar(tick.Objects, ref member);
            }
            FloatText(tick, CaptainFeet, ~0, $"<b><color=#B0FADA>Rank {level}</color></b>", $"<b>Rank {level}</b>", 25, lift: 14);
            ShowHeal(tick, CaptainFeet, 0);
            PlaySound(tick, Sound.Heal);
        }
        RefreshHud(tick.Objects, tick.Ui);
    }

    private void HitAlly(ScriptTick tick, int slot, int damage)
    {
        ref var victim = ref party[slot];
        if (victim.Health <= 0) return;
        if (victim.Role == Role.Shield) damage = Math.Max(1, damage - gear);
        victim.Health = Math.Max(0, victim.Health - damage);
        victim.Hurt = 10;
        WorldBar(tick.Objects, ref victim);
        FloatNumber(tick, victim.Feet, ~slot, damage, ally: true);
        Burst(tick.Objects, Effect.Steel, victim.Feet + AboveFeet);
        PlaySound(tick, Sound.Hurt);
        if (slot == 0) tick.Session.Screen.Flash(System.Drawing.Color.FromArgb(36, 222, 87, 104), .15);
        if (victim.Health == 0)
        {
            victim.Fall = 27;
            victim.Strike = 0;
            victim.Cooldown = 0;
            RiseSoul(tick.Objects, victim.Feet + AboveFeet);
        }
        RefreshHud(tick.Objects, tick.Ui);
    }

    private void UpdatePoses(IGameObjects objects)
    {
        for (int i = 0; i < PartyCount; i++) UpdatePose(objects, ref party[i]);
        for (int i = 0; i < foes.Length; i++) UpdatePose(objects, ref foes[i]);
    }

    private static void UpdatePose(IGameObjects objects, ref Actor actor)
    {
        if (actor.Mirrored != actor.FacingLeft && objects.TryGetLocalTransform(actor.Motion, out var pose))
        {
            actor.Mirrored = actor.FacingLeft;
            objects.SetLocalTransform(actor.Motion, pose with { Scale = new(actor.Mirrored ? -1 : 1, 1) });
        }
        if (actor.Health <= 0)
        {
            if (actor.Fall > 0)
            {
                actor.Animation.SetInt("Pose", 4);
                if (--actor.Fall == 0) objects.SetActive(actor.Root, false);
            }
            return;
        }
        actor.Animation.SetInt("Pose", actor.Hurt > 0 ? 3 : actor.Attack > 0 ? 2 : actor.Moving ? 1 : 0);
        if (actor.Attack > 0) actor.Attack--;
        if (actor.Hurt > 0) actor.Hurt--;
    }

    private static void WorldBar(IGameObjects objects, ref Actor actor)
    {
        if (actor.Role == Role.Guardian) return;
        bool hidden = actor.Health == 0;
        if (actor.BarHidden != hidden)
        {
            actor.BarHidden = hidden;
            float alpha = hidden ? 0 : 1;
            objects.Tween(actor.Bar).Alpha(alpha, 0);
            objects.Tween(actor.Fill).Alpha(alpha, 0);
        }
        if (actor.Health <= 0) return;
        if (objects.TryGetLocalTransform(actor.Fill, out var transform))
            objects.SetLocalTransform(actor.Fill, transform with { Scale = new(actor.Health / (float)actor.Max, 1) });
    }
}
