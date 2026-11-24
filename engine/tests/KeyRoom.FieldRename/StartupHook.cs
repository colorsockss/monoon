using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Engine.Core;

public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_RENAME_EDITOR")!;
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static void Initialize() => Start(Path.Combine(Output, "error.txt"), life => Run(life, life.MainWindow!), allow: ["Prefab · *"]);

    private static object? Call(Window w, string method, params object[] args) =>
        w.GetType().GetMethod(method, Members)!.Invoke(w, args);
    private static SceneDocument Scene(Window w) => (SceneDocument)w.GetType().GetProperty("Scene",Members)!.GetValue(w)!;
    private static void Require(bool ok, string why) { if (!ok) throw new InvalidDataException(why); }

    private static async Task Inspect(Window w, string name, string field, int expected)
    {
        Call(w, "Select", Scene(w).Objects.Single(o => o.Name == name));
        TextBox? Box() => w.GetVisualDescendants().OfType<TextBox>().SingleOrDefault(b => AutomationProperties.GetName(b) == field);
        try { await Until(() => Box() != null, "Inspector field"); } catch (TimeoutException) { } // reported below with the visible labels
        var box = Box() ?? throw new InvalidDataException($"Missing Inspector field {name}.{field}: " +
            string.Join(" ", w.GetVisualDescendants().OfType<TextBlock>().Select(b => b.Text).TakeLast(30)));
        Require(box.Text == expected.ToString(), $"{name}.{field}: expected {expected}, got {box.Text}");
        File.AppendAllText(Path.Combine(Output,"inspector.txt"),$"{name} {field} {box.Text}\n");
    }

    private static async Task Run(IClassicDesktopStyleApplicationLifetime desktop, Window main)
    {
        string root = Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!;
        string asset = "Assets/MovementPlayer.prefab", sceneFile = Path.Combine(root,"Scenes/MovementWorkshop.scene");
        await Until(() => Scene(main).Objects.Any(o => o.Name == "Player"), "Player");
        Task Open(string name) => (Task)Call(main,"SwitchScene",name)!;
        async Task Source(string field, bool save)
        {
            var closed = (Task)Call(main, "EditPrefabSource", asset)!;
            var w = desktop.Windows.Single(w => w != main && w.IsVisible);
            await Inspect(w,"Player",field,240);
            if (save) Require(await (Task<bool>)Call(w,"Save")!, "Prefab save cancelled");
            w.Close();
            await closed;
        }
        async Task SaveScene()
        {
            Require(await (Task<bool>)Call(main,"Save")!,"Scene save cancelled");
        }
        await Open("MovementWorkshop");
        await Source("Speed",false);
        await Inspect(main,"Inherited player","Speed",240);
        await Inspect(main,"Overridden player","Speed",320);
        await SaveScene();
        Require(File.ReadAllText(sceneFile).Contains("Speed: 320"),"Missing old stored override");

        File.Copy(Path.Combine(root,"renamed.source"),Path.Combine(root,"Scripts/PlayerMovement.cs"),true);
        string caller = Path.Combine(root,"Scripts/PushCrate.cs");
        File.WriteAllText(caller, File.ReadAllText(caller).Replace("movement.Speed","movement.MoveSpeed"));
        // Save the source first, while the scene and its prefab baselines on disk still use Speed.
        await Source("MoveSpeed",true);
        Require(File.ReadAllText(Path.Combine(root,asset)).Contains("MoveSpeed: 240"),"Source did not migrate on Save");
        var mixed = SceneDocument.OpenForEditing(sceneFile);
        Require(mixed.Objects.Single(o => o.Name == "Overridden player").Components.OfType<ScriptComponent>().Single()
            .Values["MoveSpeed"].GetSingle() == 320, "New prefab and old scene lost the override during refresh");
        await Inspect(main,"Inherited player","MoveSpeed",240);
        await Inspect(main,"Overridden player","MoveSpeed",320);
        await SaveScene();
        await Open("MovementWorkshop");
        await Source("MoveSpeed",false);
        await Inspect(main,"Inherited player","MoveSpeed",240);
        await Inspect(main,"Overridden player","MoveSpeed",320);
        var doc = Scene(main);
        foreach (var player in doc.Objects.Where(o => o.Prefab != null))
        {
            var fields = doc.PrefabOverrides(player).Where(x => x.Contains("values.")).ToArray();
            Require(player.Name == "Inherited player" ? fields.Length == 0 : fields is [var f] && f.EndsWith(".MoveSpeed"),
                $"{player.Name}: unexpected overrides {string.Join(", ",fields)}");
        }
        var saved = File.ReadAllText(sceneFile);
        Require(!System.Text.RegularExpressions.Regex.IsMatch(saved, @"(?m)^\s*Speed\s*:") && saved.Contains("MoveSpeed: 320"),
            "Stale field in saved instance or baseline");
        await Open("KeyRoom");
        var play = main.FindControl<Button>("PlayButton")!;
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent,play));
        var view = main.FindControl<Control>("Viewport")!;
        var game = (GameSession)view.GetType().GetField("game",Members)!.GetValue(view)!;
        game.LoadFailed += message => throw new InvalidDataException(message);
        InputReplay.Load(Path.Combine(Output,"Movement.inputreplay")).Play(game, GameSettings.Load(root).ProjectId!.Value);
        play.RaiseEvent(new RoutedEventArgs(Button.ClickEvent,play));
    }
}
