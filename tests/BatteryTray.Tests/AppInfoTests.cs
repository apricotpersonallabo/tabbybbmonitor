using System.Runtime.InteropServices;

namespace BatteryTray.Tests;

public sealed class AppInfoTests
{
    [Fact]
    public void PackagedVersionTakesPriorityOverAssemblyVersion() =>
        Assert.Equal("2.3.4.0", AppInfo.ResolveVersion(() => new Version(2, 3, 4, 0), new Version(1, 0, 0, 0)));

    [Fact]
    public void UnpackagedExecutionUsesAssemblyVersion() =>
        Assert.Equal("1.2.3.4", AppInfo.ResolveVersion(() => throw new InvalidOperationException(), new Version(1, 2, 3, 4)));

    [Fact]
    public void MissingPackageIdentityUsesAssemblyVersion() =>
        Assert.Equal("1.2.0.0", AppInfo.ResolveVersion(() => throw new COMException(), new Version(1, 2)));

    [Fact]
    public void RunningApplicationExposesFourPartVersion() =>
        Assert.Matches(@"^\d+\.\d+\.\d+\.\d+$", AppInfo.Version);
}
