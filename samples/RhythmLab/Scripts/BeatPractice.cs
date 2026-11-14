using System;
using System.Diagnostics;
using System.Globalization;
using Engine.Core;

public sealed class BeatPractice : GameBehaviour
{
    public Guid Lamp1, Lamp2, Lamp3, Lamp4, Verdict, Detail, Previous;
    private readonly RhythmTiming timing = new(120, 35, 140);
    private Guid[] lamps;
    private int lit = -1;
    private long lastBeat;
    private string lastLine;

    public override void OnCreate(ScriptLife life)
    {
        lamps = new[] { Lamp1, Lamp2, Lamp3, Lamp4 };
        foreach (var id in lamps) life.Ui.SetVisible(id, false);
    }

    public override void FixedUpdate(ScriptTick tick)
    {
        var audio = tick.Objects.Audio;
        if (audio.TryGetPlaybackFrame(tick.ObjectId, Stopwatch.GetTimestamp(), out long current))
        {
            var beat = timing.Judge(current);
            int lamp = beat.OffsetFrames >= 0 && beat.OffsetMilliseconds < 160 ? (int)((beat.Beat - 1) % 4) : -1;
            if (lamp != lit)
            {
                if (lit >= 0) tick.Ui.SetVisible(lamps[lit], false);
                if (lamp >= 0) tick.Ui.SetVisible(lamps[lamp], true);
                lit = lamp;
            }
        }

        foreach (var timestamp in tick.Input.PressTimestamps("Tap"))
        {
            if (!audio.TryGetPlaybackFrame(tick.ObjectId, timestamp, out long frame))
            {
                tick.Ui.SetText(Verdict, "재생 대기");
                continue;
            }
            var hit = timing.Judge(frame);
            string label = hit.Grade switch
            {
                BeatGrade.Early => "빠름",
                BeatGrade.OnTime => "정확",
                BeatGrade.Late => "늦음",
                _ => "판정 범위 밖"
            };
            if (hit.Grade != BeatGrade.Outside)
            {
                if (hit.Beat == lastBeat) continue;
                lastBeat = hit.Beat;
            }
            string offset = hit.OffsetMilliseconds.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture);
            string detail = $"{hit.Beat}박   /   {offset} ms";
            tick.Ui.SetText(Verdict, label);
            tick.Ui.SetText(Detail, detail);
            if (lastLine != null) tick.Ui.SetText(Previous, lastLine);
            lastLine = $"{label}    {detail}";
            tick.Session.Log($"{label}    {detail}");
        }
    }
}
