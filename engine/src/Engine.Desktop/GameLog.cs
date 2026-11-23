using Brutal.Logging;
using Engine.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Engine.Desktop;

/// <summary>
/// Player diagnostics (script logs, scene load failures, content update warnings) through Brutal.Logging.
/// Each message is written unchanged as one line, synchronously: stdout, or stderr for errors, exactly as
/// SessionState's console fallback did, so harness-parsed lines keep their bytes and order.
/// </summary>
public static class GameLog
{
    private const string Name = "MoNoon";
    private static LogCategory Category => Started.Category;

    /// <summary>Routes the session's script logs here instead of SessionState's console fallback.</summary>
    public static void Attach(SessionState state)
    {
        state.Logged += Write;
        // Start the log system off the game thread: with JIT its first use takes about 20 ms (Native AOT: under 1 ms).
        ThreadPool.UnsafeQueueUserWorkItem(static _ => _ = Started.Category, null);
    }

    public static void Write(GameLogEntry entry)
    {
        switch (entry.Level)
        {
            case GameLogLevel.Error: Category.Error(entry.Message); break;
            case GameLogLevel.Warning: Category.Warning(entry.Message); break;
            default: Category.Info(entry.Message); break;
        }
    }

    public static void Error(string message) => Category.Error(message);

    private static class Started
    {
        internal static readonly LogCategory Category = Start();

        private static LogCategory Start()
        {
            // BRUTAL's bindings trace every native call once the log system is on. Keeping their categories off
            // leaves each SDL call at the cost it had with logging disabled.
            foreach (var name in (ReadOnlySpan<string>)["Sdl", "SdlMixer", "SdlTtf"]) LogCategory.Make(name).IsCategoryEnabled = false;
            // Not MEL's console provider: it prefixes level and category and writes on a background thread.
            LogSystem.Initialize(b => b.ClearProviders().SetMinimumLevel(LogLevel.Information).AddProvider(new LineProvider()));
            return LogCategory.Make(Name);
        }
    }

    private sealed class LineProvider : ILoggerProvider, ILogger
    {
        // Other BRUTAL categories stayed silent before the log system was on; keep them silent.
        public ILogger CreateLogger(string categoryName) => categoryName == Name ? this : NullLogger.Instance;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            (logLevel >= LogLevel.Error ? Console.Error : Console.Out).WriteLine(formatter(state, exception));
        public void Dispose() { }
    }
}
