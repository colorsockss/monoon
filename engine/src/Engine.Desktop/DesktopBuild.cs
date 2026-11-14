using Engine.Core;

namespace Engine.Desktop;

public static class DesktopBuild
{
    public static string Export(string project, GameSettings settings, string target, string destination, bool nativeAot)
        => Export(project, settings, target, destination, nativeAot, BuildProfile.Distribution).Product;

    public static GameBuildResult Export(string project, GameSettings settings, string target, string destination, bool nativeAot, BuildProfile profile)
    {
        string location = AppContext.BaseDirectory;
        if (nativeAot)
            return GameBuild.ExportNativeAot(project, location, target, destination, settings, profile, ShaderPixels.Validate);
        var template = GameBuild.FindPlayerTemplate(location, target);
        return GameBuild.Export(project, template, target, destination, settings, profile, ShaderPixels.Validate);
    }
}
