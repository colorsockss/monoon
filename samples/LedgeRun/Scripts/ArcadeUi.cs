using System;
using System.Drawing;
using Engine.Core;

internal static class ArcadeUi
{
    // Advances at SDL_ttf's 32px cache size for the bundled GowunDodum face.
    // These fields use only digits, a period, slash, dash, or a trailing seconds unit.
    private static ReadOnlySpan<byte> DigitAdvances => [18, 14, 17, 18, 18, 18, 17, 17, 17, 17];

    private static float NumberWidth(string value, float size)
    {
        int advance = 0;
        foreach (char c in value)
            advance += c switch
            {
                >= '0' and <= '9' => DigitAdvances[c - '0'],
                '.' => 10,
                '/' => 16,
                _ => 14,
            };
        return advance * size / 32;
    }

    internal static void RightNumber(IGameUi ui, Guid label, string value, float columnWidth, float size)
    {
        ui.SetText(label, value);
        if (ui is ScreenUi screen)
        {
            float move = Math.Max(0, MathF.Round(columnWidth - 2 - NumberWidth(value, size * ui.Preferences.TextScale)));
            screen.SetBoxOffset(label, new RectangleF(move, 0, -move, 0));
        }
    }

    internal static void HudNumber(IGameUi ui, Guid label, string value)
    {
        if (label == Guid.Empty) return;
        ui.SetText(label, $"<b>{value}</b>");
    }
}
