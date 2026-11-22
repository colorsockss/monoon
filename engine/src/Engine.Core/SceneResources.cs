using System.Collections.Concurrent;

namespace Engine.Core;

// Renderer-thread ownership. A file has one native resource regardless of the number
// of live objects (or incoming scenes) that need it.
public sealed class SceneResources(Action<string> loadSprite, Action<string> releaseSprite,
    Action<string> loadSound, Action<string> releaseSound, Action<ShaderClip>? loadShader = null, Action<ShaderClip>? releaseShader = null) : IDisposable
{
    internal readonly record struct Asset(string Path, bool Sound, ShaderClip? Shader=null)
    {
        internal Asset(ShaderClip clip) : this("",false,clip) { }
    }
    private readonly HashSet<Asset> resident = [];
    private bool disposed;
    private int decoded;

    /// <summary>
    /// Optional worker-thread stage of a split scene load: reads a sprite (sound: true) file into a CPU buffer, or returns null to load it on the game thread.
    /// Must be thread-safe and must not touch the renderer.
    /// </summary>
    public Func<string, bool, IDisposable?>? Decode { get; init; }
    /// <summary>Game-thread upload of a buffer from <see cref="Decode"/>. The caller disposes the buffer afterwards.</summary>
    public Action<string, bool, IDisposable>? Upload { get; init; }
    /// <summary>Native sprites, sounds and shader sheets currently prepared.</summary>
    public int ResidentCount => resident.Count;
    /// <summary>Decoded buffers that are neither uploaded nor released yet.</summary>
    public int DecodedCount => Volatile.Read(ref decoded);

    internal static HashSet<Asset> Required(SceneGame scene)
    {
        var paths = new HashSet<Asset>();
        scene.PrepareAssets(p => paths.Add(new(p,false)), p => paths.Add(new(p,true)), s=>paths.Add(new(s)));
        return paths;
    }

    internal bool IsResident(Asset asset) => resident.Contains(asset);

    internal void Acquire(Asset asset, Prefetch? prefetch = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var data = prefetch?.Take(asset);
        try
        {
            if (resident.Contains(asset)) return;
            try
            {
                if (asset.Shader is {} clip) loadShader?.Invoke(clip);
                else if (data != null) Upload!(asset.Path, asset.Sound, data);
                else (asset.Sound ? loadSound : loadSprite)(asset.Path);
            }
            catch { Release(asset); throw; }
            resident.Add(asset);
        }
        finally { if (data != null) Free(data); }
    }

    private void Free(IDisposable data) { data.Dispose(); Interlocked.Decrement(ref decoded); }

    private void Release(Asset asset)
    {
        if (asset.Shader is {} clip) releaseShader?.Invoke(clip);
        else (asset.Sound ? releaseSound : releaseSprite)(asset.Path);
    }

    internal void Retain(HashSet<Asset> needed)
    {
        if (disposed) return;
        foreach (var asset in resident.Where(p => !needed.Contains(p)).ToArray())
        {
            Release(asset);
            resident.Remove(asset);
        }
    }

    /// <summary>Starts decoding the files that are not resident on worker threads. Null without a decoder.</summary>
    internal Prefetch? StartDecoding(IEnumerable<Asset> assets)
    {
        if (Decode == null || Upload == null) return null;
        var files = assets.Where(a => a.Shader == null && !resident.Contains(a)).ToArray();
        return files.Length == 0 ? null : new Prefetch(this, files);
    }

    // Decoded buffers of one loading scene. The game thread takes them in any order; the rest are freed on Dispose.
    internal sealed class Prefetch : IDisposable
    {
        private readonly SceneResources owner;
        private readonly ConcurrentDictionary<Asset, IDisposable> ready = new();
        private readonly HashSet<Asset> files;
        private readonly CancellationTokenSource cancel = new();
        private readonly Task work;

        internal Prefetch(SceneResources owner, Asset[] files)
        {
            this.owner = owner;
            this.files = [.. files];
            var token = cancel.Token;
            var options = new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) };
            work = Task.Run(() => Parallel.ForEach(files, options, asset =>
            {
                IDisposable? data;
                // A file that fails here is loaded on the game thread, which reports the error as before.
                try { data = owner.Decode!(asset.Path, asset.Sound); }
                catch (Exception) { return; }
                if (data == null) return;
                Interlocked.Increment(ref owner.decoded);
                ready[asset] = data;
            }), token);
        }

        /// <summary>The buffer is decoded (or decoding gave up), so taking it does not wait.</summary>
        internal bool IsReady(Asset asset) => !files.Contains(asset) || ready.ContainsKey(asset) || work.IsCompleted;

        internal IDisposable? Take(Asset asset) => ready.TryRemove(asset, out var data) ? data : null;

        public void Dispose()
        {
            cancel.Cancel();
            // Free buffers once in-flight decodes have stored theirs. Never blocks the game thread.
            work.ContinueWith(done =>
            {
                _ = done.Exception;
                foreach (var asset in ready.Keys)
                    if (ready.TryRemove(asset, out var data)) owner.Free(data);
                cancel.Dispose();
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        Retain([]);
        disposed = true;
    }
}
