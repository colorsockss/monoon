using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Engine.Core;
using Engine.Desktop;

namespace Engine.Editor;

internal sealed class InputMapWindow : EditorPanel
{
    private sealed record Row(TextBox Name,TextBox Controls,ComboBox Interaction,TextBox Ticks,TextBox Buffer,Grid Grid);
    private sealed record VectorRow(TextBox Name,TextBox[] Parts,Grid Grid);
    private readonly List<Row> rows=[];
    private readonly List<VectorRow> vectorRows=[];
    private readonly StackPanel fields=new() { Spacing=EditorTokens.Gap }, vectors=new() { Spacing=EditorTokens.Gap };
    private readonly TextBlock hint=new() { TextWrapping=TextWrapping.Wrap };
    private readonly DispatcherTimer timer=new() { Interval=TimeSpan.FromMilliseconds(16) };
    private SdlGamepads? pads;
    private Row? listening;
    private readonly InputBindings original;
    private const string Help="Use commas for alternatives. + Bind captures the next key, mouse button or pad control. The first vector is Move.";
    // Action, controls, interaction, ticks, buffer, bind, remove. A vector row keeps the first and last columns and splits the five between.
    private const string Columns="1.2*,2*,108,48,48,65,26";
    private static readonly string[] Interactions=["Press",ActionBinding.Hold,ActionBinding.Tap,ActionBinding.DoubleTap];
    private static readonly string[] Directions=["Left","Right","Up","Down"];

    internal InputMapWindow(InputBindings bindings)
    {
        Title="Input Actions"; MinWidth=616; MinHeight=270; original=bindings.AsMap();
        var layout=new Grid { Margin=EditorTokens.WindowPadding, RowDefinitions=new("Auto,*,Auto,Auto"),RowSpacing=EditorTokens.Gap2 };
        var header=Line(); Put(header,new TextBlock { Text="Action" },0); Put(header,new TextBlock { Text="Controls" },1);
        Put(header,new TextBlock { Text="Interaction" },2); Put(header,new TextBlock { Text="Ticks" },3); Put(header,new TextBlock { Text="Buffer" },4);
        layout.Children.Add(header);
        var vectorHeader=Line(); Put(vectorHeader,new TextBlock { Text="Vector" },0);
        var parts=Parts(); for(int i=0;i<4;i++) Put(parts,new TextBlock { Text=Directions[i] },i);
        Put(vectorHeader,parts,1); Grid.SetColumnSpan(parts,5);
        var scroll=new ScrollViewer { Content=new StackPanel { Spacing=EditorTokens.Gap2, Children={ fields,vectorHeader,vectors } },
            HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll,1); layout.Children.Add(scroll);
        Grid.SetRow(hint,2); layout.Children.Add(hint);
        var footer=new Grid { ColumnDefinitions=new("Auto,Auto,*,Auto,Auto"),ColumnSpacing=EditorTokens.Gap };
        var add=Button("+ Action","Add action",()=> {
            var row=Add(new("",[])); row.Name.Focus(); Dispatcher.UIThread.Post(()=>row.Grid.BringIntoView());
        });
        var addVector=Button("+ Vector","Add vector",()=> { var row=AddVector(new("","","","","")); scroll.ScrollToEnd(); row.Name.Focus(); });
        Put(footer,add,0); Put(footer,addVector,1); Put(footer,Button("Cancel","Cancel",()=>Close()),3);
        var save=Button("Save","Save action map",SaveMap); save.Classes.Add("primary"); Put(footer,save,4);
        Grid.SetRow(footer,3);layout.Children.Add(footer);Content=layout;
        foreach(var b in original.Actions!) Add(b);
        foreach(var v in original.Vectors??[]) AddVector(v);
        hint.Text=Help;
        Opened+=(_,_)=>{ pads=new SdlGamepads(); timer.Start(); };
        Closed+=(_,_)=>{ timer.Stop(); pads?.Dispose(); };
        Deactivated+=(_,_)=>{ Listen(null); pads?.SuppressHeld(); };
        timer.Tick+=(_,_)=>{ if(IsActive) pads?.Poll((c,v)=>{ if(listening!=null && v>=.5f) Capture(c); }); };
        AddHandler(KeyDownEvent,(_,e)=>{
            if(listening==null) return;
            e.Handled=true;
            if(e.Key==Key.Escape) Listen(null); else Capture(SceneViewport.BindingKey(e));
        },RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent,(_,e)=>{
            if(listening==null) return;
            var props=e.GetCurrentPoint(this).Properties;
            string? c=props.PointerUpdateKind switch { PointerUpdateKind.LeftButtonPressed=>"Mouse.Left",
                PointerUpdateKind.RightButtonPressed=>"Mouse.Right",PointerUpdateKind.MiddleButtonPressed=>"Mouse.Middle",_=>null };
            if(c!=null) { e.Handled=true; Capture(c); }
        },RoutingStrategies.Tunnel);
        AddHandler(PointerWheelChangedEvent,(_,e)=>{
            if(listening!=null && e.Delta.Y!=0) { e.Handled=true; Capture(e.Delta.Y>0?"Mouse.WheelUp":"Mouse.WheelDown"); }
        },RoutingStrategies.Tunnel);
    }
    private static Grid Line()=>new() { ColumnDefinitions=new(Columns),ColumnSpacing=EditorTokens.Gap };
    private static Grid Parts()=>new() { ColumnDefinitions=new("*,*,*,*"),ColumnSpacing=EditorTokens.Gap };
    private static void Put(Grid grid,Control item,int column) { Grid.SetColumn(item,column);grid.Children.Add(item); }
    private static Button Button(string text,string label,Action click)
    {
        var b=new Button { Content=text,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Center };
        AutomationProperties.SetName(b,label);b.Click+=(_,_)=>click(); return b;
    }
    private Row Add(ActionBinding action)
    {
        var grid=Line();var name=new TextBox { Text=action.Name,Watermark="Action name" };var controls=new TextBox { Text=string.Join(", ",action.Controls),Watermark="Unbound" };
        var interaction=new ComboBox { ItemsSource=Interactions,SelectedItem=action.Interaction??Interactions[0],HorizontalAlignment=HorizontalAlignment.Stretch };
        var ticks=new TextBox { Text=action.Ticks?.ToString(CultureInfo.InvariantCulture) };
        var buffer=new TextBox { Text=action.Buffer?.ToString(CultureInfo.InvariantCulture),Watermark="0" };
        // An empty Ticks field uses the runtime default of the chosen interaction (ActionBinding.Window); the watermark shows it.
        void Default()=>ticks.Watermark=interaction.SelectedItem is string i && i!=Interactions[0]
            ? new ActionBinding("",[]) { Interaction=i }.Window.ToString(CultureInfo.InvariantCulture) : "";
        interaction.SelectionChanged+=(_,_)=>Default(); Default();
        AutomationProperties.SetName(name,"Action name"); AutomationProperties.SetName(controls,"Controls for "+action.Name);
        AutomationProperties.SetName(interaction,"Interaction for "+action.Name); AutomationProperties.SetName(ticks,"Ticks for "+action.Name);
        AutomationProperties.SetName(buffer,"Buffer for "+action.Name);
        ToolTip.SetTip(ticks,"Game ticks: hold length, tap limit or double-tap gap. Empty uses the default.");
        ToolTip.SetTip(buffer,"Game ticks a trigger stays available to Triggered and Consume.");
        var row=new Row(name,controls,interaction,ticks,buffer,grid); Put(grid,name,0);Put(grid,controls,1);
        Put(grid,interaction,2);Put(grid,ticks,3);Put(grid,buffer,4);
        Put(grid,Button("+ Bind","Bind control for "+action.Name,()=>Listen(row)),5);
        var remove=EditorTokens.IconButton("IconClose","Remove action "+action.Name);
        remove.Click+=(_,_)=>{ rows.Remove(row);fields.Children.Remove(grid); };Put(grid,remove,6);
        rows.Add(row);fields.Children.Add(grid);return row;
    }
    private VectorRow AddVector(ActionVector vector)
    {
        var grid=Line();var name=new TextBox { Text=vector.Name,Watermark="Vector name" };
        AutomationProperties.SetName(name,"Vector name");
        var parts=Parts();
        var boxes=new[] { vector.Left,vector.Right,vector.Up,vector.Down }.Select((action,i)=>{
            var box=new TextBox { Text=action,Watermark=Directions[i]+" action" };
            AutomationProperties.SetName(box,$"{Directions[i]} action for "+vector.Name); Put(parts,box,i); return box; }).ToArray();
        Put(grid,name,0);Put(grid,parts,1);Grid.SetColumnSpan(parts,5);
        var row=new VectorRow(name,boxes,grid);
        var remove=EditorTokens.IconButton("IconClose","Remove vector "+vector.Name);
        remove.Click+=(_,_)=>{ vectorRows.Remove(row);vectors.Children.Remove(grid); };Put(grid,remove,6);
        vectorRows.Add(row);vectors.Children.Add(grid);return row;
    }
    private void Listen(Row? row)
    {
        listening=row;
        hint.Text=row==null?Help:$"Binding {row.Name.Text}. Press a control, or Esc to cancel.";
    }
    private void Capture(string control)
    {
        if(listening is not {} row) return;
        if(!InputControls.Supported(control)) { hint.Text=$"Unsupported control: {control}. Try another, or Esc."; return; }
        var previous=InputBindings.Keys(row.Controls.Text??"");
        if(!previous.Contains(control,StringComparer.OrdinalIgnoreCase)) row.Controls.Text=string.Join(", ",previous.Append(control));
        Listen(null);
    }
    private static int? Ticks(string action,string what,string? text)
    {
        if(string.IsNullOrWhiteSpace(text)) return null;
        if(int.TryParse(text.Trim(),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out int n)) return n;
        throw new InvalidDataException($"{action}: {what} must be a whole number of game ticks, or empty.");
    }
    private void SaveMap()
    {
        try
        {
            var actions=rows.Select(r=>{ var name=r.Name.Text?.Trim()??"";
                return new ActionBinding(name,InputBindings.Keys(r.Controls.Text??"")) {
                    Interaction=r.Interaction.SelectedItem is string i && i!=Interactions[0] ? i : null,
                    Ticks=Ticks(name,"ticks",r.Ticks.Text), Buffer=Ticks(name,"buffer",r.Buffer.Text) }; }).ToArray();
            var list=vectorRows.Select(v=>new ActionVector(v.Name.Text?.Trim()??"",
                v.Parts[0].Text?.Trim()??"",v.Parts[1].Text?.Trim()??"",v.Parts[2].Text?.Trim()??"",v.Parts[3].Text?.Trim()??"")).ToArray();
            // Without vector rows the file keeps what it had (no key, or an explicit empty list).
            var result=original with { Actions=actions, Vectors=list.Length!=0 ? list : original.Vectors is [] ? original.Vectors : null };
            result.Validate(); Close(result);
        }
        catch(InvalidDataException ex) { hint.Text=ex.Message; }
    }
}
