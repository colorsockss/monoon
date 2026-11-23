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

    // Sorted by (Left, Id) across ticks: an insertion sort is linear while bodies keep their order.
    private static readonly Comparison<Body> sweepOrder = static (a,b) => {
        int order = a.ContactBounds.Left.CompareTo(b.ContactBounds.Left);
        return order == 0 ? a.Id.CompareTo(b.Id) : order;
    };
    private int contactTick;
    // Rows of the sweep (by Y) as ranks in ascending order: rowStart[r]..rowStart[r+1] in rowItems.
    private int[] sweepRow0 = [], sweepRow1 = [], rowStart = [], rowFill = [], rowItems = [];
    private readonly List<int> sweepCandidates = [];

    private void CollectContacts()
    {
        contactTick++;
        int added = 0;
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
            body.SweepTick = contactTick;
            if (!body.InSweep) { body.InSweep = true; contactSweep.Add(body); added++; }
        }
        int kept = 0;
        for (int i = 0; i < contactSweep.Count; i++)
        {
            var body = contactSweep[i];
            if (body.SweepTick == contactTick) contactSweep[kept++] = body;
            else body.InSweep = false;
        }
        contactSweep.RemoveRange(kept, contactSweep.Count - kept);
        SortSweep();

        // Pairs are visited in (rank a, rank b) order like a plain sweep over Left, so nextContacts and crossings get the
        // same insertion order. Rows by Y skip bodies that overlap in X only (a column of equal Left was O(n²)).
        var sweep = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(contactSweep);
        bool rows = BuildRows(sweep);
        for (int i = 0; i < sweep.Length; i++)
        {
            var a = sweep[i];
            var ac = a.ColliderConfig!;
            if (!rows || sweepRow1[i] - sweepRow0[i] > 3)
                for (int j = i + 1; j < sweep.Length && sweep[j].ContactBounds.Left <= a.ContactBounds.Right; j++)
                    ContactCandidate(a,ac,sweep[j]);
            else
            {
                sweepCandidates.Clear();
                for (int row = sweepRow0[i]; row <= sweepRow1[i]; row++)
                {
                    int from = rowStart[row], to = rowStart[row + 1];
                    int first = Array.BinarySearch(rowItems, from, to - from, i + 1);
                    for (int k = first < 0 ? ~first : first; k < to; k++)
                    {
                        int j = rowItems[k];
                        if (sweep[j].ContactBounds.Left > a.ContactBounds.Right) break;
                        if (row == Math.Max(sweepRow0[i], sweepRow0[j])) sweepCandidates.Add(j); // Its first shared row only.
                    }
                }
                if (sweepRow1[i] != sweepRow0[i]) sweepCandidates.Sort();
                foreach (int j in sweepCandidates) ContactCandidate(a,ac,sweep[j]);
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

    private void ContactCandidate(Body a, Collider2DComponent ac, Body b)
    {
        var bc = b.ColliderConfig!;
        if (!CollisionGeometry.Pair(ac,bc) || !BoundsMeet(a.ContactBounds,b.ContactBounds)) return;
        bool trigger = ac.IsTrigger || bc.IsTrigger;
        if (!trigger && a.MotionOwner != null && a.MotionOwner == b.MotionOwner) return;
        var pair = ContactPair.Create(a,b,trigger);
        if (CollisionGeometry.Cast(a.EndShape,Vector2.Zero,b.EndShape,!trigger,false) != null)
            nextContacts.Add(pair);
        else if (!contacts.Contains(pair) && (a.Moved || b.Moved) && Crossed(a,b,trigger))
            crossings.Add(pair);
    }

    private void SortSweep()
    {
        var sweep = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(contactSweep);
        // The order is total (Ids are unique), so this gives exactly what List.Sort gives; a reshuffle falls back to it.
        long budget = 4L * sweep.Length + 64;
        for (int i = 1; i < sweep.Length; i++)
        {
            var body = sweep[i];
            int j = i - 1;
            for (; j >= 0 && sweepOrder(sweep[j],body) > 0; j--) { sweep[j + 1] = sweep[j]; budget--; }
            sweep[j + 1] = body;
            if (budget < 0) { contactSweep.Sort(sweepOrder); return; }
        }
    }

    // Row height is the mean body height, so a body covers about two rows. False: use the plain sweep.
    private bool BuildRows(ReadOnlySpan<Body> sweep)
    {
        int n = sweep.Length;
        if (n < 64) return false;
        float top = float.PositiveInfinity, bottom = float.NegativeInfinity;
        double heights = 0;
        foreach (var b in sweep)
        {
            top = Math.Min(top, b.ContactBounds.Top);
            bottom = Math.Max(bottom, b.ContactBounds.Bottom);
            if (!(b.ContactBounds.Height >= 0)) return false;
            heights += b.ContactBounds.Height;
        }
        double span = (double)bottom - top, height = Math.Max(heights / n, span / (4.0 * n));
        if (!double.IsFinite(span) || !(span > 0) || !(height > 0)) return false;
        int count = (int)Math.Min(4L * n, (long)(span / height)) + 1;
        if (sweepRow0.Length < n) { sweepRow0 = new int[n * 2]; sweepRow1 = new int[n * 2]; }
        if (rowStart.Length < count + 1) { rowStart = new int[count * 2 + 1]; rowFill = new int[count * 2 + 1]; }
        // Monotonic in y, so two intervals that meet (touching included) share at least one row.
        int Row(float y) => (int)Math.Min(count - 1, ((double)y - top) / height);
        Array.Clear(rowStart, 0, count + 1);
        for (int i = 0; i < n; i++)
        {
            int r0 = sweepRow0[i] = Row(sweep[i].ContactBounds.Top), r1 = sweepRow1[i] = Row(sweep[i].ContactBounds.Bottom);
            for (int r = r0; r <= r1; r++) rowStart[r + 1]++;
        }
        for (int r = 0; r < count; r++) rowStart[r + 1] += rowStart[r];
        if (rowItems.Length < rowStart[count]) rowItems = new int[rowStart[count] * 2];
        Array.Copy(rowStart, rowFill, count);
        for (int i = 0; i < n; i++)
            for (int r = sweepRow0[i]; r <= sweepRow1[i]; r++) rowItems[rowFill[r]++] = i;
        return true;
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
