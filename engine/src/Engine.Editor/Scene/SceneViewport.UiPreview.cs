using RectangleF = System.Drawing.RectangleF;
using Vector2 = System.Numerics.Vector2;
using Avalonia;
using Engine.Core;

namespace Engine.Editor;

public sealed partial class SceneViewport
{
    private ScreenUi? previewUi;
    internal sealed record UiPreview(int Width, int Height, string Locale, float TextScale);
    internal UiPreview? Preview { get; private set; }
    private GameSettings? previewSource, previewSettings;
    internal GameSettings LayoutSettings
    {
        get
        {
            if (Preview is not {} preview) return Scene.Settings;
            if (previewSettings == null || !ReferenceEquals(previewSource, Scene.Settings))
            {
                previewSource = Scene.Settings;
                previewSettings = previewSource with { Width = preview.Width, Height = preview.Height,
                    Accessibility = previewSource.Accessibility with { Locale = preview.Locale, TextScale = preview.TextScale } };
            }
            return previewSettings;
        }
    }
    internal void SetUiPreview(UiPreview? preview)
    {
        Preview = preview;
        previewSettings = null;
        InvalidateUi();
    }

    internal (RectangleF Box, RectangleF Parent) UiPlacement(SceneObject obj)
    {
        var ui = PreviewUi;
        var box = ui.Bounds(obj.Id) ?? RectangleF.Empty;
        for (var parent = obj.Parent; parent != null; parent = parent.Parent)
            if (ui.Bounds(parent.Id) is {} bounds) return (box, bounds);
        return (box, new RectangleF(0, 0, LayoutSettings.Width, LayoutSettings.Height));
    }
    private ScreenUi PreviewUi
    {
        get
        {
            if (previewUi == null)
            {
                var document = Preview == null ? Scene : Scene.WithSettings(LayoutSettings);
                try { previewUi = new(document, Scene.ProjectDirectory is { } root ? ProjectAssets.ReadManifest(root) : null); }
                catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
                {
                    previewUi = new(document, null);
                    if (spriteErrors.Add(ex.Message)) PlayFailed?.Invoke(ex.Message);
                }
                foreach (var id in HiddenIds) previewUi.SetVisible(id, false);
            }
            previewUi.PreviewModeFor(Selected?.Id);
            return previewUi;
        }
    }
    internal void InvalidateUi() { previewUi = null; editOrder = null; RefreshScene(); }
    private (RectangleF Frame, float Zoom) UiFrame()
    {
        if (Scene.Camera is {} camera)
        {
            var shot=camera.GetComponent<Camera2DComponent>()!.View(LayoutSettings.Resolution,camera.WorldPosition);
            var pos=shot.ScreenToWorld(Vector2.Zero);
            return (new(pos.X,pos.Y,LayoutSettings.Width/shot.Zoom,LayoutSettings.Height/shot.Zoom),shot.Zoom);
        }
        const float zoom=1;
        var center = Scene.Camera?.WorldPosition ?? new Vector2(LayoutSettings.Width / 2f, LayoutSettings.Height / 2f);
        return (LayoutSettings.CameraFrame(center, zoom), zoom);
    }
    private Point EditUiPoint(Point world)
    {
        var (f, z) = UiFrame();
        return new((world.X - f.Left) * z, (world.Y - f.Top) * z);
    }
}
