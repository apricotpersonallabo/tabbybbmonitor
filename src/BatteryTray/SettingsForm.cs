namespace BatteryTray;

internal sealed class SettingsForm : Form
{
    private readonly IBluetoothBatteryProvider _provider = new BluetoothBatteryProvider();
    private readonly IStartupRegistration _startupRegistration;
    private readonly bool _isOnboarding;
    private readonly List<DeviceSettings> _devices;
    private readonly Dictionary<string, BluetoothBatteryDevice> _observed =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly DataGridView _grid = new();
    private readonly TextBox _searchBox = new();
    private readonly CheckBox _showUnavailable = new();
    private readonly Label _deviceCountLabel = new();
    private readonly Label _detailName = new();
    private readonly Label _detailStatus = new();
    private readonly CheckBox _monitorDevice = new();
    private readonly NumericUpDown _critical = CreatePercentageInput();
    private readonly NumericUpDown _low = CreatePercentageInput();
    private readonly NumericUpDown _caution = CreatePercentageInput();
    private readonly PictureBox _iconPreview = new();
    private readonly Icon _windowIcon = TrayIconFactory.Create();
    private readonly NumericUpDown _pollInterval = new();
    private readonly ComboBox _languageCombo = new();
    private readonly CheckBox _notificationsEnabled = new();
    private readonly NumericUpDown _defaultCritical = CreatePercentageInput();
    private readonly NumericUpDown _defaultLow = CreatePercentageInput();
    private readonly NumericUpDown _defaultCaution = CreatePercentageInput();
    private readonly Button _rediscoverButton = new();
    private readonly Button _deleteUnavailableButton = new();
    private readonly CheckBox _startAtSignIn = new();
    private readonly Label _startAtSignInLabel = new();
    private readonly FlowLayoutPanel _startupPanel = new();
    private readonly Label _startupNote = new();
    private readonly Label _onboardingStatus = new();
    private readonly Button _openBluetoothSettings = new();
    private Button? _saveButton;
    private bool _updatingDetails;
    private bool _startupStateLoaded;
    private bool _initialStartupEnabled;

    public SettingsForm(
        AppSettings settings,
        IReadOnlyList<BluetoothBatteryDevice> observedDevices,
        IStartupRegistration startupRegistration,
        bool isOnboarding = false)
    {
        _startupRegistration = startupRegistration;
        _isOnboarding = isOnboarding;
        Text = I18n.Get(TextId.AppName);
        Icon = _windowIcon;
        RightToLeft = I18n.IsRightToLeft ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = I18n.IsRightToLeft;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 560);
        Size = new Size(1040, 650);
        ShowInTaskbar = true;
        Font = SystemFonts.MessageBoxFont;
        BackColor = Color.FromArgb(247, 248, 250);

        _devices = settings.Devices.Select(device => device.Clone()).ToList();
        MergeObserved(settings, observedDevices);
        ConfigureGeneralSettings(settings);
        ConfigureGrid();
        BuildLayout();
        RefreshGrid();
        Shown += async (_, _) =>
        {
            await LoadStartupStateAsync();
            if (_isOnboarding)
                await RediscoverAsync(showErrors: false);
        };
    }

    public AppSettings Result { get; private set; } = new();

    private void MergeObserved(AppSettings settings, IEnumerable<BluetoothBatteryDevice> devices)
    {
        foreach (var observed in devices)
        {
            _observed[observed.Id] = observed;
            var configured = FindDevice(observed.Id);
            if (configured is null)
                _devices.Add(settings.CreateDeviceSettings(observed.Id, observed.Name));
            else
                configured.DisplayName = observed.Name;
        }
    }

    private void ConfigureGeneralSettings(AppSettings settings)
    {
        _pollInterval.Minimum = 10;
        _pollInterval.Maximum = 3600;
        _pollInterval.Value = Math.Clamp(settings.PollIntervalSeconds, 10, 3600);
        _pollInterval.Width = 100;
        _pollInterval.TextAlign = HorizontalAlignment.Right;
        _languageCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _languageCombo.Width = 260;
        var languageChoices = I18n.GetLanguageChoices();
        _languageCombo.Items.AddRange(languageChoices.Cast<object>().ToArray());
        _languageCombo.SelectedItem = languageChoices.FirstOrDefault(choice =>
            string.Equals(choice.Code, settings.UiLanguage, StringComparison.OrdinalIgnoreCase)) ?? languageChoices[0];
        _notificationsEnabled.Checked = settings.NotificationsEnabled;
        _notificationsEnabled.Text = I18n.Get(TextId.NotifyOnThresholdCrossing);
        _notificationsEnabled.AutoSize = true;
        _defaultCritical.Value = Math.Clamp(settings.DefaultCriticalThreshold, 0, 100);
        _defaultLow.Value = Math.Clamp(settings.DefaultLowThreshold, 0, 100);
        _defaultCaution.Value = Math.Clamp(settings.DefaultCautionThreshold, 0, 100);
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = Color.White;
        _grid.BorderStyle = BorderStyle.None;
        _grid.AutoGenerateColumns = false;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.MultiSelect = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.RowTemplate.Height = 38;
        _grid.ColumnHeadersHeight = 36;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(238, 241, 245);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(65, 70, 80);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(222, 235, 252);
        _grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(30, 35, 45);

        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "Enabled", HeaderText = I18n.Get(TextId.ColumnMonitor), FillWeight = 13, MinimumWidth = 58
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Name", HeaderText = I18n.Get(TextId.ColumnDevice), ReadOnly = true, FillWeight = 52, MinimumWidth = 170
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Battery", HeaderText = I18n.Get(TextId.ColumnBattery), ReadOnly = true, FillWeight = 17, MinimumWidth = 72
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "State", HeaderText = I18n.Get(TextId.ColumnState), ReadOnly = true, FillWeight = 20, MinimumWidth = 82
        });

        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grid.CellValueChanged += GridCellValueChanged;
        _grid.SelectionChanged += (_, _) => LoadSelectedDevice();
    }

    private void BuildLayout()
    {
        var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(22, 14, 22, 10) };
        header.Controls.Add(new Label
        {
            Text = I18n.Get(TextId.AppName),
            Font = new Font(Font.FontFamily, 15f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(22, 12)
        });
        header.Controls.Add(new Label
        {
            Text = I18n.Get(TextId.HeaderDescription),
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
            Location = new Point(24, 45)
        });

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(16, 6) };
        tabs.TabPages.Add(BuildDevicesTab());
        tabs.TabPages.Add(BuildGeneralTab());

        var saveButton = new Button
        {
            Text = I18n.Get(_isOnboarding ? TextId.StartMonitoring : TextId.Save),
            AutoSize = true,
            Padding = new Padding(18, 3, 18, 3)
        };
        _saveButton = saveButton;
        var cancelButton = new Button { Text = I18n.Get(TextId.Cancel), DialogResult = DialogResult.Cancel, AutoSize = true, Padding = new Padding(10, 3, 10, 3) };
        saveButton.Click += SaveClicked;
        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 10, 18, 10)
        };
        footer.Controls.Add(saveButton);
        footer.Controls.Add(cancelButton);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Margin = Padding.Empty };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, _isOnboarding ? 138 : 0));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(BuildOnboardingBanner(), 0, 1);
        root.Controls.Add(tabs, 0, 2);
        root.Controls.Add(footer, 0, 3);
        Controls.Add(root);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private Control BuildOnboardingBanner()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(232, 243, 255),
            Visible = _isOnboarding
        };
        var title = new Label
        {
            Text = I18n.Get(TextId.OnboardingTitle),
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6)
        };
        var description = new Label
        {
            Text = I18n.Get(TextId.OnboardingDescription),
            ForeColor = Color.FromArgb(45, 65, 90),
            AutoSize = true,
            MaximumSize = new Size(780, 0),
            Margin = new Padding(0, 0, 0, 8)
        };
        _onboardingStatus.Text = I18n.Get(TextId.Discovering);
        _onboardingStatus.AutoSize = true;
        _onboardingStatus.Font = new Font(Font, FontStyle.Bold);
        _onboardingStatus.MaximumSize = new Size(650, 0);
        _onboardingStatus.Margin = new Padding(0, 5, 12, 0);
        _openBluetoothSettings.Text = I18n.Get(TextId.OpenBluetoothSettings);
        _openBluetoothSettings.AutoSize = true;
        _openBluetoothSettings.Visible = false;
        _openBluetoothSettings.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _openBluetoothSettings.Click += (_, _) => OpenSystemSettings("ms-settings:bluetooth");

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 12, 24, 10),
            ColumnCount = 2,
            RowCount = 3
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(title, 0, 0);
        layout.SetColumnSpan(title, 2);
        layout.Controls.Add(description, 0, 1);
        layout.SetColumnSpan(description, 2);
        layout.Controls.Add(_onboardingStatus, 0, 2);
        layout.Controls.Add(_openBluetoothSettings, 1, 2);
        panel.Controls.Add(layout);
        return panel;
    }

    private TabPage BuildDevicesTab()
    {
        var page = new TabPage(I18n.Get(TextId.DevicesTab)) { BackColor = Color.FromArgb(247, 248, 250), Padding = new Padding(12) };
        _searchBox.PlaceholderText = I18n.Get(TextId.SearchDevices);
        _searchBox.Width = 240;
        _searchBox.Margin = new Padding(0, 2, 10, 2);
        _searchBox.TextChanged += (_, _) => RefreshGrid();
        _showUnavailable.Text = I18n.Get(TextId.ShowUnavailable);
        _showUnavailable.AutoSize = true;
        _showUnavailable.Margin = new Padding(0, 6, 12, 0);
        _showUnavailable.CheckedChanged += (_, _) => RefreshGrid();
        _rediscoverButton.Text = I18n.Get(TextId.Rediscover);
        _rediscoverButton.AutoSize = true;
        _rediscoverButton.Click += async (_, _) => await RediscoverAsync();
        _deviceCountLabel.AutoSize = true;
        _deviceCountLabel.ForeColor = SystemColors.GrayText;
        _deviceCountLabel.Margin = new Padding(12, 7, 0, 0);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        toolbar.Controls.Add(_searchBox);
        toolbar.Controls.Add(_showUnavailable);
        toolbar.Controls.Add(_rediscoverButton);
        toolbar.Controls.Add(_deviceCountLabel);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Size = new Size(960, 420),
            FixedPanel = FixedPanel.Panel2,
            Panel1MinSize = 380,
            Panel2MinSize = 300,
            SplitterDistance = 610,
            BackColor = Color.FromArgb(220, 223, 228)
        };
        split.Panel1.BackColor = Color.White;
        split.Panel1.Controls.Add(_grid);
        split.Panel2.BackColor = Color.White;
        split.Panel2.Controls.Add(BuildDeviceDetails());

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(toolbar, 0, 0);
        layout.Controls.Add(split, 0, 1);
        page.Controls.Add(layout);
        return page;
    }

    private Control BuildDeviceDetails()
    {
        _detailName.Text = I18n.Get(TextId.SelectDevice);
        _detailName.AutoSize = true;
        _detailName.Font = new Font(Font.FontFamily, 12f, FontStyle.Bold);
        _detailStatus.AutoSize = true;
        _detailStatus.ForeColor = SystemColors.GrayText;
        _monitorDevice.Text = I18n.Get(TextId.MonitorThisDevice);
        _monitorDevice.AutoSize = true;
        _monitorDevice.CheckedChanged += (_, _) => DetailValueChanged();
        _critical.ValueChanged += (_, _) => DetailValueChanged();
        _low.ValueChanged += (_, _) => DetailValueChanged();
        _caution.ValueChanged += (_, _) => DetailValueChanged();
        _iconPreview.Size = new Size(42, 42);
        _iconPreview.SizeMode = PictureBoxSizeMode.CenterImage;
        _iconPreview.Image = _windowIcon.ToBitmap();

        var defaultsButton = new Button { Text = I18n.Get(TextId.ResetDefaults), AutoSize = true };
        defaultsButton.Click += (_, _) => ApplyDefaultsToSelected();
        var applyAllButton = new Button { Text = I18n.Get(TextId.ApplyThresholdsToAll), AutoSize = true };
        applyAllButton.Click += (_, _) => ApplyThresholdsToAll();
        _deleteUnavailableButton.Text = I18n.Get(TextId.DeleteDeviceSettings);
        _deleteUnavailableButton.AutoSize = true;
        _deleteUnavailableButton.ForeColor = Color.FromArgb(180, 35, 35);
        _deleteUnavailableButton.Visible = false;
        _deleteUnavailableButton.Click += (_, _) => DeleteSelectedUnavailable();

        var thresholds = new TableLayoutPanel { Size = new Size(320, 114), ColumnCount = 3, RowCount = 3 };
        thresholds.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 18));
        thresholds.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        thresholds.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        AddThresholdRow(thresholds, 0, Color.FromArgb(220, 45, 45), I18n.Get(TextId.CriticalAtOrBelow), _critical);
        AddThresholdRow(thresholds, 1, Color.FromArgb(239, 122, 22), I18n.Get(TextId.LowAtOrBelow), _low);
        AddThresholdRow(thresholds, 2, Color.FromArgb(35, 126, 214), I18n.Get(TextId.CautionAtOrBelow), _caution);

        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 8, 0, 0) };
        buttons.Controls.Add(defaultsButton);
        buttons.Controls.Add(applyAllButton);
        buttons.Controls.Add(_deleteUnavailableButton);

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(20, 18, 16, 12)
        };
        var titleRow = new FlowLayoutPanel { AutoSize = true, Width = 320, Margin = Padding.Empty };
        titleRow.Controls.Add(_iconPreview);
        var titleText = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, Margin = new Padding(10, 0, 0, 0) };
        titleText.Controls.Add(_detailName);
        titleText.Controls.Add(_detailStatus);
        titleRow.Controls.Add(titleText);
        panel.Controls.Add(titleRow);
        panel.Controls.Add(_monitorDevice);
        panel.Controls.Add(new Label
        {
            Text = I18n.Get(TextId.NotificationThresholds),
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 18, 0, 8)
        });
        panel.Controls.Add(thresholds);
        panel.Controls.Add(new Label
        {
            Text = I18n.Get(TextId.ThresholdOrderHelp),
            ForeColor = SystemColors.GrayText,
            MaximumSize = new Size(320, 0),
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0)
        });
        panel.Controls.Add(buttons);
        SetDetailControlsEnabled(false);
        return panel;
    }

    private TabPage BuildGeneralTab()
    {
        var page = new TabPage(I18n.Get(TextId.GeneralTab)) { BackColor = Color.White, Padding = new Padding(28, 24, 28, 24) };
        var form = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 10 };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddSectionTitle(form, 0, I18n.Get(TextId.MonitoringSection));
        AddFormRow(form, 1, I18n.Get(TextId.PollInterval), BuildUnitInput(_pollInterval, I18n.Get(TextId.SecondsRange)));
        AddFormRow(form, 2, I18n.Get(TextId.Notifications), _notificationsEnabled);
        ConfigureStartupControls();
        form.Controls.Add(_startAtSignInLabel, 0, 3);
        form.Controls.Add(_startupPanel, 1, 3);
        AddFormRow(form, 4, I18n.LanguageLabel, _languageCombo);
        AddSectionTitle(form, 5, I18n.Get(TextId.NewDeviceDefaults));
        AddFormRow(form, 6, I18n.Get(TextId.BandCritical), BuildUnitInput(_defaultCritical, I18n.Get(TextId.PercentAtOrBelow)));
        AddFormRow(form, 7, I18n.Get(TextId.BandLow), BuildUnitInput(_defaultLow, I18n.Get(TextId.PercentAtOrBelow)));
        AddFormRow(form, 8, I18n.Get(TextId.BandCaution), BuildUnitInput(_defaultCaution, I18n.Get(TextId.PercentAtOrBelow)));
        var note = new Label
        {
            Text = I18n.Get(TextId.DefaultsNote),
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            Margin = new Padding(0, 12, 0, 0)
        };
        form.Controls.Add(note, 1, 9);
        page.Controls.Add(form);
        return page;
    }

    private void ConfigureStartupControls()
    {
        _startAtSignInLabel.Text = I18n.Get(TextId.StartAtSignIn);
        _startAtSignInLabel.AutoSize = true;
        _startAtSignInLabel.MaximumSize = new Size(220, 0);
        _startAtSignInLabel.Margin = new Padding(0, 8, 12, 8);
        _startAtSignInLabel.Visible = false;

        _startAtSignIn.AutoSize = true;
        _startAtSignIn.Enabled = false;
        _startAtSignIn.AccessibleName = I18n.Get(TextId.StartAtSignIn);
        _startupNote.AutoSize = true;
        _startupNote.ForeColor = SystemColors.GrayText;
        _startupNote.MaximumSize = new Size(520, 0);
        _startupPanel.AutoSize = true;
        _startupPanel.FlowDirection = FlowDirection.TopDown;
        _startupPanel.WrapContents = false;
        _startupPanel.Margin = new Padding(0, 4, 0, 4);
        _startupPanel.Visible = false;
        _startupPanel.Controls.Add(_startAtSignIn);
        _startupPanel.Controls.Add(_startupNote);
    }

    private void RefreshGrid(string? selectDeviceId = null)
    {
        selectDeviceId ??= SelectedDevice?.DeviceId;
        var filter = _searchBox.Text.Trim();
        var filtered = _devices
            .Where(device => _showUnavailable.Checked || _observed.ContainsKey(device.DeviceId))
            .Where(device => filter.Length == 0 || device.DisplayName.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
            .OrderByDescending(device => _observed.ContainsKey(device.DeviceId))
            .ThenBy(device => device.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        _grid.Rows.Clear();
        foreach (var device in filtered)
        {
            _observed.TryGetValue(device.DeviceId, out var observed);
            var band = device.Classify(observed?.BatteryLevel);
            var rowIndex = _grid.Rows.Add(
                device.IsEnabled,
                device.DisplayName,
                observed?.BatteryLevel is int level ? $"{level}%" : "—",
                observed is null ? I18n.Get(TextId.Unavailable) : BandText(band));
            var row = _grid.Rows[rowIndex];
            row.Tag = device;
            row.Cells["State"].Style.ForeColor = BandColor(band, observed is not null);
            if (string.Equals(device.DeviceId, selectDeviceId, StringComparison.OrdinalIgnoreCase))
                row.Selected = true;
        }

        _deviceCountLabel.Text = I18n.Format(TextId.DeviceCount, filtered.Length, _devices.Count);
        if (_grid.SelectedRows.Count == 0 && _grid.Rows.Count > 0)
            _grid.Rows[0].Selected = true;
        LoadSelectedDevice();
    }

    private DeviceSettings? SelectedDevice =>
        _grid.SelectedRows.Count == 0 ? null : _grid.SelectedRows[0].Tag as DeviceSettings;

    private void LoadSelectedDevice()
    {
        var device = SelectedDevice;
        _updatingDetails = true;
        try
        {
            SetDetailControlsEnabled(device is not null);
            _deleteUnavailableButton.Visible = device is not null && !_observed.ContainsKey(device.DeviceId);
            if (device is null)
            {
                _detailName.Text = I18n.Get(TextId.SelectDevice);
                _detailStatus.Text = "";
                return;
            }

            _observed.TryGetValue(device.DeviceId, out var observed);
            var band = device.Classify(observed?.BatteryLevel);
            _detailName.Text = device.DisplayName;
            _detailStatus.Text = observed is null
                ? I18n.Get(TextId.CurrentlyUnavailable)
                : observed.BatteryLevel is int level
                    ? I18n.Format(TextId.BatteryStatus, level, BandText(band))
                    : I18n.Get(TextId.BatteryLevelUnavailable);
            _monitorDevice.Checked = device.IsEnabled;
            _critical.Value = device.CriticalThreshold;
            _low.Value = device.LowThreshold;
            _caution.Value = device.CautionThreshold;
        }
        finally
        {
            _updatingDetails = false;
        }
    }

    private void DetailValueChanged()
    {
        if (_updatingDetails || SelectedDevice is not { } device)
            return;

        device.IsEnabled = _monitorDevice.Checked;
        device.CriticalThreshold = (int)_critical.Value;
        device.LowThreshold = (int)_low.Value;
        device.CautionThreshold = (int)_caution.Value;
        if (_grid.SelectedRows.Count > 0)
            _grid.SelectedRows[0].Cells["Enabled"].Value = device.IsEnabled;
    }

    private void GridCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "Enabled")
            return;
        if (_grid.Rows[e.RowIndex].Tag is not DeviceSettings device)
            return;
        device.IsEnabled = Convert.ToBoolean(_grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value);
        if (ReferenceEquals(device, SelectedDevice))
        {
            _updatingDetails = true;
            _monitorDevice.Checked = device.IsEnabled;
            _updatingDetails = false;
        }
    }

    private async Task RediscoverAsync(bool showErrors = true)
    {
        _rediscoverButton.Enabled = false;
        _rediscoverButton.Text = I18n.Get(TextId.Discovering);
        if (_isOnboarding)
        {
            _onboardingStatus.Text = I18n.Get(TextId.Discovering);
            _openBluetoothSettings.Visible = false;
        }
        UseWaitCursor = true;
        try
        {
            var found = await _provider.GetDevicesAsync(CancellationToken.None);
            _observed.Clear();
            var defaults = CurrentDefaults();
            MergeObserved(defaults, found);
            RefreshGrid();
            if (_isOnboarding)
            {
                _onboardingStatus.Text = found.Count == 0
                    ? I18n.Get(TextId.OnboardingNoDevicesHelp)
                    : I18n.Format(TextId.DeviceCount, found.Count, found.Count);
                _openBluetoothSettings.Visible = found.Count == 0;
            }
        }
        catch (Exception exception)
        {
            if (_isOnboarding)
            {
                _onboardingStatus.Text = $"{I18n.Get(TextId.DiscoveryFailed)}: {exception.Message}";
                _openBluetoothSettings.Visible = true;
            }
            if (showErrors)
                MessageBox.Show(this, exception.Message, I18n.Get(TextId.DiscoveryFailed), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            UseWaitCursor = false;
            _rediscoverButton.Text = I18n.Get(TextId.Rediscover);
            _rediscoverButton.Enabled = true;
        }
    }

    private void DeleteSelectedUnavailable()
    {
        if (SelectedDevice is not { } device || _observed.ContainsKey(device.DeviceId))
            return;

        if (MessageBox.Show(this,
                I18n.Format(TextId.DeleteDeviceConfirmation, device.DisplayName),
                I18n.Get(TextId.DeleteUnavailableTitle), MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            return;

        _devices.Remove(device);
        RefreshGrid();
    }

    private void ApplyDefaultsToSelected()
    {
        if (SelectedDevice is not { } device)
            return;
        device.CriticalThreshold = (int)_defaultCritical.Value;
        device.LowThreshold = (int)_defaultLow.Value;
        device.CautionThreshold = (int)_defaultCaution.Value;
        LoadSelectedDevice();
    }

    private void ApplyThresholdsToAll()
    {
        if (!ThresholdsValid((int)_critical.Value, (int)_low.Value, (int)_caution.Value))
        {
            ShowThresholdError();
            return;
        }
        foreach (var device in _devices)
        {
            device.CriticalThreshold = (int)_critical.Value;
            device.LowThreshold = (int)_low.Value;
            device.CautionThreshold = (int)_caution.Value;
        }
        MessageBox.Show(this, I18n.Format(TextId.AppliedThresholds, _devices.Count), I18n.Get(TextId.ApplyAllTitle),
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async void SaveClicked(object? sender, EventArgs e)
    {
        DetailValueChanged();
        if (!ThresholdsValid((int)_defaultCritical.Value, (int)_defaultLow.Value, (int)_defaultCaution.Value))
        {
            ShowThresholdError(I18n.Get(TextId.InvalidDefaultThresholds));
            return;
        }
        var invalid = _devices.FirstOrDefault(device => !device.HasValidThresholds());
        if (invalid is not null)
        {
            ShowThresholdError(I18n.Format(TextId.InvalidDeviceThresholds, invalid.DisplayName));
            return;
        }

        if (_startupStateLoaded && _startAtSignIn.Enabled &&
            _startAtSignIn.Checked != _initialStartupEnabled)
        {
            var requestedStartup = _startAtSignIn.Checked;
            if (_saveButton is not null)
                _saveButton.Enabled = false;
            var startupResult = await _startupRegistration.SetEnabledAsync(requestedStartup);
            if (_saveButton is not null)
                _saveButton.Enabled = true;
            ApplyStartupState(startupResult);
            if (requestedStartup != startupResult.IsEnabled)
            {
                var message = startupResult.State switch
                {
                    StartupRegistrationState.DisabledByUser => I18n.Get(TextId.StartupDisabledByUser),
                    StartupRegistrationState.PolicyControlled => I18n.Get(TextId.StartupControlledByPolicy),
                    _ => startupResult.ErrorMessage ?? I18n.Get(TextId.StartupUnavailable)
                };
                MessageBox.Show(this, message, I18n.Get(TextId.SettingsError),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        Result = CurrentDefaults();
        Result.OnboardingCompleted = true;
        Result.PollIntervalSeconds = (int)_pollInterval.Value;
        Result.NotificationsEnabled = _notificationsEnabled.Checked;
        Result.Devices = _devices;
        DialogResult = DialogResult.OK;
        Close();
    }

    private async Task LoadStartupStateAsync()
    {
        var result = await _startupRegistration.GetStateAsync();
        ApplyStartupState(result);
    }

    private void ApplyStartupState(StartupRegistrationResult result)
    {
        _startupStateLoaded = result.State != StartupRegistrationState.Unsupported;
        _initialStartupEnabled = result.IsEnabled;
        _startAtSignIn.Checked = result.IsEnabled;
        _startAtSignIn.Enabled = result.CanChange;
        _startAtSignInLabel.Visible = _startupStateLoaded;
        _startupPanel.Visible = _startupStateLoaded;
        _startupNote.Text = result.State switch
        {
            StartupRegistrationState.DisabledByUser => I18n.Get(TextId.StartupDisabledByUser),
            StartupRegistrationState.PolicyControlled => I18n.Get(TextId.StartupControlledByPolicy),
            _ => string.Empty
        };
        _startupNote.Visible = _startupNote.Text.Length > 0;
    }

    private static void OpenSystemSettings(string uri)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri)
            {
                UseShellExecute = true
            });
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(exception.Message, I18n.Get(TextId.SettingsError),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private AppSettings CurrentDefaults() => new()
    {
        UiLanguage = (_languageCombo.SelectedItem as LanguageChoice)?.Code ?? "system",
        DefaultCriticalThreshold = (int)_defaultCritical.Value,
        DefaultLowThreshold = (int)_defaultLow.Value,
        DefaultCautionThreshold = (int)_defaultCaution.Value
    };

    private void SetDetailControlsEnabled(bool enabled)
    {
        _monitorDevice.Enabled = enabled;
        _critical.Enabled = enabled;
        _low.Enabled = enabled;
        _caution.Enabled = enabled;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _iconPreview.Image?.Dispose();
            _windowIcon.Dispose();
        }
        base.Dispose(disposing);
    }

    private DeviceSettings? FindDevice(string id) => _devices.FirstOrDefault(device =>
        string.Equals(device.DeviceId, id, StringComparison.OrdinalIgnoreCase));

    private static NumericUpDown CreatePercentageInput() => new()
    {
        Minimum = 0,
        Maximum = 100,
        Width = 72,
        TextAlign = HorizontalAlignment.Right
    };

    private static void AddThresholdRow(TableLayoutPanel table, int row, Color color, string text, Control input)
    {
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        table.Controls.Add(new Panel { BackColor = color, Size = new Size(10, 10), Margin = new Padding(2, 12, 4, 0) }, 0, row);
        table.Controls.Add(new Label { Text = text, AutoSize = true, Margin = new Padding(4, 9, 4, 0) }, 1, row);
        table.Controls.Add(BuildUnitInput(input, "%"), 2, row);
    }

    private static Control BuildUnitInput(Control input, string unit)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        panel.Controls.Add(input);
        panel.Controls.Add(new Label { Text = unit, AutoSize = true, Margin = new Padding(4, 6, 0, 0) });
        return panel;
    }

    private static void AddSectionTitle(TableLayoutPanel table, int row, string text)
    {
        var label = new Label
        {
            Text = text,
            Font = new Font(SystemFonts.MessageBoxFont!.FontFamily, 11f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, row == 0 ? 0 : 24, 0, 10)
        };
        table.Controls.Add(label, 0, row);
        table.SetColumnSpan(label, 2);
    }

    private static void AddFormRow(TableLayoutPanel table, int row, string label, Control control)
    {
        table.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            MaximumSize = new Size(220, 0),
            Margin = new Padding(0, 8, 12, 8)
        }, 0, row);
        control.Margin = new Padding(0, 4, 0, 4);
        table.Controls.Add(control, 1, row);
    }

    private static bool ThresholdsValid(int critical, int low, int caution) =>
        critical < low && low < caution;

    private void ShowThresholdError(string? prefix = null) =>
        MessageBox.Show(this,
            $"{prefix ?? I18n.Get(TextId.InvalidThresholds)}\n{I18n.Get(TextId.ThresholdOrderInstruction)}",
            I18n.Get(TextId.SettingsError), MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private static string BandText(BatteryBand band) => band switch
    {
        BatteryBand.Critical => I18n.Get(TextId.BandCritical),
        BatteryBand.Low => I18n.Get(TextId.BandLow),
        BatteryBand.Caution => I18n.Get(TextId.BandCaution),
        BatteryBand.Good => I18n.Get(TextId.BandGood),
        _ => I18n.Get(TextId.BandUnknown)
    };

    private static Color BandColor(BatteryBand band, bool isObserved) => !isObserved
        ? SystemColors.GrayText
        : band switch
        {
            BatteryBand.Critical => Color.FromArgb(190, 35, 35),
            BatteryBand.Low => Color.FromArgb(205, 95, 10),
            BatteryBand.Caution => Color.FromArgb(25, 105, 185),
            BatteryBand.Good => Color.FromArgb(25, 135, 70),
            _ => SystemColors.GrayText
        };
}
