using System;
using System.Collections.Generic;
using System.Numerics;
using Engine.Core;

// Sample policy: the engine supplies perception/path queries and the ordinary grounded motor.
public sealed class Lookout : GameBehaviour
{
    public Guid Player, Region, Deck, Incline, Readout;
    public float PatrolSpan=56, PatrolSpeed=32, ChaseSpeed=76, Sight=112, LoseSightDelay=.35f;
    private enum Mode { Patrol, Chase, Return }
    private Mode mode;
    private int lastSupportCount;
    private Guid lastChaseSupport;
    private bool initialized, toRight=true, hasPlan, visible, descended, resumed, witnessed;
    private float lost, cooldown, excursion;
    private Vector2 home,lastSeen,destination;
    private IReadOnlyList<WalkPoint> route=Array.Empty<WalkPoint>();
    private int next, patrolTurns, maxPath, crossed, displayKey=-1;
    private string history="Patrol";

    public override void FixedUpdate(ScriptTick tick)
    {
        var nav=tick.Objects.Navigation;
        if (!nav.TryGetFeet(tick.ObjectId,out var feet) || !tick.Objects.Motion.TryGetMotion(tick.ObjectId,out var body)) return;
        if (!initialized) { home=feet;initialized=true; }
        bool seen=nav.CanSee(tick.ObjectId,Player,Math.Max(0,Sight));
        if (seen && nav.TryGetFeet(Player,out var target))
        {
            lastSeen=target;lost=0;
            if(mode!=Mode.Chase)
            {
                Change(Mode.Chase);
                excursion=0;descended=false;maxPath=0;crossed=0;resumed=false;lastSupportCount=0;lastChaseSupport=Guid.Empty;
            }
        }
        else if(mode==Mode.Chase && (lost+=tick.DeltaTime)>=Math.Max(0,LoseSightDelay))
            Change(Mode.Return);

        visible=seen;
        float speed=mode==Mode.Patrol?PatrolSpeed:ChaseSpeed;
        Vector2 goal=mode switch { Mode.Chase=>lastSeen, Mode.Return=>home, _=>home+(toRight?new Vector2(PatrolSpan,0):Vector2.Zero) };
        if(mode!=Mode.Chase && body.Support==Deck && Math.Abs(feet.X-goal.X)<3)
        {
            if(mode==Mode.Return)
            {
                Change(Mode.Patrol);toRight=true;resumed=true;
            }
            else {toRight=!toRight;patrolTurns++;}
            goal=home+(toRight?new Vector2(PatrolSpan,0):Vector2.Zero);
            if(resumed && patrolTurns>0 && !witnessed && Math.Abs(feet.X-home.X)>PatrolSpan*.8f)
            {
                var report=new GameData();
                report.Set("sequence",history);report.Set("chaseWaypoints",maxPath);
                report.Set("waypointsReached",crossed);report.Set("supportSegments",lastSupportCount);report.Set("chaseOnIncline",descended);
                report.Set("resumedOnDeck",body.Support==Deck);report.Set("patrolSpan",Math.Round(Math.Abs(feet.X-home.X)));
                report.Set("chaseDistanceFromHome",Math.Round(excursion));
                report.Set("passed",descended && crossed>=2 && maxPath>=3 && history.Contains("Chase > Return > Patrol"));
                if(tick.Session.Storage.IsAvailable) tick.Session.Storage.SaveSlot(4,report);
                witnessed=true;
            }
        }
        cooldown-=tick.DeltaTime;
        // Refresh for moving targets, newly loaded tiles and changes under moving platforms.
        if(body.Grounded && (cooldown<=0 || !hasPlan || Vector2.Distance(goal,destination)>12))
        {
            route=nav.FindWalkPath(Region,tick.ObjectId,goal);destination=goal;
            next=route.Count>1 ? 1 : 0;hasPlan=true;cooldown=.35f;
            if(mode==Mode.Chase) maxPath=Math.Max(route.Count,maxPath);
        }
        const float tolerance=2;
        while(next<route.Count && Math.Abs(route[next].Feet.X-feet.X)<tolerance)
        {next++;if(mode==Mode.Chase) crossed++;}
        float axis=0;
        if(route.Count>0 && body.Grounded)
        {
            float marker=next<route.Count?route[next].Feet.X:goal.X;
            // FindWalkPath has already limited both endpoint projections to one column.
            axis=Math.Clamp((marker-feet.X)/tick.DeltaTime,-Math.Max(0,speed),Math.Max(0,speed));
        }
        tick.Objects.Motion.DriveCharacter(tick.ObjectId,axis);
        if(mode==Mode.Chase)
        {
            if(body.Support is Guid support && support!=lastChaseSupport) {lastSupportCount++;lastChaseSupport=support;}
            if(body.Support==Incline) descended=true;
            excursion=Math.Max(excursion,Math.Abs(feet.X-home.X));
        }
        int key=(int)mode*100000+route.Count*10+(visible?1:0);
        if(key!=displayKey) {displayKey=key;tick.Ui.SetText(Readout,$"Lookout   {mode}     |     {(visible?"Player visible":"No sight")}     |     Route   {route.Count} nodes");}
    }

    private void Change(Mode value)
    {
        mode=value;hasPlan=false;route=Array.Empty<WalkPoint>();next=0;cooldown=0;
        if(value==Mode.Chase) history="Patrol > Chase";
        else history+=" > "+value;
    }
}
