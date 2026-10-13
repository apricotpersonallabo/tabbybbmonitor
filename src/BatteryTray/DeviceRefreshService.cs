namespace BatteryTray;

internal sealed record DeviceSnapshot(
    IReadOnlyList<BluetoothBatteryDevice> Devices, bool HasResult, bool IsRefreshing, string? Error);

internal sealed class DeviceRefreshService(IBluetoothBatteryProvider provider) : IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private Task<DeviceSnapshot>? _pending;
    private bool _disposed;

    public DeviceSnapshot Snapshot { get; private set; } = new([], false, false, null);
    public event Action<DeviceSnapshot>? Changed;

    // 呼び出し元はUIスレッド。開始通知より先に共有タスクを設定し、再入にも対応する。
    public Task<DeviceSnapshot> RefreshAsync()
    {
        if (_pending is not null)
            return _pending;
        if (_disposed)
            return Task.FromResult(Snapshot);
        var completion = new TaskCompletionSource<DeviceSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = completion.Task;
        _ = FetchAsync(completion);
        return completion.Task;
    }

    private async Task FetchAsync(TaskCompletionSource<DeviceSnapshot> completion)
    {
        try
        {
            Snapshot = Snapshot with { IsRefreshing = true, Error = null };
            Changed?.Invoke(Snapshot);
            var devices = await provider.GetDevicesAsync(_cancellation.Token);
            if (!_disposed)
                Snapshot = new(devices, true, false, null);
        }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception exception)
        {
            if (!_disposed)
                Snapshot = Snapshot with { IsRefreshing = false, Error = exception.Message };
        }
        finally
        {
            Snapshot = Snapshot with { IsRefreshing = false };
            var completedSnapshot = Snapshot;
            _pending = null;
            try
            {
                if (!_disposed)
                    Changed?.Invoke(completedSnapshot);
            }
            finally { completion.TrySetResult(completedSnapshot); }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _cancellation.Cancel();
        _cancellation.Dispose();
        Changed = null;
    }
}
