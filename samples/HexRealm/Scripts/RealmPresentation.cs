using System;
using System.Drawing;
using System.Numerics;
using Engine.Core;

public sealed partial class RealmBoard
{
    private const int TrailCells = 8;
    private const float StepSeconds = .16f;
    private const float FogSeconds = .30f;
    private const int FogSteps = 9, FirstFadedMist = 203;
    private float hitSoundCooldown;
    private long lastHitCue = -1;
    private static readonly int[] TerrainBase = [0, 1, 4, 7, 0, 10, 13];
    private readonly Vector2[] trails = new Vector2[RealmRules.UnitLimit * TrailCells];
    private readonly PieceView[] pieces = new PieceView[RealmRules.UnitLimit];
    private readonly TownView[] towns = new TownView[RealmRules.CityLimit];
    private readonly Impact[] impacts = new Impact[RealmRules.UnitLimit * 2];
    private readonly Effect[] effects = new Effect[RealmRules.UnitLimit * 3];
    private readonly Parcel[] claims = new Parcel[Count];
    private readonly ClaimReport[] claimReports = new ClaimReport[RealmRules.CityLimit + 1];
    private int claimCount, reportCount;
    private readonly bool[] priorExplored = new bool[Count];
    private readonly float[] fogTime = new float[Count];
    private readonly int[] fogOverlay = new int[Count];
    private int revealing;

    private struct PieceView
    {
        internal RealmRules.Unit? Unit;
        internal Guid Actor, Bar, Fill;
        internal Vector2 Position;
        internal int Steps, HeldDamage;
        internal float Age, Flash;
        internal bool Moving, Strike, Routed, FacesLeft, ArrowFlinch, BowLit;
        internal string? Tint;
    }

    private struct TownView
    {
        internal Guid House, Ribbon, Label, Timing, Bar, Fill;
        internal int Side, Art, Population, Eta;
    }

    private readonly record struct Impact(float Wait, Vector2 At, int Piece, int Damage, bool Routed,
        RealmRules.Unit? Subject = null, bool Arrow = false);
    private readonly record struct Effect(Guid Id, Vector2 At, float Life, float Total, bool Rising);
    private readonly record struct Parcel(int Index, bool WasVisible);
    private readonly record struct ClaimReport(RealmRules.City City, ClaimCause Cause, int Side, int Start, int Count);

    private void GroundTiles()
    {
        for (int i = 0; i < Count; i++)
        {
            int terrain = rules.Terrain[i];
            tiles[i] = terrain == 4 ? 0 : TerrainBase[terrain] + (int)(RealmRules.Noise(rules.Seed, RealmRules.PointAt(i)) % 3);
        }
        Replace(Ground);
        for (int i = 0; i < Count; i++)
        {
            var cell = RealmRules.PointAt(i);
            int mask = 0;
            if (rules.Terrain[i] != 4)
                for (int edge = 0; edge < 6; edge++)
                {
                    var next = RealmRules.Add(cell, RealmRules.Neighbors[edge]);
                    if (!RealmRules.Contains(next) || rules.Terrain[RealmRules.Index(next)] == 4) mask |= 1 << edge;
                }
            tiles[i] = mask == 0 ? 0 : 8 + mask;
        }
        Replace(Coast);
    }

    private static void Visibility(RealmRules game, bool[] sight, bool[] history)
    {
        Array.Clear(sight);
        foreach (var actor in game.Units)
            if (actor.Alive && actor.Side == 1) Reveal(actor.Cell, actor.Kind == UnitKind.Scout ? 3 : 2, sight, history);
        foreach (var town in game.Cities)
            if (town.Side == 1) Reveal(town.Cell, 2 + town.Population / 3, sight, history);
        for (int i = 0; i < Count; i++)
            if (game.Owner[i] == 1) sight[i] = history[i] = true;
    }

    private static void Reveal(Point origin, int radius, bool[] sight, bool[] history)
    {
        for (int r = Math.Max(0, origin.Y - radius); r < Math.Min(RealmRules.Height, origin.Y + radius + 1); r++)
        for (int q = Math.Max(0, origin.X - radius); q < Math.Min(RealmRules.Width, origin.X + radius + 1); q++)
        {
            var cell = new Point(q, r);
            if (RealmRules.Distance(origin, cell) > radius) continue;
            int i = RealmRules.Index(cell);
            sight[i] = history[i] = true;
        }
    }

    private void Replace(Guid layer) => objects.ReplaceTiles(layer, RealmRules.Width, RealmRules.Height, tiles);

    // Include the chart's shallow waters. The mist bank is drawn from coordinates,
    // so it cannot trace an undiscovered shoreline.
    private static bool InMistBank(Point cell) =>
        cell.X is > 0 and < RealmRules.Width - 1 && cell.Y is > 0 and < RealmRules.Height - 1 &&
        cell.X + cell.Y is > 2 and < RealmRules.Width + RealmRules.Height - 4;

    private bool IsCharted(int index) => explored[index] || !InMistBank(RealmRules.PointAt(index));

    private int Mist(int index) => 1 + (int)(RealmRules.Noise(rules.Seed, RealmRules.PointAt(index)) % 3);

    private void RefreshMap(bool animateFog = false)
    {
        bool title = !playing;
        if (animateFog) explored.CopyTo(priorExplored, 0);
        if (!title) Visibility(rules, visible, explored);
        for (int i = 0; i < Count; i++)
        {
            if (animateFog && !priorExplored[i] && explored[i] && InMistBank(RealmRules.PointAt(i)))
            {
                // Let the cover on the old chart edge clear ahead of the outer tiles.
                var cell = RealmRules.PointAt(i);
                bool touchesKnown = false;
                foreach (var edge in RealmRules.Neighbors)
                {
                    var adjacent = RealmRules.Add(cell, edge);
                    if (!RealmRules.Contains(adjacent) || !priorExplored[RealmRules.Index(adjacent)]) continue;
                    touchesKnown = true;
                    break;
                }
                fogTime[i] = FogSeconds + (touchesKnown ? 0 : .05f);
                fogOverlay[i] = Mist(i);
                revealing++;
            }
            if (!title && !IsCharted(i))
                tiles[i] = Mist(i);
            else if (rules.Owner[i] == 2) tiles[i] = 202;
            else tiles[i] = !title && !visible[i] && rules.Terrain[i] != 4 ? 4 : 0;
        }
        Replace(Fog);
        objects.ReplaceTiles(RevealFog, RealmRules.Width, RealmRules.Height, fogOverlay);
        for (int i = 0; i < Count; i++)
        {
            int side = rules.Owner[i];
            var cell = RealmRules.PointAt(i);
            int mask = 0;
            if (side != 0 && (title || explored[i]))
                for (int edge = 0; edge < 6; edge++)
                {
                    var next = RealmRules.Add(cell, RealmRules.Neighbors[edge]);
                    if (!RealmRules.Contains(next) || rules.Owner[RealmRules.Index(next)] != side) mask |= 1 << edge;
                }
            tiles[i] = mask == 0 ? 0 : 71 + (side - 1) * 63 + mask;
        }
        Replace(Borders);
        for (int i = 0; i < Count; i++)
            tiles[i] = (!title && !explored[i]) || rules.Improvements[i] == 0 ? 0 : 197 + rules.Improvements[i];
        Replace(Districts);
        UpdateTowns(title);
        UpdatePieces(title);
    }

    private void StepFog(float seconds)
    {
        if (revealing == 0) return;
        bool coverChanged = false, showLandmarks = false;
        for (int i = 0; i < Count; i++)
        {
            float old = fogTime[i];
            if (old <= 0) continue;
            float left = fogTime[i] = Math.Max(0, old - seconds);
            if (left == 0) revealing--;
            if (old > FogSeconds / 2 && left <= FogSeconds / 2) showLandmarks = true;
            int level = (int)MathF.Ceiling(left * (FogSteps / FogSeconds));
            int tile = level switch
            {
                0 => 0,
                >= FogSteps => Mist(i),
                _ => FirstFadedMist + (level - 1) * 3 + Mist(i) - 1
            };
            if (tile == fogOverlay[i]) continue;
            fogOverlay[i] = tile;
            coverChanged = true;
        }
        if (coverChanged) objects.ReplaceTiles(RevealFog, RealmRules.Width, RealmRules.Height, fogOverlay);
        if (showLandmarks && turnPhase is TurnPhase.None or TurnPhase.Reports)
        {
            UpdateTowns(false);
            UpdatePieces(false);
        }
    }

    private void UpdateTowns(bool title)
    {
        for (int i = 0; i < rules.Cities.Count; i++)
        {
            var city = rules.Cities[i];
            ref var view = ref towns[i];
            int cell = RealmRules.Index(city.Cell);
            if (!title && (!explored[cell] || city.Side == 2 && fogTime[cell] > FogSeconds / 2)) continue;
            Vector2 at = centers[cell];
            if (view.House == Guid.Empty)
            {
                view.House = objects.Rent(TownArt, at - new Vector2(28, 33));
                view.Label = objects.Rent(TownLine, at - new Vector2(66, 71));
                view.Timing = objects.Rent(TownLine, at - new Vector2(66, 54));
                view.Bar = objects.Rent(CityBar, at - new Vector2(39, 39));
            }
            int art = (city.Side - 1) * 3 + Math.Min(2, (city.Population - 1) / 2);
            if (view.Side == 0 || view.Art != art)
            {
                view.Art = art;
                objects.SetSprite(view.House, TownSprites[art]);
            }
            bool grew = view.Population > 0 && view.Population < city.Population;
            if (view.Side != city.Side || view.Population != city.Population)
            {
                string ink = city.Side == 1 ? "#245d50" : "#803f2d";
                objects.SetWorldText(view.Label, $"<color={ink}><b>{TownName(city)} / {city.Population}</b></color>");
                view.Population = city.Population;
            }
            if (view.Side != city.Side)
            {
                Return(ref view.Ribbon);
                Return(ref view.Fill);
                view.Ribbon = objects.Rent(Signs[city.Side - 1], at - new Vector2(66, 75));
                view.Fill = objects.Rent(Fill[city.Side - 1], at - new Vector2(39, 38));
                view.Eta = int.MinValue;
                view.Side = city.Side;
            }
            var growth = rules.Forecast(city);
            int eta = city.CanGrow ? growth.Turns : -1;
            if (view.Eta != eta)
            {
                view.Eta = eta;
                objects.SetWorldText(view.Timing, eta switch
                {
                    -1 => "Fully grown",
                    0 => "No harvest",
                    1 => "Growth in 1 turn",
                    _ => $"Growth in {eta} turns"
                });
            }
            float progress = city.CanGrow ? Math.Clamp(growth.Food / (float)growth.Goal, 0, 1) : 1;
            objects.SetActive(view.Fill, grew || progress > 0);
            if (grew)
                objects.Tween(view.Fill).Scale(new(78, 1), .16f).Delay(.12f).Scale(new(78 * progress, 1), .21f);
            else
                objects.SetLocalTransform(view.Fill, new(at - new Vector2(39, 38), 0, new(78 * progress, 1)));
        }
    }

    private void RecordClaim(RealmRules.City town, ClaimCause cause, ReadOnlySpan<Point> cells)
    {
        claimReports[reportCount++] = new(town, cause, town.Side, claimCount, cells.Length);
        foreach (Point cell in cells)
        {
            int index = RealmRules.Index(cell);
            claims[claimCount++] = new(index, visible[index]);
        }
    }

    private bool PresentClaims()
    {
        int featured = -1;
        bool displayed = false;
        for (int i = 0; i < reportCount; i++)
        {
            var report = claimReports[i];
            // The rival can conquer a city immediately after that city's harvest.
            if (report.City.Side != report.Side) continue;
            for (int j = report.Start; j < report.Start + report.Count; j++)
            {
                var claim = claims[j];
                if (!visible[claim.Index] && !claim.WasVisible) continue;
                displayed = true;
                Vector2 at = centers[claim.Index];
                var id = objects.Rent(ClaimInk[report.City.Side - 1], at - new Vector2(24, 28));
                objects.Animator(id)?.Play();
                TrackEffect(new(id, at, .80f, .80f, false));
            }
            int index = report.Cause == ClaimCause.Growth ? claims[report.Start].Index : RealmRules.Index(report.City.Cell);
            if (!visible[index] && !(report.Cause == ClaimCause.Capture && explored[index])) continue;
            if (featured < 0 || report.City.Side == 1 &&
                (claimReports[featured].City.Side != 1 || inspected == report.City.Cell)) featured = i;
            Vector2 pos = centers[index] - new Vector2(43, report.Cause == ClaimCause.Growth ? 39 : 18);
            var pop = objects.Rent(LandGain, pos);
            objects.SetWorldText(pop, $"<b>+{report.Count} Land</b>");
            TrackEffect(new(pop, pos, 1.03f, 1.03f, true));
        }
        if (featured >= 0)
        {
            var report = claimReports[featured];
            string name = TownName(report.City);
            string terrain = TerrainNames[rules.Terrain[claims[report.Start].Index]];
            bool large = ui.Preferences.TextScale > 1.5f;
            string notice = report.Cause switch
            {
                ClaimCause.Growth when large => $"{name} grew to {report.City.Population}",
                ClaimCause.Growth => $"{name} grew to {report.City.Population}: {terrain} claimed",
                ClaimCause.Founding when large => $"{name}: +{report.Count} Land",
                ClaimCause.Founding => $"{name} founded: +{report.Count} Land",
                ClaimCause.Capture when large => $"{name} captured",
                _ => $"{name} captured: +{report.Count} Land"
            };
            if (report.Cause == ClaimCause.Growth && report.City.Side == 1) objects.Audio.Play(BuildSound);
            ShowNotice($"<b>{notice}</b>", 2.6f, priority: true);
        }
        claimCount = reportCount = 0;
        return displayed;
    }

    private void UpdatePieces(bool title)
    {
        for (int i = 0; i < rules.Units.Count; i++)
            RefreshPiece(i, title);
    }

    private void RefreshPiece(int i, bool title)
    {
        var actor = rules.Units[i];
        ref var view = ref pieces[i];
        if (view.Unit != actor)
        {
            ReturnPiece(ref view);
            view.Unit = actor;
        }
        bool display = (actor.Alive || view.Routed) && !TrainingPending(actor);
        if (display && !title && actor.Side == 2)
        {
            var cell = view.Steps == 0 ? actor.Cell : objects.TileCellAt(Ground, DrawnAt(i));
            display = cell is { } shown && RealmRules.Contains(shown) && visible[RealmRules.Index(shown)] &&
                fogTime[RealmRules.Index(shown)] <= FogSeconds / 2;
        }
        if (!display)
        {
            ReturnPieceArt(ref view);
            return;
        }
        if (view.Actor == Guid.Empty)
        {
            int art = (actor.Side - 1) * 5 + (int)actor.Kind;
            Vector2 at = DrawnAt(i);
            view.Actor = objects.Rent(UnitArt[art], at - new Vector2(19, 29));
            if (actor.Kind == UnitKind.Archer && objects.GetComponent<RealmBowRelease>(view.Actor) is { } bow)
                bow.Board = this;
            view.Bar = objects.Rent(HealthBar, at + new Vector2(-14, 12));
            view.Fill = objects.Rent(Fill[actor.Side - 1], at + new Vector2(-13, 13));
            view.Position = at;
        }
        DrawPiece(i, true);
    }

    private void PlayRecruitEntrance(int index)
    {
        ref var view = ref pieces[index];
        if (view.Actor == Guid.Empty) return;
        var at = view.Position;
        Emit(Smoke, at, .72f);
        var ink = objects.Rent(ClaimInk[view.Unit!.Side - 1], at - new Vector2(24, 28));
        objects.Animator(ink)?.Play();
        TrackEffect(new(ink, at, .80f, .80f, false));
        objects.Tween(view.Actor).Alpha(0, 0).Alpha(1, .24f);
        objects.Tween(view.Bar).Alpha(0, 0).Alpha(1, .24f);
        objects.Tween(view.Fill).Alpha(0, 0).Alpha(1, .24f);
    }

    private void DrawPiece(int index, bool force = false)
    {
        ref var view = ref pieces[index];
        var unit = view.Unit!;
        Vector2 at = DrawnAt(index);
        if (at != view.Position || force || view.ArrowFlinch)
        {
            view.Position = at;
            const float scale = 1.12f;
            float offset = view.FacesLeft ? 38 * scale : 0;
            if (view.ArrowFlinch)
            {
                offset += ui.Preferences.ShakeScale * 14 * view.Flash * MathF.Cos((.15f - view.Flash) * 46);
                if (view.Flash <= 0) view.ArrowFlinch = false;
            }
            objects.SetLocalTransform(view.Actor, new(at + new Vector2(offset - 21, -32),
                0, new(view.FacesLeft ? -scale : scale, scale)));
            objects.SetLocalTransform(view.Bar, new(at + new Vector2(-14,12), 0, Vector2.One));
            int maxHp = unit.Kind is UnitKind.Settler or UnitKind.Worker ? 4 : 7;
            int health = Math.Clamp(unit.Health + view.HeldDamage, 1, maxHp);
            objects.SetLocalTransform(view.Fill, new(at + new Vector2(-13,13), 0, new(26 * health / (float)maxHp, 1)));
        }
        bool moving = view.Steps > 0 && !view.Strike;
        if (view.Moving != moving)
        {
            view.Moving = moving;
            objects.Animator(view.Actor)?.SetBool("Moving", moving);
        }
        string? tint = view.Flash > 0 && ui.Preferences.FlashScale > 0 ? "#ffc5a0" :
            unit.Moves == 0 && view.Steps == 0 && !view.BowLit ? "#9faeab" : null;
        if (tint != view.Tint)
        {
            view.Tint = tint;
            objects.SetSpriteTint(view.Actor, tint);
        }
    }

    private void RefreshUnitHealth(int index)
    {
        var actor = rules.Units[index];
        int max = actor.Kind is UnitKind.Settler or UnitKind.Worker ? 4 : 7;
        ui.SetText(Stats, $"{Math.Clamp(actor.Health + pieces[index].HeldDamage, 0, max)}/{max}");
    }

    private void Animate(float seconds)
    {
        if (researchGlintTime > 0 && (researchGlintTime -= seconds) <= 0) ui.SetVisible(ResearchGlint, false);
        if (hitSoundCooldown > 0) hitSoundCooldown = Math.Max(0, hitSoundCooldown - seconds);
        for (int i = 0; i < rules.Units.Count; i++)
        {
            ref var view = ref pieces[i];
            bool redraw = view.Steps > 0 || view.Flash > 0;
            view.Flash = Math.Max(0, view.Flash - seconds);
            if (view.Steps > 0 && (view.Age += seconds) >= (view.Steps - 1) * StepSeconds)
            {
                view.Steps = 0;
                view.Strike = false;
            }
            if (!redraw) continue;
            if (rules.Units[i].Side == 2) RefreshPiece(i, false);
            else if (view.Actor != Guid.Empty) DrawPiece(i);
        }
        AdvanceArrows(seconds);
        for (int i = 0; i < impacts.Length; i++)
        {
            var impact = impacts[i];
            if (impact.Wait == 0) continue;
            float left = impact.Wait - seconds;
            impacts[i] = left > 0 ? impact with { Wait = left } : default;
            if (left <= 0) ShowImpact(impact);
        }
        for (int i = 0; i < effects.Length; i++)
        {
            var fx = effects[i];
            if (fx.Id == Guid.Empty) continue;
            float left = fx.Life - seconds;
            if (left <= 0)
            {
                objects.Return(fx.Id);
                effects[i] = default;
                continue;
            }
            effects[i] = fx with { Life = left };
            if (fx.Rising) objects.SetLocalTransform(fx.Id, new(fx.At + new Vector2(0, -27 * (1 - left / fx.Total)), 0, Vector2.One));
        }
    }

    public Vector2 DrawnAt(int i)
    {
        ref var view = ref pieces[i];
        int length = view.Steps;
        if (length == 0) return centers[RealmRules.Index(rules.Units[i].Cell)];
        float travel = Math.Clamp(view.Age / StepSeconds, 0, length - 1);
        int step = Math.Min((int)travel, length - 2), at = i * TrailCells + step;
        float t = travel - step;
        t = t * t * (3 - 2 * t);
        return trails[at] + (trails[at + 1] - trails[at]) * t;
    }

    private int StartTrail(RealmRules.Unit actor, int cells, bool strike)
    {
        int i = rules.Units.IndexOf(actor);
        ref var view = ref pieces[i];
        if (view.Unit != actor)
        {
            ReturnPiece(ref view);
            view.Unit = actor;
        }
        int at = i * TrailCells;
        trails[at] = DrawnAt(i);
        view.Steps = cells;
        view.Age = 0;
        view.Strike = strike;
        return at;
    }

    private void TrailMove(RealmRules.Unit actor, ReadOnlySpan<Point> cells)
    {
        if (actor.Side == 2)
        {
            bool seen = false;
            foreach (var cell in cells)
                if (visible[RealmRules.Index(cell)]) { seen = true; break; }
            if (!seen) return;
        }
        int count = Math.Min(cells.Length, TrailCells);
        int start = StartTrail(actor, count, false);
        for (int k = 1; k < count; k++) trails[start + k] = centers[RealmRules.Index(cells[cells.Length - count + k])];
        if (actor.Side == 1) objects.Audio.Play(MoveSound);
        else if (turnPhase == TurnPhase.RivalDelay) rivalMotion = Math.Max(rivalMotion, (count - 1) * StepSeconds);
    }

    private void TrailStrike(RealmRules.Unit attacker, Point target, RealmRules.Unit? defender, RealmRules.Battle hit)
    {
        int fromCell = RealmRules.Index(attacker.Cell), targetCell = RealmRules.Index(target);
        if (!visible[fromCell] && !visible[targetCell]) return;
        Vector2 from = centers[fromCell], to = centers[targetCell];
        int attackerIndex = rules.Units.IndexOf(attacker);
        int targetIndex = defender == null ? -1 : rules.Units.IndexOf(defender);
        if (attacker.Kind == UnitKind.Archer)
        {
            AimArrow(attackerIndex, target, from, to, hit,
                new(0, to, targetIndex, Math.Min(hit.Damage, hit.Health), defender is { Alive: false }, defender, true),
                new(.12f, from, attackerIndex, hit.Retaliation, !attacker.Alive, attacker));
            return;
        }
        int start = StartTrail(attacker, 3, true);
        if (turnPhase == TurnPhase.RivalDelay) rivalMotion = Math.Max(rivalMotion, 2 * StepSeconds);
        trails[start + 1] = from + (to - from) * .46f;
        trails[start + 2] = from;
        if (pieces[attackerIndex].Actor != Guid.Empty)
            objects.Animator(pieces[attackerIndex].Actor)?.SetTrigger("Strike");
        QueueImpact(new(.13f, to, targetIndex, Math.Min(hit.Damage,hit.Health), !hit.City && hit.Damage >= hit.Health));
        if (hit.Retaliation > 0) QueueImpact(new(.19f, from, attackerIndex, hit.Retaliation, !attacker.Alive));
    }

    private void QueueImpact(Impact impact)
    {
        for (int i = 0; i < impacts.Length; i++)
        {
            if (impacts[i].Wait != 0) continue;
            impacts[i] = impact;
            if (impact.Routed && impact.Piece >= 0 && impact.Subject == null)
                pieces[impact.Piece].Routed = true;
            return;
        }
    }

    private void ShowImpact(Impact item)
    {
        // A simultaneous volley only needs one copy of the same impact sound.
        long tick = session.Time.TickCount;
        if ((item.Arrow || hitSoundCooldown == 0) && tick != lastHitCue)
        {
            objects.Audio.Play(HitSound);
            hitSoundCooldown = .085f;
            lastHitCue = tick;
        }
        if (item.Piece >= 0 && (item.Subject == null || pieces[item.Piece].Unit == item.Subject))
        {
            ref var view = ref pieces[item.Piece];
            if (item.Subject != null) view.HeldDamage -= item.Damage;
            if (item.Routed)
            {
                view.Routed = false;
                RefreshPiece(item.Piece, false);
            }
            else
            {
                view.Flash = .15f;
                view.ArrowFlinch = item.Arrow;
                if (item.Subject != null)
                {
                    RefreshPiece(item.Piece, false);
                    if (unit == item.Piece) RefreshUnitHealth(unit);
                }
            }
        }
        if (item.Arrow) Emit(ArrowDust, item.At - new Vector2(0, 11), .25f);
        else Emit(item.Routed ? Smoke : Sparks, item.At, item.Routed ? .92f : .60f);
        bool bowExchange = item.Arrow || item.Subject != null;
        var at = item.At - (bowExchange ? new Vector2(13, 48) : new Vector2(24, 30));
        var pop = objects.Rent(bowExchange ? ArrowDamage : Number, at);
        objects.SetWorldText(pop, $"<b><color=#ffedac>-{item.Damage}</color></b>");
        TrackEffect(new(pop, at, .76f, .76f, true));
    }

    private void Emit(PrefabReference prefab, Vector2 at, float duration)
    {
        Guid id = objects.Rent(prefab, at - new Vector2(26, 26));
        objects.Animator(id)?.Play();
        TrackEffect(new(id,at,duration,duration,false));
    }

    private void TrackEffect(Effect effect)
    {
        for (int i = 0; i < effects.Length; i++)
        {
            if (effects[i].Id != Guid.Empty) continue;
            effects[i] = effect;
            return;
        }
        objects.Return(effect.Id);
    }

    private void ResetPresentation()
    {
        hitSoundCooldown = 0;
        lastHitCue = -1;
        claimCount = reportCount = 0;
        foreach (ref var piece in pieces.AsSpan()) ReturnPiece(ref piece);
        foreach (ref var town in towns.AsSpan())
        {
            Return(ref town.House);
            Return(ref town.Ribbon);
            Return(ref town.Label);
            Return(ref town.Timing);
            Return(ref town.Bar);
            Return(ref town.Fill);
            town = default;
        }
        foreach (var effect in effects)
            if (effect.Id != Guid.Empty) objects.Return(effect.Id);
        Array.Clear(effects);
        Array.Clear(impacts);
        ResetArrows();
        Array.Clear(fogTime);
        Array.Clear(fogOverlay);
        revealing = 0;
    }

    private void ReturnPiece(ref PieceView view)
    {
        ReturnPieceArt(ref view);
        view = default;
    }

    private void ReturnPieceArt(ref PieceView view)
    {
        Return(ref view.Actor);
        Return(ref view.Bar);
        Return(ref view.Fill);
        view.Moving = false;
        view.Tint = null;
    }

    private void Return(ref Guid id)
    {
        if (id == Guid.Empty) return;
        objects.Return(id);
        id = Guid.Empty;
    }
}
