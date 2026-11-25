using Engine.Core;

if (args is ["--render-candidates", var collisionLab, var evidence])
{
    RenderCandidateChecks.Run(Path.GetFullPath(collisionLab), Path.GetFullPath(evidence));
    return;
}

var repository = Path.GetFullPath(args.FirstOrDefault() ?? ".");
if (File.Exists(Path.Combine(repository, "MoNoonEngine.slnx"))) repository = Directory.GetParent(repository)!.FullName;
// Check groups register themselves with [Check] in their own files (CheckRegistry.cs). --only <name> runs one group (or the full
// run up to it); the older --<name>-only flags stay as aliases for one phase. --tier fast skips the groups that export through
// player-templates/ (docs/manual/agent-workflow.md#enginechecks-등록과-계층).
var steps = CheckStep.All();
var onlyNames = CheckStep.OnlyNames(steps);
var onlyAt = Array.IndexOf(args, "--only");
var only = onlyAt >= 0 ? args.ElementAtOrDefault(onlyAt + 1)
    : args.Where(a => a.Length > 7 && a.StartsWith("--") && a.EndsWith("-only")).Select(a => a[2..^5]).FirstOrDefault();
if ((onlyAt >= 0 || only != null) && !onlyNames.Contains(only))
    throw new ArgumentException($"Unknown check group '{only}'. Use --only <name> with one of: {string.Join(", ", onlyNames)}.");
var tierAt = Array.IndexOf(args, "--tier");
var tier = tierAt >= 0 ? args.ElementAtOrDefault(tierAt + 1) : "full";
if (tier is not ("fast" or "full")) throw new ArgumentException($"Unknown tier '{tier}'. Use --tier fast or --tier full.");
if (steps[^1].Name != nameof(BuildSizeChecks)) throw new InvalidOperationException("The build-size stage must stay last; give " + steps[^1].Name + " a lower order.");
if (args.Contains("--list"))
{
    foreach (var s in steps)
        Console.WriteLine($"{s.Info.Order,9} {s.Name,-40} {string.Join(",", s.Info.Only),-28} {(s.Info.StopAfter is { } stop ? "stop:" + stop : ""),-22}{(s.Info.Templates ? " templates" : "")}{(s.Info.Full ? "" : " only")}");
    return;
}
var project = Path.Combine(repository, "samples", "KeyRoom");
var tileSample=Path.Combine(repository,"samples","KeyRoom");
if (args is [_, "--tile-build",var fixture,var output])
{
    TileChecks.Prepare(tileSample,fixture);
    var grids=fixture+"-grids";
    TileChecks.Prepare(Path.Combine(repository,"samples","KeyRoomTiles"),grids);
    foreach (var rid in new[]{"osx-arm64","win-x64","linux-x64"})
        Console.WriteLine(GameBuild.Export(fixture,GameBuild.FindPlayerTemplate(Path.Combine(repository,"engine"),rid),rid,output,GameSettings.Load(fixture)));
    TileChecks.Run(fixture,grids);
    return;
}
if (args is [_, "--check-vorbis-stream", var musicFile])
{
    VorbisChecks.CheckLongTrack(musicFile);
    return;
}
if (args is [_, "--check-shader-players", var releaseGame, var aotGame])
{
    BakedShaderChecks.ComparePlayers(repository, releaseGame, aotGame);
    return;
}
if (args is [_, "--sprite-sample",var folderPath]) { SpriteChecks.Prepare(Path.GetFullPath(folderPath)); return; }
var scratch = Directory.CreateTempSubdirectory("monoon-check-").FullName;
var previousProfile = Environment.GetEnvironmentVariable("MONOON_USER_DATA");
Environment.SetEnvironmentVariable("MONOON_USER_DATA",Path.Combine(scratch,".profiles"));
try
{
    if (args is [_, "--inventory-replay", var replayBuilds])
    {
        InputReplayChecks.CheckRecording(scratch);
        await InputReplayChecks.RunInventory(repository, replayBuilds, scratch);
        return;
    }
    if (args is [_, "--inventory-builds", _, ..])
    {
        BuildProfile? expectedProfile = args switch
        {
            [_, _, _] => null,
            [_, _, _, "--profile", "distribution"] => BuildProfile.Distribution,
            [_, _, _, "--profile", "development"] => BuildProfile.Development,
            _ => throw new ArgumentException("Expected --inventory-builds <directory> [--profile development|distribution].")
        };
        DialogueChecks.Run(repository, scratch);
        QuestChecks.Run(repository, scratch);
        InventoryChecks.Run(repository, scratch);
        await InventoryBuildChecks.Run(args[2], scratch, expectedProfile);
        return;
    }
    var run = new CheckRun(repository, scratch, args, only);
    var skipped = 0;
    foreach (var step in CheckStep.Select(steps, only))
    {
        if (tier == "fast" && step.Info.Templates) { Console.WriteLine($"SKIP {step.Name} (--tier fast: needs player templates)"); skipped++; continue; }
        step.Run(run);
    }
    if (only != null) return;
    Console.WriteLine(tier == "fast" ? $"All fast-tier checks passed ({skipped} template groups skipped)." : "All checks passed.");
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    Environment.ExitCode = 1;
}
finally
{
    Environment.SetEnvironmentVariable("MONOON_USER_DATA",previousProfile);
    Directory.Delete(scratch, recursive: true);
}
