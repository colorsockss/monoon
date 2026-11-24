using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private void SliceFields(UiImageComponent image, Action<string,Control> row)
    {
        var path = image.Sprite is Guid id && project != null ? ProjectAssets.ReadManifest(project).PathFor(id) : null;
        System.Drawing.Size size = default;
        if (path != null)
        {
            try { size = SpriteRegion.ReadPngSize(Path.Combine(project!, path)); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { Status.Text = ex.Message; }
        }
        var use = new CheckBox {Content="9-slice", IsChecked=image.Borders != null, IsEnabled=image.Borders!=null || size.Width>0 && size.Height>0};
        row("Mode",use);
        use.IsCheckedChanged += (_,_) =>
        {
            int x = Math.Min(8,(size.Width-1)/2), y = Math.Min(8,(size.Height-1)/2);
            image.Borders = use.IsChecked == true ? new(x,y,x,y) : null;
            MarkDirty(); RefreshComponentList();
        };
        if (image.Borders is not {} edges || size.IsEmpty) return;
        try { edges.Validate(size); }
        catch (InvalidDataException ex) { Status.Text = ex.Message; return; }
        Pair("Left","Right",edges.Left,edges.Right,size.Width,(a,b)=>image.Borders=image.Borders! with {Left=a,Right=b});
        Pair("Top","Bottom",edges.Top,edges.Bottom,size.Height,(a,b)=>image.Borders=image.Borders! with {Top=a,Bottom=b});

        void Pair(string firstLabel,string lastLabel,int a,int b,int extent,Action<int,int> apply)
        {
            NumericUpDown Field(int value,int max) => new() {Minimum=0, Maximum=max, Value=value,
                Increment=1, FormatString="0", HorizontalAlignment=HorizontalAlignment.Stretch, ShowButtonSpinner=false};
            var first=Field(a,extent-1-b); var last=Field(b,extent-1-a);
            AutomationProperties.SetName(first,firstLabel+" slice border");
            AutomationProperties.SetName(last,lastLabel+" slice border");
            ToolTip.SetTip(first,firstLabel+" border in PNG pixels"); ToolTip.SetTip(last,lastLabel+" border in PNG pixels");
            var pair=new Grid {ColumnDefinitions=new("*,*"),ColumnSpacing=4};
            pair.Children.Add(first);Grid.SetColumn(last,1);pair.Children.Add(last);row(firstLabel[0]+" / "+lastLabel[0],pair);
            void Changed(NumericUpDown field)
            {
                if(first.Value is not {} start || last.Value is not {} end ||
                    start!=decimal.Truncate(start) || end!=decimal.Truncate(end) || start+end>=extent) return;
                first.Maximum=extent-1-end; last.Maximum=extent-1-start;
                apply((int)start,(int)end);MarkDirty(field);
            }
            first.ValueChanged+=(_,_)=>Changed(first);last.ValueChanged+=(_,_)=>Changed(last);
        }
    }
}
