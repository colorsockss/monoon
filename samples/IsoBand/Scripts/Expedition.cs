using System;
using System.Drawing;
using System.Numerics;
using Engine.Core;

public sealed partial class Expedition : GameBehaviour
{
    public Guid Floor, Camera, EncounterFocus, Portal, Campfire, FieldRest;
    public Guid[] Companions = [], Enemies = [], PartyMotion = [], PartyBars = [], PartyLife = [],
        FoeMotion = [], FoeBars = [], FoeLife = [];
    public int[] EnemyGroups = [], EnemyKinds = [], RegionTotals = [];
    public SceneReference Next, Haven;
    public SceneReference[] Routes = [];
    public int Zone;
    public string ZoneName = "", ZoneSubtitle = "";

    private const float WalkSpeed = 68;
    private const int SaveVersion = 1;
    private const int PulseInterval = 320;
    private const int GuardianHealth = 156;
    private const string Started = "isoband.started";
    private const string ExpeditionReturned = "isoband.returned";
    private const string DefeatedFlag = "isoband.retreat";
    private static readonly string[] GearPrompts = ["Gear  1/3", "Gear  2/3", "Gear  3/3"];
    private static readonly Vector2[] Formation = [new(-26, 11), new(26, 11), new(-16, -16), new(16, -16)];

    private readonly Vector2[] path = new Vector2[64];
    private NavArea2D area = null!;
    private NavCrowd2D crowd = null!;
    private Actor[] party = [], foes = [];
    private GameData progress = null!;
    private int routeCount, waypoint, level, recruits, gear, xp, coins, defeated, skill, ticks, group;
    private int fieldTicks;
    private int transition, intro, chapterTicks, guardianClock, windup;
    private bool cleared, fighting, resting, nearShop;
    private Menu menu;
    private Vector2 guardianTarget;
    private Guid destination;
    private bool arrivedFromCrypt;

    public int Defeated => defeated;
    public int Encounter => group + 1;
    public bool IsCleared => cleared;

    private enum Role { Captain, Shield, Archer, Healer, Foe, Guardian }
    private enum Menu { None, Title, Help, Ending }
    private enum Sound { Strike, Hurt, Bow, Pulse, Crown, Clear, Heal, Windup, Step }

    private struct Actor
    {
        public Guid Root, Motion, Bar, Fill;
        public IGameAnimator Animation;
        public Vector2 Feet;
        public Role Role;
        public Vector2 Push;
        public int PushTicks;
        public int Health, Max, Cooldown, Hurt, Attack, Fall;
        public int Target, Strike;
        public bool Moving, FacingLeft, Mirrored, BarHidden;
    }

    public override void OnCreate(ScriptLife life)
    {
        var objects = life.Objects;
        progress = life.Session.Storage.IsAvailable ? life.Session.Storage.LoadSlot(1, SaveVersion) ?? new() : new();
        if (life.Session.Storage.IsAvailable && !life.Session.Has(Started))
        {
            if (life.Session.Storage.LoadSettings(1) is { } settings)
                life.Ui.Preferences.LoadFrom(settings);
        }
        sceneObjects = objects;
        StartEffects(objects);
        measureParticles = Environment.GetEnvironmentVariable("MONOON_ISOBAND_PARTICLE_STATS") == "1";
        fieldTicks = Math.Max(-1, Read("fieldTicks", Read("route") == 0 ? 0 : -1));
        xp = Math.Max(0, Read("xp"));
        coins = Math.Max(0, Read("coins", 6));
        level = 1 + xp / 12;
        recruits = Math.Clamp(Read("recruits", 2), 2, Companions.Length);
        gear = Math.Clamp(Read("gear", 1), 1, 3);
        area = BakeNavigation(objects, Floor);
        crowd = new NavCrowd2D(area, Companions.Length) { Speed = 100, MaxPlansPerStep = 4 };
        party = new Actor[Companions.Length + 1];
        party[0] = LoadActor(objects, life.ObjectId, PartyMotion[0], PartyBars[0], PartyLife[0], Role.Captain, MaxHealth(Role.Captain));
        for (int i = 0; i < Companions.Length; i++)
        {
            int slot = i + 1;
            Role role = i switch { 0 => Role.Shield, 2 => Role.Healer, _ => Role.Archer };
            party[slot] = LoadActor(objects, Companions[i], PartyMotion[slot], PartyBars[slot], PartyLife[slot], role, MaxHealth(role));
            objects.SetActive(Companions[i], i < recruits);
            if (i < recruits) crowd.Add(party[slot].Feet, party[slot].Feet);
        }
        foes = new Actor[Enemies.Length];
        for (int i = 0; i < foes.Length; i++)
        {
            bool guardian = Zone == 3 && i == foes.Length - 1;
            int hp = guardian ? GuardianHealth : 10 + Zone * 5 + EnemyGroups[i] * 3 + i % 3 * 2;
            foes[i] = LoadActor(objects, Enemies[i], FoeMotion[i], FoeBars[i], FoeLife[i], guardian ? Role.Guardian : Role.Foe, hp);
            if (!area.IsOpen(foes[i].Feet))
                throw new InvalidOperationException($"IsoBand foe {i + 1} in {ZoneName} starts outside walkable terrain.");
        }

        life.Ui.SetVisible(BossPanel, false);
        life.Ui.SetVisible(ChapterPanel, false);
        life.Ui.SetVisible(MenuPanel, false);
        life.Ui.SetVisible(PromptPanel, false);
        life.Ui.SetVisible(WavePanel, false);
        life.Ui.SetVisible(ShopPanel, false);
        RefreshHud(life.Objects, life.Ui);
        PulseHud(life.Ui, TweenClock.Ui);
        LayoutHud(life.Ui);
        life.Session.Screen.Fade(Color.FromArgb(255, 13, 19, 26), 0);
        life.Session.Screen.Fade(Color.Transparent, .34);
        if (Zone != 0)
        {
            intro = 54;
            Notice(life.Ui, ZoneName, ZoneSubtitle, scroll: true);
            return;
        }

        if (ExpeditionComplete && (!life.Session.Has(Started) || life.Session.Clear(ExpeditionReturned)))
        {
            life.Session.Set(Started);
            OpenMenu(life.Ui, Menu.Ending);
        }
        else if (!life.Session.Has(Started))
            OpenMenu(life.Ui, Menu.Title);
        else
        {
            bool lost = life.Session.Clear(DefeatedFlag);
            Notice(life.Ui, lost ? "Band retreated" : "Back in Haven",
                lost ? "Rearm and try again." : "Visit Ysra by the fire.");
        }
    }

    private int MaxHealth(Role role) => role switch
    {
        Role.Captain => 29 + level * 3,
        Role.Shield => 34 + level * 3 + gear * 2,
        _ => 21 + level * 3
    };
    private int Read(string key, int fallback = 0) => (int)progress.GetNumber(key, fallback);
    private bool ExpeditionComplete => Read("route") >= Routes.Length;
    private int PartyCount => recruits + 1;
    private bool GuardianActive => fighting && Zone == 3 && group == 2 && !cleared;

    private static Actor LoadActor(IGameObjects objects, Guid root, Guid motion, Guid bar, Guid fill, Role role, int hp)
    {
        if (!objects.TryGetPosition(root, out var feet) || objects.Animator(motion) is not { } animation)
            throw new InvalidOperationException($"IsoBand needs a motion animator for {role}.");
        return new() { Root = root, Motion = motion, Bar = bar, Fill = fill, Feet = feet, Role = role, Animation = animation,
            Health = hp, Max = hp, Target = -1 };
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        ticks++;
        if (transition > 0)
        {
            if (--transition == 0)
            {
                if (arrivedFromCrypt)
                    tick.Session.LoadScene(destination, static session => session.Log("IsoBand zone 4: Returned to Haven."));
                else tick.Session.LoadScene(destination);
            }
            return;
        }
        if (tick.Input.Pressed("Ledger") && menu is Menu.None or Menu.Help)
        {
            if (menu == Menu.Help) OpenMenu(tick.Ui, Menu.None);
            else OpenMenu(tick.Ui, Menu.Help);
            return;
        }
        if (menu != Menu.None)
        {
            UpdateMenu(tick);
            return;
        }
        if (Zone != 0 && !cleared && fieldTicks is >= 0 and < int.MaxValue) fieldTicks++;
        UpdateEffects(tick);
        UpdateNotice(tick);
        if (shineTicks > 0 && --shineTicks == 0) tick.Ui.SetVisible(PulseShine, false);
        if (intro > 0)
        {
            intro--;
            return;
        }

        MoveCaptain(tick);
        MoveBand(tick);
        if (Zone == 0)
        {
            VisitHaven(tick);
            UpdatePoses(tick.Objects);
            return;
        }

        if (party[0].Health <= 0)
        {
            tick.Session.Set(DefeatedFlag);
            SaveFieldTime(tick.Session);
            BeginTravel(tick, Haven.Id);
            return;
        }

        if (skill > 0)
        {
            skill--;
            if (skill % 12 == 0) PulseHud(tick.Ui);
        }
        if (!cleared)
        {
            FindEncounter(tick);
            if (tick.Input.Pressed("Skill") && skill == 0) CastPulse(tick);
            if (fighting)
            {
                ActEnemies(tick);
                for (int i = 0; i < PartyCount; i++) ActAlly(tick, i);
                CheckEncounter(tick);
            }
        }
        UpdatePoses(tick.Objects);
        FrameEncounter(tick.Objects);
        VisitField(tick);
    }

    private void VisitHaven(ScriptTick tick)
    {
        bool nearCamp = NearObject(tick.Objects, Campfire, 65);
        bool nearGate = NearObject(tick.Objects, Portal, 46);
        if (nearCamp != nearShop)
        {
            nearShop = nearCamp;
            tick.Ui.SetVisible(ShopPanel, nearShop);
            if (nearShop) ShowShop(tick.Ui);
        }
        if (nearCamp && tick.Input.Pressed("Hire")) Hire(tick);
        if (nearCamp && tick.Input.Pressed("Gear")) Upgrade(tick);
        if (tick.Input.Pressed("NewExpedition") && ExpeditionComplete)
        {
            RestartExpedition(tick);
            return;
        }
        if (nearGate && tick.Input.Pressed("Travel"))
        {
            if (ExpeditionComplete) OpenMenu(tick.Ui, Menu.Ending);
            else BeginTravel(tick, Routes[Math.Clamp(Read("route"), 0, Routes.Length - 1)].Id);
        }
        SetPrompt(tick.Ui, nearShop ? null : nearGate ? "Open gate"
            : ticks < 600 ? "Find Ysra" : GearPrompts[gear - 1], nearGate && !nearShop ? PromptKey.Gate : PromptKey.None);
    }

    private void ShowShop(IGameUi ui)
    {
        bool full = recruits >= Math.Min(Companions.Length, level >= 3 ? 4 : 3);
        ui.SetText(HireLine, full ? "Full" : recruits == 2 ? "Healer" : "Archer");
        ui.SetText(GearLine, gear >= 3 ? "Max tier" : "Steel");
        ui.SetVisible(ShopPrices[0], !full);
        ui.SetVisible(ShopPrices[1], gear < 3);
    }

    private void Hire(ScriptTick tick)
    {
        int cap = level >= 3 ? 4 : 3;
        if (coins < 6 || recruits >= Math.Min(Companions.Length, cap))
        {
            Notice(tick.Ui, "Hiring ledger", coins < 6 ? "A hire costs 6 crowns." : "No place at this rank.");
            return;
        }
        coins -= 6;
        int slot = ++recruits;
        ref var member = ref party[slot];
        Vector2 target = CaptainFeet + Formation[slot - 1];
        Place(tick.Objects, ref member, area.IsOpen(target) ? target : CaptainFeet);
        crowd.Add(member.Feet, member.Feet);
        tick.Objects.SetActive(member.Root, true);
        Save(tick.Session);
        PlaySound(tick, Sound.Crown);
        RefreshHud(tick.Objects, tick.Ui);
        ShowShop(tick.Ui);
        Notice(tick.Ui, member.Role == Role.Healer ? "Healer enlisted" : "Archer enlisted",
            member.Role == Role.Healer ? "Heals wounded allies." : "Covers the back line.");
    }

    private void Upgrade(ScriptTick tick)
    {
        if (gear >= 3 || coins < 8)
        {
            Notice(tick.Ui, "Armory ledger", gear >= 3 ? "Masterwork steel." : "Needs 8 crowns.");
            return;
        }
        coins -= 8;
        gear++;
        for (int i = 0; i < PartyCount; i++) party[i].Health = party[i].Max = MaxHealth(party[i].Role);
        Save(tick.Session);
        PlaySound(tick, Sound.Crown);
        RefreshHud(tick.Objects, tick.Ui);
        ShowShop(tick.Ui);
        Notice(tick.Ui, $"Steel tier {gear}", "Damage and guard up.");
    }

    private void VisitField(ScriptTick tick)
    {
        bool nearCamp = NearObject(tick.Objects, Campfire, 35) || NearObject(tick.Objects, FieldRest, 30);
        bool nearGate = NearObject(tick.Objects, Portal, 47);
        if (!fighting && nearCamp && tick.Input.Pressed("Rest") && PartyNeedsRest())
        {
            for (int i = 0; i < PartyCount; i++)
            {
                ref var member = ref party[i];
                if (member.Health == 0)
                {
                    Place(tick.Objects, ref member, area.IsOpen(CaptainFeet + Formation[i - 1]) ? CaptainFeet + Formation[i - 1] : CaptainFeet);
                    member.Fall = 0;
                    member.Attack = 0;
                    member.Hurt = 0;
                    tick.Objects.SetActive(member.Root, true);
                }
                member.Health = member.Max;
                WorldBar(tick.Objects, ref member);
            }
            crowd.Clear();
            for (int i = 1; i < PartyCount; i++) crowd.Add(party[i].Feet, party[i].Feet);
            ShowHeal(tick, CaptainFeet, 0);
            PlaySound(tick, Sound.Heal);
            resting = true;
            RefreshHud(tick.Objects, tick.Ui);
        }
        if (!nearCamp) resting = false;
        if (cleared)
            SetPrompt(tick.Ui, nearGate ? "Cross gate" : "Follow the light", nearGate ? PromptKey.Gate : PromptKey.None);
        else if (nearCamp && !fighting && (PartyNeedsRest() || resting))
            SetPrompt(tick.Ui, resting ? "Band restored" : "Rest", resting ? PromptKey.None : PromptKey.Rest);
        else
            SetPrompt(tick.Ui, null, wave: true);

        if (!cleared || !nearGate) return;
        if (Next.Id != Guid.Empty && tick.Input.Pressed("ReturnToHaven")) BeginTravel(tick, Haven.Id);
        else if (tick.Input.Pressed("Travel"))
        {
            if (Next.Id != Guid.Empty) BeginTravel(tick, Next.Id);
            else
            {
                tick.Session.Set(ExpeditionReturned);
                BeginTravel(tick, Haven.Id, true);
            }
        }
    }

    private bool PartyNeedsRest()
    {
        for (int i = 0; i < PartyCount; i++) if (party[i].Health < party[i].Max) return true;
        return false;
    }

    private void BeginTravel(ScriptTick tick, Guid next, bool finale = false)
    {
        transition = 22;
        destination = next;
        arrivedFromCrypt = finale;
        tick.Session.Screen.Fade(Color.FromArgb(255, 13, 19, 26), .32);
    }

    private Vector2 CaptainFeet => party[0].Feet;

    internal static NavArea2D BakeNavigation(IGameObjects objects, Guid floor)
    {
        const int cells = 13;
        // The four maps share this 64×32 grid and the same navigation margin.
        var firstTile = objects.TileCellCenter(floor, Point.Empty)!.Value;
        var area = NavArea2D.Bake(new(firstTile.X - 391, firstTile.Y + 23, 780, 358), 5, 3.5f, []);
        for (int y = 0; y < cells; y++)
            for (int x = 0; x < cells; x++)
            {
                Point cell = new(x, y);
                if (objects.TryGetTile(floor, cell, out var tile) && tile.MoveCost == 0)
                    CloseWater(area, objects.TileCellCenter(floor, cell)!.Value);
            }
        return area;
    }

    private static void CloseWater(NavArea2D area, Vector2 water)
    {
        var first = area.Center(0, 0);
        float step = area.CellSize, r = area.AgentRadius;
        int left = Math.Max(0, (int)MathF.Ceiling((water.X - 32 - r - first.X) / step));
        int right = Math.Min(area.Columns - 1, (int)MathF.Floor((water.X + 32 + r - first.X) / step));
        int top = Math.Max(0, (int)MathF.Ceiling((water.Y - 16 - r - first.Y) / step));
        int bottom = Math.Min(area.Rows - 1, (int)MathF.Floor((water.Y + 16 + r - first.Y) / step));
        for (int y = top; y <= bottom; y++)
        {
            int from = -1, to = -1;
            for (int x = left; x <= right; x++)
            {
                if (!TouchesWater(area.Center(x, y) - water, r)) continue;
                if (from < 0) from = x;
                to = x;
            }
            if (from < 0) continue;
            // The diamond is convex. A narrow strip over its blocked centers closes
            // this run; padding plus the agent radius stays short of the next row.
            var at = area.Center(from, y);
            area.AddObstacle(new(at.X - .25f, at.Y - .25f, (to - from) * step + .5f, .5f));
        }
    }

    private static bool TouchesWater(Vector2 delta, float radius)
    {
        float x = MathF.Abs(delta.X), y = MathF.Abs(delta.Y);
        if (x + 2*y <= 32) return true;
        // Distance to the sloping edge (32, 0) → (0, 16) in the first quadrant.
        var edge = new Vector2(-32, 16);
        var offset = new Vector2(x - 32, y);
        float along = Math.Clamp(Vector2.Dot(offset, edge) / 1280, 0, 1);
        return (offset - edge * along).LengthSquared() < radius * radius;
    }

    private void FrameEncounter(IGameObjects objects)
    {
        if (!fighting)
        {
            if (objects.IsActive(EncounterFocus)) objects.SetActive(EncounterFocus, false);
            return;
        }

        Vector2 sum = Vector2.Zero;
        int count = 0;
        for (int i = 0; i < foes.Length; i++)
            if (EnemyGroups[i] == group && foes[i].Health > 0)
            {
                sum += foes[i].Feet - new Vector2(0, foes[i].Role == Role.Guardian ? 28 : 14);
                count++;
            }
        if (count == 0) return;
        Vector2 delta = sum / count - CaptainFeet;
        float distance = delta.Length();
        if (distance > 102) delta *= 102 / distance;
        objects.SetLocalTransform(EncounterFocus, new(CaptainFeet + delta, 0, Vector2.One));
        if (!objects.IsActive(EncounterFocus)) objects.SetActive(EncounterFocus, true);
    }

    private void MoveCaptain(ScriptTick tick)
    {
        ref var captain = ref party[0];
        captain.Moving = false;
        if (captain.Health <= 0) return;
        Vector2 direction = tick.Direction;
        float step = WalkSpeed * tick.DeltaTime;
        if (direction != Vector2.Zero)
        {
            routeCount = 0;
            Place(tick.Objects, ref captain, Slide(captain.Feet, direction * step));
        }
        else
        {
            if (tick.Input.Pressed("ClickMove") && tick.Input.PointerInside &&
                tick.Cameras.TryGetView(Camera, out var view) &&
                tick.Objects.TileCellAt(Floor, view.ScreenToWorld(tick.Input.PointerPosition)) is { } cell &&
                tick.Objects.TryGetTile(Floor, cell, out var tile) && tile.MoveCost > 0 &&
                tick.Objects.TileCellCenter(Floor, cell) is { } target)
            {
                routeCount = area.FindPath(captain.Feet, target, path);
                waypoint = 1;
                if (routeCount > 1)
                {
                    tick.Objects.SetActive(Waypoint, true);
                    ShowWaypoint(tick.Objects, target);
                }
            }
            if (waypoint < routeCount)
            {
                Vector2 delta = path[waypoint] - captain.Feet;
                float distance = delta.Length();
                Place(tick.Objects, ref captain, distance <= step ? path[waypoint++] : captain.Feet + delta * (step / distance));
            }
        }
        if (waypoint >= routeCount && tick.Objects.IsActive(Waypoint)) tick.Objects.SetActive(Waypoint, false);
        if (captain.Moving && ticks % 22 == 0) PlaySound(tick, Sound.Step);
    }

    private void MoveBand(ScriptTick tick)
    {
        for (int i = 1; i < PartyCount; i++)
        {
            ref var member = ref party[i];
            member.Moving = false;
            if (member.Health <= 0) continue;
            if (ticks % 7 != 1) continue;
            Vector2 goal = CaptainFeet + Formation[i - 1];
            if (fighting && member.Role == Role.Shield)
            {
                int target = TargetFoe(member.Feet, 76);
                if (target >= 0) goal = foes[target].Feet + new Vector2(-13, 8);
            }
            crowd.SetGoal(i - 1, goal);
        }
        crowd.Step(tick.DeltaTime);
        for (int i = 1; i < PartyCount; i++)
        {
            ref var member = ref party[i];
            if (member.Health > 0) Place(tick.Objects, ref member, crowd.Positions[i - 1]);
        }
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
        Vector2 delta = feet - actor.Feet;
        if (delta.LengthSquared() < .0025f) return;
        actor.Moving = true;
        if (actor.Attack <= 0 && MathF.Abs(delta.X) > .035f)
            actor.FacingLeft = delta.X < 0;
        actor.Feet = feet;
        objects.SetLocalTransform(actor.Root, new(feet, 0, Vector2.One));
    }

    private static bool Near(Vector2 a, Vector2 b, float range) => Vector2.DistanceSquared(a, b) <= range * range;
    private bool NearObject(IGameObjects objects, Guid id, float range) =>
        objects.TryGetPosition(id, out var position) && Near(position, CaptainFeet, range);

    private void RestartExpedition(ScriptTick tick)
    {
        progress.Set("route", 0);
        fieldTicks = 0;
        Save(tick.Session);
        BeginTravel(tick, Routes[0].Id);
    }

    private void SaveFieldTime(SessionState session)
    {
        progress.Set("fieldTicks", fieldTicks);
        if (session.Storage.IsAvailable) session.Storage.SaveSlot(1, progress);
    }

    private void Save(SessionState session)
    {
        progress.Set("xp", xp);
        progress.Set("coins", coins);
        progress.Set("recruits", recruits);
        progress.Set("gear", gear);
        SaveFieldTime(session);
    }
}
