using Avalonia.Controls;
using Avalonia.Layout;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private void CameraInspector(Camera2DComponent camera,Action<string,Control> row)
    {
        NumericUpDown Field(float value,decimal min,decimal max,decimal increment,Action<float> change)
        {
            var n=new NumericUpDown { Minimum=min, Maximum=max, Increment=increment, Value=decimal.CreateSaturating(value) };
            n.ValueChanged+=(_,_)=> { if (n.Value is {} v && (float)v!=value) { value=(float)v;change(value);MarkDirty(n); } };
            return n;
        }
        ComboBox Choices<T>(T[] values,T current,Action<T> set) where T:notnull
        {
            var picker=new ComboBox { ItemsSource=values, SelectedItem=current, HorizontalAlignment=HorizontalAlignment.Stretch };
            picker.SelectionChanged+=(_,_)=> { if (picker.SelectedItem is T selected && !Equals(current,selected))
                { current=selected; set(selected); MarkDirty(picker); } };
            return picker;
        }
        row("Output",Choices([Camera2DComponent.Primary,Camera2DComponent.Extra,Camera2DComponent.Minimap],camera.Output,
            v=>{ camera.Output=v; if (v!=Camera2DComponent.Primary) camera.SplitWith=null; RefreshComponentList(); }));
        if (camera.Output==Camera2DComponent.Primary)
        {
            bool start=Inspected?.Id==Scene.Camera?.Id;
            var button=new Button { Content=start?"Start camera":"Set as start camera",IsEnabled=!start };
            button.Click+=SetStartCamera;row("",button);
        }
        else row("Order",Field(camera.Order,-32768,32767,1,v=>camera.Order=(int)v));
        if (camera.Output==Camera2DComponent.Minimap)
        {
            var frames=new CheckBox { Content="Show view frames", IsChecked=camera.ShowViews };
            frames.IsCheckedChanged+=(_,_)=>{ camera.ShowViews=frames.IsChecked==true; MarkDirty(); };
            row("",frames);
        }
        row("Zoom",Field(camera.Zoom,.05m,32,.05m,v=>camera.Zoom=v));
        var objects=new List<ObjectReference> { new(Guid.Empty,"None") };
        objects.AddRange((Viewport.PlayWorld?.Objects??Scene.Objects).Where(o=>o.Id!=Inspected?.Id)
            .Select(o=>new ObjectReference(o.Id,ObjectPath(o))));
        var current=objects.Find(x=>x.Id==(camera.Follow??Guid.Empty));
        if (current==null) { current=new(camera.Follow!.Value,"Missing target"); objects.Add(current); }
        var follow=Choices(objects.ToArray(),current,o=>camera.Follow=o.Id==Guid.Empty?null:o.Id);
        ToolTip.SetTip(follow,"Follow the object's world center."); row("Follow",follow);
        row("Half-life",Field(camera.HalfLife,0,3600,.05m,v=>camera.HalfLife=v));
        // 16.11: the dead zone pair shares the field column like Bounds' X / Y.
        var zone=new Grid { ColumnDefinitions=new("*,*"), ColumnSpacing=3 };
        var zoneWidth=Field(camera.DeadZoneWidth,0,1000000,1,v=>camera.DeadZoneWidth=v);
        var zoneHeight=Field(camera.DeadZoneHeight,0,1000000,1,v=>camera.DeadZoneHeight=v);
        zoneWidth.ShowButtonSpinner=zoneHeight.ShowButtonSpinner=false; Grid.SetColumn(zoneHeight,1); zone.Children.Add(zoneWidth); zone.Children.Add(zoneHeight);
        ToolTip.SetTip(zone,"World pixels the target can move before the camera follows."); row("Deadzone",zone);
        var lead=Field(camera.LookAhead,0,(decimal)Camera2DComponent.MaxLookAhead,.05m,v=>camera.LookAhead=v);
        ToolTip.SetTip(lead,"Look-ahead: seconds of the target's velocity to aim ahead."); row("Lead time",lead);
        var group=new TextBox { Text=camera.FrameGroup, Watermark="No group" };
        ToolTip.SetTip(group,"Object group to keep in view together with Follow. The camera zooms out to fit it.");
        group.LostFocus+=(_,_)=> { var v=string.IsNullOrWhiteSpace(group.Text)?null:group.Text.Trim(); if (v!=camera.FrameGroup) { camera.FrameGroup=v; MarkDirty(group); } };
        row("Frame",group);
        var padding=Field(camera.FramePadding,0,1000000,1,v=>camera.FramePadding=v);
        ToolTip.SetTip(padding,"World pixels kept around the framed group."); row("Padding",padding);
        Box("Bounds",()=>camera.Bounds,b=>camera.Bounds=b,new(0,0,Scene.Settings.Width,Scene.Settings.Height),false);
        Box("Viewport",()=>camera.Viewport,b=>camera.Viewport=b,camera.DefaultViewport(),true);
        if (camera.Output==Camera2DComponent.Primary) SplitRows();

        // Merging split screen: an Extra camera to share the viewports with, the merge / split distances and the blend time.
        void SplitRows()
        {
            var partners=new List<ObjectReference> { new(Guid.Empty,"None") };
            partners.AddRange((Viewport.PlayWorld?.Objects??Scene.Objects).Where(o=>o.GetComponent<Camera2DComponent>()?.Output==Camera2DComponent.Extra)
                .Select(o=>new ObjectReference(o.Id,ObjectPath(o))));
            var paired=partners.Find(x=>x.Id==(camera.SplitWith??Guid.Empty));
            if (paired==null) { paired=new(camera.SplitWith!.Value,"Missing camera"); partners.Add(paired); }
            var pick=Choices(partners.ToArray(),paired,o=>
            {
                camera.SplitWith=o.Id==Guid.Empty?null:o.Id;
                // Start from distances that fit the game view: merge within 40% of its width, split past 50%.
                if (camera.SplitWith!=null && camera.MergeDistance==0)
                    (camera.MergeDistance,camera.SplitDistance,camera.BlendSeconds)=(Scene.Settings.Width*.4f/camera.Zoom,Scene.Settings.Width*.5f/camera.Zoom,.5f);
                RefreshComponentList();
            });
            ToolTip.SetTip(pick,"Extra camera that splits this view left and right when the two targets move apart.");
            row("Split with",pick);
            if (camera.SplitWith==null) return;
            var merge=Field(camera.MergeDistance,.01m,1000000,1,v=>camera.MergeDistance=v);
            ToolTip.SetTip(merge,"World pixels between the targets: closer than this merges the views."); row("Merge at",merge);
            var split=Field(camera.SplitDistance,.01m,1000000,1,v=>camera.SplitDistance=v);
            ToolTip.SetTip(split,"World pixels between the targets: farther than this splits the views (at least Merge at)."); row("Split at",split);
            var blend=Field(camera.BlendSeconds,0,(decimal)Camera2DComponent.MaxBlendSeconds,.05m,v=>camera.BlendSeconds=v);
            ToolTip.SetTip(blend,"Seconds the views take to merge or split."); row("Blend",blend);
        }

        // Each coordinate pair shares the existing inspector field column.
        void Box(string label,Func<CameraRect?> read,Action<CameraRect?> write,CameraRect fallback,bool normalized)
        {
            var check=new CheckBox { Content=normalized?"Custom region":"Limit to map", IsChecked=read()!=null };
            row(label,check);
            check.IsCheckedChanged+=(_,_)=> { write(check.IsChecked==true?fallback:null); MarkDirty(); RefreshComponentList(); };
            if (read() is not {} initial) return;
            Pair("X / Y",true);Pair("W / H",false);
            void Pair(string name,bool origin)
            {
                var grid=new Grid { ColumnDefinitions=new("*,*"), ColumnSpacing=3 };
                for (int i=0;i<2;i++)
                {
                    bool x=i==0;
                    float val=origin?(x?initial.X:initial.Y):(x?initial.Width:initial.Height);
                    decimal min=origin?(normalized?0:-1000000000m):(normalized?.001m:.01m);
                    var field=Field(val,min,normalized?1:1000000000m,normalized?.01m:1,v=>
                    {
                        if (read() is not {} r) return;
                        var next=origin?(x?r with { X=v }:r with { Y=v }):(x?r with { Width=v }:r with { Height=v });
                        if (normalized)
                        {
                            if (origin)
                            {
                                float px=Math.Min(next.X,.999f),py=Math.Min(next.Y,.999f);
                                next=next with { X=px,Y=py,Width=Math.Min(next.Width,1-px),Height=Math.Min(next.Height,1-py) };
                            }
                            else next=next with { Width=Math.Min(next.Width,1-next.X),Height=Math.Min(next.Height,1-next.Y) };
                        }
                        write(next);
                        // Refresh if clamping changed another displayed coordinate.
                        if (normalized) Avalonia.Threading.Dispatcher.UIThread.Post(RefreshIfAdjusted);
                        void RefreshIfAdjusted() { if (Inspected?.GetComponent<Camera2DComponent>()==camera &&
                            (next.Width!=r.Width && origin || next.Height!=r.Height && origin ||
                             (origin?(x?next.X:next.Y):(x?next.Width:next.Height))!=v)) RefreshComponentList(); }
                    });
                    field.ShowButtonSpinner=false;
                    ToolTip.SetTip(field,$"{label} {(origin?(x?"X":"Y"):(x?"width":"height"))} ({(normalized?"0–1":"world pixels")})");
                    Grid.SetColumn(field,i);grid.Children.Add(field);
                }
                row(name,grid);
            }
        }
    }
}
