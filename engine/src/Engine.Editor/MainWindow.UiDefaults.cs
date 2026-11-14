using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private async Task EditUiDefaults()
    {
        if(project==null && !await Save())return;
        var live=Viewport.PlayWorld?.Ui.Preferences;
        var defaults=Scene.Settings.Accessibility;
        var dialog=new Window {Title="UI Accessibility",Width=325,MinWidth=280,Height=215,MinHeight=205,WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false};
        var panel=new StackPanel { Margin=new Thickness(14),Spacing=5 };dialog.Content=panel;
        void Row(string title,Control field)
        {
            var row=new Grid { ColumnDefinitions=new("72,*"),ColumnSpacing=5 };
            row.Children.Add(new TextBlock { Text=title,Classes={"fieldLabel"} });AutomationProperties.SetName(field,title);
            Grid.SetColumn(field,1);row.Children.Add(field);panel.Children.Add(row);
        }
        var locale=new TextBox {Text=live?.Locale??defaults.Locale,MaxLength=64};
        var size=new NumericUpDown { Minimum=75,Maximum=200,Increment=5,Value=(decimal)((live?.TextScale??defaults.TextScale)*100) };
        var contrast=new CheckBox {Content="High contrast",IsChecked=live?.HighContrast??defaults.HighContrast};
        Row("Language",locale);Row("Text (%)",size);Row("Colors",contrast);
        var cancel=new Button {Content="Cancel",IsCancel=true};cancel.Click+=(_,_)=>dialog.Close();
        var save=new Button {Content=live==null?"Save defaults":"Apply in Play",IsDefault=true};
        save.Click+=(_,_)=>{if(!string.IsNullOrWhiteSpace(locale.Text)&&size.Value!=null)dialog.Close(true);};
        panel.Children.Add(new StackPanel {Orientation=Orientation.Horizontal,Spacing=6,HorizontalAlignment=HorizontalAlignment.Right,Children={cancel,save}});
        if(!await dialog.ShowDialog<bool>(this))return;
        var edited=new UiDefaults(locale.Text!.Trim(),(float)size.Value!.Value/100,contrast.IsChecked==true);
        if(live!=null){live.Locale=edited.Locale;live.TextScale=edited.TextScale;live.HighContrast=edited.HighContrast;}
        else {var settings=Scene.Settings with { Accessibility=edited };settings.Save(project!);Scene.Settings=settings; MarkDirty();Viewport.InvalidateUi();}
    }
}
