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
        var image = file != null ? DecodePng(file, 160) : null;
        ProjectPreview.Source = image;
        ProjectPreviewPanel.IsVisible = image != null;
        previewBitmap?.Dispose();
        previewBitmap = image;
        previewFile = file;
        previewHash = hash;
    }

    // Small PNGs keep their pixels; larger ones decode at the height shown.
    private static Bitmap? DecodePng(string file, int maxHeight)
    {
        if (!File.Exists(file)) return null;
        try
        {
            var size = SpriteRegion.ReadPngSize(file);
            using var input = File.OpenRead(file);
            return size.Height > maxHeight ? Bitmap.DecodeToHeight(input, maxHeight) : new Bitmap(input);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException) { return null; }
    }

    // Grid thumbnails, decoded once per file version. Bitmaps are not disposed here: a cleared entry may still be on screen.
    private static class ProjectThumbnails
    {
        private static readonly Dictionary<string, (DateTime Stamp, Bitmap? Image)> cache = [];
        public static Bitmap? Get(string file)
        {
            var stamp = File.GetLastWriteTimeUtc(file);
            if (cache.TryGetValue(file, out var hit) && hit.Stamp == stamp) return hit.Image;
            // shortcut: a project with more than 512 PNGs re-decodes as folders change; add an LRU if that shows up in profiles.
            if (cache.Count >= 512) cache.Clear();
            var image = DecodePng(file, 96);
            cache[file] = (stamp, image);
            return image;
        }
    }
}
