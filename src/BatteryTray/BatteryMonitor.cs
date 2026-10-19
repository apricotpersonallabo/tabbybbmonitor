using System.Text.Json;

namespace BatteryTray;

// Kept separate from editable settings: saving/canceling a dialog must not restore old observations.
internal sealed class BatteryMonitorState
{
    public Dictionary<string, DeviceMonitorState> Devices { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class DeviceMonitorState
{
    public int? LastBatteryLevel { get; set; }
    public DateTimeOffset? LastObservedAt { get; set; }
    public DateTimeOffset? LastNotificationAt { get; set; }
    public BatteryBand LastNotificationBand { get; set; } = BatteryBand.Unknown;
    public DateTimeOffset? LastLowNotificationAt { get; set; }
    public DateTimeOffset? LastCriticalNotificationAt { get; set; }
}

internal sealed class BatteryMonitorStore(string? path = null)
{
    private readonly string _path = path ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BatteryTray", "monitor-state.json");

    public BatteryMonitorState Load()
    {
        try
        {
            var state = JsonSerializer.Deserialize<BatteryMonitorState>(File.ReadAllText(_path));
            if (state?.Devices is null) return new();
            state.Devices = new(state.Devices.Where(pair => pair.Value is not null), StringComparer.OrdinalIgnoreCase);
            foreach (var item in state.Devices.Values)
            {
                if (item.LastBatteryLevel is not (>= 0 and <= 100) || item.LastObservedAt is null)
                {
                    item.LastBatteryLevel = null;
                    item.LastObservedAt = null;
                }
            }
            return state;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        { return new(); }
    }

    public bool Save(BatteryMonitorState state)
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(state));
            File.Move(_path + ".tmp", _path, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}

internal sealed record BatteryNotice(BluetoothBatteryDevice Device, BatteryBand Band, BatteryBand? PreviousBand);

internal sealed class BatteryMonitor(BatteryMonitorState state)
{
    private sealed record Previous(int? Level, BatteryBand Band, bool Allowed);
    private readonly Dictionary<string, Previous> _previous = new(StringComparer.OrdinalIgnoreCase);
    public BatteryMonitorState State { get; } = state;

    public static bool Allowed(AppSettings settings, DeviceSettings device, DateTimeOffset now) =>
        settings.NotificationsEnabled && device.IsEnabled && device.NotificationMode != DeviceNotificationMode.Off &&
        !(settings.NotificationsPausedUntil > now) && !(device.NotificationsPausedUntil > now);

    // Reclassify known readings after edits without inventing a new connection or threshold crossing.
    public void Reconfigure(AppSettings settings)
    {
        foreach (var device in settings.Devices)
            if (_previous.TryGetValue(device.DeviceId, out var previous))
                _previous[device.DeviceId] = previous with { Band = device.Classify(previous.Level) };
    }

    public void Suppress(IEnumerable<string> ids)
    {
        foreach (var id in ids)
            if (_previous.TryGetValue(id, out var previous))
                _previous[id] = previous with { Allowed = false };
    }

    public IReadOnlyList<BatteryNotice> Evaluate(AppSettings settings,
        IReadOnlyList<BluetoothBatteryDevice> devices, DateTimeOffset now)
    {
        var notices = new List<BatteryNotice>();
        var available = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var device in devices)
        {
            var configured = settings.Devices.FirstOrDefault(item =>
                string.Equals(item.DeviceId, device.Id, StringComparison.OrdinalIgnoreCase));
            if (configured is null) continue;
            var valid = device.IsConnected && device.BatteryLevel is >= 0 and <= 100;
            var level = valid ? device.BatteryLevel : null;
            var band = configured.Classify(level);
            var allowed = Allowed(settings, configured, now);
            _previous.TryGetValue(device.Id, out var previous);
            available.Add(device.Id);
            _previous[device.Id] = new(level, band, allowed);
            if (!valid) continue;

            if (!State.Devices.TryGetValue(device.Id, out var saved))
                State.Devices[device.Id] = saved = new();
            saved.LastBatteryLevel = level;
            saved.LastObservedAt = device.ObservedAt;
            if (!allowed) continue;
            if (configured.NotificationMode == DeviceNotificationMode.CriticalOnly && band != BatteryBand.Critical)
                continue;

            var entry = previous is null || previous.Band == BatteryBand.Unknown || !previous.Allowed;
            var transition = !entry && previous!.Band != band;
            if (entry)
            {
                if (band is not (BatteryBand.Low or BatteryBand.Critical)) continue;
                var lastInBand = band == BatteryBand.Critical ? saved.LastCriticalNotificationAt : saved.LastLowNotificationAt;
                lastInBand ??= saved.LastNotificationBand == band ? saved.LastNotificationAt : null;
                var escalated = band == BatteryBand.Critical && saved.LastNotificationBand > BatteryBand.Critical;
                if (!escalated && lastInBand is { } last && now - last < TimeSpan.FromHours(1)) continue;
            }
            else if (!transition) continue;

            notices.Add(new(device, band, entry ? null : previous!.Band));
            saved.LastNotificationAt = now;
            saved.LastNotificationBand = band;
            if (band == BatteryBand.Low) saved.LastLowNotificationAt = now;
            if (band == BatteryBand.Critical) saved.LastCriticalNotificationAt = now;
        }
        foreach (var id in _previous.Keys.Where(id => !available.Contains(id)).ToArray())
            _previous.Remove(id);
        return notices;
    }
}

internal static class BatteryObservationText
{
    public static string Previous(DeviceMonitorState? state) =>
        state is { LastBatteryLevel: int level, LastObservedAt: { } at }
            ? I18n.Format(TextId.PreviousBattery, level, at.ToLocalTime().ToString("g")) : string.Empty;

    public static string CheckedAt(DateTimeOffset at) =>
        I18n.Format(TextId.LastChecked, at.ToLocalTime().ToString("g"));
}
