namespace BatteryTray.Tests;

public sealed class BatteryMonitorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-19T10:00:00Z");
    private readonly AppSettings _settings = new() { Devices = [new() { DeviceId = "mouse", DisplayName = "Mouse" }] };
    private readonly BatteryMonitor _monitor = new(new());
    private static BluetoothBatteryDevice Device(int? level, bool connected = true) =>
        new("mouse", "Mouse", level, connected) { ObservedAt = Now };

    [Theory]
    [InlineData(10, 1)]
    [InlineData(25, 1)]
    [InlineData(50, 0)]
    [InlineData(90, 0)]
    [InlineData(null, 0)]
    public void InitialKnownLowBatteryNotifiesOnce(int? level, int count)
    {
        Assert.Equal(count, _monitor.Evaluate(_settings, [Device(level)], Now).Count);
        Assert.Empty(_monitor.Evaluate(_settings, [Device(level)], Now.AddMinutes(2)));
    }

    [Fact]
    public void ReconnectCooldownSurvivesRestartAndCriticalEscalationBypassesIt()
    {
        Assert.Single(_monitor.Evaluate(_settings, [Device(25)], Now));
        _monitor.Evaluate(_settings, [], Now.AddMinutes(1));
        Assert.Empty(_monitor.Evaluate(_settings, [Device(25)], Now.AddMinutes(2)));
        var restarted = new BatteryMonitor(_monitor.State);
        Assert.Empty(restarted.Evaluate(_settings, [Device(25)], Now.AddMinutes(3)));
        restarted.Evaluate(_settings, [Device(null)], Now.AddMinutes(4));
        Assert.Single(restarted.Evaluate(_settings, [Device(10)], Now.AddMinutes(5)));
        restarted.Evaluate(_settings, [Device(10, false)], Now.AddMinutes(6));
        Assert.Empty(restarted.Evaluate(_settings, [Device(10)], Now.AddMinutes(7)));
        restarted.Evaluate(_settings, [], Now.AddMinutes(64));
        Assert.Single(restarted.Evaluate(_settings, [Device(10)], Now.AddMinutes(65)));
    }

    [Fact]
    public void UnknownRecoveryAndNormalTransitionsProduceOneNoticePerDevice()
    {
        _monitor.Evaluate(_settings, [Device(null)], Now);
        Assert.Null(Assert.Single(_monitor.Evaluate(_settings, [Device(25)], Now.AddMinutes(1))).PreviousBand);
        var notice = Assert.Single(_monitor.Evaluate(_settings, [Device(10)], Now.AddMinutes(2)));
        Assert.Equal(BatteryBand.Low, notice.PreviousBand);
        Assert.Equal(BatteryBand.Good, Assert.Single(_monitor.Evaluate(_settings, [Device(90)], Now.AddMinutes(3))).Band);
    }

    [Theory]
    [InlineData(false, true, 0)]
    [InlineData(true, false, 0)]
    [InlineData(true, true, 2)]
    public void DisabledNotificationsStillRecordHistory(bool global, bool enabled, int mode)
    {
        _settings.NotificationsEnabled = global;
        _settings.Devices[0].IsEnabled = enabled;
        _settings.Devices[0].NotificationMode = (DeviceNotificationMode)mode;
        Assert.Empty(_monitor.Evaluate(_settings, [Device(10)], Now));
        Assert.Equal(10, _monitor.State.Devices["mouse"].LastBatteryLevel);
    }

    [Fact]
    public void CriticalOnlyExcludesLowAndRecoveryNotifications()
    {
        _settings.Devices[0].NotificationMode = DeviceNotificationMode.CriticalOnly;
        Assert.Empty(_monitor.Evaluate(_settings, [Device(25)], Now));
        Assert.Single(_monitor.Evaluate(_settings, [Device(10)], Now.AddMinutes(1)));
        Assert.Empty(_monitor.Evaluate(_settings, [Device(90)], Now.AddMinutes(2)));
    }

    [Fact]
    public void CombinedPausesResumeOnlyAfterBothExpire()
    {
        _settings.NotificationsPausedUntil = Now.AddHours(1);
        _settings.Devices[0].NotificationsPausedUntil = Now.AddHours(8);
        Assert.Empty(_monitor.Evaluate(_settings, [Device(10)], Now));
        Assert.Empty(_monitor.Evaluate(_settings, [Device(10)], Now.AddHours(1)));
        Assert.Single(_monitor.Evaluate(_settings, [Device(10)], Now.AddHours(8)));
        Assert.Empty(_monitor.Evaluate(_settings, [Device(10)], Now.AddHours(9)));
    }

    [Fact]
    public void ManualResumeRechecksLowBatteryButRespectsCooldown()
    {
        _monitor.Evaluate(_settings, [Device(25)], Now);
        _monitor.Suppress(["mouse"]);
        Assert.Empty(_monitor.Evaluate(_settings, [Device(25)], Now.AddMinutes(5)));
        _monitor.Suppress(["mouse"]);
        Assert.Single(_monitor.Evaluate(_settings, [Device(25)], Now.AddHours(2)));
    }

    [Fact]
    public void SavingThresholdChangesDoesNotInventAnEntryOrTransition()
    {
        _monitor.Evaluate(_settings, [Device(40)], Now);
        _settings.Devices[0].LowThreshold = 45;
        _monitor.Reconfigure(_settings);
        Assert.Empty(_monitor.Evaluate(_settings, [Device(40)], Now.AddMinutes(1)));
    }

    [Fact]
    public void RecoveryNotificationDoesNotEraseRecentLowBandCooldown()
    {
        Assert.Single(_monitor.Evaluate(_settings, [Device(25)], Now));
        Assert.Single(_monitor.Evaluate(_settings, [Device(90)], Now.AddMinutes(1)));
        _monitor.Evaluate(_settings, [], Now.AddMinutes(2));
        Assert.Empty(_monitor.Evaluate(_settings, [Device(25)], Now.AddMinutes(3)));
        _monitor.Evaluate(_settings, [], Now.AddMinutes(61));
        Assert.Single(_monitor.Evaluate(_settings, [Device(25)], Now.AddMinutes(62)));
    }

    [Fact]
    public void DisconnectedUnknownMissingAndInvalidValuesPreserveLastKnownObservation()
    {
        _monitor.Evaluate(_settings, [Device(65)], Now);
        foreach (var device in new[] { Device(10, false), Device(null), Device(101), Device(-1) })
            Assert.Empty(_monitor.Evaluate(_settings, [device], Now.AddMinutes(1)));
        _monitor.Evaluate(_settings, [], Now.AddMinutes(2));
        var state = _monitor.State.Devices["mouse"];
        Assert.Equal(65, state.LastBatteryLevel);
        Assert.Equal(Now, state.LastObservedAt);
    }

    [Fact]
    public void SeveralDevicesProduceOneBatch()
    {
        _settings.Devices.Add(new() { DeviceId = "keyboard" });
        var batch = _monitor.Evaluate(_settings, [Device(25), new("keyboard", "Keyboard", 10, true)], Now);
        Assert.Equal(2, batch.Count);
        Assert.Equal(2, batch.Select(notice => notice.Device.Id).Distinct().Count());
    }

    [Fact]
    public void RuntimeStoreRoundTripsAndRecoversCorruptState()
    {
        var directory = Path.Combine(Path.GetTempPath(), "BatteryMonitorTests-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "state.json");
            var store = new BatteryMonitorStore(path);
            Assert.Empty(store.Load().Devices);
            _monitor.Evaluate(_settings, [Device(25)], Now);
            Assert.True(store.Save(_monitor.State));
            var loaded = store.Load();
            Assert.Equal(25, loaded.Devices["MOUSE"].LastBatteryLevel);
            Assert.Empty(new BatteryMonitor(loaded).Evaluate(_settings, [Device(25)], Now.AddMinutes(1)));
            File.WriteAllText(path, "invalid");
            Assert.Empty(store.Load().Devices);
            File.WriteAllText(path, "{\"Devices\":null}");
            Assert.Empty(store.Load().Devices);
            // A directory is not a writable state file. Failure must not stop monitoring.
            var blocked = Path.Combine(directory, "blocked");
            Directory.CreateDirectory(blocked);
            Assert.False(new BatteryMonitorStore(blocked).Save(_monitor.State));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void OldSettingsKeepExistingNotificationBehavior()
    {
        var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{\"Devices\":[{\"DeviceId\":\"mouse\"}]}")!;
        Assert.Equal(DeviceNotificationMode.AllTransitions, settings.Devices[0].NotificationMode);
        Assert.Null(settings.NotificationsPausedUntil);
        Assert.Null(settings.Devices[0].NotificationsPausedUntil);
        Assert.Single(_monitor.Evaluate(settings, [Device(10)], Now));
    }
}
