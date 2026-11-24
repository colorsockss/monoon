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
        void Row(string label, Control editor) => InspectorRow(fields, label, editor);
        // Object fields (13.7/13.17): drop, pick or clear is one Undo step, then the section shows the new value.
        void Asset(string label, string suffix, Guid? current, Action<Guid?> set, Func<string, bool>? accepts = null) =>
            Row(label, AssetField(label, accepts ?? EndsWith(suffix), current, id => { if (id == current) return; set(id); MarkDirty(); RefreshComponentList(); }));
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
        // 13.9: swatch + ColorPicker popup + hex text. Dragging in the popup and typing apply live; closing the popup, Enter or
        // leaving the text records one Undo step.
        void Color(string label, string value, Action<string> set)
        {
            var field = new ColorField(label, value, alpha: false);
            field.Changed += (hex, commit) => { set(hex); if (commit) MarkDirty(); else Viewport.InvalidateUi(); };
            Row(label, field);
        }
        void Sorting(Func<int> layer, Action<int> setLayer, Func<int> order, Action<int> setOrder)
        {
            if (Inspected?.GetComponent<SpriteRendererComponent>() != null) return; // The sprite's slot is shared.
            Number("Layer", layer(), -32768, 32767, v => setLayer((int)v));
            Number("Order", order(), -32768, 32767, v => setOrder((int)v));
        }
        // The point selected in the Scene view; Add, Delete and Smooth/Corner each record one undo step.
        void PathRows(PathPoint[] points, Action<PathPoint[]> set, bool pinFirst, bool curves = true)
        {
            if (points.Length == 0) return;
            int index = Math.Clamp(Viewport.PathIndex, 0, points.Length - 1);
            Row("Point", ValueLabel($"{index + 1} / {points.Length}"));
            void Coordinate(string name, float value, Func<PathPoint, float, PathPoint> with)
            {
                Number(name, value, -1000000, 1000000, v => { var copy = (PathPoint[])points.Clone(); copy[index] = with(copy[index], v); points = copy; set(copy); });
                fields.Children[^1].IsEnabled = !(pinFirst && index == 0) && !Viewport.IsPlaying;
            }
            Coordinate("X", points[index].X, (p, v) => p with { X = v });
            Coordinate("Y", points[index].Y, (p, v) => p with { Y = v });
            // Icon buttons keep Add and Delete narrow so Smooth/Corner fits the value column at the Inspector's minimum width.
            var buttons = new Grid { ColumnDefinitions = new("Auto,Auto,*"), ColumnSpacing = EditorTokens.FieldGap };
            void Action(int column, Button button, Func<PathEdit, int, (PathPoint[], int)?> change)
            {
                button.Click += (_, _) => Viewport.EditPath(change);
                Grid.SetColumn(button, column);
                buttons.Children.Add(button);
            }
            Action(0, EditorTokens.IconButton("IconAdd", "Add point"), (path, i) => (path.Insert(i), i + 1));
            Action(1, EditorTokens.IconButton("IconClose", "Delete point"), (path, i) => path.CanRemove(i) ? (path.Remove(i), Math.Max(0, i - 1)) : null);
            var shape = new Button { Content = PathEdit.Smooth(points[index]) ? "Corner" : "Smooth",
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
            ToolTip.SetTip(shape, "Add or remove tangent handles");
            if (curves) Action(2, shape, (path, i) => (path.ToggleSmooth(i), i));
            buttons.IsEnabled = !Viewport.IsPlaying;
            Row("", buttons);
        }
        switch (component)
        {
            case Line2DComponent line:
                Number("Width", line.Width, .01m, 1024, v => line.Width = v);
                Color("Start color", line.StartColor, v => line.StartColor = v);
                Color("End color", line.EndColor, v => line.EndColor = v);
                Sorting(() => line.SortingLayer, v => line.SortingLayer = v, () => line.Order, v => line.Order = v);
                // The Scene view edits an Edge Collider 2D on the same object instead (PathEdit.Of).
                if (Inspected?.GetComponent<EdgeCollider2DComponent>() == null) PathRows(line.Points, p => line.Points = p, false);
                break;
            case Text2DComponent worldText:
                Text("Text key", worldText.TextKey ?? "", s => worldText.TextKey = string.IsNullOrWhiteSpace(s) ? null : s.Trim());
                Text("Text", worldText.Text, s => worldText.Text = s);
                Number("Size", worldText.Size, 1, 1024, v => worldText.Size = v);
                Color("Color", worldText.Color, v => worldText.Color = v);
                Choice("Align", worldText.Align, v => worldText.Align = v);
                var worldRich = new CheckBox { Content = "Rich text tags", IsChecked = worldText.RichText };
                ToolTip.SetTip(worldRich, "<color=#RRGGBB>, <b> and <size=N>");
                worldRich.IsCheckedChanged += (_, _) => { worldText.RichText = worldRich.IsChecked == true; MarkDirty(); };
                Row("", worldRich);
                Sorting(() => worldText.SortingLayer, v => worldText.SortingLayer = v, () => worldText.Order, v => worldText.Order = v);
                if (Inspected?.GetComponent<SpriteRendererComponent>() == null)
                {
                    var worldByY = new CheckBox { Content = "Use foot Y", IsChecked = worldText.SortByY };
                    worldByY.IsCheckedChanged += (_, _) => { worldText.SortByY = worldByY.IsChecked == true; MarkDirty(); };
                    Row("Y sort", worldByY);
                }
                break;
            case Trail2DComponent trail:
                Number("Time (s)", trail.Time, .01m, 10, v => trail.Time = v);
                Number("Width", trail.Width, .01m, 256, v => trail.Width = v);
                Color("Head color", trail.StartColor, v => trail.StartColor = v);
                Color("Tail color", trail.EndColor, v => trail.EndColor = v);
                Number("Max points", trail.MaxPoints, 2, 256, v => trail.MaxPoints = (int)v);
                Number("Min step", trail.MinStep, 0, 4096, v => trail.MinStep = v);
                Sorting(() => trail.SortingLayer, v => trail.SortingLayer = v, () => trail.Order, v => trail.Order = v);
                break;
            case PathFollower2DComponent follower:
                Number("Speed", follower.Speed, .01m, 100000, v => follower.Speed = v);
                Choice("Repeat", follower.Repeat, v => { follower.Repeat = v; Viewport.InvalidateUi(); });
                var autoplayPath = new CheckBox { Content = "Play on start", IsChecked = follower.PlayOnStart };
                autoplayPath.IsCheckedChanged += (_, _) => { follower.PlayOnStart = autoplayPath.IsChecked == true; MarkDirty(); };
                Row("", autoplayPath);
                PathRows(follower.Points, p => follower.Points = p, true);
                break;
            case CutsceneDirectorComponent director:
                Asset("Dialogue", CutsceneSequence.Extension, director.Sequence, v => director.Sequence = v);
                break;
            case Parallax2DComponent parallax:
                Number("Scroll X", parallax.ScrollX, -16, 16, v => parallax.ScrollX = v);
                Number("Scroll Y", parallax.ScrollY, -16, 16, v => parallax.ScrollY = v);
                var repeatX = new CheckBox { Content = "X", IsChecked = parallax.RepeatX };
                var repeatY = new CheckBox { Content = "Y", IsChecked = parallax.RepeatY };
                repeatX.IsCheckedChanged += (_, _) => { parallax.RepeatX = repeatX.IsChecked == true; MarkDirty(); };
                repeatY.IsCheckedChanged += (_, _) => { parallax.RepeatY = repeatY.IsChecked == true; MarkDirty(); };
                Row("Repeat", new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { repeatX, repeatY } });
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
                if (Inspected?.GetComponent<SpriteRendererComponent>()==null)
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
                Asset("Particles",ParticleEffect.Extension,emitter.Particles,v=>emitter.Particles=v);
                var editParticles = new Button { Content = "Edit in Particle panel", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
                editParticles.Click += (_, _) => OpenParticlePanel();
                Row("", editParticles);
                Choice("Trigger",emitter.Trigger,v=>emitter.Trigger=v);
                Number("Seed",emitter.Seed,0,16777216,v=>emitter.Seed=(uint)v);
                Number("Offset X",emitter.OffsetX,-16384,16384,v=>emitter.OffsetX=v);
                Number("Offset Y",emitter.OffsetY,-16384,16384,v=>emitter.OffsetY=v);
                Sorting(() => emitter.SortingLayer, v => emitter.SortingLayer = v, () => emitter.Order, v => emitter.Order = v);
                ParticleCurveRows(emitter, fields);
                break;
            case PointLight2DComponent light:
                Number("Radius",light.Radius,.1m,16384,v=>light.Radius=v);
                Number("Intensity",light.Intensity,0,1,v=>light.Intensity=v);
                Number("Flicker",light.Flicker,0,1,v=>light.Flicker=v);
                Number("Hz",light.Frequency,0,30,v=>light.Frequency=v);
                Color("Color",light.Color,v=>light.Color=v);
                var shadows = new CheckBox { Content = "Cast shadows", IsChecked = light.Shadows };
                shadows.IsCheckedChanged += (_, _) => { light.Shadows = shadows.IsChecked == true; MarkDirty(); };
                Row("", shadows);
                break;
            case GlobalLight2DComponent global:
                Color("Ambient",global.Color,v=>global.Color=v);
                Number("Unlit layer",global.UnlitLayer,-32768,32767,v=>global.UnlitLayer=(int)v);
                break;
            case NavigationRegion2DComponent nav:
                Number("Spacing",nav.Spacing,4,128,v=>nav.Spacing=v);
                Number("Agent W",nav.AgentWidth,1,4096,v=>nav.AgentWidth=v);
                Number("Agent H",nav.AgentHeight,1,4096,v=>nav.AgentHeight=v);
                Number("Slope °",nav.MaxSlope,0,80,v=>nav.MaxSlope=v);
                CollisionMaskField(nav.TerrainMask,v=>nav.TerrainMask=v,Row);
                // Top-down bake (16.15): radius Agent W / 2, cells Spacing; the Scene view shades the open cells.
                Row("Top-down",ValueLabel(Viewport.SelectedNavArea() is {} area ? $"{area.OpenCells():N0} / {area.Columns*area.Rows:N0} cells open" : "—"));
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
                Number("Friction", rigid.Friction, 0, 100, v => rigid.Friction = v);
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
            case Joint2DComponent joint:
                var self = Inspected?.Id;
                Row("Connect", ObjectField("Connect", o => o.Id != self, joint.Connected, id => { joint.Connected = id; MarkDirty(); RefreshComponentList(); }));
                // Checkboxes sit in the value column like "Freeze rotation"; toggling shows or hides dependent rows.
                void Toggle(string text, bool value, Action<bool> set)
                {
                    var box = new CheckBox { Content = text, IsChecked = value };
                    box.IsCheckedChanged += (_, _) => { set(box.IsChecked == true); MarkDirty(); RefreshComponentList(); };
                    Row("", box);
                }
                switch (joint)
                {
                    case DistanceJoint2DComponent distance:
                        Number("Length", distance.Length, 0, 1000000, v => distance.Length = v);
                        Toggle("Spring", distance.Spring, v => distance.Spring = v);
                        if (distance.Spring)
                        {
                            Number("Hz", distance.Hertz, 0, 1000, v => distance.Hertz = v);
                            Number("Damping", distance.DampingRatio, 0, 100, v => distance.DampingRatio = v);
                        }
                        break;
                    case RevoluteJoint2DComponent revolute:
                        Number("Anchor X", revolute.AnchorX, -1000000, 1000000, v => revolute.AnchorX = v);
                        Number("Anchor Y", revolute.AnchorY, -1000000, 1000000, v => revolute.AnchorY = v);
                        Toggle("Angle limit", revolute.Limit, v => revolute.Limit = v);
                        if (revolute.Limit)
                        {
                            Number("Lower °", revolute.LowerAngle, -180, 180, v => revolute.LowerAngle = Math.Min(v, revolute.UpperAngle));
                            Number("Upper °", revolute.UpperAngle, -180, 180, v => revolute.UpperAngle = Math.Max(v, revolute.LowerAngle));
                        }
                        Toggle("Motor", revolute.Motor, v => revolute.Motor = v);
                        if (revolute.Motor)
                        {
                            Number("Speed °/s", revolute.MotorSpeed, -36000, 36000, v => revolute.MotorSpeed = v);
                            Number("Torque", revolute.MaxMotorTorque, 0, 100000000, v => revolute.MaxMotorTorque = v);
                        }
                        break;
                    case PrismaticJoint2DComponent prismatic:
                        Number("Axis X", prismatic.AxisX, -1, 1, v => prismatic.AxisX = v);
                        Number("Axis Y", prismatic.AxisY, -1, 1, v => prismatic.AxisY = v);
                        Toggle("Limit", prismatic.Limit, v => prismatic.Limit = v);
                        if (prismatic.Limit)
                        {
                            Number("Lower", prismatic.Lower, -1000000, 1000000, v => prismatic.Lower = Math.Min(v, prismatic.Upper));
                            Number("Upper", prismatic.Upper, -1000000, 1000000, v => prismatic.Upper = Math.Max(v, prismatic.Lower));
                        }
                        Toggle("Motor", prismatic.Motor, v => prismatic.Motor = v);
                        if (prismatic.Motor)
                        {
                            Number("Speed", prismatic.MotorSpeed, -100000, 100000, v => prismatic.MotorSpeed = v);
                            Number("Force", prismatic.MaxMotorForce, 0, 100000000, v => prismatic.MaxMotorForce = v);
                        }
                        Toggle("Spring", prismatic.Spring, v => prismatic.Spring = v);
                        if (prismatic.Spring)
                        {
                            Number("Hz", prismatic.Hertz, 0, 1000, v => prismatic.Hertz = v);
                            Number("Damping", prismatic.DampingRatio, 0, 100, v => prismatic.DampingRatio = v);
                        }
                        break;
                    case WeldJoint2DComponent weld:
                        Number("Linear Hz", weld.LinearHertz, 0, 1000, v => weld.LinearHertz = v);
                        Number("Angular Hz", weld.AngularHertz, 0, 1000, v => weld.AngularHertz = v);
                        Number("Damping", weld.DampingRatio, 0, 100, v => weld.DampingRatio = v);
                        break;
                }
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
                if (collider is CapsuleCollider2DComponent capsule)
                    Number("Radius",capsule.Radius,0,1000000,v=>capsule.Radius=v);
                if (collider is EdgeCollider2DComponent edge && Inspected != null && PathEdit.Of(Inspected) is { } edgePath)
                {
                    var loop = new CheckBox { Content="Loop", IsChecked=edge.Loop, IsEnabled=edge.Points.Length>=3 };
                    loop.IsCheckedChanged += (_,_) => { edge.Loop=loop.IsChecked==true; MarkDirty(); };
                    Row("",loop);
                    PathRows(edgePath.Points, edgePath.Set, false, curves: false);
                }
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
                    Row("Follow", ObjectField("Follow", _ => true, map.StreamTarget, id => { map.StreamTarget = id; MarkDirty(); RefreshComponentList(); }, "Active camera"));
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
            case AudioSourceComponent audio: Asset("Sound", "", audio.Sound, id => audio.Sound = id, AudioFile.IsSupported); SourceFields(audio,Row); break;
            case AudioListener2DComponent:
                Row("Listen at",new TextBlock { Text="Object center",Classes={"fieldLabel"} }); break;
            case UiCanvasComponent canvas:
                Number("Order", canvas.Order, -32768, 32767, n => canvas.Order = (int)n);
                break;
            case UiRectComponent rect:
                var anchor = new ComboBox { ItemsSource = ScreenUi.Anchors, SelectedItem = rect.Anchor, HorizontalAlignment = HorizontalAlignment.Stretch };
                anchor.SelectionChanged += (_, _) => { if (anchor.SelectedItem is string a) { rect.Anchor = a; MarkDirty(); } };
                Row("Anchor", anchor);
                if (Inspected is {} widget) UiPinFields(widget, rect, Row);
                var scaleWidth = new CheckBox { Content="Scale width and X with text", IsChecked=rect.ScaleHorizontalWithText };
                scaleWidth.IsCheckedChanged += (_,_) => { rect.ScaleHorizontalWithText=scaleWidth.IsChecked==true; MarkDirty(); };
                Row("",scaleWidth);
                Text("Show if", rect.ShowIf, s => rect.ShowIf = s);
                var modeRoot = new CheckBox { Content = "Input mode root", IsChecked = rect.InputModeRoot };
                modeRoot.IsCheckedChanged += (_, _) => { rect.InputModeRoot = modeRoot.IsChecked == true; MarkDirty(); };
                Row("", modeRoot);
                break;
            case UiImageComponent image:
                Asset("Sprite", ".png", image.Sprite, id => {
                    if (id != image.Sprite) { image.Sprite = id; image.Borders = null; RefreshComponentList(); }
                });
                SliceFields(image, Row);
                Color("Fill", image.Color, s => image.Color = s);
                break;
            case UiLayoutComponent stack:
                var axes = new ComboBox { ItemsSource = new[] { "Vertical", "Horizontal", "Grid", "Wrap" }, SelectedItem = stack.Axis, HorizontalAlignment = HorizontalAlignment.Stretch };
                Row("Axis", axes);
                // 0 fits as many cells as the width allows (stored as no value), like Wrap.
                Number("Columns", stack.Columns ?? 0, 0, 256, v => stack.Columns = v >= 1 ? (int)v : null);
                var columnsRow = fields.Children[^1];
                columnsRow.IsEnabled = stack.Axis == "Grid";
                axes.SelectionChanged += (_,_) => { if (axes.SelectedItem is string axis) { stack.Axis=axis; columnsRow.IsEnabled = axis == "Grid"; MarkDirty(); } };
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
                var richText=new CheckBox { Content="Rich text tags",IsChecked=label.RichText };
                ToolTip.SetTip(richText,"<color=#RRGGBB>, <b>, <size=N> and <icon=name>");
                richText.IsCheckedChanged+=(_,_)=>{label.RichText=richText.IsChecked==true;MarkDirty();};Row("",richText);
                Number("Reveal /s", label.RevealRate, 0, 10000, n => label.RevealRate = n);
                var manifest=project==null?null:ProjectAssets.ReadManifest(project);
                var icons=new TextBox { AcceptsReturn=true, MinHeight=28, MaxHeight=110, Watermark="herb=Assets/Icons/Herb.png",
                    Text=string.Join("\n", (label.Icons??[]).Select(p=>p.Key+"="+(manifest?.PathFor(p.Value)??p.Value.ToString()))) };
                ToolTip.SetTip(icons,"One <icon=name> and project PNG per line (herb=Assets/Icons/Herb.png).");
                icons.TextChanged+=(_,_)=>{
                    var map=new Dictionary<string,Guid>(StringComparer.Ordinal);
                    foreach(var line in (icons.Text??"").Split('\n',StringSplitOptions.RemoveEmptyEntries)){
                        int at=line.IndexOf('=');if(at<=0)return;
                        string png=line[(at+1)..].Trim();
                        Guid? id=Guid.TryParse(png,out var raw)?raw:manifest?.IdFor(png);
                        if(id is not Guid found || !map.TryAdd(line[..at].Trim(),found))return;
                    }
                    label.Icons=map.Count==0?null:map;MarkDirty(icons);
                };
                Row("Icons",icons);
                Number("Size", label.Size, 1, 1024, n => label.Size = n);
                Color("Color", label.Color, s => label.Color = s);
                Number("Inset X", label.InsetX, -16384, 16384, n => label.InsetX = n);
                Number("Inset Y", label.InsetY, -16384, 16384, n => label.InsetY = n);
                break;
            case UiControlComponent button:
                if (button is UiSliderComponent slider)
                {
                    Number("Minimum", slider.Minimum, -1000000, 1000000, v => slider.Minimum = v);
                    Number("Maximum", slider.Maximum, -1000000, 1000000, v => slider.Maximum = v);
                    Number("Step", slider.Step, .0001m, 1000000, v => slider.Step = v);
                    Number("Value", slider.Initial, -1000000, 1000000, v => slider.Initial = v);
                }
                if (button is UiToggleComponent toggle)
                {
                    var check = new CheckBox { Content = "On", IsChecked = toggle.Initial };
                    check.IsCheckedChanged += (_, _) => { toggle.Initial = check.IsChecked == true; MarkDirty(); };
                    Row("Default", check);
                }
                if (button is UiDropdownComponent dropdown)
                {
                    var options = new TextBox { AcceptsReturn = true, MinHeight = 28, MaxHeight = 110,
                        Text = string.Join("\n", dropdown.Options) };
                    options.TextChanged += (_, _) => {
                        var items = (options.Text ?? "").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                        if (items.Length == 0) return;
                        dropdown.Options = items;
                        dropdown.Initial = Math.Min(dropdown.Initial, items.Length - 1);
                        MarkDirty(options);
                    };
                    Row("Options", options);
                    Number("Index", dropdown.Initial, 0, int.MaxValue, v => dropdown.Initial = Math.Clamp((int)v, 0, dropdown.Options.Length - 1));
                }
                var names = new List<string> { "button" };
                if (project != null && Scene.Settings.UiTheme != null)
                {
                    try { names = UiTheme.Load(project, ProjectAssets.ReadManifest(project), Scene.Settings.UiTheme).Names.Order().ToList(); }
                    catch (Exception ex) when (ex is InvalidDataException or IOException) { Status.Text = ex.Message; }
                }
                string current = button.Style ?? "button";
                if (!names.Contains(current)) names.Add(current);
                var style = new ComboBox { ItemsSource=names, SelectedItem=current, HorizontalAlignment=HorizontalAlignment.Stretch };
                style.SelectionChanged += (_,_) => { if (style.SelectedItem is string name) {button.Style=name=="button"?null:name; MarkDirty();} };
                Row("Style",style);
                var enabled = new CheckBox { Content = "Interactable", IsChecked = button.Interactable };
                enabled.IsCheckedChanged += (_, _) => { button.Interactable = enabled.IsChecked == true; MarkDirty(); };
                Row("", enabled);
                if (button is UiButtonComponent slot)
                {
                    var draggable = new CheckBox { Content = "Drag and drop slot", IsChecked = slot.Draggable };
                    ToolTip.SetTip(draggable, "Drag onto another slot, or activate to pick up and activate another to drop (OnUiDrop).");
                    draggable.IsCheckedChanged += (_, _) => { slot.Draggable = draggable.IsChecked == true; MarkDirty(); };
                    Row("", draggable);
                }
                break;
        }
        return fields;
    }
}
