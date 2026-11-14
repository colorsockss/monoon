using System.Numerics;
using Brutal.SdlApi;

namespace Engine.Desktop;

// The editor owns its AppKit/Avalonia event loop. Poll only SDL gamepad state here;
// do not pump or steal the editor's keyboard, mouse or window events.
public sealed class SdlGamepads : IDisposable
{
    private static readonly (string Name,SdlGamepadButton Button)[] buttons = [
        ("South",SdlGamepadButton.South),("East",SdlGamepadButton.East),("West",SdlGamepadButton.West),("North",SdlGamepadButton.North),
        ("Start",SdlGamepadButton.Start),("Back",SdlGamepadButton.Back),("LeftShoulder",SdlGamepadButton.LeftShoulder),
        ("RightShoulder",SdlGamepadButton.RightShoulder),("LeftStick",SdlGamepadButton.LeftStick),("RightStick",SdlGamepadButton.RightStick),
        ("DpadLeft",SdlGamepadButton.DpadLeft),("DpadRight",SdlGamepadButton.DpadRight),("DpadUp",SdlGamepadButton.DpadUp),("DpadDown",SdlGamepadButton.DpadDown)];
    private static readonly string[] names = [..buttons.Select(b=>"Pad."+b.Name),
        "Pad.LeftStickLeft","Pad.LeftStickRight","Pad.LeftStickUp","Pad.LeftStickDown",
        "Pad.RightStickLeft","Pad.RightStickRight","Pad.RightStickUp","Pad.RightStickDown","Pad.LeftTrigger","Pad.RightTrigger"];
    private readonly float[] last = new float[names.Length], values = new float[names.Length];
    private readonly bool[] blocked = new bool[names.Length];
    private SdlGamepad pad;
    private bool opened, initialized, sync=true;
    private readonly bool priorPadEvents, priorJoyEvents;
    private long nextScan;
    public SdlGamepads()
    {
        initialized=Sdl.Init.InitSubSystem(SdlInitFlags.Gamepad);
        if(initialized)
        {
            priorPadEvents=Sdl.Gamepad.GamepadEventsEnabled();
            priorJoyEvents=Sdl.Joystick.JoystickEventsEnabled();
            Sdl.Gamepad.SetGamepadEventsEnabled(false);
            Sdl.Joystick.SetJoystickEventsEnabled(false);
        }
    }
    public void SuppressHeld() => sync=true;
    public void Poll(Action<string,float> changed)
    {
        if (!initialized) return;
        Sdl.Gamepad.UpdateGamepads();
        if (opened && !Sdl.Gamepad.GamepadConnected(pad))
        {
            Sdl.Gamepad.CloseGamepad(pad); opened=false;
            for (int i=0;i<last.Length;i++) if (last[i]!=0) changed(names[i],0);
            Array.Clear(last); sync=true; nextScan=0;
        }
        if (!opened)
        {
            if (Environment.TickCount64<nextScan) return;
            nextScan=Environment.TickCount64+400;
            var ids=Sdl.Gamepad.GetGamepads();
            if (ids.Length==0) return;
            try { pad=Sdl.Gamepad.OpenGamepad(ids[0]); opened=Sdl.Gamepad.GamepadConnected(pad); }
            catch (SdlException) { return; } // Hot unplug between enumeration and open.
            if (!opened) { Sdl.Gamepad.CloseGamepad(pad); return; }
        }
        int index=0;
        foreach (var (_,button) in buttons) values[index++]=Sdl.Gamepad.GetGamepadButton(pad,button)?1:0;
        void Stick(SdlGamepadAxis x,SdlGamepadAxis y)
        {
            var v = new Vector2(Sdl.Gamepad.GetGamepadAxis(pad,x),Sdl.Gamepad.GetGamepadAxis(pad,y))/32767f;
            var len=v.Length();
            v=len<=.2f ? Vector2.Zero : v/len*Math.Clamp((len-.2f)/.8f,0,1);
            values[index++]=Math.Max(0,-v.X); values[index++]=Math.Max(0,v.X);
            values[index++]=Math.Max(0,-v.Y); values[index++]=Math.Max(0,v.Y);
        }
        Stick(SdlGamepadAxis.Leftx,SdlGamepadAxis.Lefty); Stick(SdlGamepadAxis.Rightx,SdlGamepadAxis.Righty);
        foreach(var axis in triggerAxes) values[index++]=Math.Clamp((Sdl.Gamepad.GetGamepadAxis(pad,axis)/32767f-.1f)/.9f,0,1);
        for(int i=0;i<values.Length;i++)
        {
            if(sync) blocked[i]=values[i]>0;
            if(values[i]==0) blocked[i]=false;
            var value=blocked[i]?0:values[i];
            if(value==last[i]) continue;
            last[i]=value; changed(names[i],value);
        }
        sync=false;
    }
    private static readonly SdlGamepadAxis[] triggerAxes=[SdlGamepadAxis.LeftTrigger,SdlGamepadAxis.RightTrigger];
    public void Dispose()
    {
        if(opened) Sdl.Gamepad.CloseGamepad(pad);
        if(initialized)
        {
            Sdl.Joystick.SetJoystickEventsEnabled(priorJoyEvents);
            Sdl.Gamepad.SetGamepadEventsEnabled(priorPadEvents);
            Sdl.Init.QuitSubSystem(SdlInitFlags.Gamepad);
        }
        initialized=opened=false;
    }
}
