using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;

namespace BatteryTray;

// Windows restarts registered desktop processes after Store deployment finishes.
internal sealed class UpdateRestartRegistration(
    Func<string, uint, int> register,
    Func<TimeSpan> uptime,
    Func<TimeSpan, CancellationToken, Task> delay)
{
    internal const string RestartArgument = "--after-update";
    // Restart for package updates only, not crashes, hangs, or Windows reboots.
    internal const uint RestartFlags = 1 | 2 | 8;
    internal static readonly TimeSpan MinimumUptime = TimeSpan.FromSeconds(60);
    private static readonly long StartupTimestamp = Stopwatch.GetTimestamp();

    public static UpdateRestartRegistration Create() => new(RegisterApplicationRestart,
        () => Stopwatch.GetElapsedTime(StartupTimestamp), Task.Delay);

    public void TryRegisterAtStartup()
    {
        try
        {
            if (Package.Current.SignatureKind == PackageSignatureKind.Store)
                Register();
        }
        catch (Exception exception) when (exception is InvalidOperationException or COMException)
        {
            // Ordinary executable execution has no package identity. A registration
            // failure must not stop monitoring; installation retries registration.
        }
    }

    public async Task PrepareAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Register();
        // Restart Manager suppresses restarts for processes younger than 60 seconds.
        var remaining = MinimumUptime - uptime();
        if (remaining > TimeSpan.Zero)
            await delay(remaining, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void Register() => Marshal.ThrowExceptionForHR(register(RestartArgument, RestartFlags));

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegisterApplicationRestart(string commandLine, uint flags);
}
