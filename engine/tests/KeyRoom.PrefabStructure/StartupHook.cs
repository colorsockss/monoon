using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Engine.Core;

// Drives the packaged editor: add a lock and a sound to one chest, apply only the lock, Undo, Redo, save.
public static class StartupHook
{
    private static readonly string Output = Environment.GetEnvironmentVariable("MONOON_STRUCTURE_EDITOR")!;
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static void Initialize() => Task.Run(async () =>
    {
        while (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: not null })
            await Task.Delay(100);
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var desktop = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
            try { await Run(desktop.MainWindow!); }
            catch (Exception ex) { File.WriteAllText(Path.Combine(Output, "error.txt"), ex.ToString()); }
            finally { desktop.Shutdown(); }
        });
    });

    private static object? Call(Window w, string method, params object?[] args) => w.GetType().GetMethod(method, Members)!.Invoke(w, args);
    private static SceneDocument Scene(Window w) => (SceneDocument)w.GetType().GetProperty("Scene", Members)!.GetValue(w)!;
    private static void Require(bool ok, string why) { if (!ok) throw new InvalidDataException(why); }
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, b));
    private static bool Structural(string? item) => item?.Contains(" (added") == true || item?.Contains(" (removed") == true;
    private static void Log(string line) => File.AppendAllText(Path.Combine(Output, "editor.txt"), line + "\n");

    private static async Task Wait(Func<bool> ready, string what)
    {
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (!ready()) { if (DateTime.UtcNow > deadline) throw new TimeoutException(what); await Task.Delay(80); }
    }

    private static void Capture(Control area, string name)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(area.Bounds.Width), (int)Math.Ceiling(area.Bounds.Height)));
        bitmap.Render(area);
        bitmap.Save(Path.Combine(Output, name + ".png"));
    }

    private static string State(Window main, string prefab)
    {
        var scene = Scene(main);
        string Chest(string name)
        {
            var chest = scene.Objects.Single(o => o.Name == name);
            return $"{name}: lock {scene.Objects.Count(o => o.Name == "Lock" && o.Parent == chest)}, sound {(chest.GetComponent<AudioSourceComponent>() != null ? 1 : 0)}";
        }
        var source = File.ReadAllText(prefab).Contains("name: Lock") ? "source lock" : "source without lock";
        return $"{source}; {Chest("Chest 1")}; {Chest("Chest 2")}; {Chest("Chest 3")}";
    }

    private static async Task Run(Window main)
    {
        string root = Environment.GetEnvironmentVariable("MONOON_QA_PROJECT")!;
        string prefab = Path.Combine(root, "Assets", "Chest.prefab"), sceneFile = Path.Combine(root, "Scenes", "PrefabWorkshop.scene");
        await Wait(() => Scene(main).Objects.Count > 1, "KeyRoom");
        await (Task)Call(main, "SwitchScene", "PrefabWorkshop")!;
        var chest = Scene(main).Objects.Single(o => o.Name == "Chest 2");
        Call(main, "Select", chest);
        Call(main, "AddObject", chest, true);
        var added = Scene(main).Objects.Single(o => o.Parent == chest && o.Name.StartsWith("Square"));
        (added.Name, added.Tag, added.X, added.Y, added.Width, added.Height) = ("Lock", "Lock", 24, 12, 12, 14);
        added.GetComponent<SpriteRendererComponent>()!.SquareColor = "#E8C547";
        Call(main, "FinishObjectChange", added, null);
        Call(main, "Select", chest);
        var sound = ProjectAssets.Scan(root).IdFor("Assets/Unlock.wav");
        Require((bool)Call(main, "AttachComponent", chest, new AudioSourceComponent { Sound = sound })!, "Audio Source was not added");
        string before = State(main, prefab);
        Log("edited   " + before);

        var overrides = main.FindControl<Button>("OverridesButton")!;
        Require(overrides.IsVisible, "Overrides… is hidden for a prefab root");
        Click(overrides);
        await Wait(() => main.OwnedWindows.Any(w => w.Title == "Prefab Overrides" && w.IsVisible), "Prefab Overrides window");
        var window = main.OwnedWindows.Single(w => w.Title == "Prefab Overrides");
        await Task.Delay(300);
        var list = window.GetVisualDescendants().OfType<ListBox>().Single();
        string Items() => string.Join(" | ", list.Items.Select(i => i!.ToString()).Where(Structural));
        Log("listed   " + Items());
        Require(Items() == "Chest 2 · audioSource (added) | Lock (added object)", "Unexpected structural list: " + Items());
        list.SelectedItem = list.Items.Single(i => i!.ToString() == "Lock (added object)");
        await Task.Delay(200);
        Capture(window, "overrides");
        Button Labeled(string text) => window.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == text);
        Click(Labeled("Apply to Source"));
        await Wait(() => Items() == "Chest 2 · audioSource (added)", "list after applying the lock");
        string applied = State(main, prefab);
        Log("applied  " + applied);
        Require(applied == "source lock; Chest 1: lock 1, sound 0; Chest 2: lock 1, sound 1; Chest 3: lock 1, sound 0", applied);
        window.Width = window.MinWidth;
        list.SelectedItem = list.Items.Single(i => Structural(i!.ToString()));
        await Task.Delay(300);
        Capture(window, "compact");
        Click(Labeled("Done"));
        await Wait(() => !window.IsVisible, "closing overrides");

        await (Task)Call(main, "RestoreEdit", false)!;
        Log("undo     " + State(main, prefab));
        Require(State(main, prefab) == before, "Undo did not restore source and instance: " + State(main, prefab));
        await (Task)Call(main, "RestoreEdit", true)!;
        Log("redo     " + State(main, prefab));
        Require(State(main, prefab) == applied, "Redo did not reapply the lock");
        Require(await (Task<bool>)Call(main, "Save")!, "Scene save cancelled");
        var reopened = SceneDocument.OpenForEditing(sceneFile);
        var remaining = reopened.PrefabOverrides(reopened.Objects.Single(o => o.Name == "Chest 2")).Where(Structural).ToArray();
        Log("reopened " + string.Join(" | ", remaining));
        Require(remaining is ["Chest 2 · audioSource (added)"], "Saved scene lost the remaining override");
    }
}
