using System.Globalization;

namespace Engine.Core;

public sealed partial class ScreenUi
{
    // Keep line breaks between drawing and hit testing. A bounds query before the first draw
    // uses provisional widths; the first render measures the same lines with its real font.
    private void MeasureLines(Node n,ICanvas2D? canvas)
    {
        if(n.Label is not { Wrap:true } label || n.Field!=null)return;
        var text=Label(n);
        float available=Math.Max(1,n.AllocatedWidth-Math.Max(0,label.InsetX)),size=label.Size*Preferences.TextScale;
        if(n.LineSource==text && n.LineWidth==available && n.LineSize==size && (canvas==null || n.FontBackend==canvas.GetType()))return;
        n.LineSource=text;n.LineWidth=available;n.LineSize=size;n.FontBackend=canvas?.GetType();
        float Measure(string s)=>canvas?.MeasureText(s,size)??Elements(s).Sum(part => (part[0] < 128 ? .55f : 1f) * size);
        n.Lines=WrapText(text,available,Measure);
    }
    internal static string[] WrapText(string text,float available,Func<string,float> measure)
    {
        var lines=new List<string>();
        foreach(var paragraph in text.Replace("\r\n","\n").Split('\n'))
        {
            if(paragraph.Length==0){lines.Add("");continue;}
            int start=0,end=0,breakAt=0;float used=0;
            foreach(string part in Elements(paragraph))
            {
                float width=measure(part);
                if(used+width>available && end>start)
                {
                    int cut=breakAt>start?breakAt:end;
                    lines.Add(paragraph[start..cut].TrimEnd());start=cut;used=measure(paragraph[start..end]);breakAt=start;
                }
                end+=part.Length;used+=width;
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
