using System.Drawing;
using Engine.Core;

internal static class UiThemeChecks
{
    internal static void Run(string project)
    {
        var guide = SceneDocument.Load(Path.Combine(project, "Scenes", "RoomGuide.scene"));
        var assets = ProjectAssets.ReadManifest(project);
        var ui = new ScreenUi(guide, assets);
        var probe = new PaintProbe();
        var close = guide.Objects.Single(o => o.Name == "Close guide").Id;
        var increase = guide.Objects.Single(o => o.Name == "Larger text").Id;
        var panel = guide.Objects.Single(o => o.Name == "Guide panel").Id;
        RectangleF Box(Guid id) => ui.Bounds(id)!.Value;
        void Draw() { probe.Fills.Clear(); probe.Slices.Clear(); probe.Text.Clear(); ui.Draw(probe); }
        void Paint(Guid id, string color)
        {
            Draw();
            SceneObject.TryColor(color, out var expected);
            Check(probe.Fills.Any(f => f.Box == Box(id) && f.Color.ToArgb() == expected.ToArgb()), $"button {color}");
        }
        Paint(close, "#33566A"); Paint(increase,"#33566A");
        var original = Box(panel);
        var b = Box(close);
        ui.Drag(b.X+2,b.Y+2); Paint(close,"#437879");
        ui.Pointer(true,b.X+2,b.Y+2); Paint(close,"#1E414B");
        ui.Drag(-1,-1); ui.Pointer(false,-1,-1); Paint(close,"#365F86");
        int clicks=0; ui.Dispatch(_=>clicks++); Check(clicks==0,"drag-out cancels the click");
        ui.Key("Return",true); Paint(close,"#1E414B"); ui.Key("Return",false); Paint(close,"#365F86");
        ui.Dispatch(_=>clicks++); Check(clicks==1,"keyboard activation fires once");
        ui.SetInteractable(close,false); Paint(close,"#343F49");
        ui.Pointer(true,b.X+2,b.Y+2);ui.Pointer(false,b.X+2,b.Y+2);ui.Dispatch(_=>clicks++);
        Check(clicks==1,"disabled button ignores input");
        ui.SetInteractable(close,true); ui.Preferences.Locale="ko";ui.Preferences.TextScale=2;Draw();
        b=Box(close);
        var large=Box(panel);
        Check(large.Height>original.Height && large.Contains(b) && probe.Text.Any(t=>t=="닫기"),"Korean 200% reflows inside panel");
        Check(probe.Slices.Count==9 && probe.Slices[0].Dest.Size==new SizeF(8,8) &&
            probe.Slices[^1].Dest.Right==large.Right && probe.Slices[^1].Dest.Bottom==large.Bottom,
            "four corners retain source size on enlarged panel");
        ui.Pointer(true,b.Right+.5f,b.Bottom-2);ui.Pointer(false,b.Right+.5f,b.Bottom-2);ui.Dispatch(_=>clicks++);
        ui.Pointer(true,b.Right-.5f,b.Bottom-2);ui.Pointer(false,b.Right-.5f,b.Bottom-2);ui.Dispatch(_=>clicks++);
        Check(clicks==2,"only the inside edge activates");
        var asymmetric = new UiNineSlice(new(3,2,6,4),new(16,16));
        probe.Slices.Clear();asymmetric.Draw(probe,"frame",new(2,3,40,32),2);
        Check(probe.Slices[0].Dest.Size==new SizeF(6,4) && probe.Slices[^1].Dest.Size==new SizeF(12,8),"asymmetric margins scale with the viewport");
        probe.Slices.Clear();asymmetric.Draw(probe,"frame",new(2,3,4.5f,3),1);
        Check(probe.Slices.Count==4 && probe.Slices.Sum(s=>s.Dest.Width*s.Dest.Height)==13.5f &&
            probe.Slices[0].Dest.Right==probe.Slices[1].Dest.Left,"undersized destinations do not overlap");
        try { _=new UiNineSlice(new(8,1,8,1),new(16,16)); throw new Exception("overlapping source borders accepted"); }
        catch(InvalidDataException) { }
    }

    private static void Check(bool ok,string title)
    {
        if(!ok)throw new Exception("UI theme: "+title);
        Console.WriteLine("PASS UI theme: "+title);
    }

    private sealed class PaintProbe : ICanvas2D
    {
        public List<(RectangleF Box,Color Color)> Fills {get;}=[];
        public List<(RectangleF Dest,SpriteRegion Source)> Slices {get;}=[];
        public List<string> Text {get;}=[];
        public void Clear(Color color) {}
        public void FillRectangle(RectangleF box,Color color)=>Fills.Add((box,color));
        public void DrawText(string text,float x,float y,float size,Color color)=>Text.Add(text);
        public bool DrawSprite(string path,RectangleF dest)=>true;
        public bool DrawSprite(string path,RectangleF dest,SpriteRegion? source)
        {
            if(source!=null)Slices.Add((dest,source));
            return true;
        }
        public IDisposable PushClip(RectangleF box)=>new Lease();
        private sealed class Lease:IDisposable {public void Dispose(){}}
    }
}
