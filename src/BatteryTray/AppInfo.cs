using System.Runtime.InteropServices;
using Windows.ApplicationModel;

namespace BatteryTray;

internal static class AppInfo
{
    public const string SupportUrl = "https://apricotpersonallabo.github.io/tabbybbmonitor/";

    public static string Version => ResolveVersion(ReadPackageVersion,
        typeof(AppInfo).Assembly.GetName().Version ?? new Version(1, 0, 0, 0));

    private static Version ReadPackageVersion()
    {
        var version = Package.Current.Id.Version;
        return new Version(version.Major, version.Minor, version.Build, version.Revision);
    }

    internal static string ResolveVersion(Func<Version> readPackageVersion, Version assemblyVersion)
    {
        Version version;
        try { version = readPackageVersion(); }
        catch (Exception exception) when (exception is InvalidOperationException or COMException)
        {
            version = assemblyVersion;
        }
        return $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}.{Math.Max(0, version.Revision)}";
    }
}
