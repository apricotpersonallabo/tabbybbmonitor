namespace BatteryTray;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var instance = new SingleInstanceCoordinator();
        if (!instance.IsPrimary)
        {
            instance.RequestActivation();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext(instance));
    }
}
