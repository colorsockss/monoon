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
        row("Mask", MaskPicker(() => [mask], change =>
        {
            uint next = change(mask);
            if (mask == next) return;
            setMask(mask = next);
            MarkDirty();
        }));
    }

    private static Button MaskPicker(Func<uint[]> read, Action<Func<uint, uint>> apply)
    {
        string Label(uint[] values)
        {
            uint first = values[0];
            if (values.Any(value => value != first)) return "Mixed";
            return first switch
            {
                0 => "None",
                uint.MaxValue => "All layers",
                _ when BitOperations.IsPow2(first) => $"Layer {BitOperations.TrailingZeroCount(first)}",
                _ => $"{BitOperations.PopCount(first)} layers · {first:X8}"
            };
        }
        var menu = new MenuFlyout();
        var button = new Button { Flyout = menu, HorizontalAlignment = HorizontalAlignment.Stretch };
        Avalonia.Automation.AutomationProperties.SetName(button, "Collision mask");
        var bits = new List<(uint Bit, MenuItem Item)>();
        void Refresh()
        {
            var values = read();
            button.Content = Label(values);
            foreach (var (bit, item) in bits)
            {
                bool all = values.All(mask => (mask & bit) != 0);
                bool any = values.Any(mask => (mask & bit) != 0);
                item.Header = $"{(all ? "✓" : any ? "−" : "　")}  {BitOperations.TrailingZeroCount(bit)}";
            }
        }
        MenuItem Item(string title, Action change)
        {
            var item = new MenuItem { Header = title };
            item.Click += (_, _) => { change(); Refresh(); };
            return item;
        }
        menu.Items.Add(Item("All layers", () => apply(_ => uint.MaxValue)));
        menu.Items.Add(Item("None", () => apply(_ => 0)));
        for (int group = 0; group < 32; group += 8)
        {
            var section = new MenuItem { Header = $"Layers {group}–{group + 7}" };
            menu.Items.Add(section);
            for (int i = group; i < group + 8; i++)
            {
                uint bit = 1u << i;
                var item = Item("", () => apply(CollisionMaskSelection.ToggleBit(read(), bit)));
                bits.Add((bit, item));
                section.Items.Add(item);
            }
        }
        Refresh();
        return button;
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
        void Invalid() { error.Text="Use 3–8 convex points: x,y; x,y; …"; error.IsVisible=true; }
        field.LostFocus+=(_,_)=>Commit();
        field.KeyDown+=(_,e)=> { if (e.Key==Key.Enter) { Commit(); e.Handled=true; } else if(e.Key==Key.Escape) { field.Text=Format();error.IsVisible=false;e.Handled=true; } };
        var editor=new StackPanel { Spacing=2 }; editor.Children.Add(field); editor.Children.Add(error);
        row("Points",editor);
    }
}
