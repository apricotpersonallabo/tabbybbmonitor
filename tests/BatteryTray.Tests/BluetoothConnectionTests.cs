namespace BatteryTray.Tests;

public sealed class BluetoothConnectionTests
{
    [Theory]
    [InlineData("System.Devices.Aep.IsPresent")]
    [InlineData("System.Devices.InterfaceEnabled")]
    [InlineData("{83DA6326-97A6-4088-9453-A1923F573B29} 15")]
    public void PresenceDoesNotImplyConnection(string property)
    {
        var properties = new Dictionary<string, object> { [property] = true };
        Assert.False(BluetoothBatteryProvider.ResolveConnection("device", properties, Ids()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExplicitConnectionStateIsAuthoritative(bool connected)
    {
        var properties = new Dictionary<string, object> { ["System.Devices.Aep.IsConnected"] = connected };
        Assert.Equal(connected, BluetoothBatteryProvider.ResolveConnection("device", properties, Ids("device")));
    }

    [Fact]
    public void ConnectionQueryMatchesInterfaceAepAndContainerIds()
    {
        var container = Guid.NewGuid();
        var endpointProperties = new Dictionary<string, object> { ["System.Devices.ContainerId"] = container };
        var connectedIds = BluetoothBatteryProvider.GetIdentityKeys("endpoint", endpointProperties)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.True(BluetoothBatteryProvider.ResolveConnection("ENDPOINT", new Dictionary<string, object>(), connectedIds));
        Assert.True(BluetoothBatteryProvider.ResolveConnection("interface", new Dictionary<string, object>
        {
            ["System.Devices.Aep.AepId"] = "endpoint"
        }, connectedIds));
        Assert.True(BluetoothBatteryProvider.ResolveConnection("node", new Dictionary<string, object>
        {
            ["System.Devices.ContainerId"] = container.ToString("B")
        }, connectedIds));
        Assert.False(BluetoothBatteryProvider.ResolveConnection("unrelated", new Dictionary<string, object>(), connectedIds));
    }

    [Fact]
    public void UnknownOrInvalidConnectionStateIsDisconnectedWithoutMatchingEvidence()
    {
        Assert.False(BluetoothBatteryProvider.ResolveConnection("device", new Dictionary<string, object>(), Ids()));
        Assert.False(BluetoothBatteryProvider.ResolveConnection("device", new Dictionary<string, object>
        {
            ["System.Devices.Aep.IsConnected"] = "invalid"
        }, Ids()));
    }

    [Fact]
    public void EmptyContainerDoesNotConnectUnrelatedDevices()
    {
        var properties = new Dictionary<string, object> { ["System.Devices.ContainerId"] = Guid.Empty };
        var connectedIds = BluetoothBatteryProvider.GetIdentityKeys("connected", properties).ToHashSet();
        Assert.False(BluetoothBatteryProvider.ResolveConnection("other", properties, connectedIds));
    }

    [Fact]
    public void MergingPreservesBatteryInformationAndConfirmedConnection()
    {
        var candidates = new[]
        {
            new BluetoothBatteryDevice("same", "Mouse", 75, false),
            new BluetoothBatteryDevice("same", "Mouse", null, true)
        };
        var merged = BluetoothBatteryProvider.Merge(candidates.GroupBy(device => device.Id).Single());
        Assert.True(merged.IsConnected);
        Assert.Equal(75, merged.BatteryLevel);
    }

    [Fact]
    public void MultipleDisconnectedEnumerationPathsRemainDisconnected()
    {
        var candidates = new[]
        {
            new BluetoothBatteryDevice("same", "Mouse", 75, false),
            new BluetoothBatteryDevice("same", "Mouse", null, false)
        };
        Assert.False(BluetoothBatteryProvider.Merge(candidates.GroupBy(device => device.Id).Single()).IsConnected);
    }

    private static HashSet<string> Ids(params string[] ids) => new(ids, StringComparer.OrdinalIgnoreCase);
}
