using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;

namespace BatteryTray.Tests;

[Collection("UI language")]
public sealed class UpdateUiTests
{
    [Fact]
    public void HelpShowsSharedStateAndDisposedFormsUnsubscribe() => RunSta(() =>
    {
        var completion = new TaskCompletionSource<bool?>();
        var provider = new StoreUpdateServiceTests.FakeProvider { Check = _ => completion.Task };
        using var service = new StoreUpdateService(provider);
        var form = new SettingsForm(new AppSettings(), [], new UnsupportedStartup(), updateService: service);
        form.ShowUpdatePage();
        var check = Field<Button>(form, "_checkUpdates");
        Assert.Equal("HelpTab", ((TabControl)check.Parent!.Parent!.Parent!.Parent!).SelectedTab!.Name);
        var pending = service.CheckAsync(1);
        Assert.False(check.Enabled);
        var status = Field<Label>(form, "_updateStatus");
        var checkingText = status.Text;
        form.Dispose();
        completion.SetResult(true);
        PumpUntil(() => pending.IsCompleted);
        Assert.Equal(checkingText, status.Text);

        using var reopened = new SettingsForm(new AppSettings(), [], new UnsupportedStartup(), updateService: service);
        Assert.Equal(I18n.Get(TextId.UpdateAvailable), Field<Label>(reopened, "_updateStatus").Text);
        Assert.True(Field<Button>(reopened, "_installUpdate").Enabled);
        Assert.Equal(1, provider.CheckCalls);
    });

    [Fact]
    public void CheckButtonCoalescesRequestsAndInstallProgressKeepsOwnerAlive() => RunSta(() =>
    {
        var checkCompletion = new TaskCompletionSource<bool?>();
        var installCompletion = new TaskCompletionSource<UpdateInstallResult>();
        IProgress<double>? progress = null;
        var provider = new StoreUpdateServiceTests.FakeProvider
        {
            Check = _ => checkCompletion.Task,
            Install = (reporter, _) => { progress = reporter; return installCompletion.Task; }
        };
        using var service = new StoreUpdateService(provider);
        using var form = new SettingsForm(new AppSettings(), [], new UnsupportedStartup(), updateService: service);
        form.ShowUpdatePage();
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new System.Drawing.Point(-32000, -32000);
        form.Opacity = 0;
        form.Show();
        var check = Field<Button>(form, "_checkUpdates");
        check.PerformClick();
        check.PerformClick();
        Assert.Equal(1, provider.CheckCalls);
        Assert.False(check.Enabled);
        checkCompletion.SetResult(true);
        PumpUntil(() => service.Snapshot.State == UpdateState.Available);
        var install = service.InstallAsync(form.Handle);
        Assert.Same(install, service.InstallAsync(form.Handle));
        progress!.Report(0.42);
        PumpUntil(() => Field<Label>(form, "_updateStatus").Text.Contains("42"));
        Assert.False(check.Enabled);
        Assert.False(Field<Button>(form, "_installUpdate").Enabled);
        form.Close();
        Assert.False(form.IsDisposed);
        installCompletion.SetResult(UpdateInstallResult.Canceled);
        PumpUntil(() => install.IsCompleted);
        Assert.True(check.Enabled);
        Assert.True(Field<Button>(form, "_installUpdate").Enabled);
        form.Close();
        Assert.True(form.IsDisposed);
    });

    [Fact]
    public void UnsupportedExecutionDisablesCheckAndExplainsWhy() => RunSta(() =>
    {
        using var service = new StoreUpdateService(new StoreUpdateServiceTests.FakeProvider
        {
            Check = _ => Task.FromResult<bool?>(null)
        });
        using var form = new SettingsForm(new AppSettings(), [], new UnsupportedStartup(), updateService: service);
        var pending = service.CheckAsync(1);
        PumpUntil(() => pending.IsCompleted);
        Assert.False(Field<Button>(form, "_checkUpdates").Enabled);
        Assert.Equal(I18n.Get(TextId.UpdateUnsupported), Field<Label>(form, "_updateStatus").Text);
    });

    private static T Field<T>(SettingsForm form, string name) =>
        (T)typeof(SettingsForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;

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
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "UI test timed out.");
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    private sealed class UnsupportedStartup : IStartupRegistration
    {
        public Task<StartupRegistrationResult> GetStateAsync() =>
            Task.FromResult(new StartupRegistrationResult(StartupRegistrationState.Unsupported, false));
        public Task<StartupRegistrationResult> SetEnabledAsync(bool enabled) => GetStateAsync();
    }
}
