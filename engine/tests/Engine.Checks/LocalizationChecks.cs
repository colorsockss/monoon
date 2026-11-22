using System.Drawing;
using Engine.Core;
using Engine.Desktop;
using Engine.Editor;
using SkiaSharp;

internal static class LocalizationChecks
{
    internal static void Run(string project, string scratch)
    {
        var scene = SceneDocument.Load(Path.Combine(project, "Scenes", "KeyRoom.scene"));
        using var game = new GameSession(scene);
        var ui = game.Current.Ui;
        var canvas = new Labels();
        var router = new GameInputRouter(game.Actions, () => ui, game.LogicalSize);
        var loop = new GameLoop(game, game.Actions);
        void Step() => loop.Advance(GameTime.FixedStep);
        void Key(string key) { router.Key(key, true); router.Key(key, false); Step(); }
        void Expect(params string[] labels)
        {
            canvas.Drawn.Clear();
            ui.Draw(canvas);
            foreach (var label in labels)
                if (!canvas.Drawn.Contains(label)) throw new Exception($"Missing label '{label}' in {string.Join(" | ", canvas.Drawn)}");
        }

        Key("I");
        Expect("Inventory", "Pocket compass  /  In bag", "Quest  /  Find Brass key");
        Key("L");
        Expect("가방", "휴대용 나침반  /  보관 중", "의뢰  /  황동 열쇠 찾기");
        Key("L");
        Expect("Inventory", "Pocket compass  /  In bag", "Quest  /  Find Brass key");
        Console.WriteLine("PASS KeyRoom bag title, item and quest switch en→ko and ko→en on the first L input frame");
        ui.Preferences.Locale = "ko-KR";
        Step();
        Expect("가방", "휴대용 나침반  /  보관 중");
        Key("I");
        router.Key("Left", true);
        for (int i = 0; i < 25; i++) Step();
        router.Key("Left", false);
        Key("I");
        Expect("의뢰 완료  /  황동 열쇠");
        var title = scene.Objects.Single(o => o.Name == "Inventory").Id;
        ui.SetText(title, "override");
        Expect("override");
        ui.SetTextKey(title, "bag.title");
        ui.Preferences.Locale = "fr-CA";
        Expect("Inventory", "Pocket compass  /  In bag", "Quest complete  /  Brass key");
        if (ui.Translate("unknown") != "unknown" || ui.Translate("unknown", "author text") != "author text")
            throw new Exception("Missing key fallbacks");

        // A deliberately incomplete regional column must fall back per row, even for an exact tag.
        var folder = Directory.CreateDirectory(Path.Combine(scratch, "LocaleFixture")).FullName;
        var table = GameTable.Parse("""
            version: 1
            columns: [{name: en, type: Text}, {name: ko, type: Text}, {name: ko-KR, type: Text}]
            rows:
            - {key: parent, cells: ["English", "한국어", ""]}
            - {key: region, cells: ["English", "", "대한민국"]}
            - {key: fallback, cells: ["English", "", ""]}
            """);
        table.Save(Path.Combine(folder, "Strings.table"), false);
        var assets = ProjectAssets.Scan(folder);
        var language = GameLocalization.Load(folder, assets, new() { Table = assets.IdFor("Strings.table") });
        if (language.Resolve("KO-kr", "parent") != "한국어" || language.Resolve("ko-KR", "region") != "대한민국" ||
            language.Resolve("ko-KR", "fallback") != "English") throw new Exception("Per-row locale fallback");

        var fonts = canvas.Fonts;
        if (fonts.Count != 2) throw new Exception("Expected two packaged fonts");
        using var first = SKTypeface.FromFile(fonts[0]);
        using var second = SKTypeface.FromFile(fonts[1]);
        var strings = GameTable.Load(Path.Combine(project, "Assets", "KeyRoomText.table"));
        var hangul = strings.Rows.SelectMany(row => row.Cells).SelectMany(cell => cell.GetString()!)
            .Where(ch => ch is >= '가' and <= '힣').Distinct().ToArray();
        if (hangul.Any(ch => !first.ContainsGlyph(ch) && !second.ContainsGlyph(ch)))
            throw new Exception("A Korean syllable in the KeyRoom table has no packaged glyph.");
        Console.WriteLine($"PASS all {hangul.Length} Korean syllables in the KeyRoom table have packaged glyphs");
        using var renderer = new GameTextRasterizer();
        byte[] Pixels(string word, params string[] files)
        {
            renderer.UseFonts(files);
            using var bitmap = renderer.Rasterize(word);
            return bitmap.GetPixelSpan().ToArray();
        }
        var primary = Pixels("가", fonts[0]);
        var fallback = Pixels("휴", fonts[1]);
        if (!Pixels("가", fonts[0], fonts[1]).SequenceEqual(primary) || !Pixels("휴", fonts[0], fonts[1]).SequenceEqual(fallback) ||
            Pixels("휴", fonts[0]).SequenceEqual(fallback) || Pixels("가", fonts[1]).SequenceEqual(primary) ||
            !Pixels("휴", fonts[0], fonts[1]).SequenceEqual(fallback))
            throw new Exception("Packaged primary and fallback glyphs or NFC composition changed.");
        CheckPlayerGlyphs(project, scratch, fonts, scene);
        CheckFontImport(folder, scratch, fonts[0]);
        Console.WriteLine("PASS shared KeyRoom UI/item/quest translations, per-row locale fallback, primary/fallback Korean pixel matching");
    }

    private static void CheckPlayerGlyphs(string project, string scratch, IReadOnlyList<string> fonts, SceneDocument scene)
    {
        var game = Path.Combine(scratch, "CookedFonts");
        var packaged = fonts.Select(path => Path.Combine(game, Path.GetRelativePath(project, path))).ToArray();
        for (int i = 0; i < packaged.Length; i++)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(packaged[i])!);
            File.Copy(fonts[i], packaged[i]);
        }
        var localization = GameLocalization.Load(project, ProjectAssets.ReadManifest(project), GameSettings.Load(project).Localization);
        FontGlyphBuild.Write(project, game, localization, [scene]);
        using IGameTextRasterizer preview = new GameTextRasterizer();
        using var player = new SdlFontRasterizer();
        preview.UseFonts(fonts);
        player.UseFonts(packaged);
        const string label = "가방 / 휴대용 나침반";
        if (!preview.Rasterize(label).Pixels.SequenceEqual(player.Rasterize(label).Pixels) ||
            preview.Measure(label, 19) != player.Measure(label, 19))
            throw new Exception("Cooked Korean glyph pixels or UI metrics differ from the editor.");
        using var fallback = SKTypeface.FromFile(fonts[1]);
        var baked = FontGlyphs.Read(packaged[1], File.ReadAllBytes(packaged[1]))!;
        var dynamicChar = Enumerable.Range('가', '힣' - '가' + 1).First(cp => fallback.ContainsGlyph(cp) && !baked.ContainsKey(cp));
        var dynamicLabel = char.ConvertFromUtf32(dynamicChar);
        player.UseFonts([packaged[1]]);
        var pixels = player.Rasterize(dynamicLabel).Pixels;
        if (pixels.SequenceEqual(player.Rasterize("\u0378").Pixels) || !pixels.Where((_, i) => i % 4 == 3).Any(a => a > 0))
            throw new Exception("A character entered after export lost its glyph.");
        // Replace a packaged font as a content update would. The old coverage must be ignored.
        File.Copy(fonts[1], packaged[0], true);
        player.UseFonts([packaged[0]]);
        if (FontGlyphs.Read(packaged[0], File.ReadAllBytes(packaged[0])) != null ||
            !player.Rasterize(dynamicLabel).Pixels.SequenceEqual(pixels))
            throw new Exception("Stale cooked glyphs were applied to a replacement font.");
        Console.WriteLine("PASS SDL game text: cooked Korean glyphs and layout match preview; additional and updated glyphs load from the font.");
    }

    private static void CheckFontImport(string folder, string scratch, string font)
    {
        var imported = ProjectImport.Copy(folder, "Assets", [font]).Single();
        var license = imported + ".license";
        var original = File.ReadAllBytes(font + ".license");
        var other = "Assets/Primary.ttf";
        var edit = ProjectPathEdit.Move(folder, imported, other);
        if (File.Exists(Path.Combine(folder, license)) || !File.ReadAllBytes(Path.Combine(folder, other + ".license")).SequenceEqual(original))
            throw new Exception("Moving the font did not move its license.");
        edit.Restore(false);
        var selection = WorkspaceArchives.Dependencies(folder, [Path.Combine(folder, imported)]);
        if (!selection.Contains(license)) throw new Exception("Missing license in font package dependencies.");
        var archive = Path.Combine(scratch, "Font.monopackage");
        WorkspaceArchives.Export(folder, archive, WorkspaceArchives.Package, selection);
        var clone = Directory.CreateDirectory(Path.Combine(scratch, "FontClone")).FullName;
        WorkspaceArchives.Install(archive, clone, WorkspaceArchives.Package);
        if (!File.ReadAllBytes(Path.Combine(clone, license)).SequenceEqual(original)) throw new Exception("Font license lost in package.");
        var damaged = Path.Combine(scratch, "Broken.ttf");
        File.WriteAllBytes(damaged, [0, 1, 2]);
        try { ProjectImport.Copy(folder, "Assets", [damaged]); throw new Exception("Invalid font imported."); }
        catch (InvalidDataException) { }
        if (File.Exists(Path.Combine(folder, "Assets/Broken.ttf"))) throw new Exception("Partial font import.");
    }

    private sealed class Labels : ICanvas2D, IDisposable
    {
        public readonly List<string> Drawn = [];
        public IDisposable PushClip(RectangleF area) => this;
        public void Dispose() { }
        public IReadOnlyList<string> Fonts { get; private set; } = [];
        public void UseFonts(IReadOnlyList<string> paths) => Fonts = paths;
        public void DrawText(string text, float x, float y, float size, Color color) => Drawn.Add(text);
        public void Clear(Color color) { }
        public void FillRectangle(RectangleF area, Color color) { }
        public bool DrawSprite(string path, RectangleF area) => true;
    }
}
