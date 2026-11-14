using Brutal.SdlApi;
using Engine.Core;
using System.Numerics;
using Engine.Desktop;

// Real SDL virtual device and mapping, without a system driver or a physical controller.
unsafe
{
    using var pads = new SdlGamepads();
    var actions=new InputActions(new());
    var desc=new SdlVirtualJoystickDesc { version=(uint)sizeof(SdlVirtualJoystickDesc), type=(ushort)SdlJoystickType.Gamepad,
        naxes=6,nbuttons=15,button_mask=0x7fff,axis_mask=63 };
    var id=Sdl.Joystick.AttachVirtualJoystick(&desc);
    if(id==0) throw new Exception(Sdl.PInvoke.GetError());
    try
    {
        if(!Sdl.Gamepad.GetGamepads().Contains(id)) throw new Exception("Virtual device was not mapped as a gamepad.");
        if(Sdl.Gamepad.GetGamepads().First()!=id) throw new Exception("Disconnect other controllers before the virtual pad check.");
        using var joy=Sdl.Joystick.OpenJoystick(id);
        void Read() => pads.Poll((c,v)=>actions.Set(c,v));
        Read();
        Sdl.Joystick.SetJoystickVirtualAxis(joy,0,1900); Read();
        Check(actions.Take().Move.X==0,"analog dead zone");
        Sdl.Joystick.SetJoystickVirtualAxis(joy,0,20000); Read();
        var frame=actions.Take();
        Check(frame.ActiveDevice==InputDevice.Gamepad && frame.Move.X is >.4f and <.7f,"analog magnitude and device");
        Sdl.Joystick.SetJoystickVirtualButton(joy,0,true); Read();
        Check(actions.Take().InteractPressed && !actions.Take().InteractPressed,"south button single press");
        actions.Press("A"); Check(actions.Take().ActiveDevice==InputDevice.Keyboard,"keyboard takes over");
        Sdl.Joystick.SetJoystickVirtualButton(joy,0,false); Read(); Check(actions.Take().Released("Interact"),"button release");
        Sdl.Joystick.SetJoystickVirtualButton(joy,14,true); Read(); Check(actions.Take().ActiveDevice==InputDevice.Gamepad,"D-pad switches back");
        actions.Release("A");
    }
    finally { if (!Sdl.Joystick.DetachVirtualJoystick(id)) throw new Exception(Sdl.PInvoke.GetError()); }
    _=actions.Take(); // Consume the last D-pad press before observing the unplug tick.
    pads.Poll((c,v)=>actions.Set(c,v));
    var detached=actions.Take();
    Check(detached.Move==Vector2.Zero && detached.Released("MoveRight"),"disconnect releases held controls");

    // Reconnect to the same reader, then play the authored sample with its own action map.
    var second = Sdl.Joystick.AttachVirtualJoystick(&desc);
    if (second == 0) throw new Exception(Sdl.PInvoke.GetError());
    var profile = Directory.CreateTempSubdirectory("monoon-pad-");
    try
    {
        var scene = SceneDocument.Load(Path.GetFullPath(args.Length == 0 ? "samples/KeyRoom/Scenes/KeyRoom.scene" : args[0]));
        using var game = new GameSession(scene, null, null, new PlayerStorage(profile.FullName));
        var router = new GameInputRouter(game.Actions, () => game.Current.Ui, game.LogicalSize);
        using var joystick = Sdl.Joystick.OpenJoystick(second);
        void Step() { pads.Poll(router.Pad); game.FixedUpdate(router.Take(), 1f / 60); }
        void Button(SdlGamepadButton button, bool down)
        {
            if (!Sdl.Joystick.SetJoystickVirtualButton(joystick, (int)button, down))
                throw new Exception(Sdl.PInvoke.GetError());
            Step();
        }
        // The first poll synchronizes the reconnected device without treating held buttons as new.
        Thread.Sleep(450);
        Step();
        Button(SdlGamepadButton.Start, true);
        Check(game.State.Has("controls"), "reconnected pad opens KeyRoom controls");
        Button(SdlGamepadButton.Start, false);
        Button(SdlGamepadButton.Start, true);
        Check(!game.State.Has("controls"), "Start closes KeyRoom controls");
        Button(SdlGamepadButton.Start, false);
        Button(SdlGamepadButton.South, true);
        Check(game.Current.Objects.Any(o => o.Name == "Coin"), "South opens chest and spawns coin");
        Button(SdlGamepadButton.South, false);
        Button(SdlGamepadButton.DpadRight, true);
        for (int i = 0; i < 32; i++) Step();
        Check(game.State.Has("coin") && !game.Current.Objects.Any(o => o.Name == "Coin"), "D-pad moves player to collect coin");
    }
    finally
    {
        profile.Delete(recursive: true);
        if (!Sdl.Joystick.DetachVirtualJoystick(second)) throw new Exception(Sdl.PInvoke.GetError());
    }
}
static void Check(bool ok,string name) { if(!ok) throw new Exception(name); Console.WriteLine("PASS "+name); }
