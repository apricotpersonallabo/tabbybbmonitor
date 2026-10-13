using System.Drawing;

namespace BatteryTray.Tests;

[Collection("UI language")]
public sealed class TrayPresentationTests
{
    [Fact]
    public void SharedListContainsOnlyConnectedDevicesInNameOrderWithBatteryOrUnknown()
    {
        var lines = TrayDeviceList.Create(new(
        [
            new("off", "Off", 20, false),
            new("b", "Beta", null, true),
            new("a", "Alpha", 80, true)
        ], true, false, null));
        Assert.Equal(new[] { "a", "b" }, lines.Select(line => line.DeviceId));
        Assert.Equal("Alpha  80%", lines[0].Text);
        Assert.Equal($"Beta  {I18n.Get(TextId.BatteryUnknown)}", lines[1].Text);
    }

    [Fact]
    public void InitialRefreshAndEmptyResultHaveDistinctMessages()
    {
        Assert.Equal(I18n.Get(TextId.Discovering), Assert.Single(TrayDeviceList.Create(new([], false, true, null))).Text);
        Assert.Equal(I18n.Get(TextId.NoBluetoothDevices), Assert.Single(TrayDeviceList.Create(new([], true, false, null))).Text);
    }

    [Fact]
    public void PreviousDevicesRemainAlongsideBusyOrErrorStatus()
    {
        BluetoothBatteryDevice[] devices = [new("mouse", "Mouse", 50, true)];
        var busy = TrayDeviceList.Create(new(devices, true, true, null));
        Assert.Equal("Mouse  50%", busy[0].Text);
        Assert.Equal(I18n.Get(TextId.Discovering), busy[1].Text);
        var failed = TrayDeviceList.Create(new(devices, true, false, "Offline"));
        Assert.Equal(busy[0], failed[0]);
        Assert.Contains("Offline", failed[1].Text);
    }

    [Fact]
    public void LongNamesAndManyDevicesAreNotTruncated()
    {
        var name = new string('x', 200);
        var devices = Enumerable.Range(0, 100).Select(index => new BluetoothBatteryDevice(index.ToString(), name + index, 50, true)).ToArray();
        var lines = TrayDeviceList.Create(new(devices, true, false, null));
        Assert.Equal(100, lines.Count);
        Assert.All(lines, line => Assert.Contains(name, line.Text));
    }

    [Fact]
    public void HoverShowsOnceAfterDelayAndCanRestartAfterLeaving()
    {
        var session = new TrayHoverSession();
        var point = new Point(50, 50);
        session.Observe(point, 1000);
        Assert.False(session.ShouldShow(point, Rectangle.Empty, 1499, false));
        session.Observe(new Point(51, 51), 1400);
        Assert.True(session.ShouldShow(point, Rectangle.Empty, 1500, false));
        Assert.False(session.ShouldShow(point, Rectangle.Empty, 3000, false));
        Assert.False(session.ShouldShow(new Point(300, 300), Rectangle.Empty, 3001, false));
        session.Observe(point, 4000);
        Assert.True(session.ShouldShow(point, Rectangle.Empty, 4500, false));
    }

    [Fact]
    public void ClickOrOpenMenuDismissesHoverUntilPointerLeaves()
    {
        var session = new TrayHoverSession();
        var point = new Point(50, 50);
        session.Observe(point, 0);
        session.Dismiss();
        Assert.False(session.ShouldShow(point, Rectangle.Empty, 500, false));
        session.ShouldShow(new Point(300, 300), Rectangle.Empty, 1000, false);
        session.Observe(point, 1000);
        Assert.False(session.ShouldShow(point, Rectangle.Empty, 1500, true));
        Assert.False(session.ShouldShow(point, Rectangle.Empty, 2000, false));
    }

    [Fact]
    public void PointerMayMoveFromIconIntoPopup()
    {
        var session = new TrayHoverSession();
        session.Observe(new Point(50, 50), 0, new Rectangle(40, 40, 24, 24));
        Assert.True(session.IsWithin(new Point(100, 25), new Rectangle(40, 0, 200, 35)));
        Assert.False(session.IsWithin(new Point(100, 80), new Rectangle(40, 0, 200, 35)));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1919, 1079)]
    [InlineData(-1600, 500)]
    public void PopupPlacementStaysWithinMonitor(int x, int y)
    {
        var area = x < 0 ? new Rectangle(-1920, 0, 1920, 1040) : new Rectangle(0, 0, 1920, 1040);
        var size = new Size(400, 300);
        var location = TrayDeviceTooltip.ConstrainLocation(new Point(x, y), size, area);
        Assert.True(area.Contains(new Rectangle(location, size)));
    }
}
