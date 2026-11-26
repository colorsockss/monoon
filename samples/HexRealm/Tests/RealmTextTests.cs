using System.Collections.Generic;
using System.Text.RegularExpressions;
using Engine.Core;

public sealed class RealmTextTests
{
    // The printable ASCII cmap of the bundled GowunDodum subset omits some punctuation.
    private const string Punctuation = " !()+,-./:?[]{}";
    private static readonly Regex RichTags = new(@"</?b>|<color=#[0-9a-fA-F]{6}>|</color>");

    [GameTest("Scenes/Realm.scene", MaxFrames = 3300)]
    public IEnumerable<ScriptWait> UiStringsUseGlyphsInTheBundledFont(GameTestRun run)
    {
        var board = run.Script<RealmBoard>("Board controller");
        var checkedText = new HashSet<(string, bool)>();
        void CheckText(string? text, bool rich)
        {
            if (text == null || !checkedText.Add((text, rich))) return;
            foreach (char c in rich ? RichTags.Replace(text, "") : text)
                run.Assert(char.IsAsciiLetterOrDigit(c) || Punctuation.Contains(c),
                    $"GowunDodum has no approved glyph for U+{(int)c:X4} in '{text}'.");
        }
        void CheckLabels()
        {
            foreach (var obj in run.World.Objects)
            {
                if (obj.GetComponent<UiTextComponent>() is { } label)
                    CheckText(run.World.Ui.TextOf(obj.Id), label.RichText);
                if (obj.GetComponent<Text2DComponent>() is { } worldText)
                    CheckText(worldText.Text, worldText.RichText);
            }
        }

        CheckLabels(); // All authored UI, including hidden screens.
        run.Tap("N");
        yield return ScriptWait.Frames(32);
        CheckLabels();
        run.Tap("B");
        yield return ScriptWait.Frames(3);
        run.Tap("R");
        yield return ScriptWait.Frames(3);
        CheckLabels();
        while (board.Turn <= RealmRules.LastTurn && board.Winner == 0)
        {
            run.Tap("Return");
            yield return ScriptWait.Frames(3);
            CheckLabels(); // Includes the transient rival stamp.
            yield return ScriptWait.Frames(107);
            CheckLabels(); // Includes the turn stamp, forecast and any claim notice.
        }
        run.Assert(board.Winner != 0, "the campaign reached the charter");
        yield return ScriptWait.Frames(110);
        CheckLabels();
        run.Tap("F5");
        yield return ScriptWait.Frames(3);
        CheckLabels();
    }
}
