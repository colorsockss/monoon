using System.Drawing;
using System.Globalization;
using System.Text;

namespace Engine.Core;

public sealed partial class ScreenUi
{
    private readonly Dictionary<Guid,string> values=[];
    private Guid? edit;
    private int caret;
    public Guid? Focused=>focus;
    public void ClearFocus() => SetFocus(null);
    public Guid? Editing=>edit;
    public string Preedit { get;private set; }="";
    public int Caret=>caret;
    public string? Value(Guid id)=>byId.GetValueOrDefault(id)?.Field is {} f?values.GetValueOrDefault(id)??Trim(f.Value,f.MaxLength):null;
    private static string Trim(string s,int max)
    {
        var t=string.Concat(s.EnumerateRunes().Where(r=>!Rune.IsControl(r)).Select(r=>r.ToString()));
        var groups=StringInfo.ParseCombiningCharacters(t);return groups.Length>max?t[..groups[max]]:t;
    }
    private void EndEditing(){edit=null;Preedit="";caret=0;}
    private void SetFocus(Guid? id){if(edit!=id)EndEditing();focus=id;}
    private void Begin(Node n){SetFocus(n.Object.Id);edit=focus;caret=Value(edit!.Value)!.Length;Preedit="";}
    public bool Compose(string text)
    {
        Arrange();if(edit==null)return false;
        Preedit=Trim(text,byId[edit.Value].Field!.MaxLength);return true;
    }
    public bool CommitText(string text)
    {
        Arrange();if(edit is not Guid id)return false;
        var old=Value(id)!;
        int remaining=byId[id].Field!.MaxLength-StringInfo.ParseCombiningCharacters(old).Length;
        var insert=Trim(text,Math.Max(0,remaining));
        values[id]=old.Insert(caret,insert);caret+=insert.Length;Preedit="";return true;
    }
    public void SetCaret(int value)
    {
        if(edit is not Guid id)return;
        var s=Value(id)!;
        caret=value>=s.Length?s.Length:StringInfo.ParseCombiningCharacters(s).LastOrDefault(i=>i<=value);
    }
    private bool EditKey(string key)
    {
        if(edit is not Guid id)return false;
        if(key=="Tab")return false;
        if(key is "Escape" or "Return")
        {
            if(Preedit.Length==0)EndEditing();
            else if(key=="Escape")Preedit="";
            return true;
        }
        if(Preedit.Length!=0)return true; // Native IME owns the marked text until commit or cancellation.
        var text=Value(id)!;
        var indices=StringInfo.ParseCombiningCharacters(text);
        int before=indices.LastOrDefault(i=>i<caret),after=indices.FirstOrDefault(i=>i>caret,text.Length);
        switch(key)
        {
            case "Left":caret=before;break;
            case "Right":caret=after;break;
            case "Home":caret=0;break;
            case "End":caret=text.Length;break;
            case "Backspace":values[id]=text.Remove(before,caret-before);caret=before;break;
            case "Delete":values[id]=text.Remove(caret,after-caret);break;
        }
        return true; // Typing must not also move a player or activate shortcuts.
    }
    private Guid? barDrag;
    private float barGrab;
    private static RectangleF Track(Node n)=>Horizontal(n)?new(n.Box.X,n.Box.Bottom-8,n.Box.Width,8):new(n.Box.Right-8,n.Box.Y,8,n.Box.Height);
    private static RectangleF Thumb(Node n)
    {
        var t=Track(n);float len=Extent(n),size=Math.Min(len,Math.Max(14,len*len/n.Content)),pos=Limit(n)>0?n.Offset/Limit(n)*(len-size):0;
        return Horizontal(n)?new(t.X+pos,t.Y,size,t.Height):new(t.X,t.Y+pos,t.Width,size);
    }
    public void Drag(float x,float y)
    {
        if(barDrag is not Guid id || !byId.TryGetValue(id,out var n))return;
        bool h=Horizontal(n);var t=Thumb(n);float len=Extent(n)-(h?t.Width:t.Height);
        if(len>0)n.Offset=Math.Clamp(((h?x-n.Box.X:y-n.Box.Y)-barGrab)/len,0,1)*Limit(n);
    }
    public bool Pointer(bool down,float x,float y)
    {
        Arrange();
        if(down && nodes.LastOrDefault(n=>ReceivesInput(n.Object.Id)&&n.Scroll&&Limit(n)>0&&Hit(n,x,y)&&Track(n).Contains(x,y)) is {} scroll)
        {
            var t=Thumb(scroll);bool h=Horizontal(scroll);barDrag=scroll.Object.Id;
            barGrab=t.Contains(x,y)?(h?x-t.X:y-t.Y):(h?t.Width:t.Height)/2;Drag(x,y);pressed=null;return true;
        }
        if(!down&&barDrag!=null){Drag(x,y);barDrag=null;return true;}
        var target=nodes.LastOrDefault(n=>Enabled(n)&&Hit(n,x,y));
        var id=target?.Object.Id;
        if(down){pressed=id;if(id.HasValue || ActiveInputMode==null)SetFocus(id);if(target?.Field!=null)Begin(target);return id.HasValue;}
        bool captured=pressed.HasValue;
        if(target?.Button!=null && target.Field==null && id==pressed)clicks.Enqueue(id!.Value);
        pressed=null;return captured;
    }
    private bool Navigate(string key,bool reverse=false)
    {
        var list=nodes.Where(n=>Enabled(n)&&Visible(n)).ToArray();
        if(list.Length==0)return false;
        int index=Array.FindIndex(list,n=>n.Object.Id==focus);Node? next=null;
        if(key=="Tab")next=list[index<0?(reverse?list.Length-1:0):(index+(reverse?-1:1)+list.Length)%list.Length];
        else if(index<0)next=list[0];
        else
        {
            var b=list[index].Box;float best=float.MaxValue;bool horizontal=key is "Right" or "Left";
            foreach(var candidate in list)
            {
                if(candidate==list[index])continue;
                var c=candidate.Box;float dx=c.X+c.Width/2-b.X-b.Width/2,dy=c.Y+c.Height/2-b.Y-b.Height/2;
                float along=horizontal?dx:dy;if(key is "Left" or "Up")along=-along;
                if(along<=.1f)continue;
                float away=Math.Abs(horizontal?dy:dx);float cost=along+away*4+away*away/along;
                if(cost<best){best=cost;next=candidate;}
            }
        }
        if(next!=null){SetFocus(next.Object.Id);Reveal(next);}
        return true;
    }
    private bool Activate()
    {
        if(focus is not Guid id || !byId.TryGetValue(id,out var n)||!Enabled(n))return false;
        if(n.Field!=null)Begin(n);else clicks.Enqueue(id);return true;
    }
    public bool Key(string key,bool down,bool reverse=false)
    {
        if(!down)return held.Remove(key);
        if(held.Contains(key))return true;
        Arrange();
        bool used;
        if(edit!=null && key!="Tab")
        {
            used=EditKey(key);
            if(key is "Backspace" or "Delete" or "Left" or "Right")return used;
        }
        else if(key=="Tab")used=Navigate(key,reverse);
        else if(key is "Up" or "Down" or "Left" or "Right")used=(focus!=null||uiOnly||ActiveInputMode.HasValue)&&Navigate(key);
        else used=(key is "Return" or "Space")&&Activate();
        if(used)held.Add(key);return used;
    }
    private bool uiOnly;
    internal bool OwnsNavigation=>focus!=null || uiOnly && nodes.Any(n=>Enabled(n)&&Visible(n));
    public bool PadKey(string key)
    {
        Arrange();
        if(key=="Pad.East"){if(ActiveInputMode.HasValue||focus==null)return false;SetFocus(null);return true;}
        if(key=="Pad.South"){if(edit!=null){EndEditing();return true;}return Activate();}
        string? dir=key switch
        {
            "Pad.DpadLeft" or "Pad.LeftStickLeft"=>"Left","Pad.DpadRight" or "Pad.LeftStickRight"=>"Right",
            "Pad.DpadUp" or "Pad.LeftStickUp"=>"Up","Pad.DpadDown" or "Pad.LeftStickDown"=>"Down",_=>null
        };
        return dir!=null&&(focus!=null||uiOnly||ActiveInputMode.HasValue)&&Navigate(dir);
    }
    internal void Dispatch(Action<Guid> click)
    {
        while(clicks.TryDequeue(out var id))if(byId.TryGetValue(id,out var n)&&Enabled(n)&&Visible(n))click(id);
        clicks.Clear();
    }
    public void ResetInput(){SetFocus(null);pressed=barDrag=null;held.Clear();clicks.Clear();}
    public void CancelPointer()=>pressed=barDrag=null;
}
