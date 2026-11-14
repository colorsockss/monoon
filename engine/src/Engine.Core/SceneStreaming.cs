namespace Engine.Core;

public enum SceneChange { Replace, Add, Remove }
public enum SceneStatus { Queued, Reading, Preparing, Completed, Failed, Canceled }

public sealed class SceneOperation
{
    internal SceneOperation(Guid id, SceneChange change, string? text, double minimum)
        => (SceneId, Change, TransitionText, MinimumSeconds) = (id,change,text,minimum);
    public Guid SceneId { get; }
    public SceneChange Change { get; }
    public SceneStatus Status { get; internal set; }
    public float Progress { get; internal set; }
    public string? Error { get; private set; }
    public bool IsDone => Status is SceneStatus.Completed or SceneStatus.Failed or SceneStatus.Canceled;
    internal string? TransitionText { get; }
    internal double MinimumSeconds { get; }
    public void Cancel() { if (!IsDone) Status = SceneStatus.Canceled; }
    internal void Fail(string message) { Error = message; Status = SceneStatus.Failed; }
    internal void Complete() { Status = SceneStatus.Completed; Progress = 1; }
}

internal sealed record SceneRequest(Guid Id, SceneChange Change, SceneOperation? Operation, Action<SessionState>? Entered);

public sealed partial class GameSession
{
    private readonly ProjectAssets? manifest;
    private SceneResources? resources;
    private HashSet<SceneResources.Asset> live = [];
    private sealed class Loading(SceneRequest request, Task<SceneDocument> read)
    {
        public readonly SceneRequest Request = request;
        public SceneOperation Operation => Request.Operation!;
        public readonly Task<SceneDocument> Read = read;
        public SceneGame? Stage;
        public SceneResources.Asset[] Assets = [];
        public int Prepared;
        public double Elapsed;
    }
    private Loading? loading;
    private bool Blocking => loading?.Request.Change == SceneChange.Replace;
    public ScreenUi? InputUi => Blocking ? null : Current.InputUi;

    private void OnAssetsChanged()
    {
        if (resources != null) SyncResources();
    }

    private void PrepareBodies(IEnumerable<SceneGame.Body> bodies)
    {
        if (resources == null) return;
        try
        {
            Current.VisitBodyAssets(bodies, p => resources.Acquire(new(p,false)), p => resources.Acquire(new(p,true)), s=>resources.Acquire(new(s)));
        }
        catch
        {
            RetainLiveAndStaged();
            throw;
        }
    }

    private void SyncResources()
    {
        if (resources == null) return;
        live = SceneResources.Required(Current);
        foreach (var asset in live) resources.Acquire(asset);
        RetainLiveAndStaged();
    }

    private void RetainLiveAndStaged()
    {
        if (resources == null) return;
        if (loading is { Prepared: > 0 } active)
        {
            var keep = new HashSet<SceneResources.Asset>(live);
            keep.UnionWith(active.Assets.Take(active.Prepared));
            resources.Retain(keep);
        }
        else resources.Retain(live);
    }

    private SceneDocument ReadScene(Guid id)
    {
        if (project == null || manifest == null) throw new InvalidDataException("Save the project before switching scenes.");
        var path = manifest.PathFor(id);
        if (path?.EndsWith(ProjectAssets.Scene) != true) throw new InvalidDataException($"Missing scene asset {id}.");
        var doc = SceneDocument.Load(Path.Combine(project, path), manifest);
        doc.CheckReferences(manifest);
        return doc;
    }

    private SceneGame Stage(SceneDocument doc, bool add)
    {
        if (add) Current.ResolveAdditiveIds(doc);
        return new SceneGame(doc, null, State, compiledScripts, sharedScripts, manifest, additive: add);
    }

    private void PumpScenes(double seconds)
    {
        if (loading?.Operation.Status == SceneStatus.Canceled) DiscardLoading();
        if (State.Requested is {} request)
        {
            State.Requested = null;
            Start(request);
            return;
        }
        if (loading is not {} active) return;
        active.Elapsed += seconds;
        var op = active.Operation;
        if (!active.Read.IsCompleted) return;
        try
        {
            if (active.Stage == null)
            {
                active.Stage = Stage(active.Read.GetAwaiter().GetResult(), op.Change == SceneChange.Add);
                active.Assets = resources == null ? [] : SceneResources.Required(active.Stage).ToArray();
                op.Status = SceneStatus.Preparing;
                op.Progress = .5f;
            }
            // Native uploads and user component construction run on the game thread.
            // Bound uploads per UI frame so large batches leave time to present.
            if (active.Prepared < active.Assets.Length)
            {
                resources!.Acquire(active.Assets[active.Prepared]);
                active.Prepared++;
                op.Progress = .5f + .45f * active.Prepared / active.Assets.Length;
                return;
            }
            if (active.Elapsed < op.MinimumSeconds) return;
            prepare?.Invoke(active.Stage);
        }
        catch (Exception ex) { Reject(active.Request,ex); return; }

        var stage = active.Stage!;
        if (op.Change == SceneChange.Add)
        {
            try { Current.Attach(op.SceneId, stage); }
            catch (Exception ex) { Reject(active.Request,ex); return; }
            State.Added(op.SceneId);
            stage.Dispose(); // Transferred bodies receive lifecycle events in Current.
        }
        else Replace(op.SceneId,stage);
        loading = null;
        Finish(active.Request);
    }

    private void Start(SceneRequest request)
    {
        var op = request.Operation;
        if (op?.IsDone == true) { if (State.PendingScene == op) State.PendingScene = null; return; }
        if (request.Change == SceneChange.Add && State.IsSceneLoaded(request.Id))
        {
            Finish(request);
            return;
        }
        try
        {
            if (request.Change == SceneChange.Remove)
            {
                if (request.Id == SceneId) throw new InvalidOperationException("Replace the primary scene to unload it.");
                if (State.IsSceneLoaded(request.Id))
                {
                    try { Current.Detach(request.Id); }
                    finally { State.Removed(request.Id); }
                }
            }
            else if (op != null)
            {
                if (request.Change == SceneChange.Replace) Current.Ui.ResetInput();
                op.Status = SceneStatus.Reading;
                op.Progress = .05f;
                // Only immutable manifest + independent YAML/scene objects cross threads.
                loading = new(request, Task.Run(() => ReadScene(request.Id)));
                return;
            }
            else
            {
                var scene = Stage(ReadScene(request.Id), false);
                try
                {
                    prepare?.Invoke(scene);
                    if (resources != null)
                        foreach (var asset in SceneResources.Required(scene)) resources.Acquire(asset);
                }
                catch { scene.Dispose(); throw; }
                Replace(request.Id,scene);
            }
        }
        catch (Exception ex) { Reject(request,ex); return; }
        Finish(request);
    }

    private void Replace(Guid id, SceneGame scene)
    {
        var previous = Current;
        previous.AssetsChanged -= OnAssetsChanged;
        previous.PreparingBodies -= PrepareBodies;
        previous.SoundRequested -= Sound;
        if(previous.Diagnostics is {} debug){debug.Clear();scene.Diagnostics=debug;}
        Current = scene;
        Current.SoundRequested += Sound;
        Current.AssetsChanged += OnAssetsChanged;
        Current.PreparingBodies += PrepareBodies;
        try { previous.Dispose(); }
        catch (InvalidOperationException ex) { LoadFailed?.Invoke(ex.Message); }
        SceneId = id;
        State.Replaced(id);
    }

    private void Finish(SceneRequest request)
    {
        SyncResources();
        request.Operation?.Complete();
        if (State.PendingScene == request.Operation) State.PendingScene = null;
        request.Entered?.Invoke(State);
    }

    private void DiscardLoading()
    {
        if (loading is not {} active) return;
        loading = null;
        active.Stage?.Dispose();
        // A canceled read owns no runtime or SDL objects. Observe a late I/O failure.
        _ = active.Read.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        resources?.Retain(live);
        if (State.PendingScene == active.Operation) State.PendingScene = null;
    }

    private void Reject(SceneRequest request, Exception error)
    {
        request.Operation?.Fail(error.Message);
        DiscardLoading();
        resources?.Retain(live);
        if (State.PendingScene == request.Operation) State.PendingScene = null;
        LoadFailed?.Invoke(error.Message);
    }
}
