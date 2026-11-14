using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Engine.Core;
using MoNoon.Decks;

public sealed class TrailTable : GameBehaviour
{
    public ComponentReference<DeckSource> Source;
    public Guid SeedLabel, DistanceLabel, PilesLabel, HandLabel, Message, DrawButton;
    public Guid First, Second, Third, Fourth, Fifth, Sixth;
    private CardDeck deck;
    private Guid[] slots;
    private readonly Dictionary<string, int> steps = new(StringComparer.Ordinal);
    private int distance;

    public override void OnCreate(ScriptLife life)
    {
        slots = new[] { First, Second, Third, Fourth, Fifth, Sixth };
        var table = life.Objects.ReadTable(Source.Resolve(life.Objects).Catalog);
        foreach (var row in table.Rows)
        {
            double value = table.Row(row.Key).GetNumber("steps");
            if (value < 1 || value > int.MaxValue || value != Math.Truncate(value))
                throw new InvalidDataException($"'{row.Key}' needs a positive number of steps.");
            steps.Add(row.Key, (int)value);
        }
    }

    public override void OnEnable(ScriptLife life)
    {
        deck = Source.Resolve(life.Objects).Deck;
        if (deck.DrawCount + deck.Hand.Count + deck.DiscardCount > slots.Length)
            throw new InvalidDataException("CardLab has six hand slots.");
        Show(life.Ui, "카드를 뽑고 손패에서 사용할 카드를 고르세요.");
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        if (tick.Input.Pressed("Draw")) Draw(tick.Ui);
        for (int i = 0; i < slots.Length; i++)
            if (tick.Input.Pressed(PlayActions[i])) Play(i, tick.Ui);
        if (tick.Input.Pressed("Restart")) Restart(false, tick.Ui);
        if (tick.Input.Pressed("NextSeed")) Restart(true, tick.Ui);
        if (tick.Input.Pressed("Save")) Store(tick.Session.Storage, true, tick.Ui);
        if (tick.Input.Pressed("Load")) Store(tick.Session.Storage, false, tick.Ui);
    }

    private static readonly string[] PlayActions = { "Play1", "Play2", "Play3", "Play4", "Play5", "Play6" };

    public void Draw(IGameUi ui)
    {
        int before = deck.ShuffleNumber;
        if (!deck.TryDraw(out string card)) { Show(ui, "모든 카드가 손에 있습니다."); return; }
        string message = before == deck.ShuffleNumber ? "뽑은 카드" : "버린 카드를 다시 섞어 뽑은 카드";
        Show(ui, $"{message}  ·  {deck.Label(card)}");
    }

    public void Play(int index, IGameUi ui)
    {
        if (index < 0 || index >= deck.Hand.Count) return;
        string card = deck.Hand[index];
        int next = checked(distance + steps[card]);
        deck.TryPlay(index, out _);
        distance = next;
        Show(ui, $"{deck.Label(card)} 사용  ·  {steps[card]}칸 전진했습니다.");
    }

    public void Restart(bool nextSeed, IGameUi ui)
    {
        deck.Reset(nextSeed ? unchecked(deck.Seed + 1) : deck.Seed);
        distance = 0;
        Show(ui, "덱을 섞고 새 여정을 시작했습니다.");
    }

    public void Store(PlayerStorage storage, bool save, IGameUi ui)
    {
        try
        {
            if (save)
            {
                var data = deck.Capture();
                data.Set("distance", distance);
                storage.SaveSlot(1, data);
            }
            else
            {
                var data = storage.LoadSlot(1, 1);
                if (data == null) { Show(ui, "저장된 여정이 없습니다."); return; }
                if (!data.Values.TryGetValue("distance", out var value) || value.ValueKind != JsonValueKind.Number ||
                    !value.TryGetInt32(out int restored) || restored < 0)
                    throw new InvalidDataException("Invalid saved distance.");
                deck.Restore(data);
                distance = restored;
            }
            Show(ui, save ? "여정을 저장했습니다." : "저장한 여정을 불러왔습니다.");
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        { Show(ui, "여정 저장 파일을 확인하세요."); }
    }

    private void Show(IGameUi ui, string message)
    {
        ui.SetText(SeedLabel, $"시드  {deck.Seed}     /     셔플  {deck.ShuffleNumber}");
        ui.SetText(DistanceLabel, $"이동 거리   {distance}칸");
        ui.SetText(PilesLabel, $"뽑을 카드   {deck.DrawCount}장       버린 카드   {deck.DiscardCount}장");
        ui.SetText(HandLabel, $"손패   {deck.Hand.Count}장");
        ui.SetInteractable(DrawButton, deck.DrawCount + deck.DiscardCount > 0);
        for (int i = 0; i < slots.Length; i++)
        {
            bool held = i < deck.Hand.Count;
            ui.SetVisible(slots[i], held);
            if (held)
            {
                string card = deck.Hand[i];
                ui.SetText(slots[i], $"[{i + 1}]   {deck.Label(card)}   +{steps[card]}칸");
            }
        }
        ui.SetText(Message, message);
    }
}
