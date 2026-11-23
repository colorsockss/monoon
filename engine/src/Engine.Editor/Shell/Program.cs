using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Engine.Core;
using Engine.Desktop;

namespace Engine.Editor;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args is ["--generate-script-project", var projectFolder])
        {
            try { Console.WriteLine(ScriptIdeProject.Generate(projectFolder).Project); }
            catch (Exception ex) { Console.Error.WriteLine(ex.Message); Environment.ExitCode = 1; }
            return;
        }
        if (args is ["--migrate-project", var folder])
        {
            try
            {
                LegacyProject.Upgrade(folder, paths => Console.WriteLine("Migration backup: " + MigrationBackup.Create(folder, paths)));
                Console.WriteLine($"YAML project: {Path.GetFullPath(folder)}");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex.Message); Environment.ExitCode = 1; }
            return;
        }
        if (args is [("--build-native-aot" or "--build-release"), var project, var destination])
        {
            try
            {
                var root = Path.GetFullPath(project);
                LegacyProject.Upgrade(root, paths => Console.WriteLine("Migration backup: " + MigrationBackup.Create(root, paths)));
                var rid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
                var output = Path.GetFullPath(destination);
                var settings = GameSettings.Load(root);
                Console.WriteLine(DesktopBuild.Export(root, settings, rid, output, nativeAot: args[0] == "--build-native-aot"));
            }
            catch (Exception ex) { Console.Error.WriteLine(ex.Message); Environment.ExitCode = 1; }
            return;
        }
        if (args is ["--run-scene", var path])
        {
            using var game = new GameSession(SceneDocument.Load(path));
            game.LoadFailed += Console.Error.WriteLine;
            DesktopHost.Run(Path.GetFileNameWithoutExtension(path), game, null, null, ShaderPixels.Preview);
            return;
        }
        AppBuilder.Configure<EditorApp>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
    }
}

public sealed class EditorApp : Application
{
    public override void Initialize()
    {
        Name = "MoNoon";
        RequestedThemeVariant = ThemeVariant.Dark;
        var theme = new FluentTheme { DensityStyle = DensityStyle.Compact };
        var styles = new Uri("avares://MoNoon/EditorStyles.axaml");
        var editor = new StyleInclude(styles) { Source = styles };
        // Fluent derives its remaining accent shades from the same token as the editor styles.
        if (editor.TryGetResource("EditorAccent", ThemeVariant.Dark, out var accent) && accent is Avalonia.Media.ISolidColorBrush brush)
            theme.Palettes[ThemeVariant.Dark] = new ColorPaletteResources { Accent = brush.Color };
        Styles.Add(theme);
        Styles.Add(editor);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow(desktop.Args?.FirstOrDefault());
        base.OnFrameworkInitializationCompleted();
    }
}
