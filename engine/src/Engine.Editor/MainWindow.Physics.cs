using System.Globalization;
using System.Numerics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private void CollisionFilterFields(int layer,uint mask,Action<int> setLayer,Action<uint> setMask,Action<string,Control> row,string layerLabel="Layer")
    {
        var field=new NumericUpDown { Value=layer, Minimum=0, Maximum=31, Increment=1, FormatString="0" };
        field.ValueChanged+=(_,_)=> { if (field.Value is {} n && n==(int)n && (int)n!=layer) { layer=(int)n; setLayer(layer); MarkDirty(field); } };
        row(layerLabel,field);
        CollisionMaskField(mask,setMask,row);
    }
    private void CollisionMaskField(uint mask,Action<uint> setMask,Action<string,Control> row)
    {
        string Label() => mask switch { 0=>"None", uint.MaxValue=>"All layers", _ when BitOperations.IsPow2(mask)=>$"Layer {BitOperations.TrailingZeroCount(mask)}", _=>$"{BitOperations.PopCount(mask)} layers · {mask:X8}" };
        var button=new Button { Content=Label(),HorizontalAlignment=HorizontalAlignment.Stretch };
        void Update(uint value) { if (value==mask) return; mask=value; setMask(mask);button.Content=Label(); MarkDirty(); }
        button.Click+=(_,_)=>
        {
            var menu=new MenuFlyout();
            void Item(ItemsControl parent,string title,Action action)
            {
                var item=new MenuItem { Header=title }; item.Click+=(_,_)=>action(); parent.Items.Add(item);
            }
            var all=new MenuItem { Header="All layers" }; all.Click+=(_,_)=>Update(uint.MaxValue);menu.Items.Add(all);
            var none=new MenuItem { Header="None" }; none.Click+=(_,_)=>Update(0);menu.Items.Add(none);
            for (int group=0;group<32;group+=8)
            {
                var section=new MenuItem { Header=$"Layers {group}–{group+7}" };menu.Items.Add(section);
                for (int i=group;i<group+8;i++)
                {
                    uint bit=1u<<i;
                    Item(section,((mask & bit)!=0?"✓  ":"　　 ")+i,()=>Update(mask^bit));
                }
            }
            menu.ShowAt(button);
        };
        row("Mask",button);
    }
    private void PolygonFields(PolygonCollider2DComponent polygon,Action<string,Control> row)
    {
        string Format() => string.Join("; ",polygon.Points.Select(p=>FormattableString.Invariant($"{p.X},{p.Y}")));
        var field=new TextBox { Text=Format(),TextWrapping=Avalonia.Media.TextWrapping.Wrap };
        ToolTip.SetTip(field,"Local size units (0–1 spans the object). Enter x,y; x,y; … in convex boundary order.");
        var error=new TextBlock { TextWrapping=Avalonia.Media.TextWrapping.Wrap,IsVisible=false };
        void Commit()
        {
            if (field.Text==Format()) { error.IsVisible=false; return; }
            var points=new List<Vector2>();
            foreach (var pair in (field.Text??"").Split(';'))
            {
                var xy=pair.Split(',');
                if (xy.Length!=2 || !float.TryParse(xy[0],NumberStyles.Float,CultureInfo.InvariantCulture,out var x) ||
                    !float.TryParse(xy[1],NumberStyles.Float,CultureInfo.InvariantCulture,out var y)) { Invalid(); return; }
                points.Add(new(x,y));
            }
            try { Shape2D.Polygon(points.ToArray()); }
            catch (ArgumentException) { Invalid(); return; }
            polygon.Points=points.Select(v=>new ColliderVertex(v.X,v.Y)).ToArray();
            field.Text=Format(); error.IsVisible=false; MarkDirty();
        }
        void Invalid() { error.Text="Use 3–32 convex points: x,y; x,y; …"; error.IsVisible=true; }
        field.LostFocus+=(_,_)=>Commit();
        field.KeyDown+=(_,e)=> { if (e.Key==Key.Enter) { Commit(); e.Handled=true; } else if(e.Key==Key.Escape) { field.Text=Format();error.IsVisible=false;e.Handled=true; } };
        var editor=new StackPanel { Spacing=2 }; editor.Children.Add(field); editor.Children.Add(error);
        row("Points",editor);
    }
}
