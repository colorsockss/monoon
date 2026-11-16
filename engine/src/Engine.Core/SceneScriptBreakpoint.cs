namespace Engine.Core;

// Entry points emitted only for editor Play sessions opened for external source debugging.
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public static class SceneScriptBreakpoint
{
    internal static volatile Func<string, int, bool>? Filter;
    internal static volatile Action<string, int, string, string[], object?[]>? Handler;

    public static bool IsSet(string file, int line) => Filter?.Invoke(file, line) == true;
    public static void Hit(string file, int line, string method, string[] names, object?[] values) =>
        Handler?.Invoke(file, line, method, names, values);
}
