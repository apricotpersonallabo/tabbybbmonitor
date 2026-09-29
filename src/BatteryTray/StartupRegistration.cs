using Windows.ApplicationModel;

namespace BatteryTray;

internal enum StartupRegistrationState
{
    Unsupported,
    Disabled,
    Enabled,
    DisabledByUser,
    PolicyControlled
}

internal sealed record StartupRegistrationResult(
    StartupRegistrationState State,
    bool IsEnabled,
    string? ErrorMessage = null)
{
    public bool CanChange => State is StartupRegistrationState.Disabled or StartupRegistrationState.Enabled;
}

internal interface IStartupRegistration
{
    Task<StartupRegistrationResult> GetStateAsync();
    Task<StartupRegistrationResult> SetEnabledAsync(bool enabled);
}

internal sealed class StartupRegistration : IStartupRegistration
{
    internal const string TaskId = "BatteryTrayStartup";

    public async Task<StartupRegistrationResult> GetStateAsync()
    {
        try
        {
            _ = Package.Current.Id.Name;
            var task = await StartupTask.GetAsync(TaskId);
            return Map(task.State);
        }
        catch (InvalidOperationException)
        {
            return new StartupRegistrationResult(StartupRegistrationState.Unsupported, false);
        }
        catch (Exception exception)
        {
            return new StartupRegistrationResult(StartupRegistrationState.Unsupported, false, exception.Message);
        }
    }

    public async Task<StartupRegistrationResult> SetEnabledAsync(bool enabled)
    {
        try
        {
            _ = Package.Current.Id.Name;
            var task = await StartupTask.GetAsync(TaskId);
            if (enabled)
            {
                if (task.State == StartupTaskState.Disabled)
                    await task.RequestEnableAsync();
            }
            else if (task.State == StartupTaskState.Enabled)
            {
                task.Disable();
            }

            return Map(task.State);
        }
        catch (InvalidOperationException)
        {
            return new StartupRegistrationResult(StartupRegistrationState.Unsupported, false);
        }
        catch (Exception exception)
        {
            return new StartupRegistrationResult(StartupRegistrationState.Unsupported, false, exception.Message);
        }
    }

    private static StartupRegistrationResult Map(StartupTaskState state) => state switch
    {
        StartupTaskState.Disabled => new StartupRegistrationResult(StartupRegistrationState.Disabled, false),
        StartupTaskState.DisabledByUser => new StartupRegistrationResult(StartupRegistrationState.DisabledByUser, false),
        StartupTaskState.Enabled => new StartupRegistrationResult(StartupRegistrationState.Enabled, true),
        StartupTaskState.DisabledByPolicy =>
            new StartupRegistrationResult(StartupRegistrationState.PolicyControlled, false),
        StartupTaskState.EnabledByPolicy =>
            new StartupRegistrationResult(StartupRegistrationState.PolicyControlled, true),
        _ => new StartupRegistrationResult(StartupRegistrationState.Unsupported, false)
    };
}
