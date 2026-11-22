using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Engine.Core;

public static class StartupHook
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_KEYS_EDITOR")!;

    public static void Initialize() => Task.Run(async () =>
    {
        while (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime {MainWindow:not null})
            await Task.Delay(80);
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var life = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
            try { await Run(life.MainWindow!); }
            catch (Exception ex) { File.WriteAllText(Path.Combine(Output,"error.txt"),ex.ToString()); }
            finally { life.Shutdown(); }
        });
    });

    private static object? Call(object target,string name,params object[] args)
        => target.GetType().GetMethod(name,Private)!.Invoke(target,args);
    private static T Field<T>(object target,string name)
        => (T)target.GetType().GetField(name,Private)!.GetValue(target)!;
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent,b));
    private static void Check(bool test,string msg) { if (!test) throw new InvalidDataException(msg); }

    private static async Task Run(Window main)
    {
        var view = main.FindControl<Control>("Viewport")!;
        var sceneProp = view.GetType().GetProperty("Scene")!;
        while (((SceneDocument)sceneProp.GetValue(view)!).Objects.All(o=>o.Name!="Chest Lid")) await Task.Delay(100);
        var project = Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!;
        async Task<Window> Open(string clip)
        {
            await (Task)Call(main,"EditClip","Assets/"+clip+".clip")!;
            return Field<Window>(main,"clipEditor");
        }
        var edited = await Open("LidMotion");
        edited.Width = 420;
        await Task.Delay(180);
        var prop = edited.FindControl<ComboBox>("KeyProperty")!;
        var time = edited.FindControl<TextBox>("KeyTime")!;
        var value = edited.FindControl<TextBox>("KeyValue")!;
        Check(Math.Abs(time.TranslatePoint(default,edited)!.Value.X-value.TranslatePoint(default,edited)!.Value.X) < 1, "Key fields misaligned");
        foreach (double width in new[] { 710,420 })
        {
            edited.Width=width;
            await Task.Delay(150);
            var write=edited.FindControl<Button>("WriteKey")!;
            write.BringIntoView();
            await Task.Delay(120);
            var scroller=edited.FindControl<ScrollViewer>("StageScroll")!;
            double y=write.TranslatePoint(default,scroller)!.Value.Y;
            Check(y>=0 && y+write.Bounds.Height<=scroller.Bounds.Height+1,"Set Key must be reachable in the scrollable stage");
            var seconds=edited.FindControl<TextBox>("SecondsBox")!;
            Check(Math.Abs(seconds.TranslatePoint(default,edited)!.Value.X-time.TranslatePoint(default,edited)!.Value.X)<1,"Frame and key columns");
        }
        prop.SelectedItem = "Rotation";
        time.Text = "0.3333333333333333"; value.Text = "-30";
        Click(edited.FindControl<Button>("WriteKey")!);
        Check(time.Text == "0.3333333333333333", "Selected key lost timestamp precision");
        Click(edited.FindControl<Button>("DeleteKey")!);
        Check(Math.Abs(SpritePose(edited, 1.0 / 3).Rotation!.Value + 16) < .0001, "Remove precise key");
        time.Text = "0.25"; value.Text = "-18";
        Click(edited.FindControl<Button>("WriteKey")!);
        Call(edited,"History",false);
        Check(SpritePose(edited,.25).Rotation == -12, "Undo");
        Call(edited,"History",true);
        Check(SpritePose(edited,.25).Rotation == -18, "Redo");
        Click(edited.FindControl<Button>("SaveButton")!);
        edited.Close();
        edited = await Open("LidMotion");
        Check(SpritePose(edited,.25).Rotation == -18, "Reopened authored key");
        Click(edited.FindControl<Button>("DeleteKey")!);
        Click(edited.FindControl<Button>("SaveButton")!);
        Check(SpritePose(edited,.25).Rotation == -12, "Deleted key did not restore interpolation");
        var readings = new List<object>();
        foreach (string label in new[] { "lid","coin" })
        {
            if (label == "coin") { edited.Close(); edited = await Open("CoinPulse"); }
            for (int tick=0;tick<=60;tick+=15)
            {
                var pose=SpritePose(edited,tick/60.0);
                var c=pose.Paint(System.Drawing.Color.White);
                readings.Add(new { label,tick,rotation=pose.Rotation ?? 0,scaleX=pose.ScaleX ?? 1,scaleY=pose.ScaleY ?? 1,argb=c.ToArgb().ToString("X8") });
            }
            SpritePose(edited,.25);
            await Task.Delay(150);
            var panel=edited.FindControl<Control>("Properties")!;
            using var shot=new RenderTargetBitmap(new((int)Math.Ceiling(panel.Bounds.Width),(int)Math.Ceiling(panel.Bounds.Height)));
            shot.Render(panel); shot.Save(Path.Combine(Output,label+"-keys.png"));
        }
        edited.Close();
        Click(main.FindControl<Button>("PlayButton")!);
        var session = Field<GameSession>(view,"game");
        InputReplay.Load(Path.Combine(Output,"Compare.inputreplay")).Play(session,session.SceneId!.Value);
        Click(main.FindControl<Button>("PlayButton")!);
        var lid=SceneDocument.Load(Path.Combine(project,"Scenes","KeyRoom.scene")).Objects.Single(o=>o.Name=="Chest Lid");
        Check(lid.Rotation==0 && lid.ScaleY==1,"Play changed the source");
        File.WriteAllText(Path.Combine(Output,"preview.json"),JsonSerializer.Serialize(readings,new JsonSerializerOptions { WriteIndented=true }));
    }

    private static ComponentPose SpritePose(Window editor,double time)
    {
        Call(editor,"Seek",time);
        return Field<ComponentPose>(Field<Control>(editor,"framePreview"),"previewPose");
    }
}
