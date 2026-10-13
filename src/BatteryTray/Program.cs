namespace BatteryTray;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var isStartupTask = LaunchMode.IsStartupTask();
        using var instance = new SingleInstanceCoordinator();
        if (!instance.IsPrimary)
        {
            if (!isStartupTask)
                instance.RequestActivation();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext(instance, isStartupTask));
    }
}
