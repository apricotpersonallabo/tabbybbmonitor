using Windows.ApplicationModel;
using Windows.Services.Store;

namespace BatteryTray;

internal enum UpdateState { Idle, Checking, Available, Current, Preparing, Installing, Completed, Canceled, Failed, Unsupported }
internal sealed record UpdateSnapshot(UpdateState State = UpdateState.Idle, double Progress = 0, bool CanInstall = false);
internal enum UpdateInstallResult { Completed, Canceled, Failed }

internal interface IStoreUpdateProvider
{
    Task<bool?> CheckAsync(nint owner, CancellationToken cancellationToken);
    Task PrepareForInstallAsync(CancellationToken cancellationToken);
    Task<UpdateInstallResult> InstallAsync(nint owner, IProgress<double> progress, CancellationToken cancellationToken);
}

// Owned by the application context; all calls and notifications run on the UI thread.
internal sealed class StoreUpdateService(IStoreUpdateProvider provider, nint checkOwner = default) : IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private Task<UpdateSnapshot>? _pending;
    private Task<UpdateSnapshot>? _startupCheck;
    private bool _disposed;
    public UpdateSnapshot Snapshot { get; private set; } = new();
    public event Action<UpdateSnapshot>? Changed;

    public Task<UpdateSnapshot> CheckAtStartupAsync(nint owner) => _startupCheck ??= CheckAsync(owner);
    public Task<UpdateSnapshot> CheckAsync(nint owner) => RunAsync(owner, install: false);
    public Task<UpdateSnapshot> InstallAsync(nint owner) => RunAsync(owner, install: true);

    private Task<UpdateSnapshot> RunAsync(nint owner, bool install)
    {
        if (_pending is not null) return _pending;
        if (_disposed || (install && !Snapshot.CanInstall)) return Task.FromResult(Snapshot);
        var completion = new TaskCompletionSource<UpdateSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = completion.Task;
        _ = ExecuteAsync(owner, install, completion);
        return completion.Task;
    }

    private async Task ExecuteAsync(nint owner, bool install, TaskCompletionSource<UpdateSnapshot> completion)
    {
        try
        {
            var cancellationToken = _cancellation.Token;
            Publish(new(install ? UpdateState.Preparing : UpdateState.Checking, CanInstall: install));
            if (install)
            {
                await provider.PrepareForInstallAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                Publish(new(UpdateState.Installing, CanInstall: true));
                var progress = new Progress<double>(value =>
                {
                    if (!_disposed && Snapshot.State == UpdateState.Installing)
                        Publish(Snapshot with { Progress = Math.Clamp(value, 0, 1) });
                });
                var result = await provider.InstallAsync(owner, progress, cancellationToken);
                Publish(new(result switch
                {
                    UpdateInstallResult.Completed => UpdateState.Completed,
                    UpdateInstallResult.Canceled => UpdateState.Canceled,
                    _ => UpdateState.Failed
                }, CanInstall: result != UpdateInstallResult.Completed));
            }
            else
            {
                var available = await provider.CheckAsync(checkOwner != 0 ? checkOwner : owner, cancellationToken);
                Publish(new(available switch { true => UpdateState.Available, false => UpdateState.Current, null => UpdateState.Unsupported },
                    CanInstall: available == true));
            }
        }
        catch (OperationCanceledException) { Publish(new(UpdateState.Canceled, CanInstall: install)); }
        catch (Exception) { Publish(new(UpdateState.Failed, CanInstall: install)); }
        finally
        {
            _pending = null;
            completion.TrySetResult(Snapshot);
        }
    }

    private void Publish(UpdateSnapshot snapshot)
    {
        if (_disposed) return;
        Snapshot = snapshot;
        Changed?.Invoke(snapshot);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Changed = null;
        _cancellation.Cancel();
        _cancellation.Dispose();
    }
}

internal sealed class StoreUpdateProvider : IStoreUpdateProvider
{
    private IReadOnlyList<StorePackageUpdate> _updates = [];

    public Task PrepareForInstallAsync(CancellationToken cancellationToken) =>
        UpdateRestartRegistration.Create().PrepareAsync(cancellationToken);

    private static StoreContext CreateContext(nint owner)
    {
        var context = StoreContext.GetDefault();
        WinRT.Interop.InitializeWithWindow.Initialize(context, owner);
        return context;
    }

    public async Task<bool?> CheckAsync(nint owner, CancellationToken cancellationToken)
    {
        _updates = [];
        try
        {
            if (Package.Current.SignatureKind != PackageSignatureKind.Store) return null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
        _updates = await CreateContext(owner).GetAppAndOptionalStorePackageUpdatesAsync().AsTask(cancellationToken);
        return _updates.Count > 0;
    }

    public async Task<UpdateInstallResult> InstallAsync(nint owner, IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (_updates.Count == 0) return UpdateInstallResult.Failed;
        var operation = CreateContext(owner).RequestDownloadAndInstallStorePackageUpdatesAsync(_updates);
        var result = await operation.AsTask(cancellationToken,
            new Progress<StorePackageUpdateStatus>(status => progress.Report(status.TotalDownloadProgress)));
        return result.OverallState switch
        {
            StorePackageUpdateState.Completed => UpdateInstallResult.Completed,
            StorePackageUpdateState.Canceled => UpdateInstallResult.Canceled,
            _ => UpdateInstallResult.Failed
        };
    }
}
