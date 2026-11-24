using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private void SourceFields(AudioSourceComponent source,Action<string,Control> row)
    {
        var bus=new ComboBox { ItemsSource=Enum.GetValues<AudioBus>(),SelectedItem=source.Bus,HorizontalAlignment=HorizontalAlignment.Stretch };
        bus.SelectionChanged+=(_,_)=>{if(bus.SelectedItem is AudioBus b && b!=source.Bus){source.Bus=b;MarkDirty();}};
        row("Bus",bus);
        void Number(string label,float current,decimal min,decimal max,decimal step,Action<float> write)
        {
            var n=new NumericUpDown { Minimum=min,Maximum=max,Increment=step,Value=decimal.CreateSaturating(current) };
            AutomationProperties.SetName(n,label);
            if(label.StartsWith("Fade"))ToolTip.SetTip(n,"Duration in seconds");
            if(label=="Range")ToolTip.SetTip(n,"Distance in world pixels at which this source becomes silent");
            n.ValueChanged+=(_,_)=>{if(n.Value is {} v && (float)v!=current){current=(float)v;write(current);MarkDirty(n);}};
            row(label,n);
        }
        void Toggle(string title,bool current,Action<bool> write)
        {
            var c=new CheckBox { Content=title,IsChecked=current };
            c.IsCheckedChanged+=(_,_)=>{write(c.IsChecked==true);MarkDirty();};row("",c);
        }
        Number("Volume",source.Volume,0,1,.05m,v=>source.Volume=v);
        Toggle("Play on start",source.PlayOnStart,v=>source.PlayOnStart=v);
        Toggle("Loop",source.Loop,v=>source.Loop=v);
        Number("Fade in",source.FadeIn,0,3600,.1m,v=>source.FadeIn=v);
        Number("Fade out",source.FadeOut,0,3600,.1m,v=>source.FadeOut=v);
        Toggle("Spatial 2D",source.Spatial,v=>{source.Spatial=v;RefreshComponentList();});
        if(source.Spatial)Number("Range",source.Range,.01m,1000000000m,10,v=>source.Range=v);
    }

    // Bus levels, snapshots (16.10) and ducking rules in one dialog. Outside Play, Save defaults writes all three to GameSettings
    // as one Undo step; in Play every change applies to the running mixer only, and Preview plays a snapshot transition.
    private async Task EditMixer()
    {
        if(project==null && !await Save())return;
        bool live=Viewport.PlayWorld!=null;
        var settings=Scene.Settings;
        var original=Viewport.PlayWorld?.Levels??settings.Mixer;
        var edited=original;
        var snaps=(settings.AudioSnapshots??[]).ToList();
        var ducks=(settings.AudioDucking??[]).ToList();
        int snapAt=snaps.Count>0?0:-1, duckAt=ducks.Count>0?0:-1;
        bool loading=false, applied=false, configured=false, previewed=false;
        var dialog=new Window { Title="Audio Mixer",Width=340,MinWidth=300,Height=540,MinHeight=360,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false };
        var layout=new Grid { Margin=new Thickness(14),RowDefinitions=new("*,Auto,Auto"),RowSpacing=8 };dialog.Content=layout;
        var hint=new TextBlock { TextWrapping=TextWrapping.Wrap,Foreground=EditorTokens.Brush("EditorMuted") };
        AutomationProperties.SetName(hint,"Mixer message");
        StackPanel Page() => new() { Spacing=5,Margin=new Thickness(0,8,0,0) };
        var levels=Page(); var snapshotPage=Page(); var duckingPage=Page();
        static ScrollViewer Scroll(StackPanel page)=>new() { Content=page,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        var tabs=new TabControl { Items={ new TabItem { Header="Levels",Content=Scroll(levels) },
            new TabItem { Header="Snapshots",Content=Scroll(snapshotPage) },new TabItem { Header="Ducking",Content=Scroll(duckingPage) } } };
        layout.Children.Add(tabs);
        Grid.SetRow(hint,1);layout.Children.Add(hint);
        static float? Full(float v)=>v==1?null:v; // Voice, UI and Ambience at 100% stay unwritten, as in a hand-written file.
        static T[]? Keep<T>(List<T> list,T[]? before)=>list.SequenceEqual(before??[])?before:list.Count!=0?[..list]:null;
        GameSettings Candidate()=>settings with { Mixer=edited,AudioSnapshots=Keep(snaps,settings.AudioSnapshots),AudioDucking=Keep(ducks,settings.AudioDucking) };
        // The runtime's GameSettings rules; the message is the one a hand-edited file would give.
        GameSettings? Check()
        {
            var candidate=Candidate();
            try { candidate.Validate(); hint.Text=""; return candidate; }
            catch(InvalidDataException ex) { hint.Text=ex.Message; return null; }
        }
        void Live()
        {
            if(Check() is {} candidate && Viewport.PlayWorld is {} world) { world.Mixer.Configure(candidate); configured=true; }
        }
        Grid Line(Panel page,string label,string rest="*,36")
        {
            var grid=new Grid { ColumnDefinitions=EditorTokens.LabelColumns(EditorTokens.FieldLabelWidth,rest),ColumnSpacing=EditorTokens.FieldGap };
            grid.Children.Add(new TextBlock { Text=label,Classes={"fieldLabel"} });page.Children.Add(grid);return grid;
        }
        static void Put(Grid grid,Control c,int column,int span=1) { Grid.SetColumn(c,column);Grid.SetColumnSpan(c,span);grid.Children.Add(c); }
        Slider Row(Panel page,string name,string automation,float value,Action<float> set)
        {
            var grid=Line(page,name);
            var f=new Slider { Minimum=0,Maximum=100,Value=value*100,SmallChange=1,LargeChange=10 };
            AutomationProperties.SetName(f,automation);
            var pct=new TextBlock { Text=$"{value:P0}",HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center };
            // Whole percents, as shown: a drag writes 0.53, not 0.5327 (no tick snapping, whose tick bars would make the row taller).
            f.ValueChanged+=(_,_)=>{float gain=(float)(Math.Round(f.Value)/100);pct.Text=$"{gain:P0}";if(!loading)set(gain);};
            Put(grid,f,1);Put(grid,pct,2);return f;
        }
        void Level(string name,float value,Func<MixerLevels,float,MixerLevels> set)=>
            Row(levels,name,name,value,v=>{edited=set(edited,v);if(Viewport.PlayWorld is {} world)world.Levels=edited;Check();});
        Level("Master",edited.Master,(l,v)=>l with {Master=v});
        Level("Music",edited.Music,(l,v)=>l with {Music=v});
        Level("Effects",edited.Effects,(l,v)=>l with {Effects=v});
        Level("Voice",edited.Level(AudioBus.Voice),(l,v)=>l with {Voice=Full(v)});
        Level("UI",edited.Level(AudioBus.Ui),(l,v)=>l with {Ui=Full(v)});
        Level("Ambience",edited.Level(AudioBus.Ambience),(l,v)=>l with {Ambience=Full(v)});

        // A selector row (list, add, remove) above the fields of the selected entry, on both list pages.
        ComboBox Selector(Panel page,string label,string what,Action add,Action remove)
        {
            var grid=Line(page,label,"*,24,24");
            var pick=new ComboBox { HorizontalAlignment=HorizontalAlignment.Stretch };AutomationProperties.SetName(pick,label);
            var plus=EditorTokens.IconButton("IconAdd","Add "+what);plus.Click+=(_,_)=>add();
            var minus=EditorTokens.IconButton("IconClose","Remove "+what);minus.Click+=(_,_)=>remove();
            Put(grid,pick,1);Put(grid,plus,2);Put(grid,minus,3);return pick;
        }
        void Items(ComboBox pick,IEnumerable<string> names,int at)
        {
            loading=true;pick.ItemsSource=names.ToArray();pick.SelectedIndex=at;loading=false;
        }

        var snapFields=new StackPanel { Spacing=5 };
        ComboBox? snapPick=null;
        TextBox snapName=new() { Watermark="Snapshot name" };AutomationProperties.SetName(snapName,"Snapshot name");
        var snapSliders=new List<(Slider Slider,AudioBus? Bus)>();
        void ShowSnapshot()
        {
            Items(snapPick!,snaps.Select(s=>s.Name.Length>0?s.Name:"(unnamed)"),snapAt);
            snapFields.IsEnabled=snapAt>=0;
            loading=true;
            var levelsOf=snapAt>=0?snaps[snapAt].Levels??new():new();
            snapName.Text=snapAt>=0?snaps[snapAt].Name:"";
            foreach(var (slider,bus) in snapSliders)slider.Value=(bus is {} b?levelsOf.Level(b):levelsOf.Master)*100;
            loading=false;
        }
        void SetSnapshot(Func<AudioSnapshot,AudioSnapshot> change){ if(snapAt<0)return; snaps[snapAt]=change(snaps[snapAt]); Live(); }
        snapPick=Selector(snapshotPage,"Snapshot","snapshot",()=>{
            int n=snaps.Count+1;while(snaps.Any(s=>s.Name==$"Snapshot {n}"))n++;
            snaps.Add(new($"Snapshot {n}"));snapAt=snaps.Count-1;ShowSnapshot();Live();
        },()=>{ if(snapAt<0)return; snaps.RemoveAt(snapAt);snapAt=Math.Min(snapAt,snaps.Count-1);ShowSnapshot();Live(); });
        snapPick.SelectionChanged+=(_,_)=>{ if(!loading && snapPick.SelectedIndex>=0){snapAt=snapPick.SelectedIndex;ShowSnapshot();} };
        snapshotPage.Children.Add(snapFields);
        Put(Line(snapFields,"Name"),snapName,1,2);
        snapName.TextChanged+=(_,_)=>{ if(loading)return; SetSnapshot(s=>s with {Name=snapName.Text?.Trim()??""});
            loading=true;var names=snaps.Select(s=>s.Name.Length>0?s.Name:"(unnamed)").ToArray();
            snapPick.ItemsSource=names;snapPick.SelectedIndex=snapAt;loading=false; };
        void SnapLevel(string name,AudioBus? bus,Func<MixerLevels,float,MixerLevels> set)=>
            snapSliders.Add((Row(snapFields,name,"Snapshot "+name,1,v=>SetSnapshot(s=>s with {Levels=set(s.Levels??new(),v)})),bus));
        SnapLevel("Master",null,(l,v)=>l with {Master=v});
        SnapLevel("Music",AudioBus.Music,(l,v)=>l with {Music=v});
        SnapLevel("Effects",AudioBus.Effects,(l,v)=>l with {Effects=v});
        SnapLevel("Voice",AudioBus.Voice,(l,v)=>l with {Voice=Full(v)});
        SnapLevel("UI",AudioBus.Ui,(l,v)=>l with {Ui=Full(v)});
        SnapLevel("Ambience",AudioBus.Ambience,(l,v)=>l with {Ambience=Full(v)});
        var preview=Line(snapFields,"Preview","*,Auto,Auto");
        var seconds=new NumericUpDown { Minimum=0,Maximum=60,Increment=.5m,Value=1,FormatString="0.##" };
        AutomationProperties.SetName(seconds,"Preview seconds");ToolTip.SetTip(seconds,"Transition time in seconds");
        var blend=new Button { Content="Blend" };AutomationProperties.SetName(blend,"Preview transition to snapshot");
        var back=new Button { Content="Reset" };AutomationProperties.SetName(back,"Preview transition back to 100%");
        foreach(var b in new[]{blend,back})
        {
            b.IsEnabled=live;ToolTip.SetShowOnDisabled(b,true);
            ToolTip.SetTip(b,live?(b==blend?"Blend the running game to this snapshot":"Blend the running game back to 100%"):"Start Play to hear the transition.");
        }
        void Transition(string? name)
        {
            Live();
            if(Viewport.PlayWorld is {} world && hint.Text is "" && world.TransitionTo(name,(float)(seconds.Value??0)))previewed=true;
        }
        blend.Click+=(_,_)=>{ if(snapAt>=0)Transition(snaps[snapAt].Name); };
        back.Click+=(_,_)=>Transition(null);
        Put(preview,seconds,1);Put(preview,blend,2);Put(preview,back,3);
        ShowSnapshot();

        var duckFields=new StackPanel { Spacing=5 };
        ComboBox? duckPick=null;
        string Describe(AudioDucking d)=>$"{d.Trigger} → {d.Target}";
        ComboBox Bus(string label)
        {
            var c=new ComboBox { ItemsSource=Enum.GetValues<AudioBus>(),HorizontalAlignment=HorizontalAlignment.Stretch };
            AutomationProperties.SetName(c,"Ducking "+label);Put(Line(duckFields,label),c,1,2);return c;
        }
        NumericUpDown Amount(string label,decimal step,string tip)
        {
            // No Minimum/Maximum: an out-of-range value shows the runtime's message instead of being clamped silently.
            var n=new NumericUpDown { Increment=step,FormatString="0.###",Value=0 };AutomationProperties.SetName(n,"Ducking "+label);
            ToolTip.SetTip(n,tip);Put(Line(duckFields,label),n,1,2);return n;
        }
        void SetRule(Func<AudioDucking,AudioDucking> change)
        {
            if(loading || duckAt<0)return;
            ducks[duckAt]=change(ducks[duckAt]);Items(duckPick!,ducks.Select(Describe),duckAt);Live();
        }
        var trigger=Bus("Trigger");ToolTip.SetTip(trigger,"While any sound plays on this bus…");
        var target=Bus("Target");ToolTip.SetTip(target,"…this bus is lowered");
        var depth=Amount("Depth",1,"Decibels, −80 to 0");
        var attack=Amount("Attack",.05m,"Seconds to reach the depth, 0 to 10");
        var release=Amount("Release",.05m,"Seconds to return after the trigger bus goes quiet, 0 to 10");
        trigger.SelectionChanged+=(_,_)=>{ if(trigger.SelectedItem is AudioBus b)SetRule(d=>d with {Trigger=b}); };
        target.SelectionChanged+=(_,_)=>{ if(target.SelectedItem is AudioBus b)SetRule(d=>d with {Target=b}); };
        depth.ValueChanged+=(_,_)=>{ if(depth.Value is {} v)SetRule(d=>d with {Decibels=(float)v}); };
        attack.ValueChanged+=(_,_)=>{ if(attack.Value is {} v)SetRule(d=>d with {Attack=(float)v}); };
        release.ValueChanged+=(_,_)=>{ if(release.Value is {} v)SetRule(d=>d with {Release=(float)v}); };
        duckPick=Selector(duckingPage,"Rule","ducking rule",()=>{ ducks.Add(new());duckAt=ducks.Count-1;ShowRule();Live(); },
            ()=>{ if(duckAt<0)return; ducks.RemoveAt(duckAt);duckAt=Math.Min(duckAt,ducks.Count-1);ShowRule();Live(); });
        duckPick.SelectionChanged+=(_,_)=>{ if(!loading && duckPick.SelectedIndex>=0){duckAt=duckPick.SelectedIndex;ShowRule();} };
        duckingPage.Children.Add(duckFields);
        void ShowRule()
        {
            Items(duckPick!,ducks.Select(Describe),duckAt);
            duckFields.IsEnabled=duckAt>=0;
            var d=duckAt>=0?ducks[duckAt]:new();
            loading=true;
            trigger.SelectedItem=d.Trigger;target.SelectedItem=d.Target;
            depth.Value=decimal.CreateSaturating(d.Decibels);attack.Value=decimal.CreateSaturating(d.Attack);release.Value=decimal.CreateSaturating(d.Release);
            loading=false;
        }
        ShowRule();

        var cancel=new Button {Content="Cancel",IsCancel=true};cancel.Click+=(_,_)=>dialog.Close();
        var apply=new Button {Content=live?"Keep in Play":"Save defaults",IsDefault=true};
        apply.Click+=(_,_)=>{ if(Check()!=null){applied=true;dialog.Close();} };
        var buttons=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=6,Children={cancel,apply} };
        Grid.SetRow(buttons,2);layout.Children.Add(buttons);
        await dialog.ShowDialog(this);
        if(!applied)
        {
            if(Viewport.PlayWorld is {} world)
            {
                if(configured)world.Mixer.Configure(settings);
                world.Levels=original;
                if(previewed)world.TransitionTo(null);
            }
            return;
        }
        if(!live){var saved=Candidate();saved.Save(project!);Scene.Settings=saved; MarkDirty();Status.Text="Saved project mixer levels, snapshots and ducking.";}
    }
}
