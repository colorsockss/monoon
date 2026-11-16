using System.Drawing;
using System.Numerics;

namespace Engine.Core;

// Shared by the Scene panel and the player; UI gets first refusal on activation.
public sealed class GameInputRouter(InputActions actions, Func<ScreenUi?> ui, Size resolution)
{
    public void Reset() { actions.Reset(); padDown.Clear();padUsed.Clear();ui()?.ResetInput(); }
    public bool Key(string control,bool down,bool reverse=false,long? timestamp=null)
    {
        control=InputControls.KeyName(control);
        if (down && actions.Suppressed(control)) return true;
        if(down && InputControls.Supported(control)) actions.NoteDevice(InputDevice.Keyboard);
        bool used=ui()?.Key(control,down,reverse)==true;
        if(!down) return actions.Release(control) || used;
        return used || actions.Press(control,timestamp);
    }
    public void Pointer(Vector2 point,string? control=null,bool down=false) => Pointer(point,control,down,null);
    public void Pointer(Vector2 point,string? control,bool down,long? timestamp)
    {
        bool inside=point.X>=0 && point.Y>=0 && point.X<resolution.Width && point.Y<resolution.Height;
        actions.PointerAt(point,inside);
        ui()?.Drag(point.X,point.Y);
        if(control==null) return;
        if (down && actions.Suppressed(control)) return;
        if(down && inside) actions.NoteDevice(InputDevice.Pointer);
        bool used=control=="Mouse.Left" && ui()?.Pointer(down,point.X,point.Y)==true;
        if(!down) actions.Release(control);
        else if(inside && !used) actions.Press(control,timestamp);
    }
    public void CancelPointer()
    {
        ui()?.CancelPointer();
        actions.Release("Mouse.Left"); actions.Release("Mouse.Right"); actions.Release("Mouse.Middle");
    }
    public void Wheel(Vector2 point,float delta) => Wheel(point,delta,null);
    public void Wheel(Vector2 point,float delta,long? timestamp)
    {
        if(delta==0) return;
        if(ui()?.Wheel(point.X,point.Y,delta)==true){actions.NoteDevice(InputDevice.Pointer);return;}
        var key=delta>0 ? "Mouse.WheelUp":"Mouse.WheelDown";
        Pointer(point,key,true,timestamp); Pointer(point,key,false,timestamp);
    }
    private readonly HashSet<string> padDown=[],padUsed=[];
    public void Pad(string control,float value) => Pad(control,value,null);
    public void Pad(string control,float value,long? timestamp)
    {
        if (actions.Suppressed(control))
        {
            if (value <= .25f)
            {
                actions.Release(control);
                padDown.Remove(control);
                padUsed.Remove(control);
            }
            return;
        }
        if(value>=.55f && padDown.Add(control) && ui()?.PadKey(control)==true)
        { padUsed.Add(control);actions.Set(control,0);actions.NoteDevice(InputDevice.Gamepad); }
        bool used=padUsed.Contains(control);
        if(value<=.25f){padDown.Remove(control);padUsed.Remove(control);}
        if(!used)
        {
            bool navigationAxis=value>0 && ui() is { OwnsNavigation:true } && control.StartsWith("Pad.LeftStick",StringComparison.Ordinal);
            actions.Set(control,navigationAxis?0:value,timestamp);
        }
    }
    public void Compose(string text) => ui()?.Compose(text);
    public void CommitText(string text) => ui()?.CommitText(text);
    public GameInput Take() => actions.Take();
}
