namespace BatteryTray.Tests;

public sealed class BluetoothBatteryLevelTests
{
    private const string Battery = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
    private const string Present = "{83DA6326-97A6-4088-9453-A1923F573B29} 15";
    private const string Container = "System.Devices.ContainerId";

    [Fact]
    public void HandsFreeServiceSuppliesBatteryToPhysicalDeviceInSameContainer()
    {
        var container = Guid.NewGuid();
        var service = Properties(container.ToString("B"), (byte)100);
        Assert.True(BluetoothBatteryProvider.IsBluetoothBatterySource(
            @"BTHENUM\{0000111E-0000-1000-8000-00805F9B34FB}_VID&000105D6_PID&000A\service", service));
        var levels = BluetoothBatteryProvider.GetContainerBatteryLevels([service]);
        Assert.Equal(100, BluetoothBatteryProvider.ResolveBatteryLevel(Properties(container), levels));
        Assert.Null(BluetoothBatteryProvider.ResolveBatteryLevel(Properties(Guid.NewGuid()), levels));
    }

    [Fact]
    public void DeviceBatteryTakesPriorityOverServiceFallback()
    {
        var container = Guid.NewGuid();
        var levels = BluetoothBatteryProvider.GetContainerBatteryLevels([Properties(container, 100)]);
        Assert.Equal(80, BluetoothBatteryProvider.ResolveBatteryLevel(Properties(container, 80), levels));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData("invalid")]
    public void InvalidServiceBatteryIsIgnored(object battery)
    {
        var properties = Properties(Guid.NewGuid(), battery);
        Assert.False(BluetoothBatteryProvider.IsBluetoothBatterySource(@"BTHENUM\service", properties));
        Assert.Empty(BluetoothBatteryProvider.GetContainerBatteryLevels([properties]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void BatteryBoundariesAreAccepted(int battery)
    {
        var container = Guid.NewGuid();
        var levels = BluetoothBatteryProvider.GetContainerBatteryLevels([Properties(container, battery)]);
        Assert.Equal(battery, BluetoothBatteryProvider.ResolveBatteryLevel(Properties(container), levels));
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void MissingOrInvalidContainerCannotShareBattery(string container)
    {
        var levels = BluetoothBatteryProvider.GetContainerBatteryLevels([Properties(container, 100)]);
        Assert.Empty(levels);
        Assert.Null(BluetoothBatteryProvider.ResolveBatteryLevel(Properties(container), levels));
        Assert.Equal(75, BluetoothBatteryProvider.ResolveBatteryLevel(Properties(container, 75), levels));
    }

    [Fact]
    public void InactiveHandsFreeProfileStillSuppliesWindowsBatteryReading()
    {
        var container = Guid.NewGuid();
        var service = Properties(container, 100);
        service[Present] = false;
        Assert.True(BluetoothBatteryProvider.IsBluetoothBatterySource(@"BTHENUM\service", service));
        var levels = BluetoothBatteryProvider.GetContainerBatteryLevels([service]);
        Assert.Equal(100, BluetoothBatteryProvider.ResolveBatteryLevel(Properties(container), levels));
    }

    [Fact]
    public void NonBluetoothNodesAreNotBatterySources()
    {
        Assert.False(BluetoothBatteryProvider.IsBluetoothBatterySource(
            @"USB\device", Properties(Guid.NewGuid(), 100)));
    }

    private static Dictionary<string, object> Properties(object container, object? battery = null)
    {
        var properties = new Dictionary<string, object> { [Container] = container };
        if (battery is not null)
            properties[Battery] = battery;
        return properties;
    }
}
