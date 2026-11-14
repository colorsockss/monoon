using System.Drawing;
using Engine.Core;

internal static class AccessibilityChecks
{
    public static void Run()
    {
        var doc=new SceneDocument();
        var canvas=doc.CreateUi("Canvas",null);
        var list=doc.CreateUi("Scroll View",canvas);
        list.X=10;list.Y=20;list.Width=190;list.Height=80;
        var layout=list.GetComponent<UiLayoutComponent>()!;
        layout.Padding=5;layout.Gap=3;
        var field=doc.CreateUi("Text Field",list);
        var label=field.GetComponent<UiTextComponent>()!;
        label.Translations=new(){["ko"]="이름",["ja"]="名前"};
        field.Height=30;field.GetComponent<UiTextFieldComponent>()!.MaxLength=3;
        var middle=doc.CreateUi("Button",list);
        var last=doc.CreateUi("Button",list);
        var ui=new ScreenUi(doc,null);
        ui.Preferences.Locale="ko-KR";
        var paint=new TextProbe();ui.Draw(paint);
        Check(paint.Labels.Contains("이름") && !paint.Labels.Contains("Enter text"),"regional locale falls back to Korean");
        Check(ui.Bounds(field.Id)==new RectangleF(15,25,180,30) && ui.Bounds(middle.Id)!.Value.Y==58,"stack uses shared padding, gap and stretched width");
        Check(ui.Pick(25,110)!=last && !ui.Wheel(9,65,-1),"clipped content cannot be picked or scrolled from outside");
        Check(ui.Wheel(25,65,-2) && ui.ScrollOffset(list.Id)>0 && ui.Pick(25,92)==last,"wheel reveals the bottom row and its hit area");
        ui.SetVisible(middle.Id,false);
        Check(ui.ScrollOffset(list.Id)==0 && ui.Bounds(last.Id)!.Value.Y==58,"hiding a row reflows content and clamps scroll");
        ui.SetVisible(middle.Id,true);
        void Key(string name,bool reverse=false){ui.Key(name,true,reverse);ui.Key(name,false);}
        Key("Tab",true);Check(ui.Focused==last.Id && ui.ScrollOffset(list.Id)>0,"reverse Tab reveals a clipped button");
        Key("Tab");Key("Return");
        Check(ui.Editing==field.Id && ui.ScrollOffset(list.Id)==5,"wraparound reveals the first input");
        ui.Compose("ㅎ");ui.Compose("하");ui.Compose("한");
        Check(ui.Value(field.Id)=="" && ui.Preedit=="한","marked text replaces the previous composition without committing");
        ui.CommitText("한");ui.CommitText("👩‍🚀");ui.CommitText("e\u0301X");
        Check(ui.Value(field.Id)=="한👩‍🚀e\u0301" && ui.Preedit=="","limit counts graphemes instead of UTF-16 units");
        Key("Left");Key("Backspace");
        Check(ui.Value(field.Id)=="한e\u0301","backspace removes one complete emoji cluster");
        Key("Return");ui.Preferences.TextScale=2;ui.Preferences.HighContrast=true;
        Check(ui.Bounds(field.Id)!.Value.Height>40 && field.Height==30,"larger type reflows rows and leaves authored sizes intact");
        ui.PadKey("Pad.DpadDown");ui.PadKey("Pad.LeftStickDown");
        Check(ui.Focused==last.Id && ui.ScrollOffset(list.Id)>30 && ui.PadKey("Pad.South"),"directional pad and stick navigate and reveal offscreen targets");
        ui.PadKey("Pad.East");Check(ui.Focused==null,"pad cancel leaves UI focus");
        ui.Preferences.Locale="fr-CA";ui.SetText(field.Id,"");ui.Wheel(25,65,100);
        paint.Labels.Clear();ui.Draw(paint);
        Check(paint.Labels.Contains("Enter text"),"missing translation uses the authored base text");
        var prose=middle.GetComponent<UiTextComponent>()!;prose.Wrap=true;prose.Text="설정을 저장하면 다음 줄로 이어집니다";
        list.Width=90;ui.Draw(paint);
        Check(ui.Bounds(middle.Id)!.Value.Height>70,"narrow translated text wraps and contributes to stack height");
        list.Width=190;
        layout.Axis="Horizontal";layout.Stretch=false;
        ui=new ScreenUi(doc,null);
        ui.Key("Tab",true,true);
        Check(ui.ScrollOffset(list.Id)>100 && ui.Bounds(last.Id)!.Value.Right==ui.Bounds(list.Id)!.Value.Right,
            "horizontal scroll reveals the focused column");
    }
    private static void Check(bool pass,string text){if(!pass)throw new Exception(text);Console.WriteLine("PASS "+text);}
    private sealed class TextProbe:ICanvas2D,IDisposable
    {
        internal readonly List<string> Labels=[];
        public void Clear(Color color){} public void FillRectangle(RectangleF r,Color color){}
        public bool DrawSprite(string path,RectangleF r)=>true;
        public void DrawText(string text,float x,float y,float size,Color color)=>Labels.Add(text);
        public IDisposable PushClip(RectangleF area)=>this;
        public void Dispose(){}
    }
}
