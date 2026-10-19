namespace BatteryTray;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--startup-smoke-test", StringComparer.Ordinal))
            return StartupSmokeTest.Run(ApplicationConfiguration.Initialize, Console.Out, Console.Error);

        var isStartupTask = LaunchMode.IsStartupTask();
        using var instance = new SingleInstanceCoordinator();
        if (!instance.IsPrimary)
        {
            if (!isStartupTask)
                instance.RequestActivation();
            return 0;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext(instance, isStartupTask));
        return 0;
    }
}
