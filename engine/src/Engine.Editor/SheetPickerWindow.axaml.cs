using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Engine.Core;

namespace Engine.Editor;

internal partial class SheetPickerWindow : Window
{
    internal sealed record Choice(Guid Sheet, SheetSlice[] Slices, bool Replace);
    private readonly Func<Guid, Bitmap?> bitmapFor;
    private readonly string root;
    private readonly bool canReplace;
    private Bitmap? bitmap;
    private SheetGrid? grid;
    private SheetSlice[] packed = [];
    private string? sliceError;
    private bool changing;

    public SheetPickerWindow(string root, ClipPng[] images, Func<Guid, Bitmap?> bitmapFor, Guid? current, SpriteRegion? region)
    {
        InitializeComponent();
        this.bitmapFor = bitmapFor;
        this.root = root;
        canReplace = current.HasValue;
        SheetWindowLayout.ArrangeFooter(Footer, Info, Actions);
        Replace.IsVisible = canReplace;
        Sheets.ItemsSource = images;
        Sheets.SelectedItem = images.FirstOrDefault(p => p.Id == current) ?? images.FirstOrDefault();
        Zoom.ItemsSource = new[] { "Fit", "1×", "2×", "4×", "8×" };
        Zoom.SelectedIndex = 0;
        Zoom.SelectionChanged += (_,_) => ScaleSheet();
        SheetScroll.SizeChanged += (_,_) => ScaleSheet();
        Cells.SelectionChanged += UpdateStatus;
        SliceMenu.Click += (_,_) => OpenFrameMenu();
        Sheets.SelectionChanged += (_,_) => LoadSheet(null);
        foreach (var field in new[] {CellWidth, CellHeight, MarginX, MarginY, GapX, GapY})
            field.PropertyChanged += (_,e) => { if (e.Property == TextBox.TextProperty && !changing) UpdateGrid(); };
        Insert.Click += (_,_) => Finish(false);
        Replace.Click += (_,_) => Finish(true);
        Cancel.Click += (_,_) => Close();
        LoadSheet(region);
    }

    private void LoadSheet(SpriteRegion? region)
    {
        bitmap = Sheets.SelectedItem is ClipPng png ? bitmapFor(png.Id) : null;
        packed = [];
        sliceError = null;
        if (bitmap != null && Sheets.SelectedItem is ClipPng sheet)
        {
            try
            {
                packed = ProjectAssets.ReadSheetSlices(Path.Combine(root, sheet.Path));
                if (packed.Any(s => s?.Source == null || !s.Source.Fits(bitmap.PixelSize.Width, bitmap.PixelSize.Height) ||
                        (s.Canvas != null && !s.Canvas.Fits(s.Source))))
                    throw new InvalidDataException("A packed frame is outside its sheet or canvas.");
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            { sliceError = ex.Message; packed = []; }
        }
        ShowGrid.IsVisible = packed.Length == 0 && sliceError == null;
        ShowGrid.IsChecked = ShowGrid.IsVisible && region == null;
        SliceMenu.IsVisible = packed.Length > 0;
        changing = true;
        CellWidth.Text = (region?.Width ?? Math.Min(32, bitmap?.PixelSize.Width ?? 32)).ToString(CultureInfo.InvariantCulture);
        CellHeight.Text = (region?.Height ?? Math.Min(32, bitmap?.PixelSize.Height ?? 32)).ToString(CultureInfo.InvariantCulture);
        MarginX.Text = MarginY.Text = GapX.Text = GapY.Text = "0";
        changing = false;
        ScaleSheet();
        UpdateGrid();
    }

    private void ScaleSheet()
    {
        int zoom = Zoom.SelectedIndex <= 0 ? 0 : 1 << (Zoom.SelectedIndex - 1);
        Cells.Width = zoom == 0 ? Math.Max(1, SheetScroll.Bounds.Width) : (double)(bitmap?.PixelSize.Width ?? 1) * zoom;
        Cells.Height = zoom == 0 ? Math.Max(1, SheetScroll.Bounds.Height) : (double)(bitmap?.PixelSize.Height ?? 1) * zoom;
    }

    private void UpdateGrid()
    {
        if (packed.Length > 0 || sliceError != null)
        {
            grid = null;
            Cells.SetSheet(bitmap, null, packed);
            UpdateStatus();
            return;
        }
        static bool Read(TextBox field, int minimum, out int value) =>
            int.TryParse(field.Text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= minimum;

        bool hasWidth = Read(CellWidth, 1, out int w);
        bool hasHeight = Read(CellHeight, 1, out int h);
        SheetWindowLayout.UpdateGridButton(ShowGrid, w, h);
        grid = hasWidth && hasHeight &&
            Read(MarginX,0,out int mx) && Read(MarginY,0,out int my) &&
            Read(GapX,0,out int gx) && Read(GapY,0,out int gy) ? new SheetGrid(w,h,mx,my,gx,gy) : null;
        Cells.SetSheet(bitmap,grid);
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        int selected = Cells.Count;
        Insert.IsEnabled = selected > 0;
        Replace.IsEnabled = canReplace && selected == 1;
        if (sliceError != null) Info.Text = sliceError;
        else if (packed.Length > 0) Info.Text = $"{packed.Length} packed frames · {selected} selected";
        else if (bitmap == null) Info.Text = "Choose a project PNG.";
        else if (grid == null) Info.Text = "Enter whole pixel values.";
        else
        {
            var size = bitmap.PixelSize;
            int cols = grid.Columns(size.Width), rows = grid.Rows(size.Height);
            Info.Text = cols == 0 || rows == 0 ? "No complete cells fit." : $"{cols} × {rows} cells · {selected} selected";
        }
    }

    private void OpenFrameMenu()
    {
        var menu = new MenuFlyout();
        for (int i = 0; i < packed.Length; i++)
        {
            int index = i;
            var region = packed[i].Source;
            var item = new MenuItem { Header = $"Frame {i + 1} · {region.Width} × {region.Height}",
                ToggleType = MenuItemToggleType.CheckBox, IsChecked = Cells.IsSliceSelected(i) };
            item.Click += (_,_) => Cells.ToggleSlice(index);
            menu.Items.Add(item);
        }
        menu.ShowAt(SliceMenu);
    }

    private void Finish(bool replace)
    {
        if (Sheets.SelectedItem is ClipPng png && Cells.Count > 0 && (!replace || Cells.Count == 1))
            Close(new Choice(png.Id,Cells.Selection(),replace));
    }
}
