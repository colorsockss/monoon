using System.Numerics;
using Engine.Core;

internal static class InputMapChecks
{
    [Check(6700)] static void Registered(CheckRun c) => Run(c.Scratch);
    internal static void Run(string scratch)
    {
        static void Check(bool ok,string name) { if(!ok) throw new Exception("Input map: "+name); }
        for (int digit = 0; digit <= 9; digit++)
            Check(InputControls.KeyName("Digit" + digit) == digit.ToString() &&
                InputControls.KeyName("Num" + digit) == digit.ToString(), "editor and SDL digit bindings");
        var root=Directory.CreateDirectory(Path.Combine(scratch,"InputFixture")).FullName;
        File.WriteAllText(Path.Combine(root,"GameSettings"),"width: 800\nheight: 450\ninput:\n  left: J\n  right: L\n");
        var legacy=GameSettings.Load(root);
        var actions=new InputActions(legacy.Input);
        actions.Press("J"); Check(actions.Take().Move==-Vector2.UnitX,"legacy remap");
        legacy.Save(root); var yaml=File.ReadAllText(Path.Combine(root,"GameSettings"));
        Check(yaml.Contains("actions:") && !yaml.Contains("  left:") && GameSettings.Load(root).Input.Actions!.Length==5,"one canonical YAML map");
        var settings=legacy with { Input=new() { Actions=[new("Use",["U","Mouse.Left","Pad.South"]),new("UseAgain",["U"])] } };
        settings.Save(root); actions=new(GameSettings.Load(root).Input);
        var router=new GameInputRouter(actions,()=>null,new(800,450));
        router.Key("U",true);router.Key("U",true);router.Key("U",false);
        var shortTap=router.Take();
        Check(shortTap.Pressed("use") && shortTap.Released("Use") && !shortTap.Held("Use") && shortTap.Pressed("UseAgain"),"quick tap and shared control");
        Check(!router.Take().Pressed("Use") && shortTap.Pressed("Use"),"edge consumed and old snapshot immutable");
        router.Pointer(new(-1,100),"Mouse.Left",true); Check(!router.Take().Held("Use"),"letterbox ignores presses");
        router.Pointer(new(100,72),"Mouse.Left",true);router.Pad("Pad.South",1);
        var both=router.Take(); Check(both.Pressed("Use") && both.PointerInside && both.PointerPosition==new Vector2(100,72) && both.ActiveDevice==InputDevice.Gamepad,"mouse coordinates and last device");
        router.Pad("Pad.South",0); Check(!router.Take().Released("Use"),"alternative held");
        router.Pointer(new(900,50),"Mouse.Left",false); Check(router.Take().Released("Use"),"release outside");
        actions.Rebind("Use","K"); router.Key("U",true); Check(!router.Take().Pressed("Use"),"removed control");
        router.Key("K",true); Check(router.Take().Held("Use"),"session rebind");router.Reset(); Check(!router.Take().Held("Use"),"focus reset");
        Check(GameSettings.Load(root).Input.Actions![0].Controls.Contains("U"),"runtime overrides leave project unchanged");
        var scene=SceneDocument.Empty(settings);var canvas=scene.CreateUi("Canvas",null);var button=scene.CreateUi("Button",canvas);
        var ui=new ScreenUi(scene,null);var box=ui.Bounds(button.Id)!.Value;
        actions.Rebind("Use","Mouse.Left");router=new(actions,()=>ui,new(800,450));
        router.Pointer(new(box.X+1,box.Y+1),"Mouse.Left",true);router.Pointer(new(box.X+1,box.Y+1),"Mouse.Left",false);
        Check(!router.Take().Pressed("Use"),"UI consumes only its button clicks");
        try { new InputBindings { Actions=[new("one",["U"]),new("One",[])] }.Validate(); throw new Exception("duplicate accepted"); } catch(InvalidDataException) {}
        try { actions.Rebind("Use","Pad.Nonexistent"); throw new Exception("invalid control accepted"); } catch(InvalidDataException) {}
        Console.WriteLine("PASS project input map: YAML migration, named edges, device changes, rebind, focus, letterbox and UI capture");
    }
}
