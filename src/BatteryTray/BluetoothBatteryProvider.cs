using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace BatteryTray;

internal interface IBluetoothBatteryProvider
{
    Task<IReadOnlyList<BluetoothBatteryDevice>> GetDevicesAsync(CancellationToken cancellationToken);
}

internal sealed class BluetoothBatteryProvider : IBluetoothBatteryProvider
{
    private const string BluetoothProtocol = "{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}";
    private const string BluetoothBatteryLevelProperty = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
    private const string DeviceIsPresentProperty = "{83DA6326-97A6-4088-9453-A1923F573B29} 15";

    private static readonly string[] RequestedProperties =
    [
        "System.Devices.Aep.IsConnected",
        "System.Devices.Aep.IsPresent",
        "System.Devices.Aep.AepId",
        "System.Devices.Aep.ProtocolId",
        BluetoothBatteryLevelProperty,
        "System.Devices.ContainerId",
        "System.Devices.InterfaceEnabled",
        DeviceIsPresentProperty
    ];

    public async Task<IReadOnlyList<BluetoothBatteryDevice>> GetDevicesAsync(CancellationToken cancellationToken)
    {
        // AEPだけでは、WindowsがBTHLEデバイスインターフェイスとして公開する
        // キーボードやマウスが返らない環境があるため、両方の経路を統合する。
        var queries = new Func<Task<IReadOnlyList<DeviceInformation>>>[]
        {
            () => FindInterfacesAsync(BluetoothLEDevice.GetDeviceSelector()),
            () => FindInterfacesAsync(BluetoothDevice.GetDeviceSelector()),
            FindBluetoothDeviceNodesAsync
        };
        var outcomes = await Task.WhenAll(queries.Select(RunQueryAsync));
        cancellationToken.ThrowIfCancellationRequested();

        var results = outcomes.SelectMany(outcome => outcome.Devices).ToArray();
        var errors = outcomes.Where(outcome => outcome.Error is not null).Select(outcome => outcome.Error!).ToArray();
        if (results.Length == 0 && errors.Length == queries.Length)
            throw new AggregateException(I18n.Get(TextId.BluetoothEnumerationFailed), errors);

        return results
            .Where(device => !string.IsNullOrWhiteSpace(device.Name))
            .Select(Map)
            .GroupBy(device => device.Id, StringComparer.OrdinalIgnoreCase)
            .Select(Merge)
            .OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static async Task<IReadOnlyList<DeviceInformation>> FindInterfacesAsync(string selector) =>
        await DeviceInformation.FindAllAsync(selector, RequestedProperties);

    private static async Task<IReadOnlyList<DeviceInformation>> FindBluetoothDeviceNodesAsync()
    {
        var found = await DeviceInformation.FindAllAsync(
            $"System.Devices.ClassGuid:=\"{BluetoothProtocol}\"",
            RequestedProperties,
            DeviceInformationKind.Device);

        return found
            .Where(device => IsPhysicalBluetoothDeviceId(device.Id))
            .Where(device => ReadBoolean(device.Properties, DeviceIsPresentProperty) != false)
            .ToArray();
    }

    private static bool IsPhysicalBluetoothDeviceId(string id) =>
        id.StartsWith("BTHLE\\DEV_", StringComparison.OrdinalIgnoreCase) ||
        id.StartsWith("BTHENUM\\DEV_", StringComparison.OrdinalIgnoreCase) ||
        id.StartsWith("BTH\\DEV_", StringComparison.OrdinalIgnoreCase);

    private static async Task<(IReadOnlyList<DeviceInformation> Devices, Exception? Error)> RunQueryAsync(
        Func<Task<IReadOnlyList<DeviceInformation>>> query)
    {
        try
        {
            return (await query(), null);
        }
        catch (Exception exception)
        {
            return (Array.Empty<DeviceInformation>(), exception);
        }
    }

    private static BluetoothBatteryDevice Map(DeviceInformation device)
    {
        var level = ReadPercentage(device.Properties, BluetoothBatteryLevelProperty);
        var isPhysicalDeviceNode = device.Kind == DeviceInformationKind.Device &&
                                   IsPhysicalBluetoothDeviceId(device.Id);
        var connected = isPhysicalDeviceNode
            ? ReadBoolean(device.Properties, DeviceIsPresentProperty) ?? true
            : ReadBoolean(device.Properties, "System.Devices.Aep.IsConnected")
              ?? ReadBoolean(device.Properties, "System.Devices.InterfaceEnabled")
              ?? ReadBoolean(device.Properties, "System.Devices.Aep.IsPresent")
              ?? false;
        // AEP IDはデバイスインターフェイスとPnPノードの両方に現れるため、
        // ContainerIdより優先すると異なる列挙経路の同一機器を統合できる。
        var stableId = ReadString(device.Properties, "System.Devices.Aep.AepId")
                       ?? ReadString(device.Properties, "System.Devices.ContainerId")
                       ?? device.Id;

        return new BluetoothBatteryDevice(stableId, device.Name.Trim(), level, connected);
    }

    private static BluetoothBatteryDevice Merge(IGrouping<string, BluetoothBatteryDevice> group)
    {
        var candidates = group.ToArray();
        var preferred = candidates
            .OrderByDescending(device => device.BatteryLevel.HasValue)
            .ThenByDescending(device => device.IsConnected)
            .First();

        return preferred with
        {
            BatteryLevel = candidates.Select(device => device.BatteryLevel).FirstOrDefault(level => level.HasValue),
            IsConnected = candidates.Any(device => device.IsConnected),
            ObservedAt = candidates.Max(device => device.ObservedAt)
        };
    }

    private static string? ReadString(IReadOnlyDictionary<string, object> properties, string key)
    {
        if (!properties.TryGetValue(key, out var value) || value is null)
            return null;

        var text = value.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static int? ReadPercentage(IReadOnlyDictionary<string, object> properties, string key)
    {
        if (!properties.TryGetValue(key, out var value) || value is null)
            return null;

        try
        {
            var result = Convert.ToInt32(value);
            return result is >= 0 and <= 100 ? result : null;
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            return null;
        }
    }

    private static bool? ReadBoolean(IReadOnlyDictionary<string, object> properties, string key)
    {
        if (!properties.TryGetValue(key, out var value) || value is null)
            return null;

        try
        {
            return Convert.ToBoolean(value);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException)
        {
            return null;
        }
    }
}
