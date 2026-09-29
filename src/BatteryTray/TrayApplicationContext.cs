namespace BatteryTray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly SettingsStore _store = new();
    private readonly IBluetoothBatteryProvider _provider = new BluetoothBatteryProvider();
    private readonly IStartupRegistration _startupRegistration = new StartupRegistration();
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Dictionary<string, BatteryBand> _previousBands = new(StringComparer.OrdinalIgnoreCase);
    private readonly Control _dispatcher = new();
    private readonly RegisteredWaitHandle _activationRegistration;
    private readonly SettingsLoadResult _loadResult;
    private AppSettings _settings;
    private IReadOnlyList<BluetoothBatteryDevice> _devices = [];
    private Icon? _currentIcon;
    private SettingsForm? _settingsForm;
    private bool _hasCompletedInitialRefresh;

    private bool _isExiting;

    public TrayApplicationContext(SingleInstanceCoordinator instance)
    {
        _loadResult = _store.Load();
        _settings = _loadResult.Settings;
        I18n.SetLanguage(_settings.UiLanguage);
        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = I18n.Get(TextId.AppName)
        };
        _notifyIcon.DoubleClick += (_, _) => ShowSettings();

        _timer.Tick += async (_, _) => await RefreshAsync(showErrors: false);
        ApplyInterval(start: false);
        UpdateTrayIcon();
        BuildContextMenu();

        // プロセス間イベントのコールバックをWinFormsのUIスレッドへ戻すための
        // 非表示ハンドルを、メッセージループ開始前に用意する。
        _ = _dispatcher.Handle;
        _activationRegistration = instance.RegisterActivation(ActivateFromSecondaryInstance);
        Application.Idle += InitialIdle;
    }

    private void InitialIdle(object? sender, EventArgs e)
    {
        Application.Idle -= InitialIdle;
        if (!_settings.OnboardingCompleted)
        {
            ShowSettings(isOnboarding: true);
            return;
        }

        _timer.Start();
        ShowSettingsLoadWarning();
        _ = RefreshAsync(showErrors: true);
    }

    private void ActivateFromSecondaryInstance()
    {
        if (_isExiting || _dispatcher.IsDisposed)
            return;

        try
        {
            _dispatcher.BeginInvoke(() => ShowSettings(isOnboarding: !_settings.OnboardingCompleted));
        }
        catch (InvalidOperationException) when (_isExiting || _dispatcher.IsDisposed)
        {
        }
    }

    private void ShowSettingsLoadWarning()
    {
        var message = _loadResult.Status switch
        {
            SettingsLoadStatus.RecoveredInvalidJson =>
                I18n.Format(TextId.SettingsRecovered, _loadResult.Detail ?? string.Empty),
            SettingsLoadStatus.IoError =>
                I18n.Format(TextId.SettingsLoadFailed, _loadResult.Detail ?? string.Empty),
            _ => null
        };
        if (message is not null)
            _notifyIcon.ShowBalloonTip(7000, I18n.Get(TextId.SettingsError), message, ToolTipIcon.Warning);
    }

    private async Task RefreshAsync(bool showErrors)
    {
        if (!await _refreshLock.WaitAsync(0))
            return;

        try
        {
            _devices = await _provider.GetDevicesAsync(_cancellation.Token);
            MergeDiscoveredDevices();
            EvaluateTransitions();
            UpdatePresentation();
            _hasCompletedInitialRefresh = true;
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            BuildContextMenu(exception.Message);
            if (showErrors)
            {
                _notifyIcon.ShowBalloonTip(
                    5000,
                    I18n.Get(TextId.BatteryInfoUnavailableTitle),
                    exception.Message,
                    ToolTipIcon.Warning);
            }
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private void MergeDiscoveredDevices()
    {
        var changed = false;
        foreach (var device in _devices)
        {
            var configured = FindSettings(device.Id);
            if (configured is null)
            {
                // 以前のAEP IDからContainerIdベースのIDへ切り替わった場合も、
                // 同名設定が一意なら閾値を保ったまま移行する。
                var sameName = _settings.Devices
                    .Where(item => string.Equals(item.DisplayName, device.Name, StringComparison.CurrentCultureIgnoreCase))
                    .ToArray();
                if (sameName.Length == 1)
                {
                    sameName[0].DeviceId = device.Id;
                }
                else
                {
                    _settings.Devices.Add(_settings.CreateDeviceSettings(device.Id, device.Name));
                }
                changed = true;
            }
            else if (configured.DisplayName != device.Name)
            {
                configured.DisplayName = device.Name;
                changed = true;
            }
        }

        if (changed)
            _store.Save(_settings);
    }

    private void EvaluateTransitions()
    {
        foreach (var device in _devices)
        {
            var configured = FindSettings(device.Id);
            if (configured is null || !configured.IsEnabled)
                continue;

            var currentBand = configured.Classify(device.BatteryLevel);
            if (_previousBands.TryGetValue(device.Id, out var previousBand) &&
                previousBand != currentBand &&
                previousBand != BatteryBand.Unknown &&
                currentBand != BatteryBand.Unknown &&
                _hasCompletedInitialRefresh)
            {
                NotifyTransition(device, previousBand, currentBand);
            }

            _previousBands[device.Id] = currentBand;
        }
    }

    private void NotifyTransition(BluetoothBatteryDevice device, BatteryBand previous, BatteryBand current)
    {
        if (!_settings.NotificationsEnabled)
            return;

        var direction = I18n.Get(current < previous ? TextId.DirectionDecreased : TextId.DirectionRecovered);
        var icon = current <= BatteryBand.Low ? ToolTipIcon.Warning : ToolTipIcon.Info;
        _notifyIcon.ShowBalloonTip(
            7000,
            I18n.Format(TextId.TransitionTitle, device.Name, direction),
            I18n.Format(TextId.TransitionMessage, device.BatteryLevel, BandText(previous), BandText(current)),
            icon);
    }

    private void UpdatePresentation()
    {
        BuildContextMenu();
    }

    private void BuildContextMenu(string? error = null)
    {
        var menu = new ContextMenuStrip
        {
            RightToLeft = I18n.IsRightToLeft ? RightToLeft.Yes : RightToLeft.No
        };
        if (error is not null)
        {
            menu.Items.Add(new ToolStripMenuItem(I18n.Get(TextId.FetchError)) { Enabled = false });
        }
        else if (_devices.Count == 0)
        {
            menu.Items.Add(new ToolStripMenuItem(I18n.Get(TextId.NoBluetoothDevices)) { Enabled = false });
        }
        else
        {
            foreach (var device in _devices)
            {
                var configured = FindSettings(device.Id);
                var suffix = device.BatteryLevel is int level ? $"{level}%" : I18n.Get(TextId.BatteryUnknown);
                var item = new ToolStripMenuItem($"{device.Name}  {suffix}")
                {
                    Enabled = false,
                    Checked = configured?.IsEnabled == true
                };
                menu.Items.Add(item);
            }
        }

        menu.Items.Add(new ToolStripSeparator());
        var refresh = new ToolStripMenuItem(I18n.Get(TextId.RefreshNow));
        refresh.Click += async (_, _) => await RefreshAsync(showErrors: true);
        menu.Items.Add(refresh);

        var settings = new ToolStripMenuItem(I18n.Get(TextId.SettingsMenu));
        settings.Click += (_, _) => ShowSettings();
        menu.Items.Add(settings);
        menu.Items.Add(new ToolStripSeparator());
        var exit = new ToolStripMenuItem(I18n.Get(TextId.Exit));
        exit.Click += (_, _) => ExitApplication();
        menu.Items.Add(exit);

        var oldMenu = _notifyIcon.ContextMenuStrip;
        _notifyIcon.ContextMenuStrip = menu;
        oldMenu?.Dispose();
    }

    private void ShowSettings(bool isOnboarding = false)
    {
        if (_isExiting)
            return;

        if (_settingsForm is { IsDisposed: false })
        {
            if (_settingsForm.WindowState == FormWindowState.Minimized)
                _settingsForm.WindowState = FormWindowState.Normal;
            _settingsForm.Activate();
            _settingsForm.BringToFront();
            return;
        }

        isOnboarding |= !_settings.OnboardingCompleted;
        using var form = new SettingsForm(_settings, _devices, _startupRegistration, isOnboarding);
        _settingsForm = form;
        try
        {
            if (form.ShowDialog() != DialogResult.OK)
            {
                if (isOnboarding)
                    ExitApplication();
                return;
            }

            _settings = form.Result;
            I18n.SetLanguage(_settings.UiLanguage);
            _notifyIcon.Text = I18n.Get(TextId.AppName);
            _store.Save(_settings);
            ApplyInterval(start: true);
            _previousBands.Clear();
            _hasCompletedInitialRefresh = false;
            BuildContextMenu();
            _ = RefreshAsync(showErrors: true);
        }
        finally
        {
            _settingsForm = null;
        }
    }

    private DeviceSettings? FindSettings(string deviceId) =>
        _settings.Devices.FirstOrDefault(device =>
            string.Equals(device.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));

    private void ApplyInterval(bool start)
    {
        _settings.PollIntervalSeconds = Math.Clamp(_settings.PollIntervalSeconds, 10, 3600);
        _timer.Interval = checked(_settings.PollIntervalSeconds * 1000);
        if (start)
            _timer.Start();
    }

    private void UpdateTrayIcon()
    {
        var nextIcon = TrayIconFactory.Create();
        _notifyIcon.Icon = nextIcon;
        _currentIcon?.Dispose();
        _currentIcon = nextIcon;
    }

    private static string BandText(BatteryBand band) => band switch
    {
        BatteryBand.Critical => I18n.Get(TextId.BandCritical),
        BatteryBand.Low => I18n.Get(TextId.BandLow),
        BatteryBand.Caution => I18n.Get(TextId.BandCaution),
        BatteryBand.Good => I18n.Get(TextId.BandGood),
        _ => I18n.Get(TextId.BandUnknown)
    };

    private void ExitApplication()
    {
        if (_isExiting)
            return;
        _isExiting = true;
        Application.Idle -= InitialIdle;
        _cancellation.Cancel();
        _timer.Stop();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _currentIcon?.Dispose();
        _timer.Dispose();
        _activationRegistration.Unregister(null);
        _dispatcher.Dispose();
        _cancellation.Dispose();
        _refreshLock.Dispose();
        ExitThread();
    }
}
