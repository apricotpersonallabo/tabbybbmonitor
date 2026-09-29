namespace BatteryTray;

internal enum BatteryBand
{
    Unknown = -1,
    Critical = 0,
    Low = 1,
    Caution = 2,
    Good = 3
}

internal sealed record BluetoothBatteryDevice(string Id, string Name, int? BatteryLevel, bool IsConnected)
{
    public DateTimeOffset ObservedAt { get; init; } = DateTimeOffset.Now;
}

internal sealed class DeviceSettings
{
    public string DeviceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int CriticalThreshold { get; set; } = 15;
    public int LowThreshold { get; set; } = 35;
    public int CautionThreshold { get; set; } = 70;

    public BatteryBand Classify(int? level)
    {
        if (level is null)
            return BatteryBand.Unknown;
        if (level <= CriticalThreshold)
            return BatteryBand.Critical;
        if (level <= LowThreshold)
            return BatteryBand.Low;
        if (level <= CautionThreshold)
            return BatteryBand.Caution;
        return BatteryBand.Good;
    }

    public bool HasValidThresholds() =>
        CriticalThreshold is >= 0 and <= 100 &&
        LowThreshold is >= 0 and <= 100 &&
        CautionThreshold is >= 0 and <= 100 &&
        CriticalThreshold < LowThreshold && LowThreshold < CautionThreshold;

    public DeviceSettings Clone() => (DeviceSettings)MemberwiseClone();
}

internal sealed class AppSettings
{
    // 旧バージョンのJSONにはこのプロパティがないため、既定値はtrueにする。
    // 本当の初回起動だけSettingsStoreがfalseを明示する。
    public bool OnboardingCompleted { get; set; } = true;
    public string UiLanguage { get; set; } = "system";
    public int PollIntervalSeconds { get; set; } = 30;
    public bool NotificationsEnabled { get; set; } = true;
    public int DefaultCriticalThreshold { get; set; } = 15;
    public int DefaultLowThreshold { get; set; } = 35;
    public int DefaultCautionThreshold { get; set; } = 70;
    public List<DeviceSettings> Devices { get; set; } = [];

    public DeviceSettings CreateDeviceSettings(string id, string name) => new()
    {
        DeviceId = id,
        DisplayName = name,
        CriticalThreshold = DefaultCriticalThreshold,
        LowThreshold = DefaultLowThreshold,
        CautionThreshold = DefaultCautionThreshold
    };

    public bool HasValidDefaults() =>
        DefaultCriticalThreshold is >= 0 and <= 100 &&
        DefaultLowThreshold is >= 0 and <= 100 &&
        DefaultCautionThreshold is >= 0 and <= 100 &&
        DefaultCriticalThreshold < DefaultLowThreshold &&
        DefaultLowThreshold < DefaultCautionThreshold;
}
