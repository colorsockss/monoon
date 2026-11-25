using System.Collections.Generic;
using System.Linq;
using Engine.Core;

// The lighting showcase: night falls with only the lantern on, and the hero lights the three torches in order as he passes.
public sealed class LightsTests
{
    [GameTest("Scenes/ShowcaseCave.scene")]
    public IEnumerable<ScriptWait> HeroLightsTheTorchesInOrder(GameTestRun run)
    {
        bool On(string name) => run.Objects.IsActive(run.Find(name));
        yield return ScriptWait.Until(() => run.Session.Has("dark"));
        run.Assert(On("Lantern light") && !On("Torch light 1"), "night falls with only the lantern lit");
        yield return ScriptWait.Until(() => On("Torch light 1"));
        run.Assert(!On("Torch light 2") && On("Torch flame 1"), "the first torch catches before the second");
        yield return ScriptWait.Until(() => On("Torch light 3"));
        run.Assert(Enumerable.Range(1, 3).All(i => On($"Torch flame {i}")) && run.Session.Has("unlit"), "all three torches burn");
    }
}
