namespace BatteryTray.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "BatteryTray.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void MissingSettingsStartsOnboarding()
    {
        var result = CreateStore().Load();

        Assert.Equal(SettingsLoadStatus.Missing, result.Status);
        Assert.False(result.Settings.OnboardingCompleted);
    }

    [Fact]
    public void LegacySettingsAreTreatedAsOnboarded()
    {
        var path = SettingsPath();
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "{\"PollIntervalSeconds\":45}");

        var result = new SettingsStore(path).Load();

        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        Assert.True(result.Settings.OnboardingCompleted);
        Assert.Equal(45, result.Settings.PollIntervalSeconds);
    }

    [Fact]
    public void IncompleteOnboardingRemainsIncomplete()
    {
        var path = SettingsPath();
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "{\"OnboardingCompleted\":false}");

        var result = new SettingsStore(path).Load();

        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        Assert.False(result.Settings.OnboardingCompleted);
    }

    [Fact]
    public void InvalidJsonIsBackedUpWithoutRestartingOnboarding()
    {
        var path = SettingsPath();
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "{not-json");

        var result = new SettingsStore(path).Load();

        Assert.Equal(SettingsLoadStatus.RecoveredInvalidJson, result.Status);
        Assert.True(result.Settings.OnboardingCompleted);
        Assert.False(File.Exists(path));
        Assert.NotNull(result.Detail);
        Assert.True(File.Exists(result.Detail));
    }

    [Fact]
    public void LockedSettingsReturnIoErrorWithoutRestartingOnboarding()
    {
        var path = SettingsPath();
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "{}");
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var result = new SettingsStore(path).Load();

        Assert.Equal(SettingsLoadStatus.IoError, result.Status);
        Assert.True(result.Settings.OnboardingCompleted);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private SettingsStore CreateStore() => new(SettingsPath());

    [Fact]
    public void BluetoothAddressSurvivesSaveAndReload()
    {
        var settings = new AppSettings();
        var device = settings.CreateDeviceSettings("mouse", "Mouse");
        device.UpdateBluetoothAddress("aabbccddeeff");
        settings.Devices.Add(device);
        var store = CreateStore();
        store.Save(settings);
        Assert.Equal("AA:BB:CC:DD:EE:FF", Assert.Single(store.Load().Settings.Devices).BluetoothAddress);
    }

    [Fact]
    public void LegacyDeviceWithoutAddressStillLoads()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath(), """{"Devices":[{"DeviceId":"mouse","DisplayName":"Mouse","LowThreshold":40}]}""");
        var result = CreateStore().Load();
        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        var device = Assert.Single(result.Settings.Devices);
        Assert.Null(device.BluetoothAddress);
        Assert.Equal(40, device.LowThreshold);
    }

    private string SettingsPath() => Path.Combine(_directory, "settings.json");
}
