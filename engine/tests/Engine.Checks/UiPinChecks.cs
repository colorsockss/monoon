using System.Drawing;
using Engine.Core;

internal static class UiPinChecks
{
    internal static void Run(string project)
    {
        var scene = new SceneDocument();
        var canvas = scene.CreateUi("Canvas",null);
        var parent = scene.CreateUi("Group",canvas);
        parent.X = 23; parent.Y = 45; parent.Width = 270; parent.Height = 130;
        var child = scene.CreateUi("Button",parent);
        child.X = 3; child.Y = -4; child.Width = 50; child.Height = 32;
        var rect = child.GetComponent<UiRectComponent>()!;
        rect.Anchor = "Center";
        var ui = new ScreenUi(scene,null);
        void Expect(float x,float y,float w,float h)
        {
            var expected = new RectangleF(parent.X+x,parent.Y+y,w,h);
            if (ui.Bounds(child.Id) != expected) throw new Exception($"UI pins: {ui.Bounds(child.Id)} != {expected}");
        }
        Expect(113,45,50,32);
        rect.Right = 8; rect.Top = 11;
        Expect(212,11,50,32);
        rect.Left = -5; rect.Bottom = 13;
        Expect(-5,11,267,106);
        parent.Width = 110; parent.Height = 72;
        Expect(-5,11,107,48);
        rect.Right = 200; rect.Bottom = 100;
        Expect(-5,11,0,0);
        rect.Right = null; rect.Top = null; rect.Bottom = 5;
        Expect(-5,35,50,32);
        scene.Validate();
        var copy = ProjectYaml.Decode<SceneDocument>(ProjectYaml.Encode(scene));
        copy.Validate();
        var saved = copy.Objects.Single(o=>o.Id==child.Id).GetComponent<UiRectComponent>()!;
        if (copy.SchemaVersion != 28 || saved.Left != -5 || saved.Right.HasValue || saved.Top.HasValue || saved.Bottom != 5)
            throw new Exception("UI pin round trip");
        rect.Left = float.NaN;
        try { scene.Validate(); throw new Exception("Nonfinite margin accepted"); } catch (InvalidDataException) { }
        rect.Left = rect.Bottom = null;
        scene.Validate();
        if (scene.SchemaVersion >= 28) throw new Exception("Unpinned scene requires a newer reader");

        var list = scene.CreateUi("Layout",parent);
        list.X = list.Y = 0;
        list.Width = 180; list.Height = 80;
        var row = scene.CreateUi("Text",list);
        row.GetComponent<UiRectComponent>()!.Right = 45;
        ui = new ScreenUi(scene,null);
        if (ui.Bounds(row.Id)!.Value.Width != list.Width-12) throw new Exception("Layout must own its direct rows");
        var keyRoom = SceneDocument.Load(Path.Combine(project, "Scenes", "KeyRoom.scene"));
        var original = keyRoom.Settings;
        var settings = original with { Width = 480, Height = 720, Accessibility = new("ko", 2) };
        using var game = new GameSession(keyRoom, null, null, PlayerStorage.Unavailable, settings);
        var guide = ProjectAssets.ReadManifest(project).IdFor("Scenes/RoomGuide.scene")!.Value;
        game.State.LoadScene(guide);
        game.FixedUpdate(System.Numerics.Vector2.Zero, 1f/60);
        if (game.SceneId != guide || game.LogicalSize != settings.Resolution || game.Current.Ui.Preferences.TextScale != 2 ||
            game.Current.Ui.Preferences.Locale != "ko" || !ReferenceEquals(original, keyRoom.Settings))
            throw new Exception("Session settings were lost on scene change or changed the source document");
        Console.WriteLine("PASS UI edge pins, legacy anchor, resize, collapsed rect, YAML, layout precedence and session settings across scenes");
    }
}
