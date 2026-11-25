using System.Reflection;
using Engine.Core;

/// <summary>Registers one step of the full run: <c>[Check(order, "only-name", …)] static void Registered(CheckRun c)</c> in the check's
/// own file, so adding a check never edits Program.cs (docs/manual/agent-workflow.md#enginechecks-등록과-계층).</summary>
/// <param name="order">Position in the full run. Steps are 100 apart; put a new step in a gap after the steps it depends on.</param>
/// <param name="only">--only names that run just this step (with every other step carrying the same name, in order).</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
sealed class CheckAttribute(int order, params string[] only) : Attribute
{
    public int Order => order;
    public string[] Only => only;
    /// <summary>--only name that runs the full run up to and including this step.</summary>
    public string? StopAfter;
    /// <summary>Exports through player-templates/; --tier fast skips it.</summary>
    public bool Templates;
    /// <summary>False: runs only through --only (for example the minutes-long Native AOT asset report).</summary>
    public bool Full = true;
}

/// <summary>What a step gets: paths, the command line, and the KeyRoom copy in the scratch folder that the later steps share.</summary>
sealed class CheckRun(string repository, string scratch, string[] args, string? only)
{
    public string Repository => repository;
    public string Scratch => scratch;
    public string[] Args => args;
    public string? Only => only;
    public string Project => Path.Combine(repository, "samples", "KeyRoom");
    // Tile collision runs on the KeyRoom showcase (solid wall tiles, a walking player); projected layouts on the KeyRoomTiles showcase.
    public string TileSample => Path.Combine(repository, "samples", "KeyRoom");
    public string GridSample => Path.Combine(repository, "samples", "KeyRoomTiles");
    // The key room showcase (the sample's start scene since the old KeyRoom scene was retired, 2026-11-26).
    public string SceneFile => Path.Combine(scratch, "Scenes", "Showcase.scene");
    // The old door art and its sprite sheet, kept as a test fixture for the clip and sheet checks.
    public string DoorSheet => Path.Combine(repository, "engine", "tests", "fixtures", "KeyRoomDoorSheet");
    bool copied;
    SceneDocument? scene;

    /// <summary>Copies KeyRoom's Assets, Scenes, Scripts, settings and manifest into the scratch root once, with the door-sheet
    /// fixture's assets added under Assets (the steps from ProjectCatalogChecks on edit this copy and the one SceneDocument
    /// loaded from it, in order).</summary>
    public void CopyKeyRoom()
    {
        if (copied) return;
        copied = true;
        foreach (var (source, folder) in new[] { (Project, "Assets"), (Project, "Scenes"), (Project, "Scripts"), (DoorSheet, "Assets") })
            foreach (var file in Directory.EnumerateFiles(Path.Combine(source, folder), "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(scratch, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
        foreach (var file in new[] { GameSettings.FileName, ProjectAssets.Manifest }) File.Copy(Path.Combine(Project, file), Path.Combine(scratch, file));
    }

    public SceneDocument Scene
    {
        get
        {
            CopyKeyRoom();
            return scene ??= SceneDocument.Load(SceneFile);
        }
    }
}

sealed record CheckStep(string Name, CheckAttribute Info, Action<CheckRun> Run)
{
    /// <summary>Every [Check] method in this assembly, in run order (ties by name, so two steps given the same number stay stable).</summary>
    public static CheckStep[] All() =>
        typeof(CheckStep).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Select(m => (Method: m, Info: m.GetCustomAttribute<CheckAttribute>()))
            .Where(x => x.Info != null)
            .Select(x => new CheckStep(x.Method.Name == "Registered" ? x.Method.DeclaringType!.Name : x.Method.DeclaringType!.Name + "." + x.Method.Name,
                x.Info!, x.Method.CreateDelegate<Action<CheckRun>>()))
            .OrderBy(s => s.Info.Order).ThenBy(s => s.Name, StringComparer.Ordinal)
            .ToArray();

    public static string[] OnlyNames(CheckStep[] steps) =>
        steps.SelectMany(s => s.Info.StopAfter is { } stop ? s.Info.Only.Append(stop) : s.Info.Only).Distinct().Order(StringComparer.Ordinal).ToArray();

    /// <summary>The steps --only selects: the ones carrying the name, else the full run up to the step it stops after.</summary>
    public static IEnumerable<CheckStep> Select(CheckStep[] steps, string? only)
    {
        if (only == null) return steps.Where(s => s.Info.Full);
        if (steps.Any(s => s.Info.Only.Contains(only))) return steps.Where(s => s.Info.Only.Contains(only));
        var end = Array.FindIndex(steps, s => s.Info.StopAfter == only);
        return steps.Take(end + 1).Where(s => s.Info.Full);
    }
}
