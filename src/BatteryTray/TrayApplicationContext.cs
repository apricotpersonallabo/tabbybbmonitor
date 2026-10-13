namespace BatteryTray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly SettingsStore _store = new();
    private readonly DeviceRefreshService _refreshService = new(new BluetoothBatteryProvider());
    private readonly ContextMenuStrip _menu = new();
    private readonly ToolStripSeparator _deviceSeparator = new();
    private readonly TrayTooltipController _tooltip;
    private readonly IStartupRegistration _startupRegistration = new StartupRegistration();
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
    private readonly bool _isStartupTask;

    public TrayApplicationContext(SingleInstanceCoordinator instance, bool isStartupTask = false)
    {
        _isStartupTask = isStartupTask;
        _loadResult = _store.Load();
        _settings = _loadResult.Settings;
        I18n.SetLanguage(_settings.UiLanguage);
        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = string.Empty
        };
        _tooltip = new TrayTooltipController(
            () => _isExiting || _menu.Visible || _settingsForm is { IsDisposed: false },
            () => TrayDeviceList.Create(_refreshService.Snapshot),
            () => _ = RefreshAsync(showErrors: false));
        _notifyIcon.MouseMove += (_, _) => _tooltip.ObserveMouse();
        _notifyIcon.MouseDown += (_, _) => _tooltip.Dismiss();
        _notifyIcon.DoubleClick += (_, _) => ShowSettings();
        _refreshService.Changed += SnapshotChanged;

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
        if (LaunchMode.ShouldShowSettings(_settings.OnboardingCompleted, _isStartupTask))
            ShowSettings();
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
        if (_isExiting)
            return;
        var snapshot = await _refreshService.RefreshAsync();
        if (!_isExiting && showErrors && snapshot.Error is not null)
        {
            _notifyIcon.ShowBalloonTip(5000, I18n.Get(TextId.BatteryInfoUnavailableTitle),
                snapshot.Error, ToolTipIcon.Warning);
        }
    }

    private void SnapshotChanged(DeviceSnapshot snapshot)
    {
        if (_isExiting)
            return;
        if (!snapshot.IsRefreshing && snapshot.Error is null && snapshot.HasResult)
        {
            _devices = snapshot.Devices;
            MergeDiscoveredDevices();
            EvaluateTransitions();
            _hasCompletedInitialRefresh = true;
        }
        UpdatePresentation();
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
                    configured = sameName[0];
                    configured.DeviceId = device.Id;
                }
                else
                {
                    configured = _settings.CreateDeviceSettings(device.Id, device.Name);
                    _settings.Devices.Add(configured);
                }
                changed = true;
            }
            else if (configured.DisplayName != device.Name)
            {
                configured.DisplayName = device.Name;
                changed = true;
            }
            changed |= configured.UpdateBluetoothAddress(device.BluetoothAddress);
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
        _tooltip.Update();
    }

    private void BuildContextMenu()
    {
        if (_notifyIcon.ContextMenuStrip is null)
        {
            _menu.Items.Add(_deviceSeparator);
            var refresh = new ToolStripMenuItem { Name = "Refresh" };
            refresh.Click += async (_, _) => await RefreshAsync(showErrors: true);
            _menu.Items.Add(refresh);
            var settings = new ToolStripMenuItem { Name = "Settings" };
            settings.Click += (_, _) => ShowSettings();
            _menu.Items.Add(settings);
            _menu.Items.Add(new ToolStripSeparator());
            var exit = new ToolStripMenuItem { Name = "Exit" };
            exit.Click += (_, _) => ExitApplication();
            _menu.Items.Add(exit);
            _menu.Opening += (_, _) =>
            {
                _tooltip.Dismiss();
                _ = RefreshAsync(showErrors: false);
            };
            _notifyIcon.ContextMenuStrip = _menu;
        }
        _menu.RightToLeft = I18n.IsRightToLeft ? RightToLeft.Yes : RightToLeft.No;
        _menu.Items["Refresh"]!.Text = I18n.Get(TextId.RefreshNow);
        _menu.Items["Settings"]!.Text = I18n.Get(TextId.SettingsMenu);
        _menu.Items["Exit"]!.Text = I18n.Get(TextId.Exit);
        TrayDeviceList.UpdateMenu(_menu, _deviceSeparator, TrayDeviceList.Create(_refreshService.Snapshot),
            id => FindSettings(id)?.IsEnabled == true);
    }

    private void ShowSettings(bool isOnboarding = false)
    {
        if (_isExiting)
            return;

        _tooltip.Dismiss();

        if (_settingsForm is { IsDisposed: false })
        {
            if (_settingsForm.WindowState == FormWindowState.Minimized)
                _settingsForm.WindowState = FormWindowState.Normal;
            _settingsForm.Activate();
            _settingsForm.BringToFront();
            _ = _settingsForm.RefreshDevicesAsync();
            return;
        }

        isOnboarding |= !_settings.OnboardingCompleted;
        using var form = new SettingsForm(_settings, _devices, _startupRegistration, isOnboarding, _refreshService);
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
        _refreshService.Changed -= SnapshotChanged;
        _refreshService.Dispose();
        _tooltip.Dispose();
        _timer.Stop();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _currentIcon?.Dispose();
        _timer.Dispose();
        _activationRegistration.Unregister(null);
        _dispatcher.Dispose();
        ExitThread();
    }
}
