using System.Text.RegularExpressions;

namespace BatteryTray;

internal static class BluetoothAddress
{
    public static string? Normalize(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;
        var value = address.Trim();
        if (!Regex.IsMatch(value, @"\A(?:[0-9a-fA-F]{12}|(?:[0-9a-fA-F]{2}:){5}[0-9a-fA-F]{2}|(?:[0-9a-fA-F]{2}-){5}[0-9a-fA-F]{2})\z"))
            return null;
        var digits = value.Replace(":", "").Replace("-", "").ToUpperInvariant();
        return string.Join(":", Enumerable.Range(0, 6).Select(index => digits.Substring(index * 2, 2)));
    }

    public static string? FromDeviceId(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        // PnPノードとデバイスインターフェイスのDEV_直後だけを読む。
        var node = Regex.Match(id, @"\A(?:\\\\\?\\)?(?:BTHLE|BTHENUM|BTH)[\\#]DEV_(?<address>[0-9a-f]{12})(?=[\\#]|\z)", RegexOptions.IgnoreCase);
        if (node.Success)
            return Normalize(node.Groups["address"].Value);
        // AEP IDのハイフン左側はローカルアダプター、右側が対象機器。
        var endpoint = Regex.Match(id, @"\ABluetooth(?:LE)?#Bluetooth(?:LE)?(?:[0-9a-f]{12}|(?:[0-9a-f]{2}:){5}[0-9a-f]{2})-(?<address>[0-9a-f]{12}|(?:[0-9a-f]{2}:){5}[0-9a-f]{2})\z", RegexOptions.IgnoreCase);
        return endpoint.Success ? Normalize(endpoint.Groups["address"].Value) : null;
    }
}
