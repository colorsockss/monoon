using System;
using System.Numerics;
using Engine.Core;

// CollisionLab showcase probe: on its first step it measures a ray and a circle sweep toward the wall (mask: the wall layer),
// two more rays whose masks add one gate's layer each (they stop at that gate), an overlap inside the sensor (with and without
// triggers) and the polygon's empty corner, and draws the rays (RayLine, GhostRay, GlassRay) and where the sweep stops
// (SweepStop). → rolls it right at Speed units per second through the sensor (SensorEntry raises "crossed") and both gates
// (its collider ignores them: gate A's layer is not in its mask, gate B's own mask is empty) until the wall stops it. Each
// step is one Move, so a long step (a slow frame) still crosses the sensor and stops at the wall.
// It then saves what it measured to slot 1. Raises the HUD flags "cast" and "stopped".
public sealed class Probe : GameBehaviour
{
    public Guid Status, Metrics, Wall, Sensor, RayLine, SweepStop, GhostRay, GlassRay;
    private const float Speed = 120;
    private int step;
    private float lastX, asked;
    private PhysicsHit? ray,sweep,ghost,glass;
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
            // Layers: 2 wall (Solid), 4 gate A (Ghost), 5 gate B (Glass).
            ghost=physics.Raycast(center-new Vector2(0,14),Vector2.UnitX,800,new PhysicsFilter(1u<<2|1u<<4));
            glass=physics.Raycast(center+new Vector2(0,14),Vector2.UnitX,800,new PhysicsFilter(1u<<2|1u<<5));
            tick.Ui.SetText(Metrics,$"Ray {ray?.Distance:F1}     Sweep {sweep?.Distance:F1}     Sensor overlap {(sensorInside?1:0)}");
            if (RayLine!=Guid.Empty && ray is { } hit) tick.Objects.SetLinePoints(RayLine,[center,hit.Point]);
            if (GhostRay!=Guid.Empty && ghost is { } a) tick.Objects.SetLinePoints(GhostRay,[center-new Vector2(0,14),a.Point]);
            if (GlassRay!=Guid.Empty && glass is { } b) tick.Objects.SetLinePoints(GlassRay,[center+new Vector2(0,14),b.Point]);
            if (SweepStop!=Guid.Empty && sweep is { } stop)
            {
                tick.Objects.SetLocalTransform(SweepStop,new LocalTransform2D(center+new Vector2(stop.Distance,0)-new Vector2(20),0,Vector2.One));
                tick.Objects.SetActive(SweepStop,true);
            }
            tick.Session.Set("cast");
            step=1;
        }
        if (step==1 && tick.Input.Pressed("MoveRight")) { step=2; lastX=tick.Position.X; }
        if (step==2)
        {
            // A step that moved less than it asked for ended against the wall.
            if (asked==0 || tick.Position.X-lastX>asked-.01f)
            {
                lastX=tick.Position.X; asked=Speed*tick.DeltaTime;
                tick.Move(new(asked,0)); return;
            }
            step=3;
        }
        if (step!=3) return;
        step=4;
        bool crossed=tick.Session.Has("crossed");
        bool pass=crossed && filtered && cornerClear && sensorInside && ray?.Target.ObjectId==Wall && sweep?.Target.ObjectId==Wall &&
            Math.Abs(ray.Value.Distance-400)<.001f && Math.Abs(sweep.Value.Distance-380)<.001f &&
            sweep.Value.Normal==new Vector2(-1,0) && Math.Abs(tick.Position.X-460)<.001f &&
            Math.Abs((ghost?.Point.X??0)-305)<.001f && Math.Abs((glass?.Point.X??0)-405)<.001f;
        tick.Ui.SetText(Status,$"Stopped at x {tick.Position.X:F0}     Sensor {(crossed?"entered":"missed")}     Gates {(tick.Position.X>415?"passed":"blocked")}");
        tick.Session.Set("stopped");
        var report=new GameData();
        report.Set("pass",pass); report.Set("sensorOverlap",sensorInside);report.Set("sensorEntered",crossed);
        report.Set("triggerExcluded",filtered);report.Set("emptyPolygonCorner",cornerClear);
        report.Set("rayDistance",ray?.Distance??-1); report.Set("sweepDistance",sweep?.Distance??-1);
        report.Set("rayHitX",ray?.Point.X??0);report.Set("ghostRayHitX",ghost?.Point.X??0);report.Set("glassRayHitX",glass?.Point.X??0);report.Set("sweepHitX",sweep?.Point.X??0);
        report.Set("normalX",sweep?.Normal.X??0); report.Set("normalY",sweep?.Normal.Y??0);
        report.Set("stopX",tick.Position.X);report.Set("stopY",tick.Position.Y);
        if (tick.Session.Storage.IsAvailable) tick.Session.Storage.SaveSlot(1,report);
    }
}
