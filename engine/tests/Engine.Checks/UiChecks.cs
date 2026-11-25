using System.Drawing;
using Engine.Core;

internal static class UiChecks
{
    [Check(8800)] static void Registered(CheckRun c) => Run(c.Scene, c.Scratch);
    public static void Run(SceneDocument scene, string scratch)
    {
        var obj = scene.Objects.Single(o => o.Name == "Controls button");
        var assets = ProjectAssets.Scan(scratch);
        var ui = new ScreenUi(scene, assets);
        Require(ui.Bounds(obj.Id) == new RectangleF(700,402,88,28), "bottom-right anchor uses game width and height");
        var duplicate = scene.DuplicateObject(obj);
        Require(new ScreenUi(scene, assets).Pick(744,416) == duplicate, "topmost duplicate wins picking");
        scene.RemoveObject(duplicate);
        var menu = new SceneDocument();
        var canvas = menu.CreateUi("Canvas", null);
        var button = menu.CreateUi("Button", canvas);
        var group = menu.CreateUi("Group", canvas);
        group.GetComponent<UiRectComponent>()!.Anchor = "BottomRight";
        var rect = new ScreenUi(menu, null).Bounds(button.Id);
        menu.Reparent(button, group);
        Require(new ScreenUi(menu, null).Bounds(button.Id) == rect, "reparent across anchors preserves UI position");
        using (var game = new GameSession(menu))
        {
            var uiOnly = game.Current.Ui;
            Require(menu.IsUiOnly && game.Current.ActiveCameraId == null, "UI-only menu runs without world camera");
            Require(uiOnly.Key("Tab", true) && uiOnly.Key("Tab", false), "UI-only menu receives keyboard focus");
            Require(uiOnly.SetText(button.Id,"HP 12") && button.GetComponent<UiTextComponent>()!.Text == "Button", "dynamic text leaves asset unchanged");
            uiOnly.SetInteractable(button.Id,false);
            Require(!uiOnly.Key("Tab", true), "disabled button leaves navigation");
            uiOnly.SetVisible(group.Id,false);
            Require(uiOnly.Pick(20,20) == null, "hidden group removes child from hit testing");
        }
        var snapshot = scene.CaptureObjects();
        scene.RestoreObjects(snapshot);
        Require(new ScreenUi(scene, assets).Bounds(obj.Id) == ui.Bounds(obj.Id), "UI survives Undo snapshot restore");
        var old = Path.Combine(scratch, "Scenes", "OldHud.scene");
        var id = Guid.NewGuid();
        File.WriteAllText(old, $"schemaVersion: 6\nobjects:\n- id: {id}\n  name: Badge\n  x: 14\n  width: 36\n  components:\n  - {{type: hud, text: KEY, image: 875948d6-2de7-530e-8e85-b9b197747518, requiredFlag: key}}\n");
        var converted = SceneDocument.Load(old);
        var badge = converted.Objects.Single();
        Require(File.ReadAllText(old).Contains("type: hud") && badge.Id == id && badge.GetComponent<UiTextComponent>()?.InsetX == 42 &&
            badge.GetComponent<UiRectComponent>()?.ShowIf == "key", "v6 HUD converts in memory without overwriting source");
        converted.Save(old);
        Require(!File.ReadAllText(old).Contains("type: hud") && SceneDocument.Load(old).Objects.Single().GetComponent<UiImageComponent>()?.Sprite != null,
            "converted HUD saves and reopens using UI components only");
        File.Delete(old);
    }
    private static void Require(bool passed, string label)
    {
        if (!passed) throw new Exception(label);
        Console.WriteLine("PASS " + label);
    }
}
