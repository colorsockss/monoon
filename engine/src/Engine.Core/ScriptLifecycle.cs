namespace Engine.Core;

public sealed partial class SceneGame
{
    private enum LifeEvent { Create, Enable, Disable, Destroy }
    private readonly Queue<(Body Body, LifeEvent Event)> notifications = new();
    private bool started, dispatching, closing;

    private void QueueLife(Body body, LifeEvent change)
    {
        if (change == LifeEvent.Disable && capture is {} held && (held.Target == body || held.Lens == body))
            CancelWorldPointer();
        if (body.Behaviours.Length != 0) notifications.Enqueue((body, change));
    }

    private void RegisterLife(IEnumerable<Body> ordered)
    {
        // Every object in the batch is already addressable. Create all instances before enabling any.
        foreach (var body in ordered) { BindEventEndpoints(body); QueueLife(body, LifeEvent.Create); }
        foreach (var body in ordered) if (body.Active) QueueLife(body, LifeEvent.Enable);
    }

    private void FlushLife()
    {
        if (!started || dispatching || notifications.Count == 0) return;
        dispatching = true;
        try
        {
            int count = 0;
            while (notifications.TryDequeue(out var item))
            {
                if (++count > 100000) throw new InvalidOperationException("Scripts kept scheduling lifecycle callbacks without returning to the game loop.");
                // A creation callback may remove or disable a body before its queued enable.
                if (item.Event == LifeEvent.Enable)
                {
                    if (!item.Body.Active || item.Body.NotifiedActive) continue;
                    item.Body.NotifiedActive = true;
                }
                else if (item.Event == LifeEvent.Disable)
                {
                    if (!item.Body.NotifiedActive) continue;
                    item.Body.NotifiedActive = false;
                }
                using var scope = EnterScene(item.Body);
                var life = new ScriptLife(item.Body.Id, State, this, Ui);
                foreach (var script in item.Body.Behaviours)
                    try
                    {
                        switch (item.Event)
                        {
                            case LifeEvent.Create: script.OnCreate(life); break;
                            case LifeEvent.Enable: script.OnEnable(life); break;
                            case LifeEvent.Disable: script.OnDisable(life); break;
                            case LifeEvent.Destroy: script.OnDestroy(life); break;
                        }
                    }
                    catch (Exception ex) { throw ScriptFailure(script,"On"+item.Event,ex); }
                    finally { if (item.Event == LifeEvent.Destroy) script.Endpoint = null; }
            }
        }
        finally { dispatching = false; }
    }

    public void Dispose()
    {
        if (closing) return;
        closing = true;
        try
        {
            if (!started) return; // Never played, e.g. a replacement scene failed asset preloading.
            foreach (var obj in world.ParentFirst().Reverse())
            {
                var body = byId[obj.Id];
                if (!removed.Add(obj.Id)) continue;
                if (body.Active) QueueLife(body, LifeEvent.Disable);
                body.Active = false;
                Silence(body);
                QueueLife(body, LifeEvent.Destroy);
            }
            FlushLife();
        }
        finally
        {
            foreach (var stream in ownedWorlds) stream.Dispose();
            ownedWorlds.Clear();
            foreach (var body in byId.Values)
                foreach (var script in body.Behaviours)
                    if (script.Endpoint?.World == this) script.Endpoint = null;
            if (State.LiveWorld == this) State.LiveWorld = null;
            notifications.Clear();
            if (ownsScripts) scripts?.Dispose();
            if (ownsAudio) Mixer.Dispose();
        }
    }
}
