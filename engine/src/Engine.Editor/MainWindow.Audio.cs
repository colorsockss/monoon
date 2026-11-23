using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private void SourceFields(AudioSourceComponent source,Action<string,Control> row)
    {
        var bus=new ComboBox { ItemsSource=new[]{AudioBus.Effects,AudioBus.Music},SelectedItem=source.Bus,HorizontalAlignment=HorizontalAlignment.Stretch };
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
    private async Task EditMixer()
    {
        if(project==null && !await Save())return;
        bool live=Viewport.PlayWorld!=null;
        var original=Viewport.PlayWorld?.Levels??Scene.Settings.Mixer;
        var edited=original;
        var dialog=new Window { Title="Audio Mixer",Width=306,MinWidth=262,Height=230,MinHeight=226,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false };
        var panel=new StackPanel { Margin=new Thickness(14),Spacing=5 };dialog.Content=panel;
        bool applied=false;
        void Row(string name,float value,Func<MixerLevels,float,MixerLevels> set)
        {
            var grid=new Grid { ColumnDefinitions=EditorTokens.LabelColumns(EditorTokens.FieldLabelWidth,"*,36"),ColumnSpacing=EditorTokens.FieldGap };
            var f=new Slider { Minimum=0,Maximum=100,Value=value*100,SmallChange=1,LargeChange=10 };
            AutomationProperties.SetName(f,name);
            var pct=new TextBlock { Text=$"{value:P0}",HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center };
            f.ValueChanged+=(_,_)=>{float gain=(float)(f.Value/100);pct.Text=$"{gain:P0}";edited=set(edited,gain);
                if(Viewport.PlayWorld is {} world)world.Levels=edited; };
            grid.Children.Add(new TextBlock { Text=name,Classes={"fieldLabel"} });Grid.SetColumn(f,1);grid.Children.Add(f);
            Grid.SetColumn(pct,2);grid.Children.Add(pct);panel.Children.Add(grid);
        }
        Row("Master",edited.Master,(l,v)=>l with {Master=v});
        Row("Music",edited.Music,(l,v)=>l with {Music=v});
        Row("Effects",edited.Effects,(l,v)=>l with {Effects=v});
        var cancel=new Button {Content="Cancel",IsCancel=true};cancel.Click+=(_,_)=>dialog.Close();
        var apply=new Button {Content=live?"Keep in Play":"Save defaults",IsDefault=true};
        apply.Click+=(_,_)=>{applied=true;dialog.Close();};
        panel.Children.Add(new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,
            Spacing=6,Children={cancel,apply} });
        await dialog.ShowDialog(this);
        if(!applied){if(Viewport.PlayWorld is {} world)world.Levels=original;return;}
        if(!live){var settings=Scene.Settings with { Mixer=edited };settings.Save(project!);Scene.Settings=settings; MarkDirty();Status.Text="Saved project mixer levels.";}
    }
}
