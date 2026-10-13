namespace BatteryTray.Tests;

public sealed class BluetoothAddressTests
{
    [Theory]
    [InlineData("aabbccddeeff")]
    [InlineData("aa:bb:cc:dd:ee:ff")]
    [InlineData("aa-bb-cc-dd-ee-ff")]
    [InlineData(" AA:BB:CC:DD:EE:FF ")]
    public void NormalizesSupportedFormats(string address) =>
        Assert.Equal("AA:BB:CC:DD:EE:FF", BluetoothAddress.Normalize(address));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not an address")]
    [InlineData("aabbccddeeff00")]
    [InlineData("aa:bb-cc:dd:ee:ff")]
    [InlineData("gg:bb:cc:dd:ee:ff")]
    [InlineData("a35996ab-11cf-4935-8b61-a6761081ecdf")]
    public void RejectsInvalidAddresses(string? address) => Assert.Null(BluetoothAddress.Normalize(address));

    [Theory]
    [InlineData(@"BTHLE\DEV_AABBCCDDEEFF\instance")]
    [InlineData(@"BTHENUM\DEV_aabbccddeeff\instance")]
    [InlineData(@"BTH\DEV_AABBCCDDEEFF")]
    [InlineData(@"\\?\BTHLE#DEV_AABBCCDDEEFF#instance#{guid}")]
    [InlineData("BluetoothLE#BluetoothLE11:22:33:44:55:66-aa:bb:cc:dd:ee:ff")]
    [InlineData("Bluetooth#Bluetooth112233445566-AABBCCDDEEFF")]
    public void ExtractsRemoteAddressFromKnownBluetoothIds(string id) =>
        Assert.Equal("AA:BB:CC:DD:EE:FF", BluetoothAddress.FromDeviceId(id));

    [Theory]
    [InlineData("AABBCCDDEEFF")]
    [InlineData("a35996ab-11cf-4935-8b61-a6761081ecdf")]
    [InlineData(@"USB\DEV_AABBCCDDEEFF\instance")]
    [InlineData(@"BTHLE\DEV_AABBCCDDEEFF00\instance")]
    [InlineData(@"other\BTHLE\DEV_AABBCCDDEEFF\instance")]
    public void DoesNotGuessAddressesFromArbitraryIds(string id) => Assert.Null(BluetoothAddress.FromDeviceId(id));

    [Fact]
    public void UsesWindowsPropertyBeforeIdAndFallsBackToAepId()
    {
        var properties = new Dictionary<string, object>
        {
            ["System.Devices.Aep.DeviceAddress"] = "11:22:33:44:55:66",
            ["System.Devices.Aep.AepId"] = "BluetoothLE#BluetoothLE112233445566-AABBCCDDEEFF"
        };
        Assert.Equal("11:22:33:44:55:66", BluetoothBatteryProvider.ResolveBluetoothAddress(@"BTHLE\DEV_AABBCCDDEEFF", properties));
        properties["System.Devices.Aep.DeviceAddress"] = "invalid";
        Assert.Equal("AA:BB:CC:DD:EE:FF", BluetoothBatteryProvider.ResolveBluetoothAddress("interface", properties));
    }

    [Fact]
    public void MergingRetainsAddressFromAnotherEnumerationPath()
    {
        var devices = new[]
        {
            new BluetoothBatteryDevice("same", "Mouse", 75, true),
            new BluetoothBatteryDevice("same", "Mouse", null, false) { BluetoothAddress = "aabbccddeeff" }
        };
        var merged = BluetoothBatteryProvider.Merge(devices.GroupBy(device => device.Id).Single());
        Assert.Equal("AA:BB:CC:DD:EE:FF", merged.BluetoothAddress);
        Assert.Equal(75, merged.BatteryLevel);
        Assert.True(merged.IsConnected);
    }

    [Fact]
    public void MissingOrInvalidObservationRetainsSavedAddress()
    {
        var settings = new DeviceSettings();
        Assert.True(settings.UpdateBluetoothAddress("aabbccddeeff"));
        Assert.False(settings.UpdateBluetoothAddress(null));
        Assert.False(settings.UpdateBluetoothAddress("invalid"));
        Assert.Equal("AA:BB:CC:DD:EE:FF", settings.BluetoothAddress);
        Assert.False(settings.UpdateBluetoothAddress("aa:bb:cc:dd:ee:ff"));
        Assert.True(settings.UpdateBluetoothAddress("11:22:33:44:55:66"));
        Assert.Equal("11:22:33:44:55:66", settings.Clone().BluetoothAddress);
    }
}
