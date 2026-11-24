using System.Globalization;

namespace Engine.Core;

public sealed partial class ScreenUi
{
    // Keep line breaks between drawing and hit testing. A bounds query before the first draw
    // uses provisional widths; the first render measures the same lines with its real font.
    private static bool Styled(UiTextComponent label) => label.RichText || label.RevealRate > 0;
    private void MeasureLines(Node n,ICanvas2D? canvas)
    {
        if(!EngineFeatures.Text)return; // no labels without the text feature (roadmap 15.12)
        if(n.Label is not {} label || n.Field!=null || !label.Wrap && !Styled(label))return;
        var text=Label(n);
        float available=label.Wrap?Math.Max(1,n.AllocatedWidth-Math.Max(0,label.InsetX)):float.PositiveInfinity,size=label.Size*Preferences.TextScale;
        if(n.LineSource==text && n.LineWidth==available && n.LineSize==size && (canvas==null || n.FontBackend==canvas.GetType()))return;
        n.LineSource=text;n.LineWidth=available;n.LineSize=size;n.FontBackend=canvas?.GetType();
        float Measure(string s,float points)=>canvas?.MeasureText(s,points)??Elements(s).Sum(part => (part[0] < 128 ? .55f : 1f) * points);
        if(Styled(label)){n.Rich=RichTextLayout.Build(text,label.RichText,size,available,Measure,IconExists(n),Preferences.TextScale,canvas==null?null:canvas.MeasureTextEnds);n.Lines=[];}
        else n.Lines=WrapText(text,available,canvas,size);
    }
    private static Func<string,bool> IconExists(Node n) => n.Icons is {} icons ? icons.ContainsKey : _ => false;

    private bool revealing; // until a game advances reveals, previews show complete labels
    internal void AdvanceReveal(double seconds)
    {
        revealing = true;
        if (!EngineFeatures.Text) return; // no labels to reveal (roadmap 15.12)
        foreach (var n in treeOrder)
        {
            if (n.Label is not { RevealRate: > 0 } label || n.Field != null) continue;
            // A hidden label starts again when it is shown, so a reopened dialogue reveals its first line again.
            if (!Visible(n)) { n.RevealSource = null; continue; }
            var text = Label(n);
            if (text != n.RevealSource) StartReveal(n, text);
            n.Revealed = Math.Min(n.RevealTotal, n.Revealed + label.RevealRate * seconds);
        }
    }
    private static void StartReveal(Node n, string text)
    {
        n.RevealSource = text;
        n.Revealed = 0;
        n.RevealTotal = RichTextLayout.CountElements(text, n.Label!.RichText, IconExists(n));
    }
    private int Revealed(Node n, string text) =>
        !revealing || n.Label is not { RevealRate: > 0 } ? int.MaxValue : n.RevealSource == text ? (int)n.Revealed : 0;

    public UiRevealProgress? RevealProgress(Guid id)
    {
        if (byId.GetValueOrDefault(id) is not { Label: {} label, Field: null } n) return null;
        var text = Label(n);
        int total = n.RevealSource == text ? n.RevealTotal : RichTextLayout.CountElements(text, label.RichText, IconExists(n));
        return new(Math.Min(total, Revealed(n, text)), total);
    }

    public bool SkipReveal(Guid id)
    {
        if (byId.GetValueOrDefault(id) is not { Label: { RevealRate: > 0 }, Field: null } n) return false;
        var text = Label(n);
        if (text != n.RevealSource) StartReveal(n, text);
        bool skipped = n.Revealed < n.RevealTotal;
        n.Revealed = n.RevealTotal;
        return skipped;
    }
    // Line widths come from the paragraph's shaped glyph positions (MeasureTextEnds); without a canvas, an estimate.
    internal static string[] WrapText(string text,float available,ICanvas2D? canvas,float size)
    {
        var lines=new List<string>();
        foreach(var paragraph in text.Replace("\r\n","\n").Split('\n'))
        {
            if(paragraph.Length==0){lines.Add("");continue;}
            var right=new float[paragraph.Length];
            if(canvas!=null)canvas.MeasureTextEnds(paragraph,size,right);
            else{float x=0;int at=0;foreach(string part in Elements(paragraph)){x+=(part[0]<128?.55f:1f)*size;right.AsSpan(at,part.Length).Fill(x);at+=part.Length;}}
            float Edge(int i)=>i>0?right[i-1]:0;
            int start=0,end=0,breakAt=0;
            foreach(string part in Elements(paragraph))
            {
                if(Edge(end+part.Length)-Edge(start)>available && end>start)
                {
                    int cut=breakAt>start?breakAt:end;
                    lines.Add(paragraph[start..cut].TrimEnd());start=cut;breakAt=start;
                }
                end+=part.Length;
                if(string.IsNullOrWhiteSpace(part))breakAt=end;
            }
            lines.Add(paragraph[start..].TrimEnd());
        }
        return lines.ToArray();
    }
    private static IEnumerable<string> Elements(string text)
    {
        var elements=StringInfo.GetTextElementEnumerator(text);
        while(elements.MoveNext())yield return elements.GetTextElement();
    }
}
