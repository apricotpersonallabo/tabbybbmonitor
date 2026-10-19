using System.Runtime.ExceptionServices;
using System.Windows.Forms;

namespace BatteryTray.Tests;

[Collection("UI language")]
public sealed class BatteryNotificationMenuTests
{
    [Fact]
    public void TypedMenuTranslatesWithoutParsingNamesInEveryLanguage() => RunSta(() =>
    {
        var original = I18n.CurrentLanguageSetting;
        try
        {
            using var menu = BatteryNotificationMenu.Create(_ => { });
            var items = menu.DropDownItems.Cast<ToolStripMenuItem>().ToArray();
            Assert.Equal(new[] { TextId.PauseOneHour, TextId.PauseEightHours, TextId.ResumeNotifications },
                items.Select(item => (TextId)item.Tag!));
            foreach (var item in items) item.Name = "not-an-enum-name";
            foreach (var language in I18n.GetLanguageChoices().Where(choice => choice.Code != "system"))
            {
                I18n.SetLanguage(language.Code);
                BatteryNotificationMenu.Update(menu, null, DateTimeOffset.Now);
                Assert.Equal(I18n.Get(TextId.BatteryNotifications), menu.Text);
                Assert.All(items, item => Assert.Equal(I18n.Get((TextId)item.Tag!), item.Text));
                Assert.False(items[2].Enabled);
                Assert.Equal(I18n.IsRightToLeft ? RightToLeft.Yes : RightToLeft.No, menu.RightToLeft);
            }
        }
        finally { I18n.SetLanguage(original); }
    });

    [Fact]
    public void MenuActionsPauseExtendAndResumeWhileMonitoringContinues() => RunSta(() =>
    {
        var now = DateTimeOffset.Parse("2026-10-20T00:00:00Z");
        var settings = new AppSettings { Devices = [new() { DeviceId = "mouse" }] };
        var monitor = new BatteryMonitor(new());
        // Start above the low band so the post-resume alert has no cooldown to bypass.
        monitor.Evaluate(settings, [new("mouse", "Mouse", 90, true)], now);
        using var menu = BatteryNotificationMenu.Create(hours => BatteryNotificationMenu.ApplyPause(settings, monitor, hours, now));
        var items = menu.DropDownItems.Cast<ToolStripMenuItem>().ToArray();
        items[0].PerformClick();
        Assert.Equal(now.AddHours(1), settings.NotificationsPausedUntil);
        Assert.Empty(monitor.Evaluate(settings, [new("mouse", "Mouse", 25, true)], now.AddMinutes(1)));
        Assert.Equal(25, monitor.State.Devices["mouse"].LastBatteryLevel);
        items[1].PerformClick();
        Assert.Equal(now.AddHours(8), settings.NotificationsPausedUntil);
        BatteryNotificationMenu.Update(menu, settings.NotificationsPausedUntil, now);
        Assert.True(menu.Checked);
        Assert.True(items[2].Enabled);
        items[2].PerformClick();
        Assert.Null(settings.NotificationsPausedUntil);
        Assert.Single(monitor.Evaluate(settings, [new("mouse", "Mouse", 25, true)], now.AddMinutes(2)));
        BatteryNotificationMenu.Update(menu, settings.NotificationsPausedUntil, now);
        Assert.False(menu.Checked);
        Assert.False(items[2].Enabled);
        BatteryNotificationMenu.Update(menu, now.AddHours(1), now.AddHours(1));
        Assert.False(menu.Checked);
        Assert.False(items[2].Enabled);
    });

    [Fact]
    public void SmokeTestExercisesAllTranslationsAndReportsSuccess() => RunSta(() =>
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var initialized = false;
        var original = I18n.CurrentLanguageSetting;
        Assert.Equal(0, StartupSmokeTest.Run(() => initialized = true, output, error));
        Assert.True(initialized);
        Assert.Equal(StartupSmokeTest.SuccessMessage, output.ToString().Trim());
        Assert.Equal(string.Empty, error.ToString());
        Assert.Equal(original, I18n.CurrentLanguageSetting);
    });

    [Fact]
    public void SmokeTestReturnsFailureAndExceptionWithoutOpeningErrorDialogs()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(1, StartupSmokeTest.Run(() => throw new InvalidOperationException("initialization failed"), output, error));
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("initialization failed", error.ToString());
        Assert.Contains(nameof(InvalidOperationException), error.ToString());
    }

    [Fact]
    public void Version120StateAndSettingsKeepTheirValuesWithoutMigration()
    {
        var directory = Path.Combine(Path.GetTempPath(), "BatteryTrayCompatibility-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var settingsPath = Path.Combine(directory, "settings.json");
            var statePath = Path.Combine(directory, "monitor-state.json");
            const string settingsJson = """{"NotificationsPausedUntil":"2026-10-21T08:00:00+09:00","Devices":[{"DeviceId":"mouse","NotificationMode":1,"NotificationsPausedUntil":"2026-10-21T09:00:00+09:00"}]}""";
            const string stateJson = """{"Devices":{"mouse":{"LastBatteryLevel":25,"LastObservedAt":"2026-10-20T00:00:00Z","LastNotificationAt":"2026-10-20T00:00:00Z","LastNotificationBand":1,"LastLowNotificationAt":"2026-10-20T00:00:00Z","LastCriticalNotificationAt":null}}}""";
            File.WriteAllText(settingsPath, settingsJson);
            File.WriteAllText(statePath, stateJson);
            var settings = new SettingsStore(settingsPath).Load().Settings;
            var state = new BatteryMonitorStore(statePath).Load();
            Assert.Equal(DateTimeOffset.Parse("2026-10-21T08:00:00+09:00"), settings.NotificationsPausedUntil);
            Assert.Equal(DeviceNotificationMode.CriticalOnly, settings.Devices[0].NotificationMode);
            Assert.Equal(DateTimeOffset.Parse("2026-10-21T09:00:00+09:00"), settings.Devices[0].NotificationsPausedUntil);
            Assert.Equal(25, state.Devices["mouse"].LastBatteryLevel);
            Assert.Equal(BatteryBand.Low, state.Devices["mouse"].LastNotificationBand);
            Assert.Equal(DateTimeOffset.Parse("2026-10-20T00:00:00Z"), state.Devices["mouse"].LastLowNotificationAt);
            RunSta(() =>
            {
                using var output = new StringWriter();
                using var error = new StringWriter();
                Assert.Equal(0, StartupSmokeTest.Run(() => { }, output, error));
            });
            Assert.Equal(settingsJson, File.ReadAllText(settingsPath));
            Assert.Equal(stateJson, File.ReadAllText(statePath));
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { error = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
