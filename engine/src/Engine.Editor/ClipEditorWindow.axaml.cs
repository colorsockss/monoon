using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Engine.Core;

namespace Engine.Editor;

internal sealed record ClipPng(Guid Id, string Path, string Hash)
{
    public override string ToString() => Path;
}

internal partial class ClipEditorWindow : Window
{
    private sealed record Frame(Guid Sprite, string Seconds, SpriteRegion? Source = null, SpriteCanvas? Canvas = null, string? Event = null, bool UseRenderer = false)
    {
        public bool Parse(out float value) => float.TryParse(Seconds, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && float.IsFinite(value) && value > 0;
    }
    private sealed record Snapshot(Frame[] Frames, ComponentTrack[] Tracks, bool Loop, int Selected);
    private IImage?[] frameImages = [];
    private SceneAppearance?[] frameDraws = [];
    private readonly SceneViewport framePreview = new(spritePreview:true) { IsHitTestVisible=false };

    private readonly string root, folder;
    public string? RelativePath { get; private set; }
    public event Action<string>? Saved;
    private ClipPng[] images = [];
    private Dictionary<Guid,ClipPng> byId = [];
    private AssetEntry[] atlasFiles = [];
    private SpriteLibrary? library;
    private readonly SpriteCache<Bitmap> sheets = new(p=>new Bitmap(p));
    private readonly Dictionary<Guid,Bitmap?> bitmaps = [];
    private List<Frame> frames;
    private ComponentTrack[] tracks;
    private ComponentCurves? curves;
    private string? curveError;
    private int selected;
    private bool loop, refreshing, approved, asking;
    private Snapshot saved;
    private string savedName;
    private readonly Stack<Snapshot> undo = [], redo = [];
    private SpriteClipTimeline axis = new([]);
    private double position, playStart;
    private readonly Stopwatch watch = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(16) };

    public ClipEditorWindow(string root, string folder, string? relative, SpriteClip clip)
    {
        this.root = root; this.folder = folder; RelativePath = relative;
        InitializeComponent();
        PreviewHost.Child = framePreview;

        frames = clip.Frames.Select(f => new Frame(f.Sprite, f.Seconds.ToString("R", CultureInfo.InvariantCulture), f.Source, f.Canvas, f.Event, f.UseRenderer)).ToList();
        tracks = clip.Tracks ?? [];
        selected = frames.Count == 0 ? -1 : 0; loop = clip.Loop;
        saved = Capture(); savedName = relative == null ? "Animation" : Path.GetFileNameWithoutExtension(relative);
        ClipName.Text = savedName; ClipName.IsReadOnly = relative != null;
        SetCatalog(ProjectAssets.Scan(root)); ZoomBox.ItemsSource = new[]{"Fit", "1×", "2×", "4×"}; ZoomBox.SelectedIndex = 0;

        KeyProperty.ItemsSource = ComponentCurves.Properties;
        KeyProperty.SelectedIndex = 0;
        KeyProperty.SelectionChanged += (_,_) => RefreshKey();
        KeySection.IsChecked = tracks.Length != 0;
        WriteKey.Click += (_,_) => EditKey(false);
        DeleteKey.Click += (_,_) => EditKey(true);
        AddRenderer.Click += (_,_) => Change(() => { selected++; frames.Insert(selected, new(Guid.Empty,"1",UseRenderer:true)); });
        Track.SelectFrame = Select; Track.Scrub = Seek; Track.MoveFrame = Move;
        DragDrop.SetAllowDrop(Track,true);
        Track.AddHandler(DragDrop.DragOverEvent, FileDrop);
        Track.AddHandler(DragDrop.DragLeaveEvent, (_,_) => Track.Mark(-1));
        Track.AddHandler(DragDrop.DropEvent, FileDrop);
        First.Click += (_,_) => Select(0); Previous.Click += (_,_) => Select(axis.FrameAt(position)-1);
        Next.Click += (_,_) => Select(axis.FrameAt(position)+1);
        PreviewPlay.Click += (_,_) => TogglePlayback();
        timer.Tick += (_,_) => Tick();
        ZoomBox.SelectionChanged += (_,_) => Preview();
        EventBox.LostFocus += (_,_) => CommitSeconds();
        EventBox.KeyDown += (_,e) => { if (e.Key == Key.Enter) { CommitSeconds(); Track.Focus(); e.Handled = true; } };
        EventBox.PropertyChanged += (_,e) => { if (!refreshing && e.Property == TextBox.TextProperty) UpdateStatus(); };
        SecondsBox.LostFocus += (_,_) => CommitSeconds();
        SecondsBox.KeyDown += (_,e) => { if (e.Key == Key.Enter) { CommitSeconds(); Track.Focus(); e.Handled = true; } };
        SecondsBox.PropertyChanged += (_,e) => { if (!refreshing && e.Property == TextBox.TextProperty) UpdateStatus(); };
        ClipName.PropertyChanged += (_,e) => { if (!refreshing && e.Property == TextBox.TextProperty) UpdateStatus(); };
        LoopBox.IsCheckedChanged += (_,_) => { if (!refreshing && loop != (LoopBox.IsChecked == true)) Change(() => loop = LoopBox.IsChecked == true); };
        SpriteBox.SelectionChanged += (_,_) => { if (!refreshing && selected >= 0 && SpriteBox.SelectedItem is ClipPng p && frames[selected].Sprite != p.Id)
            Change(() => frames[selected] = frames[selected] with { Sprite = p.Id, Source = null, Canvas = null, UseRenderer = false }); };
        Left.Click += (_,_) => Move(selected, selected-1); Right.Click += (_,_) => Move(selected, selected+2);
        Duplicate.Click += (_,_) => { if (selected >= 0) Change(() => { frames.Insert(selected+1, frames[selected]); selected++; }); };
        Remove.Click += (_,_) => { if (selected >= 0) Change(() => { frames.RemoveAt(selected); selected = Math.Min(selected,frames.Count-1); }); };
        Add.Click += async (_,_) => await ChooseImages();
        AddCells.Click += async (_,_) => await ChooseCells();
        CloseButton.Click += (_,_) => Close(); SaveButton.Click += (_,_) => Save();
        ConfigureShortcuts();
        SizeChanged += (_,_) => LayoutStage();
        StageScroll.SizeChanged += (_,_) => LayoutStage();
        Closing += (_,e) => {
            if (approved || !Dirty) return;
            e.Cancel = true;
            if (!asking) Dispatcher.UIThread.Post(async () => await RequestClose());
        };
        Closed += (_,_) => { Pause(); sheets.Dispose(); foreach (var bitmap in bitmaps.Values) bitmap?.Dispose(); };
        Rebuild(); LayoutStage();
    }

    public void SetCatalog(ProjectAssets assets)
    {
        var fresh = assets.Entries.Where(e => SpriteAssets.IsPng(e.Path) && File.Exists(Path.Combine(root,e.Path)))
            .OrderBy(e => e.Path,StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Path,StringComparer.Ordinal)
            .Select(e => new ClipPng(e.Id,e.Path,e.Hash)).ToArray();
        var atlases=assets.Entries.Where(e=>e.Path.EndsWith(SpriteAtlas.Extension) && File.Exists(Path.Combine(root,e.Path))).ToArray();
        if (images.SequenceEqual(fresh) && atlasFiles.SequenceEqual(atlases)) return;
        library=assets.Sprites(root); atlasFiles=atlases; sheets.Clear(); framePreview.ClearSprites();

        var index = fresh.ToDictionary(p => p.Id);
        foreach (var id in bitmaps.Keys.Where(id => byId.GetValueOrDefault(id) != index.GetValueOrDefault(id)).ToArray())
        {
            bitmaps.Remove(id,out var stale); stale?.Dispose();
        }
        byId = index; images = fresh;
        refreshing = true;
        SpriteBox.ItemsSource = images;
        if (selected >= 0) SpriteBox.SelectedItem = byId.GetValueOrDefault(frames[selected].Sprite);
        refreshing = false;
        if (IsVisible)
        {
            RefreshFrameImages();
            openPicker?.RefreshBitmaps(images);
            UpdateStatus();
            Preview();
        }
    }
    private void LayoutStage()
    {
        bool narrow = ClientSize.Width > 0 && ClientSize.Width < 565;
        Stage.MinHeight = narrow ? 0 : StageScroll.Bounds.Height;
        if (narrow == (Grid.GetRow(Properties) == 1)) return;
        Stage.ColumnDefinitions = new(narrow ? "*" : "*,240");
        Stage.RowDefinitions = new(narrow ? "115,Auto" : "*");
        Grid.SetRow(Properties,narrow ? 1 : 0); Grid.SetColumn(Properties,narrow ? 0 : 1);
    }

    private void ConfigureShortcuts()
    {
        var mod = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        var menu = new NativeMenu(); var edit = new NativeMenu();
        void Bind(string label, Key key, KeyModifiers mods, Action action)
        {
            var gesture = new KeyGesture(key,mods);
            var item = new NativeMenuItem(label) { Gesture = gesture };
            item.Click += (_,_) => action(); edit.Add(item);
            AddHandler(KeyDownEvent, (_,e) => {
                if (gesture.Matches(e)) { e.Handled = true; action(); }
            }, RoutingStrategies.Tunnel);
        }
        Bind("Save Clip", Key.S,mod, () => Save());
        Bind("Undo Clip Edit", Key.Z,mod, () => History(false));
        Bind("Redo Clip Edit", Key.Z,mod | KeyModifiers.Shift, () => History(true));
        if (!OperatingSystem.IsMacOS()) Bind("Redo", Key.Y,mod, () => History(true));
        menu.Add(new NativeMenuItem("Clip") { Menu = edit }); NativeMenu.SetMenu(this,menu);
        Track.KeyDown += (_,e) => {
            if (e.Key == Key.Space) TogglePlayback();
            else if (e.Key == Key.Left) Select(selected-1);
            else if (e.Key == Key.Right) Select(selected+1);
            else return;
            e.Handled = true;
        };
    }

    private Snapshot Capture() => new(frames.ToArray(),tracks,loop,selected);
    private bool Dirty => saved.Tracks != tracks || saved.Loop != loop || !saved.Frames.SequenceEqual(frames) || ClipName.Text != savedName ||
        (selected >= 0 && (SecondsBox.Text != frames[selected].Seconds || (EventBox.Text ?? "").Trim() != (frames[selected].Event ?? "")));
    private void Change(Action change)
    {
        Pause(); undo.Push(Capture()); redo.Clear(); change(); Rebuild();
    }
    private void CommitSeconds()
    {
        if (refreshing || selected < 0) return;
        string value = (SecondsBox.Text ?? "").Trim();
        var name = EventBox.Text?.Trim();
        var frame = frames[selected] with { Seconds = value, Event = string.IsNullOrEmpty(name) ? null : name };
        if (frames[selected] != frame) Change(() => frames[selected] = frame);
    }
    private void History(bool forward)
    {
        CommitSeconds();
        var src = forward ? redo : undo; var dst = forward ? undo : redo;
        if (!src.TryPop(out var s)) return;
        Pause(); dst.Push(Capture()); frames = s.Frames.ToList(); tracks = s.Tracks; loop = s.Loop; selected = s.Selected; Rebuild();
    }
    private Bitmap? BitmapFor(Guid id)
    {
        if (bitmaps.TryGetValue(id,out var cached)) return cached;
        Bitmap? result = null;
        if (byId.TryGetValue(id,out var png))
        {
            try { result = new Bitmap(Path.Combine(root,png.Path)); }
            catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException or NotSupportedException) { }
        }
        return bitmaps[id] = result;
    }
    private void Rebuild()
    {
        axis = new(frames.Select(f => new SpriteFrame(f.Sprite, f.Parse(out var v) ? v : .1f)).ToArray());
        try { curves = new ComponentCurves(tracks,axis.Duration); curveError = null; }
        catch (InvalidDataException ex) { curves = null; curveError = ex.Message; }
        RefreshFrameImages();
        Select(selected,commit:false);
    }

    private void RefreshFrameImages()
    {
        frameImages = frames.Select(FrameImage).ToArray();
        frameDraws = frames.Select((frame,i) =>
        {
            if (frame.UseRenderer) return new SceneAppearance(SpriteAssets.SquarePath,System.Drawing.Color.White);
            if (frameImages[i]==null || !byId.TryGetValue(frame.Sprite,out var png)) return (SceneAppearance?)null;
            var packed=frame.Source==null && frame.Canvas==null ? library?.Find(frame.Sprite) : null;
            return new SceneAppearance(Path.Combine(root,packed?.Sheet ?? png.Path),System.Drawing.Color.White,
                packed?.Entry.Source ?? frame.Source,packed?.Entry.Canvas ?? frame.Canvas);
        }).ToArray();
        Track.SetFrames(axis, frames.Select((f,i) => new ClipTimelineTrack.Cell(FrameName(f) + (f.Event == null ? "" : " · " + f.Event), frameImages[i],
            !f.Parse(out _) || !f.UseRenderer && frameImages[i] == null)).ToArray());
    }
    private IImage? FrameImage(Frame frame)
    {
        if (frame.Source==null && frame.Canvas==null && library?.Find(frame.Sprite) is {} packed)
            return sheets.TryGet(Path.Combine(root,packed.Sheet),out var sheet)
                ? new SpriteCellImage(sheet,packed.Entry.Source,packed.Entry.Canvas) : null;
        var bitmap = BitmapFor(frame.Sprite);
        if (bitmap == null || frame.Source == null) return bitmap;
        return frame.Source.Fits(bitmap.PixelSize.Width, bitmap.PixelSize.Height) && frame.Canvas?.Fits(frame.Source) != false
            ? new SpriteCellImage(bitmap, frame.Source, frame.Canvas) : null;
    }

    private string FrameName(Frame frame)
    {
        if (frame.UseRenderer) return "Object renderer";
        var name = byId.TryGetValue(frame.Sprite,out var png) ? Path.GetFileName(png.Path) : "Missing PNG";
        return frame.Source is {} cell ? $"{name} [{cell.X}, {cell.Y}]" : name;
    }

    private void Select(int i) => Select(i,true);
    private void Select(int i, bool commit)
    {
        if (commit) CommitSeconds();
        Pause(); selected = frames.Count == 0 ? -1 : Math.Clamp(i,0,frames.Count-1);
        position = selected < 0 ? 0 : axis.Start(selected);
        ShowSelection();
    }
    private void ShowSelection()
    {
        refreshing = true;
        var f = selected < 0 ? null : frames[selected];
        FrameHeading.Text = f == null ? "No frame selected" : $"Frame {selected+1} of {frames.Count}";
        if (f?.Source is {} region) FrameHeading.Text += $"   ({region.X}, {region.Y}) · {region.Width} × {region.Height} px";
        ToolTip.SetTip(FrameHeading,FrameHeading.Text);
        SpriteBox.SelectedItem = f != null ? byId.GetValueOrDefault(f.Sprite) : null;
        EventBox.Text = f?.Event ?? "";
        EventBox.IsEnabled = f != null;
        SecondsBox.Text = f?.Seconds ?? ""; LoopBox.IsChecked = loop;
        FrameActions.IsEnabled = SpriteBox.IsEnabled = SecondsBox.IsEnabled = Remove.IsEnabled = Duplicate.IsEnabled = f != null;
        Left.IsEnabled = selected > 0; Right.IsEnabled = selected >= 0 && selected+1 < frames.Count;
        refreshing = false; RefreshKey(); UpdateStatus(); Preview();
    }
    private void Move(int from,int slot)
    {
        if (from < 0 || from >= frames.Count) return;
        int to = Math.Clamp(slot > from ? slot-1 : slot, 0, frames.Count-1);
        if (from == to) return;
        Change(() => { var f = frames[from]; frames.RemoveAt(from); frames.Insert(to,f); selected = to; });
    }
    private void Seek(double t)
    {
        CommitSeconds(); Pause();
        position = axis.Position(t,false);
        selected = axis.FrameAt(position); ShowSelection();
    }
    private void Pause()
    {
        watch.Reset(); timer.Stop(); PreviewPlay.Content = "Play";
    }
    private void TogglePlayback()
    {
        CommitSeconds();
        if (watch.IsRunning) { Tick(); Seek(position); return; }
        if (Problem() != null) return;
        if (position >= axis.Duration) position = 0;
        playStart = position; PreviewPlay.Content = "Pause"; watch.Restart(); timer.Start(); Preview();
    }
    private void Tick()
    {
        if (!watch.IsRunning) return;
        position = axis.Position(playStart + watch.Elapsed.TotalSeconds, loop);
        if (!loop && position >= axis.Duration) { Seek(position); return; }
        Preview();
    }
    private void Preview()
    {
        var i = axis.FrameAt(position);
        var img = i < 0 ? null : frameImages[i];
        framePreview.ShowFrame(i<0 ? null : frameDraws[i], curves?.Sample(position) ?? default);
        int zoom = ZoomBox.SelectedIndex;
        framePreview.Width = zoom <= 0 || img == null ? double.NaN : img.Size.Width * (1 << (zoom-1));
        framePreview.Height = zoom <= 0 || img == null ? double.NaN : img.Size.Height * (1 << (zoom-1));
        Caption.Text = i < 0 ? "No frames" : $"#{i+1}   " + FrameName(frames[i]) + (frames[i].Event is {} marker ? " · " + marker : "");
        ClockText.Text = FormattableString.Invariant($"{position:0.000} / {axis.Duration:0.000} s");
        Track.SetCursor(selected,position);
    }

    private string? Problem()
    {
        if (frames.Count == 0) return "Add a PNG or an object renderer frame.";
        if (curveError != null) return curveError;
        for (int i = 0; i < frames.Count; i++)
        {
            var f = frames[i];
            if (i == selected && !refreshing) f = f with { Seconds = SecondsBox.Text ?? "" };
            if (!f.Parse(out _)) return $"Frame {i+1}: enter a positive duration in seconds.";
            if (f.UseRenderer) continue;
            if (BitmapFor(f.Sprite) is not {} bitmap) return $"Frame {i+1}: choose an available PNG.";
            if (f.Source != null && !f.Source.Fits(bitmap.PixelSize.Width, bitmap.PixelSize.Height))
                return $"Frame {i+1}: cell is outside the sheet. Choose a replacement cell.";
            if (f.Canvas != null && !f.Canvas.Fits(f.Source))
                return $"Frame {i+1}: trimmed sprite does not fit its frame canvas.";
        }
        return null;
    }
    private void UpdateStatus()
    {
        var error = Problem();
        StatusLine.Text = error ?? ""; ToolTip.SetTip(StatusLine, error);
        PreviewPlay.IsEnabled = SaveButton.IsEnabled = error == null;
        Title = $"{ClipName.Text} · Sprite Animation{(Dirty ? " *" : "")}";
    }
    private void Insert(IEnumerable<ClipPng> items, int slot)
    {
        var added = items.Select(p => new Frame(p.Id,"0.1")).ToArray();
        if (added.Length == 0) return;
        CommitSeconds(); slot = Math.Clamp(slot,0,frames.Count);
        Change(() => { frames.InsertRange(slot,added); selected = slot; });
    }
    private SheetPickerWindow? openPicker;
    private async Task ChooseCells()
    {
        CommitSeconds();
        Pause();
        var frame = selected >= 0 ? frames[selected] : null;
        var picker = new SheetPickerWindow(root, images, BitmapFor, frame?.Sprite, frame?.Source);
        openPicker = picker;
        SheetPickerWindow.Choice? choice;
        try { choice = await picker.ShowDialog<SheetPickerWindow.Choice?>(this); }
        finally { openPicker = null; }
        if (choice == null) return;
        Change(() =>
        {
            if (choice.Replace)
                frames[selected] = frames[selected] with { Sprite = choice.Sheet, UseRenderer = false, Source = choice.Slices[0].Source, Canvas = choice.Slices[0].Canvas };
            else
            {
                selected++;
                frames.InsertRange(selected, choice.Slices.Select(s => new Frame(choice.Sheet, "0.1", s.Source, s.Canvas)));
            }
        });
    }

    private async Task ChooseImages()
    {
        var list = new ListBox { ItemsSource = images, SelectionMode = SelectionMode.Multiple, MinHeight = 120 };
        var ok = new Button { Content = "Insert", HorizontalAlignment = HorizontalAlignment.Right };
        var dlg = new Window { Title = "Add project PNGs", Width = 360, Height = 310, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new Grid { Margin = new Thickness(10), RowDefinitions = new("*,Auto"), RowSpacing = 6 };
        panel.Children.Add(list); Grid.SetRow(ok,1); panel.Children.Add(ok); dlg.Content = panel;
        ok.Click += (_,_) => dlg.Close(true);
        if (await dlg.ShowDialog<bool>(this)) Insert(images.Where(p => list.SelectedItems!.Contains(p)), selected+1);
    }
    private void FileDrop(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).ToArray() ?? [];
        var paths = files.OfType<string>().Select(f => Path.GetRelativePath(root,f).Replace(Path.DirectorySeparatorChar,'/')).ToHashSet(StringComparer.Ordinal);
        var pngs = images.Where(p => paths.Contains(p.Path)).ToArray();
        bool valid = files.Length > 0 && pngs.Length == files.Length;
        int slot = Track.Slot(e.GetPosition(Track).X);
        e.DragEffects = valid ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true;
        Track.Mark(valid && e.RoutedEvent != DragDrop.DropEvent ? slot : -1);
        if (valid && e.RoutedEvent == DragDrop.DropEvent) Insert(pngs,slot);
    }

    public bool ProjectDrop(PixelPoint screen, IReadOnlyList<string> paths, bool release)
    {
        var p = Track.PointToClient(screen);
        bool inside = new Rect(Track.Bounds.Size).Contains(p) && new Rect(TrackScroll.Bounds.Size).Contains(TrackScroll.PointToClient(screen));
        var pngs = paths.Select(path => images.FirstOrDefault(img => img.Path == path)).OfType<ClipPng>().ToArray();
        bool valid = inside && pngs.Length > 0 && pngs.Length == paths.Count;
        int slot = valid ? Track.Slot(p.X) : -1;
        Track.Mark(release ? -1 : slot);
        if (release && valid) { Insert(pngs,slot); Activate(); }
        return valid;
    }
    public void ClearDrop() => Track.Mark(-1);
    private bool Save()
    {
        CommitSeconds();
        try
        {
            var catalog = ProjectAssets.Scan(root);
            SetCatalog(catalog);
            if (Problem() is {} error) throw new InvalidDataException(error);
            var stem = ClipName.Text?.Trim();
            if (string.IsNullOrEmpty(stem) || (RelativePath == null && stem.Any(c => !char.IsLetterOrDigit(c) && c is not ('-' or '_' or ' '))))
                throw new InvalidDataException("Use letters, numbers, spaces, dashes or underscores for the name.");
            var path = RelativePath ?? $"{folder}/{stem}{ProjectAssets.Clip}";
            var file = Path.Combine(root,path);
            if (RelativePath == null && File.Exists(file)) throw new IOException("A clip with this name already exists.");
            var data = frames.Select((f,i) => {
                var png = catalog.PathFor(f.Sprite);
                if (!f.UseRenderer && (png == null || !SpriteAssets.IsPng(png) || !File.Exists(Path.Combine(root,png)))) throw new IOException($"Frame {i+1}: missing PNG.");
                f.Parse(out float seconds); return new SpriteFrame(f.Sprite,seconds,f.Source,f.Canvas,f.Event,f.UseRenderer);
            }).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            ProjectAssets.Write(file,new SpriteClip(data,loop, tracks.Length == 0 ? null : tracks));
            RelativePath = path; saved = Capture(); savedName = stem; ClipName.Text = stem; ClipName.IsReadOnly = true;
            ProjectAssets.Scan(root); Saved?.Invoke(path); UpdateStatus(); StatusLine.Text = "Saved " + Path.GetFileName(path); return true;
        }
        catch (Exception ex) { StatusLine.Text = ex.Message; ToolTip.SetTip(StatusLine,ex.Message); return false; }
    }
    public async Task<bool> RequestClose()
    {
        if (asking) return false;
        CommitSeconds();
        if (Dirty)
        {
            asking = true; Pause();
            var dlg = new Window { Title = "Unsaved clip", SizeToContent = SizeToContent.WidthAndHeight, CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, HorizontalAlignment = HorizontalAlignment.Right };
            foreach (var s in new[]{"Cancel","Discard","Save"}) { var b = new Button { Content = s }; b.Click += (_,_) => dlg.Close(s); row.Children.Add(b); }
            dlg.Content = new StackPanel { Margin = new Thickness(14), Spacing = 10, Children = { new TextBlock { Text = $"Save changes to {ClipName.Text}?" }, row } };
            string? choice;
            try { choice = await dlg.ShowDialog<string?>(this); } finally { asking = false; }
            if (choice != "Discard" && (choice != "Save" || !Save())) return false;
        }
        approved = true; Close(); return true;
    }
}
