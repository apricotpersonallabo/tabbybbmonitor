using System.Runtime.InteropServices;

namespace BatteryTray;

internal sealed class TrayDeviceTooltip : Form
{
    private readonly Panel _content = new() { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8) };
    private readonly Func<TooltipTheme> _readTheme;
    private TooltipTheme? _theme;
    private IntPtr _themedHandle;
    private Point _anchor;

    public TrayDeviceTooltip(Func<TooltipTheme>? readTheme = null)
    {
        _readTheme = readTheme ?? TooltipTheme.ReadWindowsTheme;
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        ControlBox = false;
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Font = SystemFonts.MessageBoxFont;
        Controls.Add(_content);
        RefreshTheme();
    }

    internal void RefreshTheme()
    {
        if (IsDisposed || Disposing)
            return;
        var theme = _readTheme();
        var window = IsHandleCreated ? Handle : IntPtr.Zero;
        if (_theme == theme && _themedHandle == window)
            return;
        _theme = theme;
        _themedHandle = window;
        BackColor = _content.BackColor = _theme.Background;
        ForeColor = _content.ForeColor = _theme.Foreground;
        foreach (Control label in _content.Controls)
        {
            label.BackColor = _theme.Background;
            label.ForeColor = _theme.Foreground;
        }
        if (IsHandleCreated)
        {
            var dark = _theme.IsDark ? 1 : 0;
            _ = DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
            var border = _theme.HighContrast ? -1 : ColorTranslator.ToWin32(_theme.Border);
            _ = DwmSetWindowAttribute(Handle, 34, ref border, sizeof(int));
            var corners = _theme.HighContrast ? 1 : 3; // DONOTROUND / ROUNDSMALL
            _ = DwmSetWindowAttribute(Handle, 33, ref corners, sizeof(int));
        }
        Invalidate(true);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RefreshTheme();
    }

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        // WM_SETTINGCHANGE / WM_THEMECHANGED / WM_SYSCOLORCHANGE
        if (message.Msg is 0x001A or 0x031A or 0x0015)
            RefreshTheme();
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x08000000 | 0x00000080; // NOACTIVATE | TOOLWINDOW
            return parameters;
        }
    }

    public void ShowAt(Point anchor, IReadOnlyList<TrayDeviceLine> lines)
    {
        RefreshTheme();
        _anchor = anchor;
        SetLines(lines);
        if (lines.Count > 0)
            Show();
    }

    public void SetLines(IReadOnlyList<TrayDeviceLine> lines)
    {
        if (lines.Count == 0)
            Hide();
        var workingArea = Screen.FromPoint(_anchor).WorkingArea;
        var width = Math.Min(600, Math.Max(180, workingArea.Width - 24));
        var desired = lines.Select(line => TextRenderer.MeasureText(line.Text, Font).Width + 40).DefaultIfEmpty(240).Max();
        width = Math.Min(width, Math.Max(240, desired));
        _content.SuspendLayout();
        try
        {
            while (_content.Controls.Count > 0)
                _content.Controls[0].Dispose();
            var top = 8;
            foreach (var line in lines)
            {
                var height = TextRenderer.MeasureText(line.Text, Font, new Size(width - 40, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 8;
                _content.Controls.Add(new Label
                {
                    Text = line.Text, UseMnemonic = false, AutoSize = false,
                    BackColor = _theme!.Background, ForeColor = _theme.Foreground,
                    Location = new Point(8, top), Size = new Size(width - 40, height),
                    RightToLeft = I18n.IsRightToLeft ? RightToLeft.Yes : RightToLeft.No
                });
                top += height;
            }
            ClientSize = new Size(width, Math.Min(top + 8, Math.Max(80, workingArea.Height - 32)));
            _content.AutoScrollMinSize = new Size(0, top + 8);
            Location = ConstrainLocation(_anchor, Size, workingArea);
        }
        finally { _content.ResumeLayout(); }
    }

    internal static Point ConstrainLocation(Point anchor, Size size, Rectangle area) => new(
        Math.Clamp(anchor.X + 4, area.Left, Math.Max(area.Left, area.Right - size.Width)),
        Math.Clamp(anchor.Y - size.Height - 4, area.Top, Math.Max(area.Top, area.Bottom - size.Height)));
}

internal sealed class TrayHoverSession
{
    private Rectangle _iconArea;
    private long _startedAt;
    private bool _active;
    private bool _triggered;
    private bool _dismissed;

    public bool IsActive => _active;

    public void Observe(Point pointer, long now, Rectangle? iconArea = null)
    {
        if (_active)
            return;
        _iconArea = iconArea ?? new Rectangle(pointer.X - 20, pointer.Y - 20, 40, 40);
        _startedAt = now;
        _active = true;
        _triggered = _dismissed = false;
    }

    public bool IsWithin(Point pointer, Rectangle popup) =>
        _active && (_iconArea.Contains(pointer) || !popup.IsEmpty && Rectangle.Inflate(popup, 8, 8).Contains(pointer));

    public bool ShouldShow(Point pointer, Rectangle popup, long now, bool suppressed)
    {
        if (!IsWithin(pointer, popup))
        {
            _active = false;
            return false;
        }
        if (suppressed)
            _dismissed = true;
        if (_triggered || _dismissed || now - _startedAt < 500)
            return false;
        _triggered = true;
        return true;
    }

    public void Dismiss() => _dismissed = true;
}

internal sealed class TrayTooltipController : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 100 };
    private readonly TrayDeviceTooltip _popup = new();
    private readonly TrayHoverSession _session = new();
    private readonly Func<bool> _suppressed;
    private readonly Func<IReadOnlyList<TrayDeviceLine>> _lines;
    private readonly Action _refresh;

    public TrayTooltipController(Func<bool> suppressed, Func<IReadOnlyList<TrayDeviceLine>> lines, Action refresh)
    {
        _suppressed = suppressed;
        _lines = lines;
        _refresh = refresh;
        _timer.Tick += (_, _) => Tick();
    }

    public void ObserveMouse()
    {
        if (!_session.IsActive)
            _session.Observe(Cursor.Position, Environment.TickCount64, TrayIconArea.Find(Cursor.Position));
        _timer.Start();
    }

    private void Tick()
    {
        var pointer = Cursor.Position;
        var bounds = _popup.Visible ? _popup.Bounds : Rectangle.Empty;
        var suppressed = _suppressed();
        var show = _session.ShouldShow(pointer, bounds, Environment.TickCount64, suppressed);
        if (!_session.IsWithin(pointer, bounds) || suppressed)
        {
            _popup.Hide();
            if (!_session.IsWithin(pointer, bounds))
                _timer.Stop();
        }
        if (show)
        {
            _popup.ShowAt(pointer, _lines());
            _refresh();
        }
    }

    public void Update()
    {
        if (_popup.Visible)
            _popup.SetLines(_lines());
    }

    public void Dismiss()
    {
        _session.Dismiss();
        _popup.Hide();
    }

    public void Dispose()
    {
        _timer.Dispose();
        _popup.Dispose();
    }
}

internal static class TrayIconArea
{
    // Explorerのアクセシビリティ情報から、通知アイコンそのものの領域を取得する。
    // シェルが情報を公開しない場合だけ、ホバー開始点付近を代用する。
    public static Rectangle? Find(Point pointer)
    {
        Accessibility.IAccessible? accessible = null;
        try
        {
            if (AccessibleObjectFromPoint(pointer, out accessible, out var child) != 0 || accessible is null)
                return null;
            accessible.accLocation(out var left, out var top, out var width, out var height, child);
            var bounds = new Rectangle(left, top, width, height);
            return width is > 0 and <= 128 && height is > 0 and <= 128 && bounds.Contains(pointer) ? bounds : null;
        }
        catch (COMException) { return null; }
        finally
        {
            if (accessible is not null && Marshal.IsComObject(accessible))
                Marshal.ReleaseComObject(accessible);
        }
    }

    [DllImport("oleacc.dll")]
    private static extern int AccessibleObjectFromPoint(Point point,
        [MarshalAs(UnmanagedType.Interface)] out Accessibility.IAccessible? accessible,
        [MarshalAs(UnmanagedType.Struct)] out object child);
}
