namespace BatteryTray.Tests;

public sealed class StoreUpdateServiceTests
{
    [Theory]
    [InlineData(true, UpdateState.Available)]
    [InlineData(false, UpdateState.Current)]
    [InlineData(null, UpdateState.Unsupported)]
    public async Task CheckMapsStoreResult(bool? available, object expectedValue)
    {
        var expected = (UpdateState)expectedValue;
        var provider = new FakeProvider { Check = _ => Task.FromResult(available) };
        using var service = new StoreUpdateService(provider);
        var states = new List<UpdateState>();
        service.Changed += snapshot => states.Add(snapshot.State);
        var result = await service.CheckAsync(1);
        Assert.Equal(expected, result.State);
        Assert.Equal(available == true, result.CanInstall);
        Assert.Equal(new[] { UpdateState.Checking, expected }, states);
    }

    [Fact]
    public async Task StartupCheckRunsOnceAndConcurrentRequestsShareWork()
    {
        var completion = new TaskCompletionSource<bool?>();
        var provider = new FakeProvider { Check = _ => completion.Task };
        using var service = new StoreUpdateService(provider);
        var first = service.CheckAtStartupAsync(1);
        Assert.Same(first, service.CheckAsync(1));
        Assert.Same(first, service.CheckAtStartupAsync(1));
        Assert.Same(first, service.InstallAsync(1));
        Assert.Equal(1, provider.CheckCalls);
        completion.SetResult(true);
        await first;
        Assert.Same(first, service.CheckAtStartupAsync(1));
        await service.CheckAsync(1);
        Assert.Equal(2, provider.CheckCalls);
    }

    [Fact]
    public async Task CheckFailureCanBeRetried()
    {
        var provider = new FakeProvider { Check = _ => throw new IOException("offline") };
        using var service = new StoreUpdateService(provider);
        Assert.Equal(UpdateState.Failed, (await service.CheckAsync(1)).State);
        provider.Check = _ => Task.FromResult<bool?>(true);
        Assert.Equal(UpdateState.Available, (await service.CheckAsync(1)).State);
    }

    [Theory]
    [InlineData(UpdateInstallResult.Completed, UpdateState.Completed, false)]
    [InlineData(UpdateInstallResult.Canceled, UpdateState.Canceled, true)]
    [InlineData(UpdateInstallResult.Failed, UpdateState.Failed, true)]
    public async Task InstallMapsResultAndAllowsRetry(object resultValue, object expectedValue, bool retry)
    {
        var result = (UpdateInstallResult)resultValue;
        var expected = (UpdateState)expectedValue;
        var provider = new FakeProvider { Install = (_, _) => Task.FromResult(result) };
        using var service = new StoreUpdateService(provider);
        await service.InstallAsync(1);
        Assert.Equal(0, provider.InstallCalls);
        await service.CheckAsync(1);
        var snapshot = await service.InstallAsync(1);
        Assert.Equal(expected, snapshot.State);
        Assert.Equal(retry, snapshot.CanInstall);
        if (retry)
        {
            await service.InstallAsync(1);
            Assert.Equal(2, provider.InstallCalls);
        }
    }

    [Fact]
    public async Task UnpackagedExecutionIsUnsupportedWithoutContactingStore()
    {
        Assert.Null(await new StoreUpdateProvider().CheckAsync(0, CancellationToken.None));
    }

    [Fact]
    public async Task InstallExceptionDoesNotEscapeAndCanBeRetried()
    {
        var provider = new FakeProvider { Install = (_, _) => throw new IOException() };
        using var service = new StoreUpdateService(provider);
        await service.CheckAsync(1);
        Assert.Equal(UpdateState.Failed, (await service.InstallAsync(1)).State);
        Assert.True(service.Snapshot.CanInstall);
    }

    [Fact]
    public async Task DisposalCancelsAndSuppressesLateResults()
    {
        var completion = new TaskCompletionSource<bool?>();
        CancellationToken token = default;
        var provider = new FakeProvider { Check = cancellation => { token = cancellation; return completion.Task; } };
        var service = new StoreUpdateService(provider);
        var notifications = 0;
        service.Changed += _ => notifications++;
        var pending = service.CheckAsync(1);
        service.Dispose();
        Assert.True(token.IsCancellationRequested);
        completion.SetResult(true);
        await pending;
        Assert.Equal(1, notifications);
        await service.CheckAsync(1);
        Assert.Equal(1, provider.CheckCalls);
    }

    internal sealed class FakeProvider : IStoreUpdateProvider
    {
        public Func<CancellationToken, Task<bool?>> Check { get; set; } = _ => Task.FromResult<bool?>(true);
        public Func<IProgress<double>, CancellationToken, Task<UpdateInstallResult>> Install { get; set; } =
            (_, _) => Task.FromResult(UpdateInstallResult.Completed);
        public int CheckCalls { get; private set; }
        public int InstallCalls { get; private set; }
        public Task<bool?> CheckAsync(nint owner, CancellationToken cancellationToken)
        {
            CheckCalls++;
            return Check(cancellationToken);
        }
        public Task<UpdateInstallResult> InstallAsync(nint owner, IProgress<double> progress, CancellationToken cancellationToken)
        {
            InstallCalls++;
            return Install(progress, cancellationToken);
        }
    }
}
