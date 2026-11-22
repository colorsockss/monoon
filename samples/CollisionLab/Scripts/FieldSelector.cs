using Engine.Core;

public sealed class FieldSelector : GameBehaviour
{
    public SceneReference Small, Large, Lab;
    private SceneOperation? loading;

    public override void FixedUpdate(ScriptTick tick)
    {
        if (tick.Input.Pressed("SmallField")) tick.Session.LoadScene(Small.Id);
        else if (tick.Input.Pressed("LargeField")) tick.Session.LoadScene(Large.Id);
        else if (tick.Input.Pressed("ReturnToLab")) tick.Session.LoadScene(Lab.Id);
        // Split loading: worker threads read the scene, and each update creates objects for at most
        // SceneLoadBudgetMilliseconds. Adding keeps this room running and can be canceled.
        else if (tick.Input.Pressed("LoadFieldInSteps")) loading = tick.Session.LoadSceneAsync(Large.Id, "Loading 10,000 colliders…");
        else if (tick.Input.Pressed("AddField")) loading = tick.Session.AddSceneAsync(Large.Id);
        else if (tick.Input.Pressed("CancelLoad")) loading?.Cancel();
    }
}
