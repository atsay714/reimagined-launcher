using System.Reflection;

namespace ReimaginedLauncher.Utilities;

internal sealed record MacLauncherBuildInfo(string Version, string UpstreamVersion)
{
    internal static MacLauncherBuildInfo Read()
    {
        var assembly = typeof(MacLauncherBuildInfo).Assembly;
        return new(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+', 2)[0],
            assembly.GetName().Version!.ToString(3));
    }
}
