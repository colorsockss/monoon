using System;
using System.Drawing;
using System.Numerics;
using Engine.Core;

public sealed class FieldPlayer : GameBehaviour
{
    public Guid Main, Second, Map, Readout;
    private int travel,leg;
    private GameData record = new();
#if MONOON_DEBUG_DRAW
    private bool gizmos;
#endif
    public override void FixedUpdate(ScriptTick tick)
    {
        if (!tick.Cameras.TryGetView(Main,out var a) || !tick.Cameras.TryGetView(Second,out var b) || !tick.Cameras.TryGetView(Map,out var map)) return;
        if (travel==0 && tick.Input.Pressed("Interact"))
        {
            leg=leg==1?2:1;travel=1;record=new GameData();
        }
        if (travel>0 && (travel==2 || travel==31 || travel==61 || travel==151))
        {
            string prefix="tick"+(travel-1);
            record.Set(prefix+"PlayerX",tick.Position.X+16);record.Set(prefix+"PlayerY",tick.Position.Y+16);
            record.Set(prefix+"MainX",a.Center.X);record.Set(prefix+"MainY",a.Center.Y);
            record.Set(prefix+"SecondX",b.Center.X);record.Set(prefix+"SecondY",b.Center.Y);
        }
        if (travel==151)
        {
            bool east=leg==1;
            record.Set("mapWidth",map.World.Width);record.Set("mapHeight",map.World.Height);
            record.Set("splitSeam",a.Viewport.Right);record.Set("secondStart",b.Viewport.Left);
            bool pass=Vector2.Distance(a.Center,east?new(1400,696):new(200,200))<.001f &&
                Vector2.Distance(b.Center,east?new(200,200):new(1400,696))<.001f &&
                map.World.Width==1600 && map.World.Height==896 && a.Viewport.Right==b.Viewport.Left;
            record.Set("pass",pass);
            if (tick.Session.Storage.IsAvailable) tick.Session.Storage.SaveSlot(leg,record);
            travel=0;
        }
        var move=travel>0 ? new Vector2(14,8)*(leg==1?1:-1) : tick.Direction*300*tick.DeltaTime;
        var target=Vector2.Clamp(tick.Position+move,Vector2.Zero,new(1568,864));
        tick.Move(target-tick.Position);
        if (travel>0) travel++;
#if MONOON_DEBUG_DRAW
        // G toggles gizmos in Scene Play and development builds. Distribution builds compile none of this.
        if (tick.Input.Pressed("DebugDraw")) gizmos=!gizmos;
        if (gizmos)
        {
            var center=tick.Position+new Vector2(16,16);
            tick.Debug.Circle(center,40,Color.Gold,2);
            tick.Debug.Line(center,b.Center,Color.Cyan,2);
            tick.Debug.Text(center+new Vector2(-36,44),$"{center.X:0}, {center.Y:0}",Color.White);
        }
#endif
        string status=travel>0?"Tracking":leg==0?"Ready":"At map edge";
        tick.Ui.SetText(Readout,$"{status}    |    Player  {tick.Position.X+16:0}, {tick.Position.Y+16:0}    |    Camera  {a.Center.X:0}, {a.Center.Y:0}");
    }
}
