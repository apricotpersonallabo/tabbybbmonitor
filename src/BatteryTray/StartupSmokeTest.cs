namespace BatteryTray;

// Runs before any single-instance coordination or application services are created.
internal static class StartupSmokeTest
{
    public const string SuccessMessage = "Startup smoke test passed.";

    public static int Run(Action initialize, TextWriter output, TextWriter error)
    {
        try
        {
            initialize();
            var originalLanguage = I18n.CurrentLanguageSetting;
            try
            {
                foreach (var language in I18n.GetLanguageChoices().Where(choice => choice.Code != "system"))
                {
                    I18n.SetLanguage(language.Code);
                    var settings = new AppSettings();
                    var monitor = new BatteryMonitor(new());
                    var now = DateTimeOffset.Now;
                    using var menu = BatteryNotificationMenu.Create(hours => BatteryNotificationMenu.ApplyPause(settings, monitor, hours, now));
                    foreach (var hours in new[] { 1, 8, 0 })
                    {
                        var item = menu.DropDownItems.Cast<ToolStripMenuItem>().Single(candidate =>
                            (TextId)candidate.Tag! == (hours == 0 ? TextId.ResumeNotifications : hours == 1 ? TextId.PauseOneHour : TextId.PauseEightHours));
                        item.PerformClick();
                        var expected = hours == 0 ? (DateTimeOffset?)null : now.AddHours(hours);
                        if (settings.NotificationsPausedUntil != expected)
                            throw new InvalidOperationException("Notification pause action failed.");
                        BatteryNotificationMenu.Update(menu, settings.NotificationsPausedUntil, now);
                        if (string.IsNullOrWhiteSpace(menu.Text) || menu.DropDownItems.Cast<ToolStripMenuItem>().Any(child => string.IsNullOrWhiteSpace(child.Text)))
                            throw new InvalidOperationException("Notification menu translation is missing.");
                    }
                }
            }
            finally { I18n.SetLanguage(originalLanguage); }
            output.WriteLine(SuccessMessage);
            return 0;
        }
        catch (Exception exception)
        {
            error.WriteLine(exception);
            return 1;
        }
    }
}
