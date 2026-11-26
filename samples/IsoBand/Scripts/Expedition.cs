using System;
using System.Numerics;
using Engine.Core;

public sealed class Expedition : GameBehaviour
{
    public Guid Floor, Navigation, Camera, Portal, Campfire, Hud, Hint;
    public Guid[] Companions = [], Enemies = [];
    public SceneReference Next, Haven;
    public SceneReference[] Routes = [];
    public int Zone;

    private const float WalkSpeed = 86;
    private const int SaveVersion = 1;
    private const string CompleteHint = "Expedition complete. N / Back starts a new expedition.";
    private static readonly Vector2[] Formation = [new(-19, 10), new(19, 12), new(0, 24), new(-31, 23), new(31, 23)];
    private readonly Vector2[] path = new Vector2[64];
    private NavArea2D area = null!;
    private NavCrowd2D crowd = null!;
    private Actor[] allies = [], foes = [];
    private Actor captain;
    private GameData progress = null!;
    private int routeCount, waypoint, level, recruits, gear, xp, coins, health, defeated, skill, ticks;
    private bool cleared;

    public int Defeated => defeated;

    private struct Actor
    {
        public Guid Id;
        public Vector2 Feet, Offset;
        public int Health, Cooldown;
    }

    public override void OnCreate(ScriptLife life)
    {
        var objects = life.Objects;
        progress = life.Session.Storage.IsAvailable ? life.Session.Storage.LoadSlot(1, SaveVersion) ?? new() : new();
        xp = Read("xp");
        coins = Read("coins");
        level = 1 + xp / 9;
        recruits = Math.Clamp(Read("recruits", 2), 2, Math.Min(5, Companions.Length));
        gear = Math.Clamp(Read("gear", 1), 1, 3);
        health = MaxHealth;
        captain = LoadActor(objects, life.ObjectId, new(12, 33), health);
        area = objects.Navigation.BakeArea(Navigation) ?? throw new InvalidOperationException("IsoBand needs a navigation region.");
        crowd = new NavCrowd2D(area, Math.Max(1, Companions.Length)) { Speed = 100, MaxPlansPerStep = 4 };
        allies = new Actor[recruits];
        for (int i = 0; i < Companions.Length; i++)
        {
            objects.SetActive(Companions[i], i < recruits);
            if (i >= recruits) continue;
            allies[i] = LoadActor(objects, Companions[i], new(13, 31), 1);
            crowd.Add(allies[i].Feet, allies[i].Feet);
        }
        foes = new Actor[Enemies.Length];
        for (int i = 0; i < foes.Length; i++)
            foes[i] = LoadActor(objects, Enemies[i], i == foes.Length - 1 && Zone == 3 ? new(18, 45) : new(12, 22),
                i == foes.Length - 1 && Zone == 3 ? 39 : 4 + Zone);
        ShowHud(life.Ui);
        if (Zone == 0 && ExpeditionComplete) life.Ui.SetText(Hint, CompleteHint);
    }

    private int MaxHealth => 12 + 2 * level;
    private int Read(string key, int fallback = 0) => (int)progress.GetNumber(key, fallback);
    private bool ExpeditionComplete => Routes.Length > 0 && Read("route") >= Routes.Length;

    private static Actor LoadActor(IGameObjects objects, Guid id, Vector2 offset, int hitPoints)
    {
        if (!objects.TryGetPosition(id, out var position)) throw new InvalidOperationException($"Missing actor {id}.");
        return new() { Id = id, Offset = offset, Feet = position + offset, Health = hitPoints };
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        ticks++;
        MoveCaptain(tick);
        if (ticks % 8 == 1)
            for (int i = 0; i < allies.Length; i++) crowd.SetGoal(i, captain.Feet + Formation[i]);
        crowd.Step(tick.DeltaTime);
        for (int i = 0; i < allies.Length; i++)
            Place(tick.Objects, ref allies[i], crowd.Positions[i]);

        if (Zone == 0)
        {
            VisitHaven(tick);
            return;
        }

        if (health <= 0)
        {
            tick.Ui.SetText(Hint, "The band retreated. Reopen the expedition from Haven.");
            tick.Session.LoadScene(Haven.Id);
            return;
        }

        if (skill > 0 && --skill == 0) ShowHud(tick.Ui);
        if (tick.Input.Pressed("Skill") && skill == 0 && !cleared)
        {
            skill = 190;
            for (int i = 0; i < foes.Length; i++)
                if (foes[i].Health > 0 && Near(foes[i].Feet, captain.Feet, 86))
                    Hit(tick, ref foes[i], 4 + gear);
            tick.Session.Screen.Shake(2, .12f);
            ShowHud(tick.Ui);
        }
        if (!cleared)
        {
            ActEnemies(tick);
            AutoAttack(tick, ref captain, 33, 2 + gear, 22);
            for (int i = 0; i < allies.Length; i++)
            {
                if (i == 2) Heal(tick, ref allies[i]);
                else if (i is 1 or 4) AutoAttack(tick, ref allies[i], 65, gear, 28);
                else AutoAttack(tick, ref allies[i], 30, gear, 25);
            }
            if (defeated == foes.Length)
            {
                cleared = true;
                progress.Set("route", Math.Max(Zone, Read("route")));
                Save(tick.Session);
                tick.Ui.SetText(Hint, Zone == 3 ? "The Stag fell. Enter at the portal to return." : "Field cleared. Enter at the portal.");
                tick.Session.Log(FormattableString.Invariant($"IsoBand zone {Zone}: {defeated} defeated, {xp} XP, {coins} crowns."));
            }
        }
        if (tick.Input.Pressed("Rest") && NearObject(tick.Objects, Campfire, 46) && !HasThreat())
        {
            health = MaxHealth;
            ShowHud(tick.Ui);
        }
        if (tick.Input.Pressed("Travel") && cleared && NearObject(tick.Objects, Portal, 54))
        {
            if (Next.Id != Guid.Empty) tick.Session.LoadScene(Next.Id);
            else tick.Session.LoadScene(Haven.Id, static session => session.Log("IsoBand zone 4: Returned to Haven."));
        }
    }

    private void VisitHaven(ScriptTick tick)
    {
        if (ExpeditionComplete && tick.Input.Pressed("NewExpedition"))
        {
            progress.Set("route", 0);
            Save(tick.Session);
            tick.Session.LoadScene(Routes[0].Id);
            return;
        }
        if (tick.Input.Pressed("Hire") && NearObject(tick.Objects, Campfire, 82))
        {
            if (coins >= 6 && recruits < Math.Min(Companions.Length, level >= 3 ? 5 : 3))
            {
                recruits++;
                coins -= 6;
                Save(tick.Session);
                tick.Session.LoadScene(Haven.Id);
                return;
            }
            tick.Ui.SetText(Hint, "HIRE   6 crowns. Higher levels open more places.");
        }
        if (tick.Input.Pressed("Gear") && NearObject(tick.Objects, Campfire, 82))
        {
            if (gear < 3 && coins >= 8)
            {
                coins -= 8;
                gear++;
                Save(tick.Session);
                ShowHud(tick.Ui);
            }
            else tick.Ui.SetText(Hint, "GEAR   8 crowns per upgrade, up to tier 3.");
        }
        if (tick.Input.Pressed("Travel") && NearObject(tick.Objects, Portal, 56) && Routes.Length > 0)
        {
            if (ExpeditionComplete) tick.Ui.SetText(Hint, CompleteHint);
            else tick.Session.LoadScene(Routes[Math.Clamp(Read("route"), 0, Routes.Length - 1)].Id);
        }
    }

    private void MoveCaptain(ScriptTick tick)
    {
        Vector2 direction = tick.Direction;
        if (direction != Vector2.Zero)
        {
            routeCount = 0;
            Vector2 step = direction * (WalkSpeed * tick.DeltaTime);
            Place(tick.Objects, ref captain, Slide(captain.Feet, step));
            return;
        }

        if (tick.Input.Pressed("ClickMove") && tick.Input.PointerInside &&
            tick.Cameras.TryGetView(Camera, out var view) &&
            tick.Objects.TileCellAt(Floor, view.ScreenToWorld(tick.Input.PointerPosition)) is { } cell &&
            tick.Objects.TryGetTile(Floor, cell, out var tile) && tile.MoveCost > 0 &&
            tick.Objects.TileCellCenter(Floor, cell) is { } target)
        {
            routeCount = area.FindPath(captain.Feet, target, path);
            waypoint = 1;
        }
        if (waypoint >= routeCount) return;
        Vector2 delta = path[waypoint] - captain.Feet;
        float length = delta.Length(), distance = WalkSpeed * tick.DeltaTime;
        if (length <= distance)
        {
            Place(tick.Objects, ref captain, path[waypoint++]);
            return;
        }
        Place(tick.Objects, ref captain, captain.Feet + delta * (distance / length));
    }

    private Vector2 Slide(Vector2 position, Vector2 delta)
    {
        if (area.IsOpen(position + delta)) return position + delta;
        var horizontal = new Vector2(position.X + delta.X, position.Y);
        if (area.IsOpen(horizontal)) return horizontal;
        var vertical = new Vector2(position.X, position.Y + delta.Y);
        return area.IsOpen(vertical) ? vertical : position;
    }

    private static void Place(IGameObjects objects, ref Actor actor, Vector2 feet)
    {
        if (feet == actor.Feet) return;
        actor.Feet = feet;
        objects.SetLocalTransform(actor.Id, new(feet - actor.Offset, 0, Vector2.One));
    }

    private void ActEnemies(ScriptTick tick)
    {
        for (int i = 0; i < foes.Length; i++)
        {
            ref var foe = ref foes[i];
            if (foe.Health <= 0) continue;
            if (foe.Cooldown > 0) foe.Cooldown--;
            Vector2 delta = captain.Feet - foe.Feet;
            float length = delta.Length();
            if (length < 18 && foe.Cooldown == 0)
            {
                health -= i == foes.Length - 1 && Zone == 3 ? 3 : 1;
                foe.Cooldown = 37;
                ShowHud(tick.Ui);
            }
            else if (length is >= 14 and < 108)
                Place(tick.Objects, ref foe, Slide(foe.Feet, delta * ((26 + Zone * 4) * tick.DeltaTime / length)));
        }
    }

    private void AutoAttack(ScriptTick tick, ref Actor ally, float range, int power, int interval)
    {
        if (ally.Cooldown > 0)
        {
            ally.Cooldown--;
            return;
        }
        int best = -1;
        float nearest = range * range;
        for (int i = 0; i < foes.Length; i++)
        {
            if (foes[i].Health <= 0) continue;
            float distance = Vector2.DistanceSquared(foes[i].Feet, ally.Feet);
            if (distance >= nearest) continue;
            nearest = distance;
            best = i;
        }
        if (best < 0) return;
        ally.Cooldown = interval;
        Hit(tick, ref foes[best], power);
    }

    private void Heal(ScriptTick tick, ref Actor healer)
    {
        if (healer.Cooldown > 0) healer.Cooldown--;
        else if (health < MaxHealth && Near(healer.Feet, captain.Feet, 55))
        {
            healer.Cooldown = 56;
            health++;
            ShowHud(tick.Ui);
        }
    }

    private void Hit(ScriptTick tick, ref Actor foe, int power)
    {
        foe.Health -= power;
        if (foe.Health > 0) return;
        tick.Objects.SetActive(foe.Id, false);
        defeated++;
        xp += 2;
        coins += 2;
        int nextLevel = 1 + xp / 9;
        if (nextLevel > level)
        {
            level = nextLevel;
            health = MaxHealth;
        }
        ShowHud(tick.Ui);
    }

    private bool HasThreat()
    {
        foreach (ref readonly var foe in foes.AsSpan())
            if (foe.Health > 0 && Near(foe.Feet, captain.Feet, 76)) return true;
        return false;
    }

    private static bool Near(Vector2 a, Vector2 b, float radius) => Vector2.DistanceSquared(a, b) <= radius * radius;

    private bool NearObject(IGameObjects objects, Guid id, float radius) =>
        objects.TryGetPosition(id, out var position) && Near(position + new Vector2(16, 17), captain.Feet, radius);

    private void ShowHud(IGameUi ui) => ui.SetText(Hud,
        $"HP {Math.Max(0, health)}/{MaxHealth}    LV {level}    XP {xp}    CROWNS {coins}    BAND {recruits + 1}    " +
        (Zone == 0 ? $"GEAR T{gear}" : $"DEFEATED {defeated}/{foes.Length}    PULSE {(skill > 0 ? "…" : "READY")}"));

    private void Save(SessionState session)
    {
        progress.Set("xp", xp);
        progress.Set("coins", coins);
        progress.Set("recruits", recruits);
        progress.Set("gear", gear);
        if (session.Storage.IsAvailable) session.Storage.SaveSlot(1, progress);
    }
}
