using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;

namespace BatteryTray.Tests;

[Collection("UI language")]
public sealed class SettingsDeviceVisibilityTests
{
    [Fact]
    public void CheckboxControlsDisconnectedAndMissingDevicesAndCombinesWithSearch() => RunSta(() =>
    {
        var settings = new AppSettings();
        settings.Devices.Add(settings.CreateDeviceSettings("missing", "Missing mouse"));
        using var form = new SettingsForm(settings,
        [
            new("connected", "Connected mouse", 80, true),
            new("unknown", "Connected keyboard", null, true),
            new("disconnected", "Disconnected mouse", 90, false)
        ], new UnsupportedStartup());
        var grid = Field<DataGridView>(form, "_grid");
        var checkbox = Field<CheckBox>(form, "_showUnavailable");
        var search = Field<TextBox>(form, "_searchBox");
        Assert.False(checkbox.Checked);
        Assert.Equal(2, grid.Rows.Count);
        Assert.All(Rows(grid), row => Assert.StartsWith("Connected", Assert.IsType<string>(row.Cells["Name"].Value)));

        checkbox.Checked = true;
        Assert.Equal(4, grid.Rows.Count);
        var disconnected = Rows(grid).Single(row => ((DeviceSettings)row.Tag!).DeviceId == "disconnected");
        Assert.Equal("—", disconnected.Cells["Battery"].Value);
        Assert.Equal(I18n.Get(TextId.Unavailable), disconnected.Cells["State"].Value);
        grid.ClearSelection();
        disconnected.Selected = true;
        Assert.Equal(I18n.Get(TextId.CurrentlyUnavailable), Field<Label>(form, "_detailStatus").Text);

        search.Text = "mouse";
        Assert.Equal(3, grid.Rows.Count);
        checkbox.Checked = false;
        Assert.Single(Rows(grid));
        Assert.Equal("connected", ((DeviceSettings)grid.Rows[0].Tag!).DeviceId);
        Assert.Equal(4, Field<List<DeviceSettings>>(form, "_devices").Count);
        Assert.Single(settings.Devices);

        // 再検出で接続状態が逆転しても、保存済みの設定は残る。
        var observations = Field<Dictionary<string, BluetoothBatteryDevice>>(form, "_observed");
        observations.Clear();
        BluetoothBatteryDevice[] rediscovered =
        [
            new("connected", "Connected mouse", 80, false),
            new("disconnected", "Disconnected mouse", 90, true)
        ];
        typeof(SettingsForm).GetMethod("MergeObserved", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, [settings, rediscovered]);
        typeof(SettingsForm).GetMethod("RefreshGrid", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, [null]);
        Assert.Single(Rows(grid));
        Assert.Equal("disconnected", ((DeviceSettings)grid.Rows[0].Tag!).DeviceId);
        Assert.Equal(4, Field<List<DeviceSettings>>(form, "_devices").Count);
    });

    [Fact]
    public void NoConnectedDevicesProducesEmptyDefaultList() => RunSta(() =>
    {
        using var form = new SettingsForm(new AppSettings(),
            [new("off", "Powered off", 50, false)], new UnsupportedStartup());
        Assert.Empty(Rows(Field<DataGridView>(form, "_grid")));
        Field<CheckBox>(form, "_showUnavailable").Checked = true;
        Assert.Single(Rows(Field<DataGridView>(form, "_grid")));
    });

    [Fact]
    public void AddressColumnShowsObservedAndSavedValuesRegardlessOfConnection() => RunSta(() =>
    {
        var settings = new AppSettings();
        foreach (var id in new[] { "connected", "disconnected", "missing" })
        {
            var device = settings.CreateDeviceSettings(id, id);
            device.UpdateBluetoothAddress("112233445566");
            settings.Devices.Add(device);
        }
        using var form = new SettingsForm(settings,
        [
            new("connected", "connected", 80, true) { BluetoothAddress = "aabbccddeeff" },
            new("disconnected", "disconnected", 80, false) { BluetoothAddress = "223344556677" },
            new("unknown", "unknown", null, true)
        ], new UnsupportedStartup());
        Field<CheckBox>(form, "_showUnavailable").Checked = true;
        var grid = Field<DataGridView>(form, "_grid");
        Assert.Equal(new[] { "Enabled", "Name", "BluetoothAddress", "Battery", "State" },
            grid.Columns.Cast<DataGridViewColumn>().Select(column => column.Name));
        Assert.True(grid.Columns["BluetoothAddress"]!.ReadOnly);
        Assert.Equal("AA:BB:CC:DD:EE:FF", Address("connected"));
        Assert.Equal("22:33:44:55:66:77", Address("disconnected"));
        Assert.Equal("11:22:33:44:55:66", Address("missing"));
        Assert.Equal("—", Address("unknown"));
        var configured = Field<List<DeviceSettings>>(form, "_devices");
        Assert.Equal("AA:BB:CC:DD:EE:FF", configured.Single(device => device.DeviceId == "connected").BluetoothAddress);
        Assert.Equal("11:22:33:44:55:66", settings.Devices[0].BluetoothAddress);

        var observations = Field<Dictionary<string, BluetoothBatteryDevice>>(form, "_observed");
        observations.Clear();
        BluetoothBatteryDevice[] rediscovered = [new("connected", "connected", 80, true)];
        typeof(SettingsForm).GetMethod("MergeObserved", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, [settings, rediscovered]);
        typeof(SettingsForm).GetMethod("RefreshGrid", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, [null]);
        Assert.Equal("AA:BB:CC:DD:EE:FF", Address("connected"));
        Assert.Equal("22:33:44:55:66:77", Address("disconnected"));

        string? Address(string id) => Rows(grid).Single(row => ((DeviceSettings)row.Tag!).DeviceId == id)
            .Cells["BluetoothAddress"].Value as string;
    });

    [Theory]
    [InlineData(1040, 650)]
    [InlineData(900, 560)]
    public void AddressColumnFitsFullAddressAtSupportedWindowSizes(int width, int height) => RunSta(() =>
    {
        using var service = new DeviceRefreshService(new ControlledBatteryProvider());
        using var form = new SettingsForm(new AppSettings(),
            [new("mouse", "Bluetooth mouse", 80, true) { BluetoothAddress = "AA:BB:CC:DD:EE:FF" }], new UnsupportedStartup(), refreshService: service);
        form.Size = new System.Drawing.Size(width, height);
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new System.Drawing.Point(-32000, -32000);
        form.Opacity = 0;
        form.Show();
        Application.DoEvents();
        using var bitmap = new System.Drawing.Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, form.ClientRectangle);
        var grid = Field<DataGridView>(form, "_grid");
        var column = grid.Columns["BluetoothAddress"]!;
        var requiredWidth = TextRenderer.MeasureText("AA:BB:CC:DD:EE:FF", grid.Font).Width + 12;
        Assert.True(column.Width >= requiredWidth, $"MAC column width {column.Width} < {requiredWidth}");
        Assert.True(grid.Columns.Cast<DataGridViewColumn>().Sum(item => item.Width) <= grid.ClientSize.Width,
            "All columns should fit without horizontal scrolling.");
        if (Environment.GetEnvironmentVariable("TABBY_LAYOUT_OUTPUT_DIR") is { } output)
            bitmap.Save(Path.Combine(output, $"TabbyBBMonitor-layout-{width}.png"));
    });

    [Fact]
    public void SnapshotRefreshPreservesUnsavedSettingsFiltersAndSelection() => RunSta(() =>
    {
        BluetoothBatteryDevice[] devices =
        [new("mouse", "Mouse", 80, true), new("other", "Other", 50, true)];
        var settings = new AppSettings();
        using var service = new DeviceRefreshService(new ControlledBatteryProvider());
        using var form = new SettingsForm(settings, devices, new UnsupportedStartup(), refreshService: service);
        Field<TextBox>(form, "_searchBox").Text = "Mouse";
        Field<CheckBox>(form, "_showUnavailable").Checked = true;
        Field<CheckBox>(form, "_monitorDevice").Checked = false;
        Field<NumericUpDown>(form, "_critical").Value = 10;
        Field<NumericUpDown>(form, "_defaultLow").Value = 40;
        var grid = Field<DataGridView>(form, "_grid");
        form.ApplyRefreshSnapshot(new(devices, true, true, null));
        Assert.Contains(I18n.Get(TextId.Discovering), Field<Label>(form, "_deviceCountLabel").Text);
        form.ApplyRefreshSnapshot(new(
            [new("mouse", "Mouse", 79, true) { BluetoothAddress = "AA:BB:CC:DD:EE:FF" }], true, false, null));
        var row = Assert.Single(Rows(grid));
        var edited = (DeviceSettings)row.Tag!;
        Assert.Equal("mouse", edited.DeviceId);
        Assert.False(edited.IsEnabled);
        Assert.Equal(10, edited.CriticalThreshold);
        Assert.Equal(40, Field<NumericUpDown>(form, "_defaultLow").Value);
        Assert.Equal("Mouse", Field<TextBox>(form, "_searchBox").Text);
        Assert.True(Field<CheckBox>(form, "_showUnavailable").Checked);
        Assert.True(row.Selected);
        Assert.Equal("79%", row.Cells["Battery"].Value);
        Assert.Equal("AA:BB:CC:DD:EE:FF", row.Cells["BluetoothAddress"].Value);
        form.ApplyRefreshSnapshot(new([], true, false, "Offline"));
        Assert.Equal("79%", Assert.Single(Rows(grid)).Cells["Battery"].Value);
        Assert.Contains("Offline", Field<Label>(form, "_deviceCountLabel").Text);
    });

    [Fact]
    public void ShowingAndReopeningSettingsRefreshesAndClosedFormIgnoresCompletion() => RunSta(() =>
    {
        var provider = new ControlledBatteryProvider();
        using var service = new DeviceRefreshService(provider);
        using var form = new SettingsForm(new AppSettings(), [], new UnsupportedStartup(), refreshService: service);
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new System.Drawing.Point(-32000, -32000);
        form.Opacity = 0;
        form.Show();
        PumpUntil(() => provider.Calls == 1);
        Assert.Equal(1, provider.Calls);
        var shared = form.RefreshDevicesAsync();
        Assert.Equal(1, provider.Calls);
        provider.Complete([new("mouse", "Mouse", 80, true)]);
        PumpUntil(() => shared.IsCompleted);
        Assert.Equal("80%", Assert.Single(Rows(Field<DataGridView>(form, "_grid"))).Cells["Battery"].Value);
        var reopened = form.RefreshDevicesAsync();
        Assert.Equal(2, provider.Calls);
        form.Close();
        provider.Complete([new("mouse", "Mouse", 79, true)]);
        PumpUntil(() => reopened.IsCompleted);
        Assert.Null(reopened.Exception);
        Assert.Equal(79, Assert.Single(service.Snapshot.Devices).BatteryLevel);
    });

    [Theory]
    [InlineData(1040, 650)]
    [InlineData(900, 560)]
    public void HelpTabShowsRuntimeVersionAndSupportLink(int width, int height) => RunSta(() =>
    {
        using var service = new DeviceRefreshService(new ControlledBatteryProvider());
        using var form = new SettingsForm(new AppSettings(), [], new UnsupportedStartup(), refreshService: service);
        var page = Assert.IsType<TabPage>(Assert.Single(form.Controls.Find("HelpTab", true)));
        var tabs = Assert.IsType<TabControl>(page.Parent);
        Assert.Equal(new[] { I18n.Get(TextId.DevicesTab), I18n.Get(TextId.GeneralTab), I18n.Get(TextId.HelpTab) },
            tabs.TabPages.Cast<TabPage>().Select(tab => tab.Text));
        tabs.SelectedTab = page;
        form.Size = new System.Drawing.Size(width, height);
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new System.Drawing.Point(-32000, -32000);
        form.Opacity = 0;
        form.Show();
        Application.DoEvents();
        var version = Assert.IsType<Label>(Assert.Single(page.Controls.Find("AppVersion", true)));
        var link = Assert.IsType<LinkLabel>(Assert.Single(page.Controls.Find("SupportLink", true)));
        Assert.Equal(AppInfo.Version, version.Text);
        Assert.Equal("https://apricotpersonallabo.github.io/tabbybbmonitor/", link.Text);
        Assert.Equal(link.Text, Assert.Single(link.Links.Cast<LinkLabel.Link>()).LinkData);
        Assert.True(page.RectangleToScreen(page.ClientRectangle).Contains(link.RectangleToScreen(link.ClientRectangle)),
            "Support URL must fit within the help tab.");
        using var bitmap = new System.Drawing.Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(System.Drawing.Point.Empty, form.Size));
        if (Environment.GetEnvironmentVariable("TABBY_LAYOUT_OUTPUT_DIR") is { } output)
            bitmap.Save(Path.Combine(output, $"TabbyBBMonitor-help-{width}.png"));
    });

    [Theory]
    [InlineData((int)StartupRegistrationState.Enabled, true, true)]
    [InlineData((int)StartupRegistrationState.Disabled, false, true)]
    [InlineData((int)StartupRegistrationState.Unsupported, false, false)]
    [InlineData((int)StartupRegistrationState.DisabledByUser, false, false)]
    [InlineData((int)StartupRegistrationState.PolicyControlled, true, false)]
    public void StartupControlRemainsVisibleForEveryState(int state, bool enabled, bool canChange) => RunSta(() =>
    {
        using var service = new DeviceRefreshService(new ControlledBatteryProvider());
        using var form = new SettingsForm(new AppSettings(), [], new UnsupportedStartup(), refreshService: service);
        var label = Field<Label>(form, "_startAtSignInLabel");
        var tabs = (TabControl)label.Parent!.Parent!.Parent!;
        tabs.SelectedIndex = 1;
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new System.Drawing.Point(-32000, -32000);
        form.Opacity = 0;
        form.Show();
        Application.DoEvents();
        typeof(SettingsForm).GetMethod("ApplyStartupState", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, [new StartupRegistrationResult((StartupRegistrationState)state, enabled)]);
        var checkbox = Field<CheckBox>(form, "_startAtSignIn");
        Assert.True(label.Visible);
        Assert.True(checkbox.Visible);
        Assert.Equal(enabled, checkbox.Checked);
        Assert.Equal(canChange, checkbox.Enabled);
        Assert.Equal(I18n.Get(TextId.Enabled), checkbox.Text);
        Assert.Equal(I18n.Get(TextId.AutoStartup), label.Text);
        if (!canChange)
            Assert.True(Field<Label>(form, "_startupNote").Visible);
    });

    private static void PumpUntil(Func<bool> completed)
    {
        var deadline = Environment.TickCount64 + 5000;
        while (!completed() && Environment.TickCount64 < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(1);
        }
        Assert.True(completed(), "UI operation did not complete.");
    }

    private static IEnumerable<DataGridViewRow> Rows(DataGridView grid) => grid.Rows.Cast<DataGridViewRow>();
    private static T Field<T>(SettingsForm form, string name) =>
        (T)typeof(SettingsForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;

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
        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();
    }

    private sealed class UnsupportedStartup : IStartupRegistration
    {
        public Task<StartupRegistrationResult> GetStateAsync() =>
            Task.FromResult(new StartupRegistrationResult(StartupRegistrationState.Unsupported, false));
        public Task<StartupRegistrationResult> SetEnabledAsync(bool enabled) => GetStateAsync();
    }
}
