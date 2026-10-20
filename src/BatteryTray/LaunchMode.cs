using System.Runtime.InteropServices;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;

namespace BatteryTray;

internal static class LaunchMode
{
    internal static bool IsUpdateRestart(IEnumerable<string> args) =>
        args.Contains(UpdateRestartRegistration.RestartArgument, StringComparer.Ordinal);

    public static bool IsStartupTask() => IsStartupTask(() => AppInstance.GetActivatedEventArgs()?.Kind);

    internal static bool IsStartupTask(Func<ActivationKind?> readKind)
    {
        try { return readKind() == ActivationKind.StartupTask; }
        catch (Exception exception) when (exception is InvalidOperationException or COMException or NotSupportedException)
        {
            return false;
        }
    }

    internal static bool ShouldShowSettings(bool onboardingCompleted, bool isBackgroundLaunch) =>
        !onboardingCompleted || !isBackgroundLaunch;
}
