using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Engine.Core;
using Engine.Editor;

// Editor-only: compiled by the MoNoon editor from Editor/, never part of the game scripts or a game build.
public sealed class KeyRoomTools : IEditorExtension
{
    private const string Coin = "Assets/Showcase/Coin.prefab";

    public void Register(EditorExtensionRegistry editor)
    {
        editor.AddMenuCommand("Place Coin", context => context.PlacePrefab(Coin));
        editor.AddInspector("CollectCoin", inspector =>
        {
            inspector.Field("LootTable");
            inspector.Row("Coins", new TextBlock { Text = $"{CoinCount(inspector.Editor)} in this scene" });
            var place = new Button { Content = "Place another coin" };
            place.Click += (_, _) => inspector.Editor.PlacePrefab(Coin);
            inspector.Row("Place", place);
        });
        // Every [InputAction] string field: a list of the project's input actions instead of free text. A pick is one Undo step.
        editor.AddFieldDrawer("InputAction", field =>
        {
            var actions = GameSettings.Load(field.Editor.ProjectDirectory).Input.Actions?.Select(a => a.Name).ToList() ?? [];
            if (field.Value is string current && !actions.Contains(current)) actions.Add(current); // keep an unknown name visible
            var picker = new ComboBox { ItemsSource = actions, SelectedItem = field.Value, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
            picker.SelectionChanged += (_, _) => { if (picker.SelectedItem is string name && name != field.Value as string) field.Set(name); };
            return picker;
        });
        editor.AddPanel("Coins", context =>
        {
            var count = new TextBlock();
            var place = new Button { Content = "Place Coin" };
            place.Click += (_, _) => context.PlacePrefab(Coin);
            var panel = new StackPanel { Spacing = 3, Children = { context.Row("In scene", count), context.Row("Prefab", new TextBlock { Text = Coin }), context.Row("Place", place) } };
            var timer = new DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(500) };
            timer.Tick += (_, _) => { count.Text = CoinCount(context).ToString(); place.IsEnabled = !context.IsPlaying; };
            panel.AttachedToVisualTree += (_, _) => { timer.Start(); count.Text = CoinCount(context).ToString(); };
            panel.DetachedFromVisualTree += (_, _) => timer.Stop();
            return panel;
        });
    }

    private static int CoinCount(EditorContext context) =>
        context.Scene.Objects.Count(o => o.Components.OfType<ScriptComponent>().Any(s => s.Script == "CollectCoin"));
}
