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
    private sealed record Row(TextBox Name,TextBox Controls,Grid Grid);
    private readonly List<Row> rows=[];
    private readonly StackPanel fields=new() { Spacing=EditorTokens.Gap };
    private readonly TextBlock hint=new() { TextWrapping=TextWrapping.Wrap };
    private readonly DispatcherTimer timer=new() { Interval=TimeSpan.FromMilliseconds(16) };
    private SdlGamepads? pads;
    private Row? listening;
    private const string Help="Use commas for alternatives. + Bind captures the next key, mouse button or pad control.";
    private const string Columns="1.2*,2*,65,26";

    internal InputMapWindow(InputBindings bindings)
    {
        Title="Input Actions"; MinWidth=435; MinHeight=270;
        var layout=new Grid { Margin=EditorTokens.WindowPadding, RowDefinitions=new("Auto,*,Auto,Auto"),RowSpacing=EditorTokens.Gap2 };
        var header=Line(); Put(header,new TextBlock { Text="Action" },0); Put(header,new TextBlock { Text="Controls" },1);
        layout.Children.Add(header);
        var scroll=new ScrollViewer { Content=fields, HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll,1); layout.Children.Add(scroll);
        Grid.SetRow(hint,2); layout.Children.Add(hint);
        var footer=new Grid { ColumnDefinitions=new("Auto,*,Auto,Auto"),ColumnSpacing=EditorTokens.Gap };
        var add=Button("+ Action","Add action",()=> {
            var row=Add("",""); scroll.ScrollToEnd(); row.Name.Focus();
        });
        Put(footer,add,0); Put(footer,Button("Cancel","Cancel",()=>Close()),2);
        var save=Button("Save","Save action map",SaveMap); save.Classes.Add("primary"); Put(footer,save,3);
        Grid.SetRow(footer,3);layout.Children.Add(footer);Content=layout;
        foreach(var b in bindings.AsMap().Actions!) Add(b.Name,string.Join(", ",b.Controls));
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
    private static void Put(Grid grid,Control item,int column) { Grid.SetColumn(item,column);grid.Children.Add(item); }
    private static Button Button(string text,string label,Action click)
    {
        var b=new Button { Content=text,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Center };
        AutomationProperties.SetName(b,label);b.Click+=(_,_)=>click(); return b;
    }
    private Row Add(string action,string sources)
    {
        var grid=Line();var name=new TextBox { Text=action,Watermark="Action name" };var controls=new TextBox { Text=sources,Watermark="Unbound" };
        AutomationProperties.SetName(name,"Action name"); AutomationProperties.SetName(controls,"Controls for "+action);
        var row=new Row(name,controls,grid); Put(grid,name,0);Put(grid,controls,1);
        Put(grid,Button("+ Bind","Bind control for "+action,()=>Listen(row)),2);
        var remove=EditorTokens.IconButton("IconClose","Remove action "+action);
        remove.Click+=(_,_)=>{ rows.Remove(row);fields.Children.Remove(grid); };Put(grid,remove,3);
        rows.Add(row);fields.Children.Add(grid);return row;
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
    private void SaveMap()
    {
        var result=new InputBindings { Actions=rows.Select(r=>new ActionBinding(r.Name.Text?.Trim()??"",InputBindings.Keys(r.Controls.Text??""))).ToArray() };
        try { result.Validate(); Close(result); }
        catch(InvalidDataException ex) { hint.Text=ex.Message; }
    }
}
