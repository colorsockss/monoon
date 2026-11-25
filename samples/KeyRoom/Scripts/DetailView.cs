using System;
using Engine.Core;

// F8 shows or hides a second camera whose output is an inset viewport over the main view (raises "detail" while it shows).
public sealed class DetailView : GameBehaviour
{
    public Guid Camera;

    public override void FixedUpdate(ScriptTick tick)
    {
        if (!tick.Input.Pressed("DetailView")) return;
        bool show = !tick.Objects.IsActive(Camera);
        tick.Objects.SetActive(Camera, show);
        if (show) tick.Session.Set("detail"); else tick.Session.Clear("detail");
    }
}
