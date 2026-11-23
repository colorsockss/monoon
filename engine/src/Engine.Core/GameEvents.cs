using VYaml.Annotations;

namespace Engine.Core;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class GameEventHandlerAttribute : Attribute { }

public readonly record struct GameEventContext(Guid SenderId, GameBehaviour Sender, Guid ObjectId, IGameObjects Objects, SessionState Session);

[YamlObject]
public sealed partial record GameEventLink(string Event, Guid Target, string Script, string Method);

public abstract partial class GameBehaviour
{
    internal SceneGame.EventEndpoint? Endpoint { get; set; }
    internal Dictionary<string, Action<GameEventContext>>? EventHandlers { get; private set; }

    // The generated factory installs these once per behaviour, before its first lifecycle callback.
    public Action GameEventPublisher(string name) => () => Endpoint?.World.Publish(this, name);
    public void RegisterGameEventHandler(string name, Action<GameEventContext> callback) =>
        (EventHandlers ??= new(StringComparer.Ordinal)).Add(name, callback);

    // Read a connected sender when restoring durable game state without replaying an occurrence.
    protected IEnumerable<T> EventSources<T>(string handler) where T : GameBehaviour =>
        Endpoint?.World.SourcesFor<T>(this, handler).ToArray() ?? [];
}

public sealed partial class SceneGame
{
    internal sealed record EventEndpoint(SceneGame World, Body Owner, ScriptComponent Script);
    private int eventDepth;

    // Editor script reload: the new instances take over the object's script slots in the same order.
    internal void ReplaceBehaviours(Body body, GameBehaviour[] next)
    {
        body.Behaviours = next;
        body.PointerHandlers = next.OfType<IWorldPointerHandler>().ToArray();
        BindEventEndpoints(body);
    }
    internal IEnumerable<Body> LiveBodies => byId.Values.Where(b => !removed.Contains(b.Id));

    private void BindEventEndpoints(Body body)
    {
        int index = 0;
        foreach (var script in body.Source.Components.OfType<ScriptComponent>())
        {
            var instance = body.Behaviours[index++];
            if (script.EventLinks is { Length: > 0 } || instance.EventHandlers != null)
                instance.Endpoint = new(this, body, script);
        }
    }

    private bool IsLive(EventEndpoint endpoint) => started && !closing && endpoint.World == this &&
        endpoint.Owner.Active && byId.TryGetValue(endpoint.Owner.Id, out var current) && current == endpoint.Owner;

    internal IEnumerable<T> SourcesFor<T>(GameBehaviour receiver, string handler) where T : GameBehaviour
    {
        if (receiver.Endpoint is not {} target || !IsLive(target)) yield break;
        foreach (var body in byId.Values)
            foreach (var candidate in body.Behaviours)
                if (candidate is T sender && sender.Endpoint is {} source && IsLive(source) &&
                    source.Script.EventLinks?.Any(l => source.Script.InstanceId(l.Target) == target.Owner.Id &&
                        l.Script == target.Script.Script && l.Method == handler) == true)
                    yield return sender;
    }

    internal void Publish(GameBehaviour sender, string name)
    {
        if (sender.Endpoint is not {} source || !IsLive(source) || source.Script.EventLinks is not {} links) return;
        if (eventDepth >= 64) throw new InvalidOperationException($"Game event cycle at {source.Script.Script}.{name}.");
        eventDepth++;
        try
        {
            foreach (var link in links)
            {
                if (!IsLive(source)) break;
                if (link.Event != name || !byId.TryGetValue(source.Script.InstanceId(link.Target), out var target) || !target.Active) continue;
                foreach (var receiver in target.Behaviours)
                {
                    if (receiver.Endpoint is not {} endpoint || !IsLive(endpoint) || endpoint.Script.Script != link.Script ||
                        receiver.EventHandlers == null || !receiver.EventHandlers.TryGetValue(link.Method, out var receive)) continue;
                    using var scope = EnterScene(target);
                    try { receive(new(source.Owner.Id, sender, target.Id, this, State)); }
                    catch (Exception ex) { throw ScriptFailure(receiver, link.Method, ex); }
                }
            }
        }
        finally { eventDepth--; }
    }
}

public sealed partial record ScriptComponent
{
    public GameEventLink[]? EventLinks { get; set; }

    internal IEnumerable<string> EventErrors(ScriptDefinition definition, IReadOnlyList<SceneObject> objects,
        IReadOnlyDictionary<string, ScriptDefinition> definitions)
    {
        if (EventLinks == null) yield break;
        var seen = new HashSet<GameEventLink>();
        foreach (var link in EventLinks)
        {
            if (!seen.Add(link)) yield return $"Duplicate {link.Event} connection.";
            else if (!definition.Events.Contains(link.Event)) yield return $"Unknown event {Script}.{link.Event}.";
            else if (objects.FirstOrDefault(o => o.Id == link.Target)?.Components.OfType<ScriptComponent>()
                    .Any(s => s.Script == link.Script) != true)
                yield return $"{link.Event}: missing {link.Script} on object {link.Target}.";
            else if (!definitions.TryGetValue(link.Script, out var def) || !def.Handlers.Contains(link.Method))
                yield return $"{link.Event}: missing handler {link.Script}.{link.Method}.";
        }
    }
}
