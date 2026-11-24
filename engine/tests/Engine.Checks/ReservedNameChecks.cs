using System.Text.RegularExpressions;
using Engine.Core;

// The player's Windows device-name checks were Regex patterns; the hand-written versions must accept and reject the same names.
static class ReservedNameChecks
{
    [Check(100, StopAfter = "names")] static void Registered(CheckRun c) => Run();
    public static void Run()
    {
        var asset = new Regex(@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)", RegexOptions.IgnoreCase);
        var script = new Regex(@"^(CON|AUX|NUL|PRN|COM[1-9]|LPT[1-9])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        string[] stems = ["CON", "con", "Con", "PRN", "aux", "NuL", "COM1", "com9", "LPT5", "lpt9", "COM0", "LPT0", "COM10", "COMX",
            "CONX", "CO", "C", "", "XCON", "NULL", "AUXILIARY", "COM", "LPT", "COM¹", "COM١", "ＣＯＮ", "CON_", "_CON", "Door", "KeyRoom"];
        string[] tails = ["", ".", ".txt", ".png.meta", "x", " ", ".scene", "1", "_1"];
        int compared = 0;
        foreach (var stem in stems)
            foreach (var tail in tails)
            {
                var name = stem + tail;
                bool expected = name.Length > 0 && asset.IsMatch(name);
                if (ProjectAssets.WindowsDeviceName(name) != expected) throw new Exception($"Device name check differs from the old pattern for '{name}'.");
                bool wasValid = !string.IsNullOrWhiteSpace(name) && name == name.Trim() && !name.StartsWith('.') && !name.EndsWith('.')
                    && !name.Any(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c)) && !asset.IsMatch(name);
                if (ProjectAssets.ValidName(name) != wasValid) throw new Exception($"Asset name validation changed for '{name}'.");
                bool wasScript = !string.IsNullOrWhiteSpace(name) && name.Length <= 100 && (char.IsLetter(name[0]) || name[0] == '_')
                    && name.All(c => char.IsLetterOrDigit(c) || c == '_') && !script.IsMatch(name);
                if (ScriptLibrary.ValidName(name) != wasScript) throw new Exception($"Script name validation changed for '{name}'.");
                compared++;
            }
        Check(!ProjectAssets.ValidName("CON") && !ProjectAssets.ValidName("com1") && !ProjectAssets.ValidName("LPT9.txt")
            && ProjectAssets.ValidName("CONX") && ProjectAssets.ValidName("COM10") && ProjectAssets.ValidName("A.CON"),
            "asset names: CON, com1, LPT9.txt reserved; CONX, COM10, A.CON allowed");
        Check(!ScriptLibrary.ValidName("Con") && !ScriptLibrary.ValidName("lpt3") && ScriptLibrary.ValidName("Console") && ScriptLibrary.ValidName("COM0"),
            "script names: Con, lpt3 reserved; Console, COM0 allowed");
        Check(compared == stems.Length * tails.Length, $"device names match the previous patterns for {compared} names");
    }

    private static void Check(bool pass, string text) { if (!pass) throw new Exception(text); Console.WriteLine("PASS " + text); }
}
