using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private Control PresentationFields(SceneComponent component)
    {
        var fields = new StackPanel { Spacing = 2 };
        void Row(string label, Control editor)
        {
            var row = new Grid { ColumnDefinitions = new("56,*"), ColumnSpacing = 5 };
            row.Children.Add(new TextBlock { Text = label, Classes = { "fieldLabel" } });
            Grid.SetColumn(editor, 1); row.Children.Add(editor); fields.Children.Add(row);
        }
        void Asset(string label, string suffix, Guid? current, Action<Guid?> set)
        {
            var items = new List<ObjectReference> { new(Guid.Empty, "None") };
            if (project != null) items.AddRange(ProjectAssets.Scan(project).Entries.Where(e => e.Path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                && File.Exists(Path.Combine(project, e.Path))).Select(e => new ObjectReference(e.Id, e.Path)));
            var picker = new ComboBox { ItemsSource = items, SelectedItem = items.FirstOrDefault(i => i.Id == (current ?? Guid.Empty)),
                HorizontalAlignment = HorizontalAlignment.Stretch };
            if (picker.SelectedItem is ObjectReference selected) ToolTip.SetTip(picker,selected.Label);
            picker.SelectionChanged += (_, _) => { if (picker.SelectedItem is ObjectReference item) { set(item.Id == Guid.Empty ? null : item.Id); MarkDirty(); } };
            Row(label, picker);
        }
        void Text(string label, string value, Action<string> set)
        {
            var box = new TextBox { Text = value };
            box.TextChanged += (_, _) => { set(box.Text ?? ""); MarkDirty(box); };
            Row(label, box);
        }
        void Number(string name, float value, decimal min, decimal max, Action<float> set)
        {
            var box = new NumericUpDown { Minimum = min, Maximum = max, Value = decimal.CreateSaturating(value) };
            box.ValueChanged += (_, _) => { if (box.Value is decimal n) { set((float)n); MarkDirty(box); } };
            Row(name, box);
        }
        void Choice<T>(string label,T current,Action<T> set) where T:struct,Enum
        {
            var control=new ComboBox {ItemsSource=Enum.GetValues<T>(),SelectedItem=current,HorizontalAlignment=HorizontalAlignment.Stretch};
            control.SelectionChanged+=(_,_)=>{ if(control.SelectedItem is T value) {set(value);MarkDirty();} };
            Row(label,control);
        }
        switch (component)
        {
            case CutsceneDirectorComponent director:
                Asset("Dialogue", CutsceneSequence.Extension, director.Sequence, v => director.Sequence = v);
                break;
            case PositionTween2DComponent tween:
                Number("Δ X",tween.OffsetX,-1000000,1000000,v=>tween.OffsetX=v);
                Number("Δ Y",tween.OffsetY,-1000000,1000000,v=>tween.OffsetY=v);
                Number("Time (s)",tween.Duration,.001m,86400,v=>tween.Duration=v);
                Choice("Ease",tween.Ease,v=>tween.Ease=v);
                Choice("Repeat",tween.Repeat,v=>tween.Repeat=v);
                var autoplayTween=new CheckBox {Content="Play on start",IsChecked=tween.PlayOnStart};
                autoplayTween.IsCheckedChanged+=(_,_)=>{tween.PlayOnStart=autoplayTween.IsChecked==true;MarkDirty();}; Row("",autoplayTween);
                break;
            case SortingGroup2DComponent group:
                Number("Layer", group.SortingLayer, -32768, 32767, v => group.SortingLayer = (int)v);
                Number("Order", group.Order, -32768, 32767, v => group.Order = (int)v);
                var byY = new CheckBox { Content = "Use foot Y", IsChecked = group.SortByY };
                Row("Y sort", byY);
                Number("Foot ΔY", group.FootOffset, -1000000, 1000000, v => group.FootOffset = v);
                var offsetRow = fields.Children[^1];
                offsetRow.IsEnabled = group.SortByY;
                ToolTip.SetTip(offsetRow, "Local Y offset from the bottom center, in world units.");
                byY.IsCheckedChanged += (_, _) =>
                {
                    group.SortByY = byY.IsChecked == true;
                    offsetRow.IsEnabled = group.SortByY;
                    MarkDirty();
                };
                break;
            case SkeletonAnimator2DComponent skeleton:
                Asset("Rig",SkeletonRig.Extension,skeleton.Rig,v=>skeleton.Rig=v);
                if (Selected?.GetComponent<SpriteRendererComponent>()==null)
                {
                    Number("Layer",skeleton.SortingLayer,-32768,32767,v=>skeleton.SortingLayer=(int)v);
                    Number("Order",skeleton.Order,-32768,32767,v=>skeleton.Order=(int)v);
                }
                Choice("Clock",skeleton.Clock,v=>{skeleton.Clock=v;RefreshComponentList();});
                if(skeleton.Clock==SkeletonClock.Travel) Number("Stride",skeleton.Stride,.1m,16384,v=>skeleton.Stride=v);
                else Number("Cycle (s)",skeleton.Period,.001m,86400,v=>skeleton.Period=v);
                break;
            case FootIK2DComponent feet:
                Number("Probe",feet.Probe,.02m,16384,v=>feet.Probe=v);
                Number("Lift",feet.Lift,0,4096,v=>feet.Lift=v);
                CollisionMaskField(feet.Mask,v=>feet.Mask=v,Row);
                break;
            case SpriteShader2DComponent shader:
                Asset("Shader",ShaderClip.Extension,shader.Shader,v=>shader.Shader=v);
                break;
            case ParticleEmitter2DComponent emitter:
                var triggers=new ComboBox {ItemsSource=Enum.GetValues<ParticleTrigger>(), SelectedItem=emitter.Trigger, HorizontalAlignment=HorizontalAlignment.Stretch};
                triggers.SelectionChanged+=(_,_)=>{ if(triggers.SelectedItem is ParticleTrigger value) { emitter.Trigger=value; MarkDirty(); } };
                Row("Trigger",triggers);
                Number("Count",emitter.Count,1,256,v=>emitter.Count=(int)v);
                Number("Life (s)",emitter.Lifetime,.01m,60,v=>emitter.Lifetime=v);
                Number("Speed",emitter.Speed,0,4096,v=>emitter.Speed=v);
                Number("Gravity",emitter.Gravity,-4096,4096,v=>emitter.Gravity=v);
                Number("Size",emitter.Size,.1m,256,v=>emitter.Size=v);
                Text("Color",emitter.Color,v=>{ if(SceneObject.TryColor(v,out _)) emitter.Color=v; });
                break;
            case PointLight2DComponent light:
                Number("Radius",light.Radius,.1m,16384,v=>light.Radius=v);
                Number("Intensity",light.Intensity,0,1,v=>light.Intensity=v);
                Number("Flicker",light.Flicker,0,1,v=>light.Flicker=v);
                Number("Hz",light.Frequency,0,30,v=>light.Frequency=v);
                Text("Color",light.Color,v=>{ if(SceneObject.TryColor(v,out _)) light.Color=v; });
                break;
            case NavigationRegion2DComponent nav:
                Number("Spacing",nav.Spacing,4,128,v=>nav.Spacing=v);
                Number("Agent W",nav.AgentWidth,1,4096,v=>nav.AgentWidth=v);
                Number("Agent H",nav.AgentHeight,1,4096,v=>nav.AgentHeight=v);
                Number("Slope °",nav.MaxSlope,0,80,v=>nav.MaxSlope=v);
                CollisionMaskField(nav.TerrainMask,v=>nav.TerrainMask=v,Row);
                break;
            case Rigidbody2DComponent rigid:
                var types = new ComboBox { ItemsSource = Enum.GetValues<BodyType2D>(), SelectedItem = rigid.BodyType, HorizontalAlignment = HorizontalAlignment.Stretch };
                types.SelectionChanged += (_, _) => { if (types.SelectedItem is BodyType2D type) { rigid.BodyType = type; MarkDirty(); RefreshComponentList(); } };
                Row("Body type", types);
                if (rigid.BodyType == BodyType2D.Dynamic)
                {
                    Number("Mass", rigid.Mass, .001m, 1000000, v => rigid.Mass = v);
                    Number("Gravity", rigid.Gravity, -100000, 100000, v => rigid.Gravity = v);
                    Number("Bounce", rigid.Restitution, 0, 1, v => rigid.Restitution = v);
                }
                if (rigid.BodyType != BodyType2D.Static)
                {
                    Number("Speed X", rigid.VelocityX, -100000, 100000, v => rigid.VelocityX = v);
                    Number("Speed Y", rigid.VelocityY, -100000, 100000, v => rigid.VelocityY = v);
                    Number("Spin °/s", rigid.AngularVelocity, -36000, 36000, v => rigid.AngularVelocity = v);
                    var freeze = new CheckBox { Content = "Freeze rotation", IsChecked = rigid.FreezeRotation };
                    freeze.IsCheckedChanged += (_, _) => { rigid.FreezeRotation = freeze.IsChecked == true; MarkDirty(); };
                    Row("", freeze);
                }
                break;
            case DistanceJoint2DComponent joint:
                var targets = new List<ObjectReference> { new(Guid.Empty, "None") };
                targets.AddRange(Scene.Objects.Where(o => o.Id != Selected?.Id).Select(o => new ObjectReference(o.Id, ObjectPath(o))));
                var connected = new ComboBox { ItemsSource = targets, SelectedItem = targets.Find(o => o.Id == (joint.Connected ?? Guid.Empty)), HorizontalAlignment = HorizontalAlignment.Stretch };
                connected.SelectionChanged += (_, _) => { if (connected.SelectedItem is ObjectReference target) { joint.Connected = target.Id == Guid.Empty ? null : target.Id; MarkDirty(); } };
                Row("Connect", connected);
                Number("Length", joint.Length, 0, 1000000, v => joint.Length = v);
                break;
            case CharacterMotor2DComponent motor:
                Number("Slope °", motor.MaxSlope, 0, 89.9m, v => motor.MaxSlope = v);
                Number("Snap", motor.GroundSnap, 0, 1000, v => motor.GroundSnap = v);
                break;
            case Camera2DComponent camera:
                CameraInspector(camera,Row); break;
            case Collider2DComponent collider:
                CollisionFilterFields(collider.Layer, collider.Mask, v=>collider.Layer=v, v=>collider.Mask=v, Row);
                var trigger = new CheckBox { Content="Is Trigger", IsChecked=collider.IsTrigger };
                trigger.IsCheckedChanged += (_,_) => { collider.IsTrigger=trigger.IsChecked==true; MarkDirty(); };
                Row("",trigger);
                if (collider is CircleCollider2DComponent circle)
                    Number("Radius",circle.Radius,0,1000000,v=>circle.Radius=v);
                if (collider is PolygonCollider2DComponent polygon)
                    PolygonFields(polygon,Row);
                break;
            case Tilemap2DComponent map:
                CollisionFilterFields(map.CollisionLayer,map.CollisionMask,v=>map.CollisionLayer=v,v=>map.CollisionMask=v,Row,"Physics");
                Asset("Tileset",ProjectAssets.Tileset,map.Tileset,id=>AssignTileset(map,id));
                var layouts = new[] { TileGrid.Orthogonal, TileGrid.Isometric, TileGrid.HexPointy, TileGrid.HexFlat };
                var layout = new ComboBox
                {
                    ItemsSource = new[] { "Orthogonal", "Isometric", "Hex · Pointy", "Hex · Flat" },
                    SelectedIndex = Array.IndexOf(layouts,map.Layout),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                layout.SelectionChanged += (_, _) =>
                {
                    if (layout.SelectedIndex < 0 || layouts[layout.SelectedIndex] == map.Layout) return;
                    Viewport.CancelStroke();
                    Viewport.LeaveTiles();
                    map.Layout = layouts[layout.SelectedIndex];
                    MarkDirty();
                };
                Row("Layout",layout);
                Number("Cell W",map.CellSize.Width,1,65535,n=>map.CellSize=map.CellSize with { Width=(int)n });
                Number("Cell H",map.CellSize.Height,1,65535,n=>map.CellSize=map.CellSize with { Height=(int)n });
                Number("Layer",map.SortingLayer,-32768,32767,n=>map.SortingLayer=(int)n);
                Number("Order",map.Order,-32768,32767,n=>map.Order=(int)n);
                var enable = new CheckBox { Content="Stream chunks", IsChecked=map.StreamRadius.HasValue };
                enable.IsCheckedChanged += (_,_) => { map.StreamRadius = enable.IsChecked == true ? 1 : null; MarkDirty(); RefreshComponentList(); };
                Row("",enable);
                if (map.StreamRadius is int radius)
                {
                    var focusTargets = new List<ObjectReference> { new(Guid.Empty, "Active camera") };
                    focusTargets.AddRange(Scene.Objects.Select(o => new ObjectReference(o.Id,ObjectPath(o))));
                    var focus = new ComboBox { ItemsSource=focusTargets, SelectedItem=focusTargets.Find(t => t.Id==(map.StreamTarget ?? Guid.Empty)), HorizontalAlignment=HorizontalAlignment.Stretch };
                    focus.SelectionChanged += (_,_) => { if (focus.SelectedItem is ObjectReference t) { map.StreamTarget=t.Id==Guid.Empty ? null : t.Id; MarkDirty(); } };
                    Row("Follow",focus);
                    Number("Radius",radius,1,16,v=>map.StreamRadius=(int)v);
                }
                TileTools(map,Row);
                break;
            case SpriteAnimatorComponent animator:
                Asset("Controller", AnimationController.Extension, animator.Controller, id => { animator.Controller = id; RefreshComponentList(); });
                if (animator.Controller == null) Asset("Clip", ProjectAssets.Clip, animator.Clip, id => animator.Clip = id);
                else if (project != null && ProjectAssets.ReadManifest(project).PathFor(animator.Controller.Value) is {} controller)
                {
                    var open = new Button { Content = "Edit Controller…" };
                    open.Click += (_,_) => RunAction(() => EditAnimator(controller));
                    Row("",open);
                }
                var auto = new CheckBox { Content = "Play on start", IsChecked = animator.PlayOnStart };
                auto.IsCheckedChanged += (_, _) => { animator.PlayOnStart = auto.IsChecked == true; MarkDirty(); };
                Row("", auto);
                break;
            case AudioSourceComponent audio: Asset("Sound", ".wav", audio.Sound, id => audio.Sound = id); SourceFields(audio,Row); break;
            case AudioListener2DComponent:
                Row("Listen at",new TextBlock { Text="Object center",Classes={"fieldLabel"} }); break;
            case UiCanvasComponent canvas:
                Number("Order", canvas.Order, -32768, 32767, n => canvas.Order = (int)n);
                break;
            case UiRectComponent rect:
                var anchor = new ComboBox { ItemsSource = ScreenUi.Anchors, SelectedItem = rect.Anchor, HorizontalAlignment = HorizontalAlignment.Stretch };
                anchor.SelectionChanged += (_, _) => { if (anchor.SelectedItem is string a) { rect.Anchor = a; MarkDirty(); } };
                Row("Anchor", anchor);
                Text("Show if", rect.ShowIf, s => rect.ShowIf = s);
                var modeRoot = new CheckBox { Content = "Input mode root", IsChecked = rect.InputModeRoot };
                modeRoot.IsCheckedChanged += (_, _) => { rect.InputModeRoot = modeRoot.IsChecked == true; MarkDirty(); };
                Row("", modeRoot);
                break;
            case UiImageComponent image:
                Asset("Sprite", ".png", image.Sprite, id => image.Sprite = id);
                Text("Fill", image.Color, s => { if (SceneObject.TryColor(s, out _)) image.Color = s; });
                break;
            case UiLayoutComponent stack:
                var axes = new ComboBox { ItemsSource = new[] { "Vertical", "Horizontal" }, SelectedItem = stack.Axis, HorizontalAlignment = HorizontalAlignment.Stretch };
                axes.SelectionChanged += (_,_) => { if (axes.SelectedItem is string axis) { stack.Axis=axis; MarkDirty(); } };
                Row("Axis", axes);
                Number("Gap", stack.Gap, 0, 16384, v => stack.Gap=v);
                Number("Padding", stack.Padding, 0, 16384, v => stack.Padding=v);
                var stretch=new CheckBox { Content="Stretch children", IsChecked=stack.Stretch };
                stretch.IsCheckedChanged+=(_,_)=>{ stack.Stretch=stretch.IsChecked==true; MarkDirty(); };
                Row("",stretch);
                break;
            case UiScrollComponent:
                Row("Scroll", new TextBlock { Text="Clip and reveal children", TextWrapping=Avalonia.Media.TextWrapping.Wrap, Classes={"fieldLabel"} });
                break;
            case UiTextFieldComponent field:
                Text("Value",field.Value,s=>field.Value=s);
                Number("Limit",field.MaxLength,1,4096,v=>field.MaxLength=(int)v);
                break;
            case UiTextComponent label:
                Text("Text key", label.TextKey ?? "", s => label.TextKey = string.IsNullOrWhiteSpace(s) ? null : s.Trim());
                Text("Text", label.Text, s => label.Text = s);
                var translations=new TextBox { AcceptsReturn=true, MinHeight=28, MaxHeight=110,
                    Watermark="ko=안녕하세요", Text=string.Join("\n", (label.Translations??[]).Select(p=>p.Key+"="+p.Value)) };
                ToolTip.SetTip(translations,"One language tag and translation per line (ko=안녕하세요).");
                translations.TextChanged+=(_,_)=>{
                    var pairs=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                    foreach(var line in (translations.Text??"").Split('\n',StringSplitOptions.RemoveEmptyEntries)){
                        int at=line.IndexOf('=');if(at<=0||!pairs.TryAdd(line[..at].Trim(),line[(at+1)..].TrimEnd('\r')))return;
                    }
                    label.Translations=pairs.Count==0?null:pairs;MarkDirty(translations);
                };
                Row("Locales",translations);
                var wrap=new CheckBox { Content="Wrap lines",IsChecked=label.Wrap };
                wrap.IsCheckedChanged+=(_,_)=>{label.Wrap=wrap.IsChecked==true;MarkDirty();};Row("",wrap);
                Number("Size", label.Size, 1, 1024, n => label.Size = n);
                Text("Color", label.Color, s => { if (SceneObject.TryColor(s, out _)) label.Color = s; });
                Number("Inset X", label.InsetX, -16384, 16384, n => label.InsetX = n);
                Number("Inset Y", label.InsetY, -16384, 16384, n => label.InsetY = n);
                break;
            case UiButtonComponent button:
                var enabled = new CheckBox { Content = "Interactable", IsChecked = button.Interactable };
                enabled.IsCheckedChanged += (_, _) => { button.Interactable = enabled.IsChecked == true; MarkDirty(); };
                Row("", enabled);
                break;
        }
        return fields;
    }
}
