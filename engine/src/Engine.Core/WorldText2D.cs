using System.ComponentModel;
using System.Drawing;
using System.Numerics;
using VYaml.Annotations;

namespace Engine.Core;

public enum TextAlign2D { Left, Center, Right }

/// <summary>Text drawn in the world at its object (roadmap 16.6): it moves, scales and sorts like a sprite and uses the
/// screen text's SDL_ttf label cache, so a label that keeps its text draws without allocating. Rotation is not applied.</summary>
[DisplayName("Text 2D"), YamlObject, EngineFeature(EngineFeature.Text)]
public sealed partial record Text2DComponent : SceneComponent
{
    public Text2DComponent() { }
    [YamlConstructor] public Text2DComponent(string text = "Text", float size = 16, string color = "#FFFFFF", TextAlign2D align = TextAlign2D.Center)
        => (Text, Size, Color, Align) = (text, size, color, align);
    public string Text { get; set; } = "Text";
    /// <summary>A row of the project's translation table; Text is the fallback.</summary>
    public string? TextKey { get; set; }
    /// <summary>Size in world units at Transform scale 1.</summary>
    public float Size { get; set; } = 16;
    public string Color { get; set; } = "#FFFFFF";
    /// <summary>Horizontal place in the object's rectangle; the text is centred vertically.</summary>
    public TextAlign2D Align { get; set; } = TextAlign2D.Center;
    /// <summary>Color, bold and size tags as in UI Text (<see cref="RichTextLayout"/>); icon tags stay literal.</summary>
    public bool RichText { get; set; }
    public int SortingLayer { get; set; }
    public int Order { get; set; }
    public bool SortByY { get; set; }

    internal void Validate(string name)
    {
        if (Text == null || Size is not (> 0 and <= 1024) || !SceneObject.TryColor(Color, out _) || !Enum.IsDefined(Align))
            throw new InvalidDataException($"{name}: Text 2D needs text, size 0–1024 and a #RRGGBB colour.");
    }
}

// Runtime and edit-view drawing of one Text 2D. The rich layout is rebuilt only when the text or the canvas type changes.
internal sealed class WorldTextLabel(Text2DComponent config)
{
    public readonly Text2DComponent Config = config;
    public string? Override; // IGameObjects.SetWorldText
    private readonly Color ink = SceneObject.TryColor(config.Color, out var c) ? c : System.Drawing.Color.White;
    private string? locale, translated, laidOut;
    private Type? backend;
    private RichTextLayout? layout;

    public string Text(ScreenUi? ui)
    {
        if (Override != null) return Override;
        if (Config.TextKey is not { Length: > 0 } key || ui == null) return Config.Text;
        // Resolve once per locale: a regional tag (ko-KR) would otherwise cut a new substring every frame.
        if (!ReferenceEquals(locale, ui.Preferences.Locale)) (locale, translated) = (ui.Preferences.Locale, ui.Translate(key, Config.Text));
        return translated!;
    }

    public void Draw(ICanvas2D canvas, string text, Vector2 size, Matrix3x2 matrix, Matrix3x2 view, RectangleF frame)
    {
        if (text.Length == 0) return;
        float width, height;
        if (Config.RichText)
        {
            if (!ReferenceEquals(laidOut, text) || backend != canvas.GetType())
            {
                layout = RichTextLayout.Build(text, true, Config.Size, float.PositiveInfinity, canvas.MeasureText, null, 1, canvas.MeasureTextEnds);
                (laidOut, backend) = (text, canvas.GetType());
            }
            (width, height) = (layout!.Width, layout.Height);
        }
        else (width, height) = (canvas.MeasureText(text, Config.Size), Config.Size * 1.4f);
        var box = new RectangleF((size.X - width) * (int)Config.Align * .5f, (size.Y - height) * .5f, width, height);
        if (!frame.IntersectsWith(Affine2D.Bounds(box, matrix))) return;
        var screen = matrix * view;
        float scale = MathF.Sqrt(screen.M11 * screen.M11 + screen.M12 * screen.M12);
        if (!(scale > 0)) return;
        var at = Affine2D.Transform(new Vector2(box.X, box.Y), screen);
        if (layout != null && Config.RichText) layout.Draw(canvas, at.X, at.Y, scale, int.MaxValue, ink);
        else canvas.DrawText(text, at.X, at.Y, Config.Size * scale, ink);
    }
}

public sealed partial class SceneGame
{
    /// <summary>Text 2D: replaces the drawn text (and its translation key) until the scene ends.</summary>
    public bool SetWorldText(Guid id, string text)
    {
        if (text == null || removed.Contains(id) || !byId.TryGetValue(id, out var b) || b.Text == null) return false;
        b.Text.Override = text;
        return true;
    }
}
