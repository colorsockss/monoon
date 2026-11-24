using System.Drawing;

namespace Engine.Core;

// Roadmap 16.13: draggable UI Button slots. A pointer drag past 4 px carries the pressed slot and drops it on the slot under
// the pointer; activating a slot (click, Enter/Space, pad South) picks it up and activating another drops it on that one.
// Escape or pad East cancels. Drops are queued with the other control events and delivered as OnUiDrop.
public sealed partial class ScreenUi
{
    private const float DragThreshold = 4;
    private Guid? carried;
    private PointF? grab;
    private bool dragging;
    private readonly Queue<(Guid Source, Guid Target)> drops = [];
    /// <summary>The slot picked up by activation or being dragged, if any.</summary>
    public Guid? Carried => carried;

    private static bool Draggable(Node n) => n.Control is UiButtonComponent { Draggable: true } && n.Field == null;

    private void Carry(Node n)
    {
        var id = n.Object.Id;
        if (carried is Guid from && from != id) drops.Enqueue((from, id));
        carried = carried == null ? id : null;
    }

    private void StartDrag(float x, float y)
    {
        if (dragging || carried != null || pressed is not Guid source || grab is not PointF from ||
            Math.Abs(x - from.X) <= DragThreshold && Math.Abs(y - from.Y) <= DragThreshold) return;
        if (byId.TryGetValue(source, out var n) && Draggable(n) && Enabled(n)) (carried, dragging) = (source, true);
    }

    // Pointer release after a drag: no click; a drop when another enabled slot is under the pointer.
    private bool EndDrag(float x, float y)
    {
        if (!dragging) { grab = null; return false; }
        var source = carried!.Value;
        (carried, dragging, grab, pressed) = (null, false, null, null);
        var target = nodes.LastOrDefault(n => n.Object.Id != source && Draggable(n) && Enabled(n) && Hit(n, x, y));
        if (target != null) drops.Enqueue((source, target.Object.Id));
        return true;
    }

    private bool CancelCarry()
    {
        if (carried == null) return false;
        (carried, dragging, grab) = (null, false, null);
        return true;
    }

    private void DispatchDrops(Action<Guid, Guid>? dropped)
    {
        while (drops.TryDequeue(out var d))
            if (byId.TryGetValue(d.Source, out var s) && byId.TryGetValue(d.Target, out var t) && Enabled(s) && Enabled(t) && Visible(t))
                dropped?.Invoke(ClickTarget(d.Source), ClickTarget(d.Target));
    }

    // A ring on the carried slot, and during a pointer drag a copy of its box and label under the pointer.
    private void DrawCarried(ICanvas2D canvas, float x, float y, float scale)
    {
        if (!EngineFeatures.Text) return; // only buttons, the text feature, are carried (roadmap 15.12)
        if (carried is not Guid id || !byId.TryGetValue(id, out var n) || !Shown(n)) return;
        RectangleF At(RectangleF b) => new(x + b.X * scale, y + b.Y * scale, b.Width * scale, b.Height * scale);
        var ring = Preferences.HighContrast ? Color.Yellow : Color.Gold;
        var box = At(n.Box);
        float u = 2 * scale;
        canvas.FillRectangle(new(box.X, box.Y, box.Width, u), ring); canvas.FillRectangle(new(box.X, box.Bottom - u, box.Width, u), ring);
        canvas.FillRectangle(new(box.X, box.Y, u, box.Height), ring); canvas.FillRectangle(new(box.Right - u, box.Y, u, box.Height), ring);
        if (!dragging || pointer is not PointF at || grab is not PointF from) return;
        var ghost = At(new(n.Box.X + at.X - from.X, n.Box.Y + at.Y - from.Y, n.Box.Width, n.Box.Height));
        canvas.FillRectangle(ghost, Color.FromArgb(200, 59, 83, 107));
        if (n.Label is {} label)
            canvas.DrawText(Label(n), ghost.X + label.InsetX * scale, ghost.Y + label.InsetY * scale, label.Size * Preferences.TextScale * scale,
                SceneObject.TryColor(label.Color, out var ink) ? ink : Color.White);
    }
}

public sealed partial class SceneGame
{
    private Action<Guid, Guid>? dropHandler;

    // The target's components receive the drop; a slot without scripts passes it to its nearest parent that has some.
    private void NotifyDrop(Guid source, Guid target)
    {
        for (var body = byId.GetValueOrDefault(target); body != null; body = body.Source.ParentId is Guid parent ? byId.GetValueOrDefault(parent) : null)
        {
            if (body.Behaviours.Length == 0) continue;
            using var scope = EnterScene(body);
            foreach (var behaviour in body.Behaviours)
            {
                if (!body.Active) break;
                using var sample = FrameProfiler.Script(behaviour, nameof(GameBehaviour.OnUiDrop));
                try { behaviour.OnUiDrop(new UiDrop(source, target, State, Ui, this)); }
                catch (Exception ex) { throw ScriptFailure(behaviour, nameof(GameBehaviour.OnUiDrop), ex); }
            }
            return;
        }
    }
}
