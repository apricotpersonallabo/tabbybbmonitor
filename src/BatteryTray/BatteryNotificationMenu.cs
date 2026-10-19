namespace BatteryTray;

internal static class BatteryNotificationMenu
{
    public static ToolStripMenuItem Create(Action<int> pause)
    {
        var menu = new ToolStripMenuItem { Name = "BatteryNotifications", Tag = TextId.BatteryNotifications };
        foreach (var (name, text, hours) in new[]
        {
            ("PauseOneHour", TextId.PauseOneHour, 1),
            ("PauseEightHours", TextId.PauseEightHours, 8),
            ("ResumeNotifications", TextId.ResumeNotifications, 0)
        })
        {
            var item = new ToolStripMenuItem { Name = name, Tag = text };
            item.Click += (_, _) => pause(hours);
            menu.DropDownItems.Add(item);
        }
        Update(menu, null, DateTimeOffset.Now);
        return menu;
    }

    public static void Update(ToolStripMenuItem menu, DateTimeOffset? pausedUntil, DateTimeOffset now)
    {
        menu.Text = I18n.Get((TextId)menu.Tag!);
        menu.Checked = pausedUntil > now;
        menu.ToolTipText = menu.Checked
            ? I18n.Format(TextId.PausedUntil, pausedUntil!.Value.ToLocalTime().ToString("g"))
            : I18n.Get(TextId.NotificationsActive);
        menu.RightToLeft = I18n.IsRightToLeft ? RightToLeft.Yes : RightToLeft.No;
        foreach (ToolStripMenuItem item in menu.DropDownItems)
        {
            var text = (TextId)item.Tag!;
            item.Text = I18n.Get(text);
            item.Enabled = text != TextId.ResumeNotifications || menu.Checked;
        }
    }

    public static void ApplyPause(AppSettings settings, BatteryMonitor monitor, int hours, DateTimeOffset now)
    {
        settings.NotificationsPausedUntil = hours == 0 ? null : now.AddHours(hours);
        if (hours != 0) monitor.Suppress(settings.Devices.Select(device => device.DeviceId));
    }
}
