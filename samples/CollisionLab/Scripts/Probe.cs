using System;
using System.Numerics;
using Engine.Core;

// CollisionLab showcase probe: on its first step it measures a ray and a circle sweep toward the wall, an overlap inside the
// sensor (with and without triggers) and the polygon's empty corner, and draws the ray (RayLine) and where the sweep stops
// (SweepStop). → moves it once by 800 units: through the sensor (SensorEntry raises "crossed") and both gates, against the wall.
// It then saves what it measured to slot 1. Raises the HUD flags "cast" and "stopped".
public sealed class Probe : GameBehaviour
{
    public Guid Status, Metrics, Wall, Sensor, RayLine, SweepStop;
    private int step;
    private PhysicsHit? ray,sweep;
    private bool sensorInside,cornerClear,filtered;
    public override void FixedUpdate(ScriptTick tick)
    {
        var physics=tick.Objects.Physics;
        if (step==0)
        {
            var walls=new PhysicsFilter(1u<<2);
            var sensors=new PhysicsFilter(1u<<3,true);
            var probe=Shape2D.Circle(new(240,200),2);
            var inside=physics.Overlap(probe,sensors);
            sensorInside=inside.Count==1 && inside[0].ObjectId==Sensor && inside[0].IsTrigger;
            filtered=physics.Overlap(probe,new PhysicsFilter(1u<<3)).Count==0;
            cornerClear=physics.Overlap(Shape2D.Circle(new(593,146),2),walls).Count==0;
            var center=tick.Position+new Vector2(20);
            ray=physics.Raycast(center,Vector2.UnitX,800,walls);
            sweep=physics.Sweep(Shape2D.Circle(center,20),new(800,0),walls);
            tick.Ui.SetText(Metrics,$"Ray {ray?.Distance:F1}     Sweep {sweep?.Distance:F1}     Sensor overlap {(sensorInside?1:0)}");
            if (RayLine!=Guid.Empty && ray is { } hit) tick.Objects.SetLinePoints(RayLine,[center,hit.Point]);
            if (SweepStop!=Guid.Empty && sweep is { } stop)
            {
                tick.Objects.SetLocalTransform(SweepStop,new LocalTransform2D(center+new Vector2(stop.Distance,0)-new Vector2(20),0,Vector2.One));
                tick.Objects.SetActive(SweepStop,true);
            }
            tick.Session.Set("cast");
            step=1;
        }
        if (step==1 && tick.Input.Pressed("MoveRight"))
        {
            tick.Move(new(800,0)); step=2; return;
        }
        if (step!=2) return;
        step=3;
        bool crossed=tick.Session.Has("crossed");
        bool pass=crossed && filtered && cornerClear && sensorInside && ray?.Target.ObjectId==Wall && sweep?.Target.ObjectId==Wall &&
            Math.Abs(ray.Value.Distance-400)<.001f && Math.Abs(sweep.Value.Distance-380)<.001f &&
            sweep.Value.Normal==new Vector2(-1,0) && Math.Abs(tick.Position.X-460)<.001f;
        tick.Ui.SetText(Status,$"Stopped at x {tick.Position.X:F0}     Sensor {(crossed?"entered":"missed")}     Gates {(tick.Position.X>390?"passed":"blocked")}");
        tick.Session.Set("stopped");
        var report=new GameData();
        report.Set("pass",pass); report.Set("sensorOverlap",sensorInside);report.Set("sensorEntered",crossed);
        report.Set("triggerExcluded",filtered);report.Set("emptyPolygonCorner",cornerClear);
        report.Set("rayDistance",ray?.Distance??-1); report.Set("sweepDistance",sweep?.Distance??-1);
        report.Set("rayHitX",ray?.Point.X??0);report.Set("sweepHitX",sweep?.Point.X??0);
        report.Set("normalX",sweep?.Normal.X??0); report.Set("normalY",sweep?.Normal.Y??0);
        report.Set("stopX",tick.Position.X);report.Set("stopY",tick.Position.Y);
        if (tick.Session.Storage.IsAvailable) tick.Session.Storage.SaveSlot(1,report);
    }
}
