using System.Diagnostics;
using Engine.Core;
using Color = System.Drawing.Color;
using RectangleF = System.Drawing.RectangleF;

namespace Engine.Editor;

// Roadmap 16.15: a selected Navigation Region 2D shows the cells a top-down agent (radius Agent W / 2) can stand on,
// baked from the document with the same rules as SceneGame.BakeArea.
public sealed partial class SceneViewport
{
    private static readonly Color NavOpenColor = Color.FromArgb(56, 84, 220, 198);
    private NavArea2D? navPreview;
    private (Guid Region, System.Numerics.Matrix3x2 Pose, float W, float H, NavigationRegion2DComponent Config, float Spacing, float Agent, uint Mask) navKey;
    private long navBaked;

    internal NavArea2D? SelectedNavArea()
    {
        if (Selection.Count != 1 || IsPlaying || Selection[0].GetComponent<NavigationRegion2DComponent>() is not { Enabled: true } config) return null;
        var region = Selection[0];
        var key = (region.Id, region.WorldMatrix, region.Width, region.Height, config, config.Spacing, config.AgentWidth, config.TerrainMask);
        // Collider and tile edits are not tracked here: re-bake at most twice a second while the region stays selected.
        if (navPreview == null || key != navKey || Stopwatch.GetElapsedTime(navBaked).TotalSeconds > .5)
        {
            try { navPreview = NavArea2D.BakeDocument(region, Scene.Objects, Palette); }
            catch (InvalidDataException) { navPreview = null; }
            (navKey, navBaked) = (key, Stopwatch.GetTimestamp());
        }
        return navPreview;
    }

    private void DrawNavArea(ISceneCanvas canvas)
    {
        if (EditMap != null || SelectedNavArea() is not { } area) return;
        // One rectangle per run of open cells in a row.
        for (int y = 0; y < area.Rows; y++)
            for (int x = 0; x < area.Columns; x++)
            {
                if (!area.IsOpen(x, y)) continue;
                int end = x;
                while (end + 1 < area.Columns && area.IsOpen(end + 1, y)) end++;
                canvas.FillRectangle(new RectangleF(area.Bounds.Left + x * area.CellSize, area.Bounds.Top + y * area.CellSize,
                    (end - x + 1) * area.CellSize, area.CellSize), NavOpenColor);
                x = end;
            }
    }
}
