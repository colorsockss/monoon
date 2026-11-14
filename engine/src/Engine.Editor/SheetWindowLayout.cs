using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Engine.Editor;

internal static class SheetWindowLayout
{
    internal static void UpdateGridButton(ToggleButton toggle, int width, int height)
    {
        toggle.Content = width > 0 && height > 0 ? $"Grid · {width} × {height}" : "Grid settings";
    }

    // Keep status readable above a single right-aligned action row on compact windows.
    internal static void ArrangeFooter(Grid footer, Control status, StackPanel actions)
    {
        footer.SizeChanged += (_, e) =>
        {
            bool compact = e.NewSize.Width < 500;
            Grid.SetColumnSpan(status, compact ? 2 : 1);
            Grid.SetColumn(actions, compact ? 0 : 1);
            Grid.SetColumnSpan(actions, compact ? 2 : 1);
            Grid.SetRow(actions, compact ? 1 : 0);
            footer.RowSpacing = compact ? 4 : 0;
        };
    }
}
