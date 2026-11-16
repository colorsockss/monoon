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
        var dialog=new Window {Title="UI Accessibility",Width=440,MinWidth=340,Height=live==null?410:225,MinHeight=205,WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false};
        var panel=new StackPanel { Margin=new Thickness(14),Spacing=5 };dialog.Content=new ScrollViewer {Content=panel};
        void Row(string title,Control field, StackPanel? container=null)
        {
            var row=new Grid { ColumnDefinitions=new("98,*"),ColumnSpacing=5 };
            row.Children.Add(new TextBlock { Text=title,Classes={"fieldLabel"} });AutomationProperties.SetName(field,title);
            Grid.SetColumn(field,1);row.Children.Add(field);(container??panel).Children.Add(row);
        }
        var locale=new TextBox {Text=live?.Locale??defaults.Locale,MaxLength=64};
        var size=new NumericUpDown { Minimum=75,Maximum=200,Increment=5,Value=(decimal)((live?.TextScale??defaults.TextScale)*100) };
        var contrast=new CheckBox {Content="High contrast",IsChecked=live?.HighContrast??defaults.HighContrast};
        Row("Language",locale);Row("Text (%)",size);Row("Colors",contrast);
        var config=Scene.Settings.Localization ?? new LocalizationSettings();
        LocalizationSettings? selectedConfig=Scene.Settings.Localization;
        var entries=ProjectAssets.Scan(project!).Entries;
        ComboBox Picker(Func<string,bool> accepts,Guid? selected)
        {
            var options=new List<ObjectReference>{new(Guid.Empty,"None")};
            options.AddRange(entries.Where(e=>accepts(e.Path)).Select(e=>new ObjectReference(e.Id,e.Path)));
            if(selected is Guid id && options.All(o=>o.Id!=id))options.Add(new(id,"Missing: "+id));
            var picker=new ComboBox {ItemsSource=options,SelectedItem=options.FirstOrDefault(o=>o.Id==(selected??Guid.Empty)),
                HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch};
            void Tip()=>ToolTip.SetTip(picker,(picker.SelectedItem as ObjectReference)?.Label);
            Tip();picker.SelectionChanged+=(_,_)=>Tip();
            return picker;
        }
        Guid? SelectedId(ComboBox picker)=>picker.SelectedItem is ObjectReference o && o.Id!=Guid.Empty ? o.Id : null;
        var table=Picker(p=>p.EndsWith(GameTable.Extension,StringComparison.Ordinal),config.Table);
        var fallbackLocale=new TextBox {Text=config.FallbackLocale,MaxLength=64};
        var primary=Picker(GameLocalization.IsFont,config.DefaultFont);
        var fallbacks=new List<ComboBox>();
        var fontRows=new StackPanel {Spacing=5};
        void AddFallback(Guid? id)
        {
            var picker=Picker(GameLocalization.IsFont,id);
            var remove=new Button {Content="−"};
            var line=new Grid {ColumnDefinitions=new("*,Auto"),ColumnSpacing=5};
            line.Children.Add(picker);Grid.SetColumn(remove,1);line.Children.Add(remove);
            Row("Fallback font",line,fontRows);var row=fontRows.Children[^1];
            fallbacks.Add(picker);
            remove.Click+=(_,_)=>{fallbacks.Remove(picker);fontRows.Children.Remove(row);};
        }
        if(live==null)
        {
            Row("Text table",table);Row("Fallback locale",fallbackLocale);Row("Default font",primary);
            panel.Children.Add(fontRows);
            foreach(var id in config.FallbackFonts??[])AddFallback(id);
            var add=new Button {Content="+ Fallback font"};add.Click+=(_,_)=>AddFallback(null);Row("",add);
        }
        var error=new TextBlock {TextWrapping=Avalonia.Media.TextWrapping.Wrap,IsVisible=false};
        panel.Children.Add(error);
        var cancel=new Button {Content="Cancel",IsCancel=true};cancel.Click+=(_,_)=>dialog.Close();
        var save=new Button {Content=live==null?"Save defaults":"Apply in Play",IsDefault=true};
        save.Click+=(_,_)=>{
            if(string.IsNullOrWhiteSpace(locale.Text)||size.Value==null)return;
            try
            {
                if(live==null)
                {
                    var fonts=fallbacks.Select(SelectedId).OfType<Guid>().ToArray();
                    selectedConfig=SelectedId(table)==null && SelectedId(primary)==null && fonts.Length==0 ? null :
                        new LocalizationSettings {Table=SelectedId(table),FallbackLocale=fallbackLocale.Text?.Trim()??"",
                            DefaultFont=SelectedId(primary),FallbackFonts=fonts};
                    GameLocalization.Load(project!,ProjectAssets.ReadManifest(project!),selectedConfig);
                }
                dialog.Close(true);
            }
            catch(Exception ex) when(ex is IOException or InvalidDataException or ArgumentException)
            {error.Text=ex.Message;error.IsVisible=true;}
        };
        panel.Children.Add(new StackPanel {Orientation=Orientation.Horizontal,Spacing=6,HorizontalAlignment=HorizontalAlignment.Right,Children={cancel,save}});
        if(!await dialog.ShowDialog<bool>(this))return;
        var edited=new UiDefaults(locale.Text!.Trim(),(float)size.Value!.Value/100,contrast.IsChecked==true);
        if(live!=null){live.Locale=edited.Locale;live.TextScale=edited.TextScale;live.HighContrast=edited.HighContrast;}
        else {var settings=Scene.Settings with { Accessibility=edited, Localization=selectedConfig };settings.Save(project!);Scene.Settings=settings; MarkDirty();Viewport.InvalidateUi();}
    }
}
