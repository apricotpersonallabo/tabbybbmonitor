namespace BatteryTray.Tests;

public sealed class UpdateRestartRegistrationTests
{
    [Theory]
    [InlineData(0, 60)]
    [InlineData(15, 45)]
    [InlineData(60, 0)]
    [InlineData(120, 0)]
    public async Task RegistersUpdateOnlyRestartAndWaitsForWindowsMinimumUptime(int uptime, int wait)
    {
        string? command = null;
        uint flags = 0;
        TimeSpan? delayed = null;
        var registration = new UpdateRestartRegistration(
            (arguments, options) => { command = arguments; flags = options; return 0; },
            () => TimeSpan.FromSeconds(uptime),
            (duration, _) => { delayed = duration; return Task.CompletedTask; });
        await registration.PrepareAsync(CancellationToken.None);
        Assert.Equal("--after-update", command);
        Assert.Equal(11u, flags);
        Assert.Equal(wait == 0 ? (TimeSpan?)null : TimeSpan.FromSeconds(wait), delayed);
        Assert.True(LaunchMode.IsUpdateRestart([command!]));
        Assert.False(LaunchMode.ShouldShowSettings(true, LaunchMode.IsUpdateRestart([command!])));
    }

    [Fact]
    public async Task RegistrationFailureStopsBeforeWaiting()
    {
        var registration = new UpdateRestartRegistration((_, _) => unchecked((int)0x80004005),
            () => throw new InvalidOperationException("Uptime must not be read."),
            (_, _) => throw new InvalidOperationException("Delay must not start."));
        await Assert.ThrowsAsync<System.Runtime.InteropServices.COMException>(() => registration.PrepareAsync(CancellationToken.None));
    }

    [Fact]
    public async Task PreparationHonorsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var registration = new UpdateRestartRegistration((_, _) => 0, () => TimeSpan.Zero,
            (_, token) => { cancellation.Cancel(); return Task.FromCanceled(token); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => registration.PrepareAsync(cancellation.Token));
    }

    [Theory]
    [InlineData("--after-update", true)]
    [InlineData("--AFTER-UPDATE", false)]
    [InlineData("--startup-smoke-test", false)]
    public void RestartArgumentIsExplicit(string argument, bool expected) =>
        Assert.Equal(expected, LaunchMode.IsUpdateRestart([argument]));
}
