namespace BatteryTray.Tests;

public sealed class DeviceRefreshServiceTests
{
    [Fact]
    public async Task ConcurrentRequestsShareFetchAndNextRequestFetchesAgain()
    {
        var provider = new ControlledBatteryProvider();
        using var service = new DeviceRefreshService(provider);
        var first = service.RefreshAsync();
        var second = service.RefreshAsync();
        Assert.Same(first, second);
        Assert.Equal(1, provider.Calls);
        Assert.True(service.Snapshot.IsRefreshing);
        Assert.False(service.Snapshot.HasResult);
        provider.Complete([new("mouse", "Mouse", 80, true)]);
        Assert.Equal(80, Assert.Single((await first).Devices).BatteryLevel);
        var next = service.RefreshAsync();
        Assert.Equal(2, provider.Calls);
        provider.Complete([new("mouse", "Mouse", 79, true)]);
        Assert.Equal(79, Assert.Single((await next).Devices).BatteryLevel);
    }

    [Fact]
    public async Task FailedFetchRetainsLastGoodSnapshotAndCanRecover()
    {
        var provider = new ControlledBatteryProvider();
        using var service = new DeviceRefreshService(provider);
        var first = service.RefreshAsync();
        provider.Complete([new("mouse", "Mouse", 80, true)]);
        await first;
        var failure = service.RefreshAsync();
        provider.Fail(new InvalidOperationException("Offline"));
        var snapshot = await failure;
        Assert.Equal("Offline", snapshot.Error);
        Assert.True(snapshot.HasResult);
        Assert.False(snapshot.IsRefreshing);
        Assert.Equal(80, Assert.Single(snapshot.Devices).BatteryLevel);
        var recovery = service.RefreshAsync();
        provider.Complete([]);
        Assert.Null((await recovery).Error);
        Assert.Empty(service.Snapshot.Devices);
    }

    [Fact]
    public async Task ShutdownCancelsFetchAndSuppressesCompletionNotifications()
    {
        var provider = new ControlledBatteryProvider();
        var service = new DeviceRefreshService(provider);
        var notifications = 0;
        service.Changed += _ => notifications++;
        var pending = service.RefreshAsync();
        service.Dispose();
        Assert.True(provider.Token.IsCancellationRequested);
        provider.Complete([new("late", "Late result", 50, true)]);
        await pending;
        Assert.Equal(1, notifications);
        Assert.Empty(service.Snapshot.Devices);
        await service.RefreshAsync();
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task ReentrantRequestDuringStartNotificationSharesFetch()
    {
        var provider = new ControlledBatteryProvider();
        using var service = new DeviceRefreshService(provider);
        Task<DeviceSnapshot>? reentrant = null;
        service.Changed += snapshot => { if (snapshot.IsRefreshing) reentrant = service.RefreshAsync(); };
        var pending = service.RefreshAsync();
        Assert.Same(pending, reentrant);
        provider.Complete([]);
        await pending;
        Assert.Equal(1, provider.Calls);
    }
}

internal sealed class ControlledBatteryProvider : IBluetoothBatteryProvider
{
    private TaskCompletionSource<IReadOnlyList<BluetoothBatteryDevice>> _completion = new();
    public int Calls { get; private set; }
    public CancellationToken Token { get; private set; }
    public Task<IReadOnlyList<BluetoothBatteryDevice>> GetDevicesAsync(CancellationToken cancellationToken)
    {
        Calls++;
        Token = cancellationToken;
        _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        return _completion.Task;
    }
    public void Complete(IReadOnlyList<BluetoothBatteryDevice> devices) => _completion.SetResult(devices);
    public void Fail(Exception exception) => _completion.SetException(exception);
}
