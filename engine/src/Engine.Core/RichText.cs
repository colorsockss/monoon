using System.Drawing;
using System.Globalization;

namespace Engine.Core;

/// <summary>Progress of a per-character reveal: elements drawn so far and in the whole label.</summary>
public readonly record struct UiRevealProgress(int Shown, int Total)
{
    public bool Complete => Shown >= Total;
}

/// <summary>
/// UI text markup: &lt;color=#RRGGBB&gt;, &lt;b&gt;, &lt;size=N&gt; (closed by &lt;/color&gt;, &lt;/b&gt;, &lt;/size&gt;) and &lt;icon=name&gt;.
/// Unknown or malformed tags stay literal. Lines break after spaces like plain wrapped labels, so Korean breaks between
/// words, and a word wider than the line breaks between characters without starting a line with closing punctuation.
/// Bold is drawn twice with a small offset and sizes scale the same label, so one font object serves every style.
/// With <c>ends</c> each run of same-style text is measured in one piece, so element positions follow the shaped glyph
/// clusters (kerning included) and reveals and colour changes line up with the drawn string.
/// </summary>
public sealed class RichTextLayout
{
    private readonly record struct Style(Color? Color, bool Bold, float Size); // a null color uses the label's ink
    private struct Element
    {
        public string Text;
        public int Style;
        public bool Icon, Break;
        public float X, Width;
        public int Line;
    }
    private readonly record struct Segment(int Start, int End, string Text, int Style, bool Icon, float X, float Width, int Line);
    private readonly record struct Row(float Top, float Height, float Max);

    private readonly List<Style> styles = [];
    private readonly Element[] elements;
    private readonly Segment[] segments;
    private readonly Row[] rows;
    /// <summary>Revealable elements: text elements, including spaces, and icons. Line breaks are not counted.</summary>
    public int Count { get; }
    public float Width { get; }
    public float Height { get; }
    public int Lines => rows.Length;

    private RichTextLayout(string text, bool rich, float size, float tagScale, float available, Func<string, float, float> measure,
        Func<string, bool>? iconExists, TextEnds? ends)
    {
        elements = Parse(text, rich, size, tagScale, styles, iconExists).ToArray();
        Count = elements.Count(e => !e.Break);        for (int i = 0; i < elements.Length;)
        {
            ref var e = ref elements[i];
            float s = styles[e.Style].Size;
            if (e.Break || e.Icon || ends == null)
            {
                if (!e.Break) e.Width = e.Icon ? s * 1.2f : measure(e.Text, s);
                i++;
                continue;
            }
            int j = i + 1;
            while (j < elements.Length && !elements[j].Break && !elements[j].Icon && elements[j].Style == e.Style) j++;
            string run = string.Concat(elements[i..j].Select(x => x.Text));
            var right = new float[run.Length];
            ends(run, s, right);            float left = 0;
            for (int k = i, at = 0; k < j; k++)
            {
                at += elements[k].Text.Length;
                elements[k].Width = Math.Max(0, right[at - 1] - left);
                left = Math.Max(left, right[at - 1]);
            }
            i = j;
        }
        var lines = new List<(int Start, int End)>();
        int start = 0;
        for (int i = 0; i <= elements.Length; i++)
        {
            if (i < elements.Length && !elements[i].Break) continue;
            Wrap(start, i, available, lines);
            start = i + 1;
        }
        var placed = new List<Row>();
        float top = 0, widest = 0;
        for (int line = 0; line < lines.Count; line++)
        {
            var (from, to) = lines[line];
            float max = 0, x = 0;
            for (int i = from; i < to; i++)
            {
                ref var e = ref elements[i];
                e.Line = line;
                e.X = x;
                x += e.Width;
                max = Math.Max(max, styles[e.Style].Size);
            }
            if (max == 0) max = size;
            widest = Math.Max(widest, x);
            placed.Add(new(top, max * 1.4f, max));
            top += max * 1.4f;
        }
        for (int i = 0; i < elements.Length; i++) if (elements[i].Break) elements[i].Line = -1;
        rows = placed.ToArray();
        Width = widest;
        Height = top;
        segments = Segments().ToArray();
    }

    /// <summary>See <see cref="ICanvas2D.MeasureTextEnds"/>.</summary>
    public delegate void TextEnds(string text, float size, Span<float> ends);

    // size is the base size in layout units; tagScale converts <size=N> to the same units.
    public static RichTextLayout Build(string text, bool rich, float size, float available,
        Func<string, float, float> measure, Func<string, bool>? iconExists = null, float tagScale = 1, TextEnds? ends = null) =>
        new(text, rich, size, tagScale, available, measure, iconExists, ends);

    /// <summary>The revealable element count of a label without measuring it.</summary>
    public static int CountElements(string text, bool rich, Func<string, bool>? iconExists = null) =>
        Parse(text, rich, 16, 1, [], iconExists).Count(e => !e.Break);

    private void Wrap(int start, int end, float available, List<(int, int)> lines)
    {
        int first = start, breakAt = start;
        float used = 0;
        for (int i = start; i < end; i++)
        {
            float width = elements[i].Width;
            if (used + width > available && i > first)
            {
                int cut = breakAt > first ? breakAt : i;
                // A forced break keeps closing punctuation with the previous character.
                if (cut == i && cut - 1 > first && Closing(elements[cut].Text)) cut--;
                lines.Add((first, cut));
                // Spaces that caused the break stay at the end of the previous line.
                while (cut < end && Space(elements[cut])) { elements[cut].Width = 0; cut++; }
                first = breakAt = cut;
                used = 0;
                for (int j = cut; j < i; j++) used += elements[j].Width;
            }
            used += width;
            if (Space(elements[i])) breakAt = i + 1;
        }
        lines.Add((first, Math.Max(first, end)));
    }

    private static bool Space(in Element e) => !e.Icon && string.IsNullOrWhiteSpace(e.Text);
    private static bool Closing(string s) => !string.IsNullOrEmpty(s) && ".,!?:;)]}…·、。，．！？」』〉》%".Contains(s[0]);

    private IEnumerable<Segment> Segments()
    {
        for (int i = 0; i < elements.Length;)
        {
            var e = elements[i];
            if (e.Break) { i++; continue; }
            int j = i + 1;
            if (!e.Icon)
                while (j < elements.Length && !elements[j].Break && !elements[j].Icon && elements[j].Style == e.Style && elements[j].Line == e.Line) j++;
            float width = 0;
            for (int k = i; k < j; k++) width += elements[k].Width;
            yield return new(i, j, e.Icon ? e.Text : string.Concat(elements[i..j].Select(x => x.Text)).TrimEnd(), e.Style, e.Icon, e.X, width, e.Line);
            i = j;
        }
    }

    /// <summary>Draws the first <paramref name="revealed"/> elements at <paramref name="scale"/>; the layout never reflows while revealing.</summary>
    public void Draw(ICanvas2D canvas, float x, float y, float scale, int revealed, Color ink, bool forceInk = false, Func<string, string?>? icon = null)
    {
        int shown = 0;
        foreach (var s in segments)
        {
            int drawable = s.End - s.Start;
            if (shown >= revealed) break;
            var style = styles[s.Style];
            var row = rows[s.Line];
            float size = style.Size * scale, sx = x + s.X * scale, sy = y + (row.Top + row.Max - style.Size) * scale;
            var color = forceInk ? ink : style.Color ?? ink;
            if (s.Icon)
            {
                if (icon?.Invoke(s.Text) is {} path)
                    canvas.DrawSprite(path, new(sx + size * .1f, sy + size * .12f, size, size));
            }
            else if (s.Text.Length > 0)
            {
                IDisposable? clip = null;
                if (shown + drawable > revealed)
                {
                    // Draw the cached label once and clip it, instead of rasterising a new prefix every step.
                    float visible = 0;
                    for (int i = s.Start; i < s.Start + (revealed - shown); i++) visible += elements[i].Width;
                    canvas.FlushSprites();
                    clip = canvas.PushClip(new(sx, y + row.Top * scale - size, visible * scale, (row.Height + row.Max) * scale + size));
                }
                using (clip)
                {
                    canvas.DrawText(s.Text, sx, sy, size, color);
                    if (style.Bold) canvas.DrawText(s.Text, sx + Math.Max(.5f, size / 20), sy, size, color);
                }
            }
            shown += drawable;
        }
    }

    private static List<Element> Parse(string text, bool rich, float size, float tagScale, List<Style> styles, Func<string, bool>? iconExists)
    {
        var result = new List<Element>();
        var stack = new List<(string Tag, Style Style)>();
        var style = new Style(null, false, size);
        int Index(Style s)
        {
            int i = styles.IndexOf(s);
            if (i >= 0) return i;
            styles.Add(s);
            return styles.Count - 1;
        }
        void Add(string part)
        {
            var elements = StringInfo.GetTextElementEnumerator(part);
            while (elements.MoveNext())
            {
                string element = elements.GetTextElement();
                if (element == "\r") continue;
                result.Add(element is "\n" or "\r\n" ? new() { Text = "", Break = true, Style = Index(style) } : new() { Text = element, Style = Index(style) });
            }
        }
        int at = 0;
        while (at < text.Length)
        {
            int open = rich ? text.IndexOf('<', at) : -1;
            if (open < 0) { Add(text[at..]); break; }
            int close = text.IndexOf('>', open + 1);
            if (close < 0) { Add(text[at..]); break; }
            Add(text[at..open]);
            string tag = text[(open + 1)..close].Trim();
            if (!Apply(tag)) Add(text[open..(close + 1)]);
            at = close + 1;
        }
        return result;

        bool Apply(string tag)
        {
            if (tag == "b") { stack.Add(("b", style)); style = style with { Bold = true }; return true; }
            if (tag.StartsWith("color=", StringComparison.Ordinal) && SceneObject.TryColor(tag[6..].Trim('"'), out var color))
            { stack.Add(("color", style)); style = style with { Color = color }; return true; }
            if (tag.StartsWith("size=", StringComparison.Ordinal) &&
                float.TryParse(tag[5..].Trim('"'), NumberStyles.Float, CultureInfo.InvariantCulture, out var points) && points is > 0 and <= 1024)
            { stack.Add(("size", style)); style = style with { Size = points * tagScale }; return true; }
            if (tag.StartsWith("icon=", StringComparison.Ordinal))
            {
                string name = tag[5..].TrimEnd('/').Trim().Trim('"');
                if (name.Length == 0 || iconExists?.Invoke(name) == false) return false;
                result.Add(new() { Text = name, Icon = true, Style = Index(style) });
                return true;
            }
            if (tag.Length > 1 && tag[0] == '/' && stack.Count > 0 && stack[^1].Tag == tag[1..])
            {
                style = stack[^1].Style;
                stack.RemoveAt(stack.Count - 1);
                return true;
            }
            return false;
        }
    }
}
