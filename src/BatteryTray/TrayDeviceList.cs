namespace BatteryTray;

internal sealed record TrayDeviceLine(string? DeviceId, string Text);

internal static class TrayDeviceList
{
    public static void UpdateMenu(ContextMenuStrip menu, ToolStripSeparator separator,
        IReadOnlyList<TrayDeviceLine> lines, Func<string, bool> isMonitored)
    {
        menu.SuspendLayout();
        try
        {
            while (menu.Items[0] != separator)
            {
                var oldItem = menu.Items[0];
                menu.Items.RemoveAt(0);
                oldItem.Dispose();
            }
            var index = 0;
            foreach (var line in lines)
            {
                menu.Items.Insert(index++, new ToolStripMenuItem(line.Text)
                {
                    Enabled = false, AutoToolTip = false,
                    Checked = line.DeviceId is not null && isMonitored(line.DeviceId)
                });
            }
        }
        finally { menu.ResumeLayout(); }
    }

    public static IReadOnlyList<TrayDeviceLine> Create(DeviceSnapshot snapshot)
    {
        var lines = snapshot.Devices.Where(device => device.IsConnected)
            .OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(device => new TrayDeviceLine(device.Id,
                $"{device.Name}  {(device.BatteryLevel is int level ? $"{level}%" : I18n.Get(TextId.BatteryUnknown))}"))
            .ToList();
        if (lines.Count == 0 && (snapshot.HasResult || !snapshot.IsRefreshing && snapshot.Error is null))
            lines.Add(new(null, I18n.Get(TextId.NoBluetoothDevices)));
        if (snapshot.IsRefreshing)
            lines.Add(new(null, I18n.Get(TextId.Discovering)));
        if (snapshot.Error is not null)
            lines.Add(new(null, $"{I18n.Get(TextId.FetchError)}: {snapshot.Error}"));
        return lines;
    }
}
