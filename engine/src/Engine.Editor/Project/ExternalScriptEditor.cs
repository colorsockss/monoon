using System.Diagnostics;

namespace Engine.Editor;

internal static class ExternalScriptEditor
{
    private static string PreferenceFile
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var file = Path.Combine(root, "MoNoon", "script-editor.txt");
            var legacy = Path.Combine(root, "Brutal Editor", "script-editor.txt");
            if (!File.Exists(file) && File.Exists(legacy))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.Copy(legacy, file);
            }
            return file;
        }
    }

    public static string Preference => File.Exists(PreferenceFile) ? File.ReadAllText(PreferenceFile).Trim() : "";

    private static bool IsMacApp(string path) => OperatingSystem.IsMacOS() &&
        path.EndsWith(".app", StringComparison.OrdinalIgnoreCase) && Directory.Exists(path);

    public static void SavePreference(string input)
    {
        string path = input.Trim();
        if (path.Length == 0)
        {
            if (File.Exists(PreferenceFile)) File.WriteAllText(PreferenceFile, "");
            return;
        }
        if (!Path.IsPathFullyQualified(path) || (!IsMacApp(path) && !File.Exists(path)))
            throw new IOException("Choose an installed application or enter its full executable path.");
        if (!OperatingSystem.IsWindows() && !IsMacApp(path) &&
            (File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0)
            throw new IOException("Choose an executable file.");
        var folder = Directory.CreateDirectory(Path.GetDirectoryName(PreferenceFile)!);
        var temp = Path.Combine(folder.FullName, Path.GetRandomFileName());
        try
        {
            File.WriteAllText(temp, path);
            File.Move(temp, PreferenceFile, overwrite: true);
        }
        finally { File.Delete(temp); }
    }

    public static bool UsesSceneWorkspace => Path.GetFileNameWithoutExtension(Preference).ToLowerInvariant()
        is "visual studio code" or "code" or "cursor" or "codium" or "vscodium" or "code - insiders" or "code-insiders";

    public static Task OpenProject(ScriptIdeProject files) => Open(UsesSceneWorkspace ? files.Workspace : files.Solution);

    public static async Task Open(string source, int line = 0)
    {
        if (!File.Exists(source)) throw new FileNotFoundException($"Script file is missing: {source}");
        string editor = Preference;
        if (line > 0 && IsMacApp(editor))
        {
            var cli = Path.Combine(editor, "Contents", "Resources", "app", "bin");
            foreach (var name in new[] { "code", "cursor", "codium" })
                if (File.Exists(Path.Combine(cli, name))) { editor = Path.Combine(cli, name); break; }
        }
        if (OperatingSystem.IsMacOS() && (editor.Length == 0 || IsMacApp(editor)))
        {
            var start = new ProcessStartInfo("/usr/bin/open") { UseShellExecute = false, RedirectStandardError = true };
            if (editor.Length > 0)
            {
                start.ArgumentList.Add("-a");
                start.ArgumentList.Add(editor);
            }
            start.ArgumentList.Add(source);
            using var launcher = Process.Start(start) ?? throw new IOException("Could not start the script editor.");
            string error = await launcher.StandardError.ReadToEndAsync();
            await launcher.WaitForExitAsync();
            if (launcher.ExitCode != 0)
                throw new IOException($"Could not open the script. Choose a script editor in Preferences. {error.Trim()}");
            return;
        }
        if (editor.Length > 0 && !File.Exists(editor))
            throw new IOException("The configured script editor is missing. Choose another in Preferences.");
        var launch = editor.Length == 0
            ? new ProcessStartInfo(source) { UseShellExecute = true }
            : new ProcessStartInfo(editor) { UseShellExecute = false,  };
        if (editor.Length != 0)
        {
            var name = Path.GetFileNameWithoutExtension(editor).ToLowerInvariant();
            if (line > 0 && name is "code" or "cursor" or "codium") { launch.ArgumentList.Add("--goto"); launch.ArgumentList.Add($"{source}:{line}"); }
            else { if (line > 0 && name is "rider" or "idea") { launch.ArgumentList.Add("--line"); launch.ArgumentList.Add(line.ToString()); } launch.ArgumentList.Add(source); }
        }
        using var process = Process.Start(launch);
    }
}
