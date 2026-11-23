using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text;
using Avalonia.Controls;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private long nextDiagnostics;
    private void InitializeDiagnostics()
    {
        LiveSection.Foldout=new ComponentFoldout();
        FrameGraph.History=Viewport.Profile;
        FrameGraph.FrameSelected+=sequence=>{FrameGraph.Selected=sequence;RefreshDiagnostics(true);};
        DebugButton.IsCheckedChanged+=(_,_)=>{RefreshDiagnostics(true);Viewport.Focus();};
        ColliderDebug.Click+=(_,_)=>{Viewport.PhysicsDebug.Colliders=ColliderDebug.IsChecked==true;RefreshDiagnostics(true);Viewport.Focus();};
        QueryDebug.Click+=(_,_)=>{Viewport.PhysicsDebug.Queries=QueryDebug.IsChecked==true;Viewport.ProbeArmed=false;RefreshDiagnostics(true);Viewport.Focus();};
        FrameDebug.Click+=(_,_)=>SetProfiler(FrameDebug.IsChecked==true);
        CloseProfiler.Click+=(_,_)=>SetProfiler(false);
        ConsoleDebug.Click+=(_,_)=>ShowConsole();
        ProbeRay.Click+=(_,_)=>{
            QueryDebug.IsChecked=true;Viewport.PhysicsDebug.Queries=true;Viewport.ProbeArmed=Selected!=null;
            if(Selected==null)Status.Text="Select an active object in the Hierarchy to cast a ray.";
            RefreshDiagnostics(true);Viewport.Focus();
        };
        Viewport.ProbeFinished+=message=>{WriteLog(new(GameLogLevel.Info,message,null,0));Status.Text=message;RefreshDiagnostics(true);};
        Viewport.Logged+=WriteLog;
        Viewport.RuntimeTick+=()=>RefreshDiagnostics();
        // The main window or a floating panel window in front keeps the game running, as the Console window did.
        Viewport.KeepSimulationActive=()=>IsActive||Dock.FloatingWindows.Any(w=>w.IsActive);
    }
    // The profiler is a dock panel; the Debug bar checkbox, Run → Frame Profiler and Window → Profiler open it.
    private void SetProfiler(bool shown)
    {
        FrameDebug.IsChecked=shown;
        if(shown)OpenPanel("Profiler");else Dock.Close("Profiler");
        RefreshDiagnostics(true);
        if(Viewport.IsPlaying)Viewport.Focus();
    }
    private void RefreshDiagnostics(bool force=false)
    {
        if (Viewport.IsPlaying && DebugButton.IsChecked == true) RefreshRoleQuery();
        else queryWorld = null;
        var now=Environment.TickCount64;
        if(!force && now<nextDiagnostics)return;
        nextDiagnostics=now+200;
        var debug=Viewport.PhysicsDebug;
        bool physics=Viewport.IsPlaying && (debug.Colliders||debug.Queries);
        bool profile=FrameDebug.IsChecked==true;
        bool tools=Viewport.IsPlaying && DebugButton.IsChecked==true;
        RoleQueryRow.IsVisible = tools;
        DiagnosticsTools.IsVisible=tools;
        DiagnosticsDock.IsVisible=physics||tools;
        PhysicsCaption.IsVisible=physics;
        if(physics){
            string query=debug.Latest is {} q ? $"{q.Kind} · "+(q.Count==0?"miss":$"{q.Count} hit · {Viewport.PlayWorld!.ObjectLabel(q.Target!.Value.ObjectId)}") : "Waiting for a query";
            PhysicsCaption.Text=Viewport.ProbeArmed?"Click inside a camera view to cast from the selected object.":
                string.Join("   |   ",new[]{debug.Colliders?"Solid: green · Trigger: pink":null,debug.Queries?query:null}.OfType<string>());
            ToolTip.SetTip(PhysicsCaption,PhysicsCaption.Text);
        }
        if(!profile)return;
        var history=Viewport.Profile;
        if(history.Count==0){FrameSummary.Text=Viewport.IsPlaying?"Sampling frames…":"Play to capture frames."; FrameDetail.Text="";ShowWork(null,false);FrameGraph.InvalidateVisual();return;}
        var f=history.At(history.Count-1);
        double total=0,peak=0,allocation=0;int g0=0,g1=0,g2=0;
        for(int i=0;i<history.Count;i++){var h=history.At(i);total+=h.Interval;peak=Math.Max(peak,h.Interval);allocation+=h.Allocation;g0+=h.Gen0;g1+=h.Gen1;g2+=h.Gen2;}
        var name=Viewport.IsPlaying?"Frame":"Last capture";
        FrameSummary.Text=$"{name} {history.Sequence:N0}   ·   {f.Interval:F1} ms   ·   avg {total/history.Count:F1}   ·   max {peak:F1} ms";
        ToolTip.SetTip(FrameSummary,FrameSummary.Text);
        FrameDetail.Text=$"Update {f.Update:F2} ms   ·   Render {f.Draw:F2} ms   ·   {(Viewport.NativeDraws?"SDL draws":"Avalonia draws")} {f.Calls}\n"+
            $"Alloc {allocation/history.Count/1024:F1} KiB/frame   ·   GC 0/1/2: {g0}/{g1}/{g2}   ·   {history.Count} frames";
        // Without a selection that is still recorded, show the slowest recorded frame.
        var work=history.Work;
        var chosen=FrameGraph.Selected is long pick?work.Find(pick):null;
        bool picked=chosen!=null;
        for(int i=0;chosen==null && i<work.Count;i++)chosen=work.At(i);
        if(!picked)for(int i=0;i<work.Count;i++)if(work.At(i).Milliseconds>chosen!.Milliseconds)chosen=work.At(i);
        ShowWork(chosen,picked);
        FrameGraph.InvalidateVisual();
    }

    private long shownWork=-1;
    private void ShowWork(ProfiledFrame? frame,bool picked)
    {
        if(frame==null){WorkTitle.Text=Viewport.IsPlaying?"":"Play to profile frame work.";WorkRows.Children.Clear();shownWork=-1;return;}
        WorkTitle.Text=$"{(picked?"Selected":"Slowest")} frame {frame.Sequence:N0}   ·   {frame.Milliseconds:F2} ms   ·   {frame.AllocatedBytes/1024d:F1} KiB"+
            (frame.Dropped>0?$"   ·   {frame.Dropped} samples dropped":"")+(picked?"":"   ·   click the graph to select a frame");
        ToolTip.SetTip(WorkTitle,WorkTitle.Text);
        if(frame.Sequence==shownWork)return;
        shownWork=frame.Sequence;
        WorkRows.Children.Clear();
        void Add(int node,int depth)
        {
            var row=new Grid{ColumnDefinitions=new("*,64,72,72")};
            var name=new TextBlock{Text=frame.Name(node),Margin=new(depth*12,0,0,0),TextTrimming=Avalonia.Media.TextTrimming.CharacterEllipsis};
            ToolTip.SetTip(name,name.Text);
            row.Children.Add(name);
            string[] numbers=[frame.Calls(node).ToString("N0",CultureInfo.InvariantCulture),
                frame.InclusiveMilliseconds(node).ToString("F3",CultureInfo.InvariantCulture),frame.SelfMilliseconds(node).ToString("F3",CultureInfo.InvariantCulture)];
            for(int i=0;i<3;i++){var cell=new TextBlock{Text=numbers[i],HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Right};Grid.SetColumn(cell,i+1);row.Children.Add(cell);}
            WorkRows.Children.Add(row);
            var children=new List<int>();
            for(int c=frame.FirstChildOf(node);c>=0;c=frame.NextSibling(c))children.Add(c);
            foreach(var c in children.OrderByDescending(frame.InclusiveMilliseconds))Add(c,depth+1);
        }
        Add(0,0);
    }

    private void RefreshLiveInspector()
    {
        var world=Viewport.PlayWorld;
        if(world==null || Selected is not {} owner || world.Inspect(owner.Id) is not {} state){LiveSection.IsVisible=false;return;}
        LiveSection.IsVisible=true;
        string active=state.Active?"Active":state.ActiveSelf?"Inactive (parent)":"Inactive (self)";
        var lines=new StringBuilder().Append(active).Append(" · ").Append(state.SpawnedFrom==null?"Scene object":Path.GetFileName(state.SpawnedFrom))
            .Append($"\nWorld  {state.Position.X:0.##}, {state.Position.Y:0.##}");
        if (world.GetComponent<SpriteRendererComponent>(owner.Id) is {} renderer)
            lines.Append($"\nSprite  {renderer.Asset ?? renderer.AssetId?.ToString() ?? "Square"}");
        if (owner.GetComponent<Collider2DComponent>() is {} c)
            lines.Append(world.IsColliderEnabled(owner.Id) ? $"\n{(c.IsTrigger?"Trigger":"Solid")} · Layer {c.Layer} · Contacts {state.Contacts}" : "\nCollider disabled");
        if(state.Animation!=null)lines.Append($"\n{state.Animation} · "+(state.Frame<0?"Stopped":$"Frame {state.Frame+1}"));
        if(owner.GetComponent<AudioSourceComponent>()!=null)lines.Append(state.AudioPlaying?"\nAudio playing":"\nAudio stopped");
        if (world.TryGetMotion(owner.Id, out var motion))
        {
            lines.Append($"\nVelocity  {motion.Velocity.X:0.##}, {motion.Velocity.Y:0.##}");
            if (owner.GetComponent<CharacterMotor2DComponent>() != null)
                lines.Append(motion.Support is Guid id ? "\nGround  " + world.ObjectLabel(id) : "\nAirborne");
        }
        if (world.TryGetTileStream(owner.Id,out var tiles))
            lines.Append($"\nChunk  {tiles.Center.X}, {tiles.Center.Y} · {tiles.Resident}/{tiles.Total} resident\nLoaded  {tiles.Loads} · Unloaded  {tiles.Unloads}");
        LiveSummary.Text=lines.ToString();
        ToolTip.SetTip(LiveSection,$"{owner.Id}\n{state.SpawnedFrom??world.SceneLabelFor(state.SceneId)}");
        var values=new List<string>();
        foreach(var script in world.LiveScripts(owner.Id))
        {
            // Read scalar fields only. Never run project getters, enumerate user objects or call their ToString.
            var type=script.GetType();var fields=new List<string>();
            for(var level=type;level!=null && level!=typeof(GameBehaviour);level=level.BaseType)
            foreach(var field in level.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly))
            {
                if(fields.Count>=48)break;
                if(field.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute),false))continue;
                var t=field.FieldType;
                if(!(t.IsPrimitive||t.IsEnum||t==typeof(string)||t==typeof(decimal)||t==typeof(Vector2)))continue;
                if(t==typeof(IntPtr)||t==typeof(UIntPtr))continue;
                var value=field.GetValue(script);
                var shown=value is string s?s.ReplaceLineEndings(" "):Convert.ToString(value,CultureInfo.InvariantCulture)??"null";
                fields.Add($"{field.Name}: {(shown.Length<=100?shown:shown[..99]+"…")}");
            }
            if(fields.Count>0)values.Add(type.Name+"\n"+string.Join("\n",fields));
        }
        ScriptValues.IsVisible=values.Count>0; ScriptValues.Text=string.Join("\n",values);
    }
}
