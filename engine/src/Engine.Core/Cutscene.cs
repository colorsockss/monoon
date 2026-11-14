using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using VYaml.Annotations;

namespace Engine.Core;

[YamlObject]
public sealed partial record DialogueLine(string Speaker, string Text);

[YamlObject]
public sealed partial record CutsceneSequence(DialogueLine[] Lines)
{
    public const string Extension = ".cutscene";
    public static CutsceneSequence Load(string path)
    {
        var sequence = ProjectYaml.Read<CutsceneSequence>(path);
        if (sequence.Lines is not { Length: > 0 and <= 1024 } ||
            sequence.Lines.Any(line => line == null || string.IsNullOrWhiteSpace(line.Speaker) ||
                string.IsNullOrWhiteSpace(line.Text) || line.Speaker.Length > 64 || line.Text.Length > 512))
            throw new InvalidDataException($"{path}: expected 1–1024 dialogue lines with a speaker (1–64 characters) and text (1–512 characters).");
        return sequence;
    }
}

[DisplayName("Cutscene Director"), YamlObject]
public sealed partial record CutsceneDirectorComponent : SceneComponent
{
    public Guid? Sequence { get; set; }
}

public enum CutsceneExit { Completed, Skipped, Interrupted }
public readonly record struct CutsceneState(Guid Director, int Line, int Count);

public sealed partial class SceneGame
{
    private sealed class Playback(Body director, CutsceneSequence sequence)
    {
        public Body Director { get; } = director;
        public DialogueLine[] Lines { get; } = sequence.Lines;
        public int Line;
        public InputDevice Device;
        public DialogueLayout? Layout;
    }

    private Playback? playback;
    private bool modalFrame;
    private Body? ViewCamera => playback?.Director is { Active: true, Camera: not null } shot ? shot : camera;
    public CutsceneState? Cutscene => playback is {} p ? new(p.Director.Id, p.Line, p.Lines.Length) : null;
    public CutsceneExit? LastCutsceneExit { get; private set; }
    public ScreenUi? InputUi => playback == null ? Ui : null;

    public bool PlayCutscene(Guid director)
    {
        if (playback != null || !IsActive(director) || byId[director] is not { Sequence: {} sequence } body ||
            body.Camera is { Output: not Camera2DComponent.Primary }) return false;
        playback = new(body, sequence);
        modalFrame = true;
        LastCutsceneExit = null;
        Ui.ResetInput();
        return true;
    }

    private void EndCutscene(CutsceneExit reason)
    {
        playback = null;
        LastCutsceneExit = reason;
        // An advance button may also be bound to jump or interact.
        Actions.DiscardGameEdges();
    }

    private void UpdateDialogue(GameInput input)
    {
        modalFrame = playback != null;
        if (playback is not {} p) return;
        p.Device = input.ActiveDevice;
        if (!IsActive(p.Director.Id)) EndCutscene(CutsceneExit.Interrupted);
        else if (input.Pressed("SkipCutscene")) EndCutscene(CutsceneExit.Skipped);
        else if (input.Pressed("DialogueNext"))
        {
            if (++p.Line == p.Lines.Length) EndCutscene(CutsceneExit.Completed);
            else p.Layout = null;
        }
    }

    private sealed record DialogueLayout(float Scale, float Size, float Pad, float Top, string[] Rows);
    private void DrawDialogue(ICanvas2D canvas, Playback p)
    {
        float w = LogicalSize.Width, h = LogicalSize.Height, scale = Ui.Preferences.TextScale;
        var line = p.Lines[p.Line];
        if (p.Layout is not {} layout || layout.Scale != scale)
        {
            float unit = Math.Min(w / 800, h / 450), pad = 20 * unit, font = 20 * unit * scale;
            string[] rows;
            float height;
            // Keep every authored line visible, including text without word separators.
            while (true)
            {
                rows = ScreenUi.WrapText(line.Text, w - 4 * pad, s => canvas.MeasureText(s, font));
                height = 2 * pad + (2 + rows.Length) * font * 1.4f;
                if (height <= h * .65f && canvas.MeasureText(line.Speaker, font) <= w - 6 * pad) break;
                font *= .85f;
            }
            p.Layout = layout = new(scale, font, pad, h - height, rows);
        }
        var (unused, size, margin, top, lines) = layout;
        var accent = Ui.Preferences.HighContrast ? Color.Yellow : Color.FromArgb(122, 223, 203);
        float left = 2 * margin, y = top + margin, step = size * 1.4f;
        canvas.FillRectangle(new(margin, top, w - 2 * margin, h - top), Color.FromArgb(16, 25, 38));
        canvas.FillRectangle(new(margin, top, w - 2 * margin, Math.Max(1, margin / 9)), accent);
        canvas.DrawText(line.Speaker, left, y, size * .83f, accent);
        foreach (var row in lines) canvas.DrawText(row, left, y += step, size, Color.White);
        string Key(string action) => (Actions.BindingFor(action, p.Device) ?? Actions.BindingFor(action, InputDevice.Keyboard) ??
            Actions.BindingFor(action, InputDevice.Gamepad) ?? action) switch { "Return" => "Enter", var key => key };
        string hint = $"{p.Line + 1} / {p.Lines.Length}     {Key("DialogueNext")}  {(p.Line + 1 == p.Lines.Length ? "Finish" : "Next")}     {Key("SkipCutscene")}  Skip";
        float hintSize = size * .72f;
        hintSize *= Math.Min(1, (w - 2 * left) / Math.Max(1, canvas.MeasureText(hint, hintSize)));
        canvas.DrawText(hint, left, y + step, hintSize, Color.WhiteSmoke);
    }
}
