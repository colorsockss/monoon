using System.Drawing;
using Engine.Core;

internal static class RichTextChecks
{
    [Check(1600, StopAfter = "rich-text")] static void Registered(CheckRun c) => Run(c.Repository);
    internal static void Run(string repo)
    {
        // Latin is half an em, everything else a full em, like the provisional UI measure.
        static float Measure(string s, float size) => s.Sum(c => c < 128 ? .5f : 1f) * size;
        Func<string, bool> herb = name => name == "herb";

        var styled = RichTextLayout.Build("A<b>B</b><color=#FF0000>C</color><size=32>D</size><icon=herb>", true, 16, float.PositiveInfinity, Measure, herb);
        Require(styled.Count == 5 && styled.Lines == 1 && styled.Height == 32 * 1.4f && Math.Abs(styled.Width - (3 * 8 + 16 + 19.2f)) < .01f,
            "tags style text without adding characters; an icon counts as one, the largest size sets the line height");
        foreach (var literal in new[] { "<color=red>x</color>", "a < b > c", "<icon=gem>", "</b>x", "<b>x</i>" })
            Require(RichTextLayout.CountElements(literal, true, herb) > 1 &&
                RichTextLayout.Build(literal, true, 16, 1000, Measure, herb).Count == RichTextLayout.CountElements(literal, true, herb),
                $"unknown or malformed tags stay literal: {literal}");
        Require(RichTextLayout.CountElements("<b>x</b>", false) == 8, "plain labels draw tags as text");
        Require(RichTextLayout.Build("<size=24>크게</size>", true, 19 * 1.5f, 1000, Measure, tagScale: 1.5f).Height == 24 * 1.5f * 1.4f,
            "size tags follow the text scale accessibility setting");

        string korean = "약초는 개울 옆 풀밭에서 잘 자라요. 아침 이슬이 마르기 전에 잎이 넓고 줄기가 곧은 것만 골라 오세요.";
        var wrapped = RichTextLayout.Build(korean, true, 10, 120, Measure);
        Require(wrapped.Lines > 3 && wrapped.Count == RichTextLayout.CountElements(korean, true), "long Korean line wraps without losing characters");
        var canvas = new Recorder();
        wrapped.Draw(canvas, 0, 0, 1, int.MaxValue, Color.White);
        Require(canvas.Texts.All(t => t.Length > 0 && t[0] != ' ' && Measure(t, 10) <= 120) &&
            string.Concat(canvas.Texts).Replace(" ", "") == korean.Replace(" ", ""),
            "Korean wraps between words, never starts a line with a space and fits the width");
        var word = RichTextLayout.Build("가나다라마바사아자차.", true, 10, 50, Measure);
        canvas = new();
        word.Draw(canvas, 0, 0, 1, int.MaxValue, Color.White);
        Require(canvas.Texts.Count == 3 && canvas.Texts.All(t => t[0] != '.') && canvas.Texts[^1].EndsWith("차."),
            "a word wider than the line breaks between characters but keeps closing punctuation on the previous line");

        canvas = new();
        styled.Draw(canvas, 0, 0, 2, 2, Color.White);
        Require(canvas.Texts.SequenceEqual(["A", "B", "B"]) && canvas.Clips == 0 && canvas.Sprites == 0,
            "a reveal draws only revealed elements; bold is drawn twice from the same glyphs");
        canvas = new();
        RichTextLayout.Build("가나다라", true, 10, 100, Measure).Draw(canvas, 0, 0, 1, 2, Color.White);
        Require(canvas.Texts.SequenceEqual(["가나다라"]) && canvas.Clips == 1 && canvas.LastClip.Width == 20,
            "a partly revealed run reuses the whole cached label and clips it");
        // Shaped runs (SDL_ttf clusters on desktop): "AV" kerns 2 px closer, so positions follow the run's cluster edges.
        static void Kerned(string s, float size, Span<float> ends)
        {
            float x = 0;
            for (int i = 0; i < s.Length; i++) ends[i] = x += size / 2 - (i > 0 && s[i - 1] == 'A' && s[i] == 'V' ? 2 : 0);
        }
        canvas = new();
        var kerned = RichTextLayout.Build("AVA", true, 10, 100, Measure, ends: Kerned);
        kerned.Draw(canvas, 0, 0, 1, 2, Color.White);
        Require(kerned.Width == 13 && canvas.Clips == 1 && canvas.LastClip.Width == 8,
            "with shaped runs the width and the reveal clip follow the cluster edges, kerning included");
        canvas = new();
        styled.Draw(canvas, 0, 0, 1, 5, Color.White, icon: name => "/icons/" + name + ".png");
        Require(canvas.Sprites == 1 && canvas.Colors.Contains(Color.FromArgb(255, 255, 0, 0)), "colors and icons are drawn");

        // Reveal through the UI API that game scripts use.
        var menu = new SceneDocument();
        var root = menu.CreateUi("Canvas", null);
        var line = menu.CreateUi("Text", root);
        var text = line.GetComponent<UiTextComponent>()!;
        (text.RichText, text.RevealRate, text.Wrap) = (true, 10, true);
        menu.Validate();
        var ui = new ScreenUi(menu, null);
        ui.SetText(line.Id, "<b>안녕</b>하세요");
        Require(ui.RevealProgress(line.Id) == new UiRevealProgress(5, 5), "previews before play show the whole label");
        ui.AdvanceReveal(.25);
        Require(ui.RevealProgress(line.Id) == new UiRevealProgress(2, 5), "the reveal advances by rate × UI time");
        ui.AdvanceReveal(.1);
        Require(ui.RevealProgress(line.Id)!.Value.Shown == 3 && ui.SkipReveal(line.Id) && ui.RevealProgress(line.Id)!.Value.Complete &&
            !ui.SkipReveal(line.Id), "skip shows the rest once");
        ui.SetText(line.Id, "다음 대사");
        Require(ui.RevealProgress(line.Id) == new UiRevealProgress(0, 5), "new text starts hidden before the next UI update");
        ui.AdvanceReveal(.2);
        ui.SetVisible(root.Id, false);
        ui.AdvanceReveal(.2);
        ui.SetVisible(root.Id, true);
        ui.AdvanceReveal(0);
        Require(ui.RevealProgress(line.Id) == new UiRevealProgress(0, 5), "a hidden label reveals again when shown");
        var drawn = new Recorder();
        ui.AdvanceReveal(.2);
        ui.Draw(drawn);
        Require(drawn.Texts.SequenceEqual(["다음", "대사"]) || drawn.Texts.SequenceEqual(["다음 대사"]), "the UI draws a revealing label");
        Require(drawn.Clips >= 1, "the UI clips the partly revealed run");
        text.RevealRate = -1;
        try { menu.Validate(); throw new Exception("Negative reveal rate accepted."); }
        catch (InvalidDataException) { Require(true, "scene validation rejects a negative reveal rate"); }
        text.RevealRate = 10;

        // InventoryLab's herb yard (Showcase.scene): the herbalist's translated rich line, icon sprite and the project's English locale.
        var sample = Path.Combine(repo, "samples", "InventoryLab");
        var scene = SceneDocument.Load(Path.Combine(sample, "Scenes", "Showcase.scene"));
        var lab = new ScreenUi(scene, ProjectAssets.ReadManifest(sample));
        Require(lab.Preferences.Locale == "en-US" && lab.Translate("herbalist.meadow").StartsWith("Herbs grow best in the <color=#8FD3FF>") &&
            lab.Images.Any(p => p.EndsWith("Herb.png")), "InventoryLab resolves the herbalist's translated line in English and its herb icon");
        lab.Preferences.Locale = "ko-KR";
        string meadow = lab.Translate("herbalist.meadow");
        Require(meadow.StartsWith("약초는 <color=#8FD3FF>"), "the same key resolves the Korean line");
        // The yard's dialogue line: 14 px text in a 494 px box.
        var layout = RichTextLayout.Build(meadow, true, 14, 494, (s, size) => Measure(s, size));
        Require(layout.Lines is >= 2 and <= 3, $"the long Korean line wraps to two or three lines in the dialogue box ({layout.Lines})");
    }

    private sealed class Recorder : ICanvas2D, IDisposable
    {
        public readonly List<string> Texts = [];
        public readonly List<Color> Colors = [];
        public int Clips, Sprites;
        public RectangleF LastClip;
        public IDisposable PushClip(RectangleF area) { Clips++; LastClip = area; return this; }
        public void Dispose() { }
        public float MeasureText(string text, float size) => text.Sum(c => c < 128 ? .5f : 1f) * size;
        public void DrawText(string text, float x, float y, float size, Color color) { Texts.Add(text); Colors.Add(color); }
        public void Clear(Color color) { }
        public void FillRectangle(RectangleF area, Color color) { }
        public bool DrawSprite(string path, RectangleF area) { Sprites++; return true; }
    }

    private static void Require(bool ok, string message)
    {
        if (!ok) throw new Exception("Rich text: " + message);
        Console.WriteLine("PASS rich text: " + message);
    }
}
