namespace BatteryTray;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = @"Local\BatteryTray.SingleInstance";
    private const string ActivationEventName = @"Local\BatteryTray.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activationEvent;
    private bool _ownsMutex;

    public SingleInstanceCoordinator()
    {
        // AutoResetEventは待機開始前にSetされてもシグナルを保持するため、
        // 一次プロセスの初期化中に二次起動されても通知を失わない。
        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);
        _mutex = new Mutex(true, MutexName, out var createdNew);
        IsPrimary = createdNew;
        _ownsMutex = createdNew;
    }

    public bool IsPrimary { get; }

    public void RequestActivation() => _activationEvent.Set();

    public RegisteredWaitHandle RegisterActivation(Action callback) =>
        ThreadPool.RegisterWaitForSingleObject(
            _activationEvent,
            static (state, _) => ((Action)state!).Invoke(),
            callback,
            Timeout.Infinite,
            executeOnlyOnce: false);

    public void Dispose()
    {
        if (_ownsMutex)
        {
            _mutex.ReleaseMutex();
            _ownsMutex = false;
        }

        _mutex.Dispose();
        _activationEvent.Dispose();
    }
}
