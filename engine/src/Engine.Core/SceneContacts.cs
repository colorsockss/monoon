using System.Drawing;
using System.Numerics;

namespace Engine.Core;

public sealed partial class SceneGame
{
    private enum ContactPhase { Enter, Stay, Exit }
    private static readonly string[] contactMethods = ["OnCollisionEnter", "OnCollisionStay", "OnCollisionExit", "OnTriggerEnter", "OnTriggerStay", "OnTriggerExit"];

    // Re-enabling the same collider starts a new contact even when both changes happen between ticks.
    private readonly record struct ContactPair(Body A, Body B, long RevisionA, long RevisionB, bool Trigger)
    {
        public static ContactPair Create(Body a, Body b, bool trigger)
        {
            if (a.Id.CompareTo(b.Id) > 0) (a,b) = (b,a);
            return new(a,b,a.ContactRevision,b.ContactRevision,trigger);
        }
        public bool Valid => A.Active && B.Active && A.ContactRevision == RevisionA && B.ContactRevision == RevisionB;
    }

    private HashSet<ContactPair> contacts = [], nextContacts = [];
    private readonly HashSet<ContactPair> crossings = [];
    private readonly List<ContactPair> endedContacts = [];
    private readonly List<Body> contactSweep = [];

    private void DispatchContacts()
    {
        // Collect a single physics snapshot before any callback can modify the world.
        nextContacts.Clear();
        crossings.Clear();
        CollectContacts();

        foreach (var pair in contacts)
            if (!nextContacts.Contains(pair)) NotifyContact(pair,ContactPhase.Exit);
        endedContacts.Clear();
        foreach (var pair in nextContacts)
        {
            bool continuing = contacts.Contains(pair);
            if (pair.Valid) NotifyContact(pair,continuing ? ContactPhase.Stay : ContactPhase.Enter);
            else
            {
                if (continuing) NotifyContact(pair,ContactPhase.Exit);
                endedContacts.Add(pair);
            }
        }
        foreach (var pair in endedContacts) nextContacts.Remove(pair);
        foreach (var pair in crossings)
        {
            if (!pair.Valid || nextContacts.Contains(pair)) continue;
            NotifyContact(pair,ContactPhase.Enter);
            NotifyContact(pair,ContactPhase.Exit);
        }
        (contacts,nextContacts) = (nextContacts,contacts);
        // A callback can remove another contact. The surviving participant receives its exit this tick.
        endedContacts.Clear();
        foreach (var pair in contacts) if (!pair.Valid) endedContacts.Add(pair);
        foreach (var pair in endedContacts)
        {
            contacts.Remove(pair);
            NotifyContact(pair,ContactPhase.Exit);
        }
    }

    private void CollectContacts()
    {
        contactSweep.Clear();
        foreach (var body in colliders)
        {
            if (!body.Active || body.Spawning || body.Collider == null) continue;
            body.EndShape = body.ShapeAt(body.Position);
            var bounds = body.EndShape.Bounds;
            if (body.Moved)
                for (float t = 0; t < 1; t = body.NextBreak(t))
                    bounds = RectangleF.Union(bounds, body.ShapeAt(body.At(t),body.AngleAt(t)).Bounds);
            bounds.Inflate(.00001f,.00001f); // Match the narrow phase's resting contact tolerance.
            body.ContactBounds = bounds;
            contactSweep.Add(body);
        }
        contactSweep.Sort(static (a,b) => {
            int order = a.ContactBounds.Left.CompareTo(b.ContactBounds.Left);
            return order == 0 ? a.Id.CompareTo(b.Id) : order;
        });
        for (int i = 0; i < contactSweep.Count; i++)
        {
            var a = contactSweep[i];
            var ac = a.ColliderConfig!;
            for (int j = i + 1; j < contactSweep.Count; j++)
            {
                var b = contactSweep[j];
                if (b.ContactBounds.Left > a.ContactBounds.Right) break;
                var bc = b.ColliderConfig!;
                if (!CollisionGeometry.Pair(ac,bc) ||
                    !BoundsMeet(a.ContactBounds,b.ContactBounds)) continue;
                bool trigger = ac.IsTrigger || bc.IsTrigger;
                if (!trigger && a.MotionOwner != null && a.MotionOwner == b.MotionOwner) continue;
                var pair = ContactPair.Create(a,b,trigger);
                if (CollisionGeometry.Cast(a.EndShape,Vector2.Zero,b.EndShape,!trigger,false) != null)
                    nextContacts.Add(pair);
                else if (!contacts.Contains(pair) && (a.Moved || b.Moved) && Crossed(a,b,trigger))
                    crossings.Add(pair);
            }
            // Adjacent solid cells form one contact with their tilemap object.
            if (tileLayers.Length == 0) continue;
            foreach (var obstacle in TileCandidates(a.ContactBounds,ac.Mask,a.Id,-1,-1,ac.Layer,a.MotionOwner, sweptMaps: true))
            {
                var map = byId[obstacle.Target.ObjectId];
                var pair = ContactPair.Create(a,map,ac.IsTrigger);
                if (CollisionGeometry.Cast(a.EndShape,Vector2.Zero,obstacle.Shape,!ac.IsTrigger,false) != null)
                    nextContacts.Add(pair);
                else if ((a.Moved || map.Moved) && !contacts.Contains(pair) && Crossed(a,map,ac.IsTrigger,obstacle.Shape))
                    crossings.Add(pair);
            }
        }
    }

    private static bool Crossed(Body a, Body b, bool trigger, Shape2D? tile = null)
    {
        for (float t = 0; t < 1;)
        {
            float next = Math.Min(a.NextBreak(t),b.NextBreak(t));
            var pa = a.At(t);
            var pb = b.At(t);
            var target = tile?.Translated(pb - b.Position) ?? b.ShapeAt(pb,b.AngleAt(t));
            var travel = (a.At(next) - pa) - (b.At(next) - pb);
            if (CollisionGeometry.Cast(a.ShapeAt(pa,a.AngleAt(t)),travel,target,!trigger,false) != null) return true;
            t = next;
        }
        return false;
    }

    private void NotifyContact(ContactPair pair, ContactPhase phase)
    {
        var first = pair.B.ColliderConfig?.IsTrigger == true && pair.A.ColliderConfig?.IsTrigger != true ? pair.B : pair.A;
        var second = first == pair.A ? pair.B : pair.A;
        Send(first,second);
        Send(second,first);

        void Send(Body self, Body other)
        {
            if (!self.Active) return;
            using var scope = EnterScene(self);
            var contact = new ScriptContact(this,self.Id,other.Id);
            foreach (var script in self.Behaviours)
            {
                if (!self.Active) break;
                using var sample = FrameProfiler.Script(script, contactMethods[(pair.Trigger ? 3 : 0) + (int)phase]);
                try
                {
                    switch (pair.Trigger,phase)
                    {
                        case (true,ContactPhase.Enter): script.OnTriggerEnter(contact); break;
                        case (true,ContactPhase.Stay): script.OnTriggerStay(contact); break;
                        case (true,ContactPhase.Exit): script.OnTriggerExit(contact); break;
                        case (false,ContactPhase.Enter): script.OnCollisionEnter(contact); break;
                        case (false,ContactPhase.Stay): script.OnCollisionStay(contact); break;
                        case (false,ContactPhase.Exit): script.OnCollisionExit(contact); break;
                    }
                }
                catch (Exception ex) { throw ScriptFailure(script,$"On{(pair.Trigger ? "Trigger" : "Collision")}{phase}",ex); }
            }
            if (phase == ContactPhase.Enter && self.ColliderConfig?.IsTrigger == true)
                TriggerEntered?.Invoke(self.Id,other.Id);
        }
    }
}
