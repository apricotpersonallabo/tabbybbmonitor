using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;

namespace BatteryTray.Tests;

[Collection("UI language")]
public sealed class TrayPopupTests
{
    [Fact]
    public void OpenMenuUpdatesInPlaceAndLateResultsDoNotReopenIt() => RunSta(() =>
    {
        var provider = new ControlledBatteryProvider();
        using var service = new DeviceRefreshService(provider);
        using var menu = new ContextMenuStrip { Opacity = 0 };
        var separator = new ToolStripSeparator();
        var command = new ToolStripMenuItem("Settings");
        menu.Items.Add(separator);
        menu.Items.Add(command);
        void Render(DeviceSnapshot snapshot) =>
            TrayDeviceList.UpdateMenu(menu, separator, TrayDeviceList.Create(snapshot), _ => true);
        service.Changed += Render;
        Render(service.Snapshot);
        menu.Opening += (_, _) => _ = service.RefreshAsync();
        menu.Show(new Point(50, 50));
        Assert.Equal(1, provider.Calls);
        Assert.True(menu.Visible);
        Assert.Same(separator, menu.Items[0]);
        Assert.False(separator.Visible);
        Assert.True(command.Available);
        provider.Complete([new("mouse", "Mouse", 79, true)]);
        PumpUntil(() => !service.Snapshot.IsRefreshing);
        Assert.True(menu.Visible);
        Assert.Equal("Mouse  79%", menu.Items[0].Text);
        Assert.True(separator.Visible);
        Assert.True(((ToolStripMenuItem)menu.Items[0]).Checked);
        Assert.Same(command, menu.Items[^1]);
        var pending = service.RefreshAsync();
        menu.Close();
        provider.Complete([new("mouse", "Mouse", 78, true)]);
        PumpUntil(() => pending.IsCompleted);
        Assert.False(menu.Visible);
        Assert.Equal("Mouse  78%", menu.Items[0].Text);
        Assert.Same(command, menu.Items[^1]);
    });

    [Fact]
    public void TooltipWrapsLongNamesScrollsLargeListsAndNeverShowsOnUpdateAlone() => RunSta(() =>
    {
        using var popup = new TrayDeviceTooltip { Opacity = 0 };
        var anchor = new Point(Screen.PrimaryScreen!.WorkingArea.Right - 2, Screen.PrimaryScreen.WorkingArea.Bottom - 2);
        var lines = Enumerable.Range(0, 100).Select(index => new TrayDeviceLine(index.ToString(),
            $"{new string('x', 180)} {index}  80%")).ToArray();
        popup.SetLines(lines);
        Assert.False(popup.Visible);
        popup.ShowAt(anchor, lines);
        Assert.True(Screen.FromPoint(anchor).WorkingArea.Contains(popup.Bounds),
            $"Popup {popup.Bounds} must fit in {Screen.FromPoint(anchor).WorkingArea}");
        var panel = Assert.IsType<Panel>(Assert.Single(popup.Controls.Cast<Control>()));
        Assert.Equal(100, panel.Controls.Count);
        Assert.True(panel.AutoScrollMinSize.Height > popup.ClientSize.Height);
        Assert.All(panel.Controls.Cast<Label>(), label => Assert.True(label.Height > popup.Font.Height));
        popup.Hide();
        popup.SetLines([new("mouse", "Mouse  79%")]);
        Assert.False(popup.Visible);
        popup.ShowAt(anchor,
        [new("mouse", "Bluetooth mouse  79%"), new("keyboard", "Bluetooth keyboard  60%")]);
        using var bitmap = new Bitmap(popup.Width, popup.Height);
        popup.DrawToBitmap(bitmap, new Rectangle(Point.Empty, popup.Size));
        if (Environment.GetEnvironmentVariable("TABBY_LAYOUT_OUTPUT_DIR") is { } output)
            bitmap.Save(Path.Combine(output, "TabbyBBMonitor-tooltip.png"));
        popup.SetLines([]);
        Assert.False(popup.Visible);
        popup.ShowAt(anchor, []);
        Assert.False(popup.Visible);
    });

    [Fact]
    public void TooltipFollowsThemeChangesAndKeepsColorsAfterContentRefresh() => RunSta(() =>
    {
        var theme = TooltipTheme.Create(false);
        using var popup = new TrayDeviceTooltip(() => theme) { Opacity = 0 };
        TrayDeviceLine[] lines = [new("mouse", "Bluetooth mouse  79%"), new("keyboard", "Bluetooth keyboard  60%")];
        popup.ShowAt(new Point(500, 500), lines);
        AssertColors();
        SavePreview("light");
        theme = TooltipTheme.Create(true);
        // Windowsから届くテーマ変更通知と同じ経路で更新する。
        SendMessage(popup.Handle, 0x001A, IntPtr.Zero, IntPtr.Zero);
        AssertColors();
        Assert.True(popup.Visible);
        SavePreview("dark");
        popup.SetLines([new("mouse", "Bluetooth mouse  78%")]);
        AssertColors();
        theme = TooltipTheme.Create(false, true);
        popup.RefreshTheme();
        AssertColors();
        Assert.Equal(SystemColors.Window, popup.BackColor);
        popup.Hide();
        theme = TooltipTheme.Create(false);
        popup.ShowAt(new Point(500, 500), lines);
        AssertColors();

        void AssertColors()
        {
            Assert.Equal(theme.Background, popup.BackColor);
            Assert.Equal(theme.Foreground, popup.ForeColor);
            var panel = Assert.IsType<Panel>(Assert.Single(popup.Controls.Cast<Control>()));
            Assert.Equal(theme.Background, panel.BackColor);
            Assert.All(panel.Controls.Cast<Label>(), label =>
            {
                Assert.Equal(theme.Background, label.BackColor);
                Assert.Equal(theme.Foreground, label.ForeColor);
            });
        }

        void SavePreview(string name)
        {
            if (Environment.GetEnvironmentVariable("TABBY_LAYOUT_OUTPUT_DIR") is not { } output)
                return;
            using var bitmap = new Bitmap(popup.Width, popup.Height);
            popup.DrawToBitmap(bitmap, new Rectangle(Point.Empty, popup.Size));
            bitmap.Save(Path.Combine(output, $"TabbyBBMonitor-tooltip-{name}.png"));
        }
    });

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

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
}
