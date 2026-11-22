using System.Drawing;
using System.Numerics;

namespace Engine.Core;

public sealed partial class ScreenUi
{
    internal GameLocalization Language { get; }
    private sealed class LocalizedLabel(string key, string[] argumentKeys)
    {
        private string? lastLocale;
        private string text = "";

        public string Resolve(GameLocalization language, string locale)
        {
            if (locale == lastLocale) return text;
            var template = language.Resolve(locale, key);
            text = argumentKeys.Length == 0 ? template : string.Format(template,
                Array.ConvertAll<string, object>(argumentKeys, argument => language.Resolve(locale, argument)));
            lastLocale = locale;
            return text;
        }
    }

    private readonly Dictionary<Guid, LocalizedLabel> keyOverrides = [];
    public string Translate(string key, string? fallback = null) => Language.Resolve(Preferences.Locale, key, fallback);
    public bool SetTextKey(Guid id, string key) => SetTextKey(id, key, []);
    public bool SetTextKey(Guid id, string key, params string[] argumentKeys)
    {
        if (byId.GetValueOrDefault(id)?.Label == null || string.IsNullOrWhiteSpace(key)) return false;
        if (argumentKeys == null || Array.Exists(argumentKeys, string.IsNullOrWhiteSpace)) return false;
        textOverrides.Remove(id);
        keyOverrides[id] = new(key, [.. argumentKeys]);
        return true;
    }
    public UiPreferences Preferences { get; }
    private bool Visible(Node n)
    {
        for(Node? p=n;p!=null;p=p.Parent)
            if(ModeClosed(p)||hidden.Contains(p.Object.Id)||active?.Invoke(p.Source)==false||p.Flag.Length>0&&flag?.Invoke(p.Flag)==false)return false;
        return true;
    }
    private bool Enabled(Node n)=>ReceivesInput(n.Object.Id)&&(n.Control!=null||n.Field!=null)&&interactOverrides.GetValueOrDefault(n.Object.Id,n.Control?.Interactable??true);
    private bool Shown(Node n)=>n.Box.Width>0 && n.Box.Height>0 && Visible(n) && n.Box.IntersectsWith(n.Clip);
    private static bool Horizontal(Node n)=>n.Layout?.Axis=="Horizontal";
    private static float Extent(Node n)=>Horizontal(n)?n.Box.Width:n.Box.Height;
    private static float Limit(Node n)=>Math.Max(0,n.Content-Extent(n));
    private float Height(Node n)=>Math.Max(n.Object.Height,n.Label==null&&n.Field==null&&n.Control is not UiDropdownComponent?0:Math.Max(0,n.Label?.InsetY??3)+(n.Label?.Size??16)*Preferences.TextScale*1.4f*Math.Max(1,n.Lines.Length));
    private static float PinnedSize(float parent, float measured, float? start, float? end) =>
        start is float near && end is float far ? Math.Max(0, parent - near - far) : measured;

    private static float PinnedStart(float parent, float size, float? start, float? end, float anchor, float offset) =>
        start ?? (end is float far ? parent - far - size : (parent - size) * anchor + offset);

    private void Arrange(ICanvas2D? metrics=null)
    {
        using var sample = FrameProfiler.Sample(FrameProfiler.UiLayout);
        ValidateInputModes();
        // Width is independent of wrapping. Resolve it first so a narrow translated label can
        // grow vertically and push later siblings before deriving positions and scroll ranges.
        for(int i=treeOrder.Length-1;i>=0;i--)
        {
            var n=treeOrder[i];n.MeasuredWidth=n.Object.Width*(n.ScaleHorizontal?Preferences.TextScale:1);
            if(n.Layout is not {} l || n.Scroll)continue;
            float w=0;int count=0;
            foreach(var child in n.Children)if(Visible(child)){
                if(Horizontal(n))w+=(count++>0?l.Gap:0)+child.MeasuredWidth;
                else w=Math.Max(w,child.MeasuredWidth);
            }
            n.MeasuredWidth=Math.Max(n.MeasuredWidth,w+2*l.Padding);
        }
        foreach(var n in treeOrder)
        {
            var p=n.Parent;
            if (n.Canvas) n.AllocatedWidth = screen.Width;
            else if (p != null && (p.Layout != null || p.Scroll))
                n.AllocatedWidth = !Horizontal(p) && (p.Layout?.Stretch ?? true)
                    ? Math.Max(0, p.AllocatedWidth - 2 * (p.Layout?.Padding ?? 0)) : n.MeasuredWidth;
            else n.AllocatedWidth = PinnedSize(p?.AllocatedWidth ?? screen.Width, n.MeasuredWidth, n.Rect?.Left, n.Rect?.Right);
            MeasureLines(n,metrics);
        }
        for(int i=treeOrder.Length-1;i>=0;i--)
        {
            var n=treeOrder[i]; n.Measured=Height(n);n.Cursor=n.Layout?.Padding??0;n.Placed=0;
            if(n.Layout==null&&!n.Scroll)continue;
            bool h=Horizontal(n);float pad=n.Layout?.Padding??0,gap=n.Layout?.Gap??0,length=2*pad,cross=0;int count=0;
            foreach(var child in n.Children)
            {
                if(!Visible(child))continue;
                length+=(count++==0?0:gap)+(h?child.MeasuredWidth:child.Measured);
                cross=Math.Max(cross,h?child.Measured:child.MeasuredWidth);
            }
            n.Content=length;
            if(!n.Scroll)
            {
                n.Measured=Math.Max(n.Measured,h?cross+2*pad:length);
            }
        }
        SizeLists();
        foreach(var n in treeOrder)
        {
            var p=n.Parent; var b=p?.Box??screen;
            n.Clip=p==null?screen:p.Scroll?RectangleF.Intersect(p.Box,p.Clip):p.Clip;
            if(n.Canvas)n.Box=screen;
            else if(p!=null && (p.Layout!=null||p.Scroll))
            {
                float pad=p.Layout?.Padding??0;bool stretch=p.Layout?.Stretch??true,h=Horizontal(p);
                if(Visible(n)&&p.Placed++>0)p.Cursor+=p.Layout?.Gap??0;
                float t=p.Cursor-p.Offset;
                n.Box=h?new(b.X+t,b.Y+pad,n.MeasuredWidth,stretch?Math.Max(0,b.Height-2*pad):n.Measured)
                    :new(b.X+pad,b.Y+t,stretch?Math.Max(0,b.Width-2*pad):n.MeasuredWidth,n.Measured);
                if(Visible(n))p.Cursor+=h?n.Box.Width:n.Box.Height;
            }
            else
            {
                int a=AnchorIndex(n.Rect?.Anchor??"TopLeft");
                var delta=n.Object.WorldPosition-(p?.Object.WorldPosition??Vector2.Zero);
                if (n.ScaleHorizontal) delta.X *= Preferences.TextScale;
                float w = n.AllocatedWidth, h = PinnedSize(b.Height, n.Measured, n.Rect?.Top, n.Rect?.Bottom);
                float x = PinnedStart(b.Width, w, n.Rect?.Left, n.Rect?.Right, (a % 3) * .5f, delta.X);
                float y = PinnedStart(b.Height, h, n.Rect?.Top, n.Rect?.Bottom, (a / 3) * .5f, delta.Y);
                n.Box = new(b.X + x, b.Y + y, w, h);
            }
            n.Offset=n.Scroll?Math.Clamp(n.Offset,0,Limit(n)):0;
            PlaceListRow(n);
        }
        if(GrowLists()){Arrange(metrics);return;}
        hovered = null;
        if (pointer is {} point)
            for (int i = nodes.Length - 1; i >= 0; i--)
                if (Enabled(nodes[i]) && Hit(nodes[i], point.X, point.Y)) { hovered = nodes[i].Object.Id; break; }
        if (pressed is Guid captured && (!byId.TryGetValue(captured, out var down) || !Enabled(down) || !Visible(down))) pressed = null;
        if(focus is Guid id&&(!byId.TryGetValue(id,out var f)||!Enabled(f)||!Visible(f)))SetFocus(null);
    }
    public RectangleF? Bounds(Guid id){Arrange();return byId.GetValueOrDefault(id)?.Box;}
    public float ScrollOffset(Guid id){Arrange();return byId.GetValueOrDefault(id)?.Offset??0;}
    public SceneObject? Pick(float x,float y){Arrange();return nodes.LastOrDefault(n=>IsWidget(n.Object)&&Hit(n,x,y))?.Object;}
    private bool Hit(Node n,float x,float y)=>n.Box.Contains(x,y)&&n.Clip.Contains(x,y)&&Visible(n);
    private void Reveal(Node n)
    {
        for(var p=n.Parent;p!=null;p=p.Parent)
        {
            if(!p.Scroll)continue;
            bool h=Horizontal(p);
            float lo=h?n.Box.Left-p.Box.Left:n.Box.Top-p.Box.Top,hi=h?n.Box.Right-p.Box.Right:n.Box.Bottom-p.Box.Bottom;
            float next=Math.Clamp(p.Offset+(lo<0?lo:Math.Max(0,hi)),0,Limit(p));
            if(next!=p.Offset){p.Offset=next;Arrange();}
        }
    }
    public bool Wheel(float x,float y,float delta)
    {
        Arrange();
        if (dropdownId is Guid open && byId[open].Control is UiDropdownComponent dropdown)
        {
            optionCursor = Math.Clamp(optionCursor - Math.Sign(delta), 0, dropdown.Options.Length - 1);
            return true;
        }
        var n=nodes.LastOrDefault(o=>ReceivesInput(o.Object.Id)&&Hit(o,x,y));
        for(;n!=null;n=n.Parent)
        {
            if(!ReceivesInput(n.Object.Id))break;
            if(!n.Scroll)continue;
            float next=Math.Clamp(n.Offset-delta*32,0,Limit(n));
            if(n.Offset!=next){n.Offset=next;return true;}
        }
        return false;
    }
    public bool SetVisible(Guid id,bool visible)
    {
        if(!byId.ContainsKey(id))return false;
        if(visible)hidden.Remove(id);else hidden.Add(id);
        Arrange();return true;
    }
    public bool SetInteractable(Guid id,bool enabled)
    {
        if(byId.GetValueOrDefault(id) is not {} n || n.Control==null&&n.Field==null)return false;
        interactOverrides[id]=enabled;Arrange();return true;
    }
    public bool SetText(Guid id,string text)
    {
        if(byId.GetValueOrDefault(id) is not {} n || n.Label==null&&n.Field==null)return false;
        if(n.Field!=null){values[id]=Trim(text,n.Field.MaxLength);if(edit==id){caret=values[id].Length;Preedit="";}}
        else { keyOverrides.Remove(id); textOverrides[id]=text; }
        return true;
    }
    private string Label(Node n)
    {
        if(textOverrides.TryGetValue(n.Object.Id,out var s))return s;
        if (keyOverrides.TryGetValue(n.Object.Id, out var binding)) return binding.Resolve(Language, Preferences.Locale);
        if (n.Label?.TextKey is { Length: > 0 } key) return Translate(key, n.Label.Text);
        var locale=Preferences.Locale;
        if(n.Label?.Translations is {} table)
        {
            while(true)
            {
                foreach(var pair in table)if(string.Equals(pair.Key,locale,StringComparison.OrdinalIgnoreCase))return pair.Value;
                int i=locale.LastIndexOf('-');if(i<0)break;locale=locale[..i];
            }
        }
        return n.Label?.Text??"";
    }
}
