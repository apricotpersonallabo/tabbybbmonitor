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
        "System.Devices.Aep.DeviceAddress",
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
        // インターフェイスやPnPノードに接続プロパティがない場合は、
        // 接続中の機器を列挙した結果とID／ContainerIdで照合する。
        var connectedQueries = new Func<Task<IReadOnlyList<DeviceInformation>>>[]
        {
            () => FindInterfacesAsync(BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected)),
            () => FindInterfacesAsync(BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected))
        };
        var discoveryTask = Task.WhenAll(queries.Select(RunQueryAsync));
        var connectionTask = Task.WhenAll(connectedQueries.Select(RunQueryAsync));
        await Task.WhenAll(discoveryTask, connectionTask);
        var outcomes = await discoveryTask;
        var connectedOutcomes = await connectionTask;
        cancellationToken.ThrowIfCancellationRequested();

        var connectedIds = connectedOutcomes
            .SelectMany(outcome => outcome.Devices)
            .SelectMany(device => GetIdentityKeys(device.Id, device.Properties))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var results = outcomes.SelectMany(outcome => outcome.Devices).ToArray();
        var errors = outcomes.Where(outcome => outcome.Error is not null).Select(outcome => outcome.Error!).ToArray();
        if (results.Length == 0 && errors.Length == queries.Length)
            throw new AggregateException(I18n.Get(TextId.BluetoothEnumerationFailed), errors);

        return results
            .Where(device => !string.IsNullOrWhiteSpace(device.Name))
            .Select(device => Map(device, connectedIds))
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

    private static BluetoothBatteryDevice Map(DeviceInformation device, IReadOnlySet<string> connectedIds)
    {
        var level = ReadPercentage(device.Properties, BluetoothBatteryLevelProperty);
        var connected = ResolveConnection(device.Id, device.Properties, connectedIds);
        // AEP IDはデバイスインターフェイスとPnPノードの両方に現れるため、
        // ContainerIdより優先すると異なる列挙経路の同一機器を統合できる。
        var stableId = ReadString(device.Properties, "System.Devices.Aep.AepId")
                       ?? ReadString(device.Properties, "System.Devices.ContainerId")
                       ?? device.Id;

        return new BluetoothBatteryDevice(stableId, device.Name.Trim(), level, connected)
        {
            BluetoothAddress = ResolveBluetoothAddress(device.Id, device.Properties)
        };
    }

    internal static string? ResolveBluetoothAddress(string id, IReadOnlyDictionary<string, object> properties) =>
        BluetoothAddress.Normalize(ReadString(properties, "System.Devices.Aep.DeviceAddress"))
        ?? BluetoothAddress.FromDeviceId(id)
        ?? BluetoothAddress.FromDeviceId(ReadString(properties, "System.Devices.Aep.AepId"));

    internal static bool ResolveConnection(
        string id, IReadOnlyDictionary<string, object> properties, IReadOnlySet<string> connectedIds) =>
        // IsPresentやInterfaceEnabledは接続状態を示さない。
        ReadBoolean(properties, "System.Devices.Aep.IsConnected")
        ?? GetIdentityKeys(id, properties).Any(connectedIds.Contains);

    internal static IEnumerable<string> GetIdentityKeys(string id, IReadOnlyDictionary<string, object> properties)
    {
        yield return id;
        if (ReadString(properties, "System.Devices.Aep.AepId") is { } aepId)
            yield return aepId;
        if (ReadString(properties, "System.Devices.ContainerId") is { } containerId &&
            Guid.TryParse(containerId, out var container) && container != Guid.Empty)
            yield return container.ToString("D");
    }

    internal static BluetoothBatteryDevice Merge(IGrouping<string, BluetoothBatteryDevice> group)
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
            BluetoothAddress = candidates.OrderByDescending(device => device.IsConnected)
                .Select(device => BluetoothAddress.Normalize(device.BluetoothAddress))
                .FirstOrDefault(address => address is not null),
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
