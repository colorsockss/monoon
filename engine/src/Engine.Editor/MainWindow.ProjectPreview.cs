using Avalonia.Media.Imaging;
using Engine.Core;

namespace Engine.Editor;

public partial class MainWindow
{
    private Bitmap? previewBitmap;
    private string? previewFile, previewHash;

    private void RefreshProjectPreview(ProjectAssets assets)
    {
        var selected = ProjectBrowser.SelectedItem as ProjectEntry;
        var hash = assets.Entries.FirstOrDefault(e => e.Path == selected?.RelativePath)?.Hash;
        if (hash != previewHash || previewBitmap == null || !File.Exists(previewFile)) ShowProjectPreview(hash);
    }

    private void ShowProjectPreview(string? hash = null)
    {
        var file = project != null && ProjectBrowser.SelectedItem is ProjectEntry { IsFolder: false } entry &&
            SpriteAssets.IsPng(entry.RelativePath) ? Path.Combine(project, entry.RelativePath) : null;
        if (file == previewFile && File.Exists(file) && previewBitmap != null && (hash == null || hash == previewHash)) return;
        Bitmap? image = null;
        if (file != null && File.Exists(file))
        {
            try
            {
                var size = SpriteRegion.ReadPngSize(file);
                using var input = File.OpenRead(file);
                image = size.Height > 160 ? Bitmap.DecodeToHeight(input, 160) : new Bitmap(input);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException) { }
        }
        ProjectPreview.Source = image;
        ProjectPreviewPanel.IsVisible = image != null;
        previewBitmap?.Dispose();
        previewBitmap = image;
        previewFile = file;
        previewHash = hash;
    }
}
