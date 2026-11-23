using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Engine.Core;
using static Engine.Editor.EditorControls;

namespace Engine.Editor;

internal sealed class AnimatorWindow : EditorPanel
{
    private sealed record ClipOption(Guid Id,string Path)
    {
        public override string ToString() => Path;
    }

    private readonly string root, folder;
    private readonly string? relative;
    private readonly byte[]? original;
    private readonly List<AnimationState> states;
    private readonly List<AnimationParameter> parameters;
    private readonly List<AnimationTransition> transitions;
    private readonly ClipOption[] clips;
    private string entry;
    private readonly TextBox name = new();
    private readonly TextBlock status = new() { TextWrapping=TextWrapping.Wrap };
    private readonly TabControl tabs = new();
    private readonly TabItem previewTab = new() { Header="Preview" };
    private readonly StackPanel previewControls = new() { Spacing=4 };
    private readonly SceneViewport previewImage = new(spritePreview:true) { IsHitTestVisible=false };
    private readonly TextBlock previewState = new() { FontWeight=FontWeight.SemiBold };
    private readonly TextBlock previewEvents = new() { TextWrapping=TextWrapping.Wrap };
    private readonly Queue<string> recent = new();
    private readonly Stopwatch elapsed = new();
    private readonly DispatcherTimer timer = new() { Interval=TimeSpan.FromSeconds(GameTime.FixedStep) };
    private AnimationPlayer? player;
    private double accumulator;
    private int eventCount;
    private ListBox stateList = null!, parameterList = null!, transitionList = null!;
    private bool committed, asking;

    public AnimatorWindow(string root,string folder,string? relative)
    {
        this.root=root; this.folder=folder; this.relative=relative;
        Title=relative==null ? "Animator Controller" : $"{Path.GetFileNameWithoutExtension(relative)} · Animator"; MinWidth=390; MinHeight=340;
        var catalog=ProjectAssets.Scan(root);
        clips=catalog.Entries.Where(e => e.Path.EndsWith(ProjectAssets.Clip) && File.Exists(Path.Combine(root,e.Path)))
            .Select(e=>new ClipOption(e.Id,e.Path)).ToArray();
        var config=relative==null ? new AnimationController("Idle",clips.Length>0 ? [new("Idle",clips[0].Id)] : [],[],[]) :
            AnimationController.Read(Path.Combine(root,relative));
        if (relative!=null) original=File.ReadAllBytes(Path.Combine(root,relative));
        states=[..config.States]; parameters=[..config.Parameters]; transitions=[..config.Transitions]; entry=config.Entry;
        name.Text=relative==null ? "Animator" : Path.GetFileNameWithoutExtension(relative);
        name.IsReadOnly=relative!=null;

        var statesTab=EditTab("States",out stateList,EditState);
        var paramTab=EditTab("Parameters",out parameterList,EditParameter);
        var rulesTab=EditTab("Transitions",out transitionList,EditTransition);
        previewTab.Content=PreviewPanel();
        tabs.ItemsSource=new[]{statesTab,paramTab,rulesTab,previewTab};
        tabs.SelectionChanged+=(_,e)=>
        {
            if (e.Source!=tabs) return;
            if (tabs.SelectedItem==previewTab) StartPreview(); else StopPreview();
        };
        var save=Button("Save Controller",SaveController);
        save.Classes.Add("primary");
        var footer=new Grid { ColumnDefinitions=new("*,Auto,Auto"),ColumnSpacing=EditorTokens.Gap };
        Put(footer,status,0,0); Put(footer,Button("Close",Close),0,1); Put(footer,save,0,2);
        var main=new Grid { Margin=EditorTokens.WindowPadding,RowDefinitions=new("Auto,*,Auto"),RowSpacing=EditorTokens.Gap2 };
        Put(main,Field("Name",name),0,0); Put(main,tabs,1,0); Put(main,footer,2,0);
        Content=main;
        RefreshLists();
        timer.Tick+=(_,_)=>Tick();
        var initial=Snapshot();
        var initialName=name.Text;
        Closing+=(_,e)=>
        {
            if (committed || initialName==name.Text && Same(initial,Snapshot())) return;
            e.Cancel=true;
            if (!asking) Dispatcher.UIThread.Post(ConfirmClose);
        };
        Closed+=(_,_)=>StopPreview();
    }

    private AnimationController Snapshot() => new(entry,[..states],[..parameters],[..transitions]);
    private static bool Same(AnimationController a,AnimationController b) => a.Entry==b.Entry && a.States.SequenceEqual(b.States) &&
        a.Parameters.SequenceEqual(b.Parameters) && a.Transitions.SequenceEqual(b.Transitions);

    private async void ConfirmClose()
    {
        if (asking) return;
        asking=true;
        var dialog=new Window { Title="Unsaved controller",SizeToContent=SizeToContent.WidthAndHeight,WindowStartupLocation=WindowStartupLocation.CenterOwner };
        dialog.Content=new StackPanel { Margin=EditorTokens.WindowPadding,Spacing=EditorTokens.Gap2,Children={
            new TextBlock { Text="Save changes to the controller?" },
            Actions(Button("Cancel",()=>dialog.Close("cancel")),Button("Discard",()=>dialog.Close("discard")),Button("Save",()=>dialog.Close("save"))) } };
        string? answer;
        try { answer=await dialog.ShowDialog<string?>(Owner); } finally { asking=false; }
        if (answer=="save") SaveController();
        else if (answer=="discard") { committed=true; Close(); }
    }

    private TabItem EditTab(string header,out ListBox list,Action<int> edit)
    {
        var items=new ListBox(); list=items;
        var pane=new Grid { RowDefinitions=new("*,Auto"),RowSpacing=EditorTokens.Gap,Margin=new(0,EditorTokens.Gap) };
        var remove=Button("Remove",()=>Remove(header,items.SelectedIndex));
        var change=Button("Edit…",()=>edit(items.SelectedIndex));
        var buttons=Actions(Button("Add…",()=>edit(-1)),change,remove);
        if (header=="States") buttons.Children.Add(Button("Set Entry",()=>{ if (items.SelectedIndex is var i && i>=0) { entry=states[i].Name; RefreshLists(i); } }));
        if (header=="Transitions")
        {
            var up=EditorTokens.IconButton("IconArrowUp","Move transition up");up.Click+=(_,_)=>MoveRule(items.SelectedIndex,-1);
            var down=EditorTokens.IconButton("IconArrowDown","Move transition down");down.Click+=(_,_)=>MoveRule(items.SelectedIndex,1);
            buttons.Children.Add(up);buttons.Children.Add(down);
        }
        items.SelectionChanged+=(_,_)=>{ change.IsEnabled=remove.IsEnabled=items.SelectedIndex>=0; };
        items.DoubleTapped+=(_,_)=>{ if (items.SelectedIndex>=0) edit(items.SelectedIndex); };
        Put(pane,items,0,0); Put(pane,buttons,1,0);
        return new TabItem { Header=header,Content=pane };
    }

    private void RefreshLists(int state=-1,int parameter=-1,int rule=-1)
    {
        stateList.ItemsSource=states.Select(s=>(s.Name==entry ? "●  " : "    ")+s.Name+"    "+(clips.FirstOrDefault(c=>c.Id==s.Clip)?.Path ?? "Missing clip")).ToArray();
        parameterList.ItemsSource=parameters.Select(p=>$"{p.Name}    {p.Kind}").ToArray();
        transitionList.ItemsSource=transitions.Select(t=>$"{t.From} → {t.To}    {t.Parameter} {t.Test}"+(parameters.Any(p=>p.Name==t.Parameter&&p.Kind=="Float") ? $" {t.Value}" : "")).ToArray();
        void Pick(ListBox list,int i) => list.SelectedIndex=list.ItemCount==0 ? -1 : Math.Clamp(i,0,list.ItemCount-1);
        Pick(stateList,state); Pick(parameterList,parameter); Pick(transitionList,rule);
        status.Text="";
    }

    private void Remove(string tab,int index)
    {
        if (index<0) return;
        if (tab=="States")
        {
            var target=states[index].Name;
            if (transitions.Any(t=>t.From==target || t.To==target)) { status.Text="Remove this state’s transitions first."; return; }
            states.RemoveAt(index);
            if (entry==target) entry=states.FirstOrDefault()?.Name ?? "";
        }
        else if (tab=="Parameters")
        {
            if (transitions.Any(t=>t.Parameter==parameters[index].Name)) { status.Text="Remove transitions using this parameter first."; return; }
            parameters.RemoveAt(index);
        }
        else transitions.RemoveAt(index);
        RefreshLists();
    }
    private void MoveRule(int index,int delta)
    {
        int other=index+delta;
        if (index<0 || other<0 || other>=transitions.Count) return;
        (transitions[index],transitions[other])=(transitions[other],transitions[index]);
        RefreshLists(rule:other);
    }

    private async void EditState(int index)
    {
        if (clips.Length==0) { status.Text="Create an animation clip first."; return; }
        var old=index>=0 ? states[index] : null;
        var text=new TextBox { Text=old?.Name ?? "" };
        var clip=Choice(clips,clips.FirstOrDefault(c=>c.Id==old?.Clip) ?? clips[0]);
        if (!await Form("State",[Field("Name",text),Field("Clip",clip)],()=>
        {
            var label=Label(text);
            if (states.Any(s=>s!=old && s.Name==label)) throw new InvalidDataException("A state with that name already exists.");
            var value=new AnimationState(label,((ClipOption)clip.SelectedItem!).Id);
            if (old==null) states.Add(value);
            else
            {
                states[index]=value;
                for (int i=0;i<transitions.Count;i++)
                {
                    var t=transitions[i]; transitions[i]=t with { From=t.From==old.Name ? label:t.From, To=t.To==old.Name ? label:t.To };
                }
            }
            if (states.Count==1 || entry==old?.Name) entry=label;
        })) return;
        RefreshLists(state:index<0 ? states.Count-1:index);
    }

    private async void EditParameter(int index)
    {
        var old=index>=0 ? parameters[index] : null;
        var text=new TextBox { Text=old?.Name };
        var kind=Choice(new[]{"Bool","Float","Trigger"},old?.Kind ?? "Bool");
        var number=Number(old?.Default ?? 0);
        var toggle=new CheckBox { IsChecked=old?.Default==1 };
        var value=new ContentControl();
        void TypeChanged() => value.Content=kind.SelectedItem switch { "Float"=>number,"Bool"=>toggle,_=>null };
        kind.SelectionChanged+=(_,_)=>TypeChanged(); TypeChanged();
        if (!await Form("Parameter",[Field("Name",text),Field("Type",kind),Field("Default",value)],()=>
        {
            var label=Label(text); var type=(string)kind.SelectedItem!;
            if (parameters.Any(p=>p!=old && p.Name==label)) throw new InvalidDataException("A parameter with that name already exists.");
            if (old!=null && old.Kind!=type && transitions.Any(t=>t.Parameter==old.Name))
                throw new InvalidDataException("Remove this parameter’s transitions before changing its type.");
            float initial=type=="Float" ? (float)(number.Value ?? 0) : type=="Bool" && toggle.IsChecked==true ? 1:0;
            var p=new AnimationParameter(label,type,initial);
            if (old==null) parameters.Add(p);
            else
            {
                parameters[index]=p;
                for (int i=0;i<transitions.Count;i++) if (transitions[i].Parameter==old.Name) transitions[i]=transitions[i] with { Parameter=label };
            }
        })) return;
        RefreshLists(parameter:index<0 ? parameters.Count-1:index);
    }

    private async void EditTransition(int index)
    {
        if (states.Count<2 || parameters.Count==0) { status.Text="Add two states and a parameter first."; return; }
        var old=index>=0 ? transitions[index] : null;
        var names=states.Select(s=>s.Name).ToArray();
        var from=Choice(names,old?.From ?? names[0]); var to=Choice(names,old?.To ?? names[1]);
        var parameter=Choice(parameters.Select(p=>p.Name),old?.Parameter ?? parameters[0].Name);
        var test=new ComboBox { HorizontalAlignment=HorizontalAlignment.Stretch };
        var number=Number(old?.Value ?? 0);
        var threshold=Field("Value",number);
        void Conditions()
        {
            var kind=parameters.First(p=>p.Name==(string)parameter.SelectedItem!).Kind;
            var tests=AnimationController.Tests(kind);
            test.ItemsSource=tests; test.SelectedItem=tests.Contains(old?.Test) ? old!.Test : tests[0];
            threshold.IsVisible=kind=="Float";
        }
        parameter.SelectionChanged+=(_,_)=>Conditions(); Conditions();
        if (!await Form("Transition",[Field("From",from),Field("To",to),Field("Parameter",parameter),Field("Condition",test),threshold],()=>
        {
            var rule=new AnimationTransition((string)from.SelectedItem!,(string)to.SelectedItem!,(string)parameter.SelectedItem!,
                (string)test.SelectedItem!,(float)(number.Value ?? 0));
            if (rule.From==rule.To) throw new InvalidDataException("Choose a different destination state.");
            if (index<0) transitions.Add(rule); else transitions[index]=rule;
        })) return;
        RefreshLists(rule:index<0 ? transitions.Count-1:index);
    }

    private Control PreviewPanel()
    {
        var picture=new Border { Child=previewImage,Padding=new(EditorTokens.Gap2),Background=EditorTokens.Brush("EditorField"),MinHeight=64 };
        var grid=new Grid { RowDefinitions=new("*,Auto,Auto,Auto"),RowSpacing=EditorTokens.Gap,Margin=new(0,EditorTokens.Gap) };
        var row=new Grid { ColumnDefinitions=new("*,Auto"),ColumnSpacing=EditorTokens.Gap };
        Put(row,previewState,0,0); Put(row,Button("Restart",StartPreview),0,1);
        Put(grid,picture,0,0); Put(grid,row,1,0);
        Put(grid,new ScrollViewer { Content=previewControls,MaxHeight=110 },2,0); Put(grid,previewEvents,3,0);
        return grid;
    }
    private void StartPreview()
    {
        StopPreview(); status.Text=""; previewState.Text=""; previewImage.ShowFrame(null);
        recent.Clear(); eventCount=0; previewEvents.Text=""; previewControls.Children.Clear(); previewImage.ClearSprites();
        try
        {
            var config=Snapshot();
            player=new AnimationPlayer(config,root,ProjectAssets.Scan(root));
            foreach (var p in parameters)
            {
                Control control;
                if (p.Kind=="Bool")
                {
                    var toggle=new CheckBox { IsChecked=p.Default!=0 };
                    toggle.IsCheckedChanged+=(_,_)=>player?.SetBool(p.Name,toggle.IsChecked==true); control=toggle;
                }
                else if (p.Kind=="Float")
                {
                    var value=Number(p.Default);
                    value.ValueChanged+=(_,_)=>player?.SetFloat(p.Name,(float)(value.Value ?? 0)); control=value;
                }
                else control=Button("Fire",()=>player?.SetTrigger(p.Name));
                previewControls.Children.Add(Field(p.Name,control));
            }
            player.Play(); accumulator=0; elapsed.Restart(); timer.Start(); Tick();
        }
        catch (Exception ex) { StopPreview(); status.Text=ex.Message; }
    }
    private void Tick()
    {
        if (player==null) return;
        try
        {
            accumulator+=Math.Min(.1,elapsed.Elapsed.TotalSeconds); elapsed.Restart();
            while (accumulator+1e-12>=GameTime.FixedStep)
            {
                player.Step((float)GameTime.FixedStep,marker=>
                {
                    if (recent.Count==4) recent.Dequeue();
                    recent.Enqueue(marker.Name);
                    previewEvents.Text=$"{++eventCount}   "+string.Join(" → ",recent);
                });
                accumulator=Math.Max(0,accumulator-GameTime.FixedStep);
            }
            previewState.Text=$"{player.State}   ·   Frame {player.Frame+1}";
            var frame=player.Appearance;
            if (frame == null && player.Frame >= 0) frame = new SceneAppearance(SpriteAssets.SquarePath,System.Drawing.Color.White);
            previewImage.ShowFrame(frame,player.Pose);
        }
        catch (Exception ex) { StopPreview(); status.Text=ex.Message; }
    }
    private void StopPreview() { timer.Stop(); elapsed.Reset(); player=null; }
    private void SaveController()
    {
        try
        {
            var stem=Label(name);
            if (stem.Any(c=>!char.IsLetterOrDigit(c) && c is not (' ' or '-' or '_')) && relative==null)
                throw new InvalidDataException("Use letters, numbers, spaces, dashes or underscores for the name.");
            var target=relative ?? $"{folder}/{stem}{AnimationController.Extension}";
            var path=Path.Combine(root,target);
            if (original!=null && (!File.Exists(path) || !original.SequenceEqual(File.ReadAllBytes(path))))
                throw new IOException("Controller changed on disk. Reopen it before saving.");
            var config=Snapshot();
            _=new AnimationPlayer(config,root,ProjectAssets.Scan(root));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            config.Save(path,original!=null); committed=true; Close(target);
        }
        catch (Exception ex) { status.Text=ex.Message; }
    }

    private async Task<bool> Form(string title,Control[] fields,Action apply)
    {
        var dlg=new Window { Title=title,Width=325,MinWidth=270,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner };
        var panel=new StackPanel { Margin=EditorTokens.WindowPadding,Spacing=EditorTokens.Gap };
        foreach (var field in fields) panel.Children.Add(field);
        var error=new TextBlock { TextWrapping=TextWrapping.Wrap,IsVisible=false }; panel.Children.Add(error);
        panel.Children.Add(Actions(Button("Cancel",()=>dlg.Close(false)),Button("Apply",()=>
        {
            try { apply(); dlg.Close(true); } catch (Exception ex) { error.Text=ex.Message; error.IsVisible=true; }
        })));
        dlg.Content=panel; return await dlg.ShowDialog<bool>(Owner);
    }
    private static string Label(TextBox box) => !string.IsNullOrWhiteSpace(box.Text) && !box.Text.Any(char.IsControl)
        ? box.Text.Trim() : throw new InvalidDataException("Enter a name.");
    private static NumericUpDown Number(float value) => new() { Value=decimal.CreateSaturating(value),Increment=.1m,Minimum=decimal.MinValue,Maximum=decimal.MaxValue };
    private static ComboBox Choice<T>(IEnumerable<T> items,T selected) => new() { ItemsSource=items.ToArray(),SelectedItem=selected,HorizontalAlignment=HorizontalAlignment.Stretch };
    private static StackPanel Actions(params Button[] buttons)
    {
        var row=new StackPanel { Orientation=Orientation.Horizontal,Spacing=EditorTokens.Gap,HorizontalAlignment=HorizontalAlignment.Right };
        foreach (var button in buttons) row.Children.Add(button); return row;
    }
    private static Grid Field(string label,Control control)
    {
        var grid=new Grid { ColumnDefinitions=EditorTokens.LabelColumns(EditorTokens.DialogLabelWidth),ColumnSpacing=EditorTokens.Gap2 };
        var text=new TextBlock { Text=label,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center };
        ToolTip.SetTip(text,label); Put(grid,text,0,0); Put(grid,control,0,1); return grid;
    }
}
