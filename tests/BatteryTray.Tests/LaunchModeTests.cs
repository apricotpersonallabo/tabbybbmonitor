using Windows.ApplicationModel.Activation;

namespace BatteryTray.Tests;

public sealed class LaunchModeTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void InitialAndManualLaunchesShowSettings(bool completed, bool startup, bool expected) =>
        Assert.Equal(expected, LaunchMode.ShouldShowSettings(completed, startup));

    [Fact]
    public void OnlyStartupTaskActivationIsBackgroundLaunch()
    {
        Assert.True(LaunchMode.IsStartupTask(() => ActivationKind.StartupTask));
        Assert.False(LaunchMode.IsStartupTask(() => ActivationKind.Launch));
        Assert.False(LaunchMode.IsStartupTask(() => null));
        Assert.False(LaunchMode.IsStartupTask(() => throw new InvalidOperationException()));
    }
}
