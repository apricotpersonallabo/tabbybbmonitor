namespace BatteryTray;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--startup-smoke-test", StringComparer.Ordinal))
            return StartupSmokeTest.Run(ApplicationConfiguration.Initialize, Console.Out, Console.Error);

        var isBackgroundLaunch = LaunchMode.IsStartupTask() || LaunchMode.IsUpdateRestart(args);
        using var instance = new SingleInstanceCoordinator();
        if (!instance.IsPrimary)
        {
            if (!isBackgroundLaunch)
                instance.RequestActivation();
            return 0;
        }

        ApplicationConfiguration.Initialize();
        UpdateRestartRegistration.Create().TryRegisterAtStartup();
        Application.Run(new TrayApplicationContext(instance, isBackgroundLaunch));
        return 0;
    }
}
