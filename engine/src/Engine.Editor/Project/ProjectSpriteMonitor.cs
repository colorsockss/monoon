using System.Threading.Channels;
using Avalonia.Threading;
using Engine.Core;

namespace Engine.Editor;

// File watchers can issue several events for one save, or drop events while the app is suspended.
internal sealed class ProjectSpriteMonitor : IDisposable
{
    private readonly CancellationTokenSource lifetime = new();
    private readonly Channel<bool> requests = Channel.CreateBounded<bool>(1);
    internal ProjectSpriteMonitor(string root, Action<ProjectAssets> apply, Action<string> report)
    {
        var token = lifetime.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                while (await requests.Reader.WaitToReadAsync(token))
                {
                    do
                    {
                        requests.Reader.TryRead(out _);
                        await Task.Delay(180, token);
                    } while (requests.Reader.TryPeek(out _));
                    for (int attempt = 0; ; attempt++)
                    {
                        try
                        {
                            string? importError = null;
                            var assets = SpriteSheetImporter.Scan(root, message => importError = message);
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                if (token.IsCancellationRequested) return;
                                apply(assets);
                                if (importError != null) report(importError);
                            });
                            break;
                        }
                        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
                        {
                            if (attempt < 2) { await Task.Delay(250 * (attempt + 1), token); continue; }
                            await Dispatcher.UIThread.InvokeAsync(() => { if (!token.IsCancellationRequested) report(ex.Message); });
                            break;
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            finally { lifetime.Dispose(); }
        });
    }

    internal void Request() => requests.Writer.TryWrite(true);
    public void Dispose() { lifetime.Cancel(); requests.Writer.TryComplete(); }
}
