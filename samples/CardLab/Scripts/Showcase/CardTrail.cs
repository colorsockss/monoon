using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Text.Json;
using Engine.Core;
using MoNoon.Decks;

// CardLab showcase. The sample's CardDeck (seeded, numbered shuffles; draw / hand / discard piles; Capture / Restore) drives a
// card table, and the table's "steps" column says how many stones a played card walks the hero along a 24-step trail.
// D draws (the card slides from the draw pile), 1-6 plays (the card flies to the discard pile, the hand closes up, the hero walks),
// S saves the piles and the hero's step, L loads them back, R restarts with the same seed, N with the next one.
// Raises the HUD flags "table" (first draw), "save" (a load restores), "seed" (a restart).
public sealed class CardTrail : GameBehaviour
{
    public ComponentReference<DeckSource> Source;
    public Guid Hero, Shadow, Info, Note, Last, Log, DrawLabel, DiscardLabel, Discard, DiscardIcon, DiscardName, DiscardSteps;
    public Guid[] DrawBacks = [], Cards = [], Icons = [], Names = [], Steps = [];
    public SpriteReference Idle, Walk1, Walk2, Walk3, Walk4;
    // Layout shared with showcase-src/card/scene.py.
    private const float CardWidth = 84, Gap = 8, HandX = 122, DrawX = 18, DiscardX = 698, StepSeconds = .26f;
    private const int TrailSteps = 24;
    private static Vector2 Stone(int i) => new(70 + i * 27.5f, 178 + 34 * MathF.Sin(i * .55f));
    private static float SlotX(int i) => HandX + i * (CardWidth + Gap);

    private CardDeck deck = null!;
    private readonly Dictionary<string, int> steps = new(StringComparer.Ordinal);
    private readonly List<string> drawn = new(), lastRun = new();   // lastRun: the first draws before a same-seed restart
    private string? discardTop;
    private int distance, walkLeft, playing = -1;
    private float wait, walkClock;
    private SpriteReference[] walk = [];

    public override void OnCreate(ScriptLife life)
    {
        var table = life.Objects.ReadTable(Source.Resolve(life.Objects).Catalog);
        foreach (var row in table.Rows) steps.Add(row.Key, (int)table.Row(row.Key).GetNumber("steps"));
        walk = [Walk1, Walk2, Walk3, Walk4];
    }

    public override void OnEnable(ScriptLife life)
    {
        deck = Source.Resolve(life.Objects).Deck;
        Refresh(life.Ui, "Draw cards and play them to walk.");
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        if (wait > 0) { wait -= tick.DeltaTime; if (wait <= 0) Continue(tick); }
        if (walkLeft > 0 || playing >= 0)
        {
            walkClock += tick.DeltaTime;
            if (walkLeft > 0) tick.Objects.SetSprite(Hero, walk[(int)(walkClock / .11f) % walk.Length]);
            return;
        }
        var input = tick.Input;
        if (input.Pressed("Draw")) Draw(tick);
        for (int i = 0; i < 6; i++) if (input.Pressed(PlayKeys[i])) { Play(tick, i); return; }
        if (input.Pressed("Save")) Save(tick);
        else if (input.Pressed("Load")) Load(tick);
        else if (input.Pressed("Restart")) Restart(tick, deck.Seed);
        else if (input.Pressed("NextSeed")) Restart(tick, unchecked(deck.Seed + 1));
    }

    private static readonly string[] PlayKeys = ["Play1", "Play2", "Play3", "Play4", "Play5", "Play6"];

    private void Draw(ScriptTick tick)
    {
        int shuffle = deck.ShuffleNumber;
        if (deck.Hand.Count >= Cards.Length || !deck.TryDraw(out string card)) { Refresh(tick.Ui, "Every card is in your hand."); return; }
        if (shuffle != deck.ShuffleNumber) discardTop = null;
        drawn.Add(deck.Label(card));
        if (drawn.Count > 7) drawn.RemoveAt(0);
        tick.Session.Set("table");
        int slot = deck.Hand.Count - 1;
        Refresh(tick.Ui, shuffle != deck.ShuffleNumber ? $"Discards reshuffled: shuffle {deck.ShuffleNumber}." : $"Drew {deck.Label(card)}, +{steps[card]} steps.");
        tick.Objects.Tween(Cards[slot], TweenClock.Ui).UiRect(new RectangleF(DrawX - SlotX(slot), 0, 0, 0), .001f)
            .UiRect(RectangleF.Empty, .35f, TweenEase.EaseOut);
    }

    private void Play(ScriptTick tick, int index)
    {
        if (index >= deck.Hand.Count) return;
        playing = index;
        wait = .3f;
        tick.Objects.Tween(Cards[index], TweenClock.Ui).UiRect(new RectangleF(DiscardX - SlotX(index), 0, 0, 0), .3f, TweenEase.EaseIn);
    }

    // The played card reached the discard pile: commit the play, close up the hand, then walk.
    private void Continue(ScriptTick tick)
    {
        if (playing < 0) { Stepped(tick); return; }
        int index = playing;
        playing = -1;
        deck.TryPlay(index, out string card);
        discardTop = card;
        int walkSteps = Math.Min(steps[card], TrailSteps - distance);
        Refresh(tick.Ui, walkSteps > 0 ? $"Played {deck.Label(card)}: walk {walkSteps}." : "The hero is already at camp.");
        for (int i = index; i < Cards.Length; i++)
        {
            var tween = tick.Objects.Tween(Cards[i], TweenClock.Ui);
            if (i < deck.Hand.Count) tween.UiRect(new RectangleF(CardWidth + Gap, 0, 0, 0), .001f).UiRect(RectangleF.Empty, .2f, TweenEase.EaseOut);
            else tween.UiRect(RectangleF.Empty, .001f);
        }
        walkLeft = walkSteps;
        walkClock = 0;
        if (walkLeft > 0) StepOnce(tick);
    }

    private void StepOnce(ScriptTick tick)
    {
        MoveHero(tick.Objects, distance + 1, StepSeconds);
        wait = StepSeconds;
    }

    private void Stepped(ScriptTick tick)
    {
        distance++;
        walkLeft--;
        if (walkLeft > 0) { StepOnce(tick); return; }
        tick.Objects.SetSprite(Hero, Idle);
        Refresh(tick.Ui, distance >= TrailSteps ? "Reached the camp!" : $"Walked to step {distance}.");
    }

    private void MoveHero(IGameObjects objects, int step, float seconds)
    {
        var foot = Stone(step);
        objects.Tween(Hero, TweenClock.Game).Move(new Vector2(MathF.Round(foot.X - 21), MathF.Round(foot.Y - 56)), seconds, TweenEase.Linear);
        objects.Tween(Shadow, TweenClock.Game).Move(new Vector2(MathF.Round(foot.X - 16), MathF.Round(foot.Y - 6)), seconds, TweenEase.Linear);
    }

    private void Save(ScriptTick tick)
    {
        var data = deck.Capture();
        data.Set("distance", distance);
        try { tick.Session.Storage.SaveSlot(1, data); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Refresh(tick.Ui, "Could not save."); return; }
        Refresh(tick.Ui, $"Saved: {deck.Hand.Count} in hand, step {distance}.");
    }

    private void Load(ScriptTick tick)
    {
        try
        {
            var data = tick.Session.Storage.LoadSlot(1, 1);
            if (data == null) { Refresh(tick.Ui, "Nothing saved yet. S saves."); return; }
            if (!data.Values.TryGetValue("distance", out var value) || !value.TryGetInt32(out int saved) || saved < 0 || saved > TrailSteps)
                throw new InvalidDataException("Invalid saved distance.");
            deck.Restore(data);
            distance = saved;
            discardTop = data.Values["discard"] is { ValueKind: JsonValueKind.Array } pile && pile.GetArrayLength() > 0 ? pile[pile.GetArrayLength() - 1].GetString() : null;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
        { Refresh(tick.Ui, "The save could not be read."); return; }
        tick.Session.Set("save");
        Settle(tick);
        Refresh(tick.Ui, $"Loaded: hand, piles and step {distance}.");
    }

    private void Restart(ScriptTick tick, int seed)
    {
        bool same = seed == deck.Seed;
        deck.Reset(seed);
        distance = 0;
        discardTop = null;
        lastRun.Clear();
        if (same) lastRun.AddRange(drawn.GetRange(0, Math.Min(3, drawn.Count)));
        drawn.Clear();
        tick.Session.Set("seed");
        Settle(tick);
        Refresh(tick.Ui, same ? $"Seed {seed} again: the same order." : $"Seed {seed}: a new order.");
    }

    // Puts every card slot back in its place and slides the hero to the current step.
    private void Settle(ScriptTick tick)
    {
        foreach (var slot in Cards) tick.Objects.Tween(slot, TweenClock.Ui).UiRect(RectangleF.Empty, .001f);
        MoveHero(tick.Objects, distance, .5f);
        tick.Objects.SetSprite(Hero, Idle);
    }

    private void Refresh(IGameUi ui, string note)
    {
        for (int i = 0; i < Cards.Length; i++)
        {
            bool held = i < deck.Hand.Count;
            ui.SetVisible(Cards[i], held);
            if (held) Face(ui, deck.Hand[i], Icons[i], Names[i], Steps[i]);
        }
        ui.SetVisible(Discard, discardTop != null);
        if (discardTop != null) Face(ui, discardTop, DiscardIcon, DiscardName, DiscardSteps);
        for (int k = 0; k < DrawBacks.Length; k++) ui.SetVisible(DrawBacks[k], k < Math.Min(DrawBacks.Length, deck.DrawCount));
        ui.SetText(DrawLabel, $"DRAW  {deck.DrawCount}");
        ui.SetText(DiscardLabel, $"DISCARD  {deck.DiscardCount}");
        ui.SetText(Log, drawn.Count == 0 ? "" : "Drawn   " + string.Join("  ·  ", drawn));
        ui.SetText(Info, $"Seed {deck.Seed}     Shuffle {deck.ShuffleNumber}     Step {distance} / {TrailSteps}");
        ui.SetText(Note, note);
        ui.SetText(Last, lastRun.Count == 0 ? "" : "Last run   " + string.Join("  ·  ", lastRun));
    }

    private void Face(IGameUi ui, string card, Guid icon, Guid name, Guid stepText)
    {
        ui.SetText(icon, $"<icon={card}>");
        ui.SetText(name, deck.Label(card));
        ui.SetText(stepText, $"+{steps[card]}");
    }
}
