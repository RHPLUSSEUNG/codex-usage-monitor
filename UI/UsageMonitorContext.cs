using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using CodexUsageMonitor.Models;
using CodexUsageMonitor.Services;

namespace CodexUsageMonitor.UI;

public sealed class UsageMonitorContext : ApplicationContext
{
    private readonly ToolStripMenuItem _statusMenu = new();
    private readonly ToolStripMenuItem _refreshMenu = new();
    private readonly ToolStripMenuItem _showBarMenu = new();
    private readonly ToolStripMenuItem _settingsMenu = new();
    private readonly ToolStripMenuItem _exitMenu = new();
    private readonly NotifyIcon _trayIcon;
    private readonly CompactBarHost _compactBar;
    private readonly Control _uiDispatcher = new();
    private readonly System.Windows.Forms.Timer _usageTimer;
    private readonly System.Windows.Forms.Timer _systemTimer;
    private readonly SystemUsageMonitor _systemMonitor = new();
    private AppSettings _settings;
    private UsageSnapshot _snapshot = UsageSnapshot.Waiting;
    private SystemUsageSnapshot _systemSnapshot = SystemUsageSnapshot.Empty;
    private CodexAppServerClient _client;
    private bool _refreshing;

    public UsageMonitorContext()
    {
        _settings = SettingsStore.Load();
        Localization.CurrentLanguage = _settings.Language;
        _client = new CodexAppServerClient(_settings.CodexExecutable);
        _uiDispatcher.CreateControl();
        _compactBar = new CompactBarHost(PostToUi);
        _compactBar.SettingsRequested += (_, _) => ShowSettings();
        _compactBar.RefreshRequested += async (_, _) => await RefreshAsync();
        _compactBar.PositionChanged += position =>
        {
            _settings.WindowPositionX = position.X;
            _settings.WindowPositionY = position.Y;
            SettingsStore.Save(_settings);
        };

        var menu = new ContextMenuStrip();
        _statusMenu.Click += (_, _) => ShowStatus();
        _refreshMenu.Click += async (_, _) => await RefreshAsync();
        _showBarMenu.Click += (_, _) => ToggleCompactBar();
        _settingsMenu.Click += (_, _) => ShowSettings();
        _exitMenu.Click += (_, _) => Exit();
        menu.Items.Add(_statusMenu);
        menu.Items.Add(_refreshMenu);
        menu.Items.Add(_showBarMenu);
        menu.Items.Add(_settingsMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exitMenu);
        ApplyLanguage();

        _trayIcon = new NotifyIcon
        {
            Visible = true,
            Text = "Codex Usage Monitor",
            ContextMenuStrip = menu,
            Icon = TrayIconFactory.Create(null, Color.Gray)
        };
        _trayIcon.MouseClick += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
                ShowStatus();
        };
        _trayIcon.DoubleClick += (_, _) => ShowSettings();

        _usageTimer = new System.Windows.Forms.Timer();
        _usageTimer.Tick += async (_, _) => await RefreshAsync();
        _systemTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _systemTimer.Tick += (_, _) => RefreshSystemUsage();
        _systemSnapshot = _systemMonitor.Sample();
        _systemTimer.Start();
        ApplySettings(startTimer: true);

        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_refreshing)
            return;
        _refreshing = true;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            _snapshot = await _client.GetUsageAsync(timeout.Token);
            UpdateDisplay();
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void UpdateDisplay()
    {
        _compactBar.Apply(_settings, _snapshot, _systemSnapshot);

        QuotaWindow? iconMetric = _settings.Weekly.Enabled ? _snapshot.Weekly : _snapshot.FiveHour;
        double? iconPercent = iconMetric is null
            ? null
            : _settings.PercentageMode == PercentageMode.Remaining
                ? iconMetric.RemainingPercent
                : iconMetric.UsedPercent;

        MetricSettings iconSettings = _settings.Weekly.Enabled ? _settings.Weekly : _settings.FiveHour;
        Color color = ParseColor(iconSettings.FillColor, Color.Gray);
        Icon? oldIcon = _trayIcon.Icon;
        _trayIcon.Icon = TrayIconFactory.Create(iconPercent, color);
        oldIcon?.Dispose();

        string fiveHour = FormatShort(_snapshot.FiveHour);
        string weekly = FormatShort(_snapshot.Weekly);
        string basis = Localization.Text(_settings.PercentageMode == PercentageMode.Remaining ? "Remaining" : "Used");
        string tooltip = $"Codex · 5H {fiveHour} · WK {weekly} ({basis})";
        _trayIcon.Text = tooltip.Length <= 127 ? tooltip : tooltip[..127];
    }

    private void RefreshSystemUsage()
    {
        _systemSnapshot = _systemMonitor.Sample();
        _compactBar.Apply(_settings, _snapshot, _systemSnapshot);
    }

    private string FormatShort(QuotaWindow? window)
    {
        if (window is null)
            return "--%";
        double percent = _settings.PercentageMode == PercentageMode.Remaining
            ? window.RemainingPercent
            : window.UsedPercent;
        return $"{Math.Round(percent):0}%";
    }

    private void ShowStatus()
    {
        string message;
        if (!string.IsNullOrWhiteSpace(_snapshot.Error))
        {
            message = _snapshot.Error + Environment.NewLine + Environment.NewLine
                + Localization.Text("LoginHint");
        }
        else
        {
            message = string.Join(Environment.NewLine, new[]
            {
                FormatStatusLine(Localization.Text("FiveHour"), _snapshot.FiveHour),
                FormatStatusLine(Localization.Text("Weekly"), _snapshot.Weekly),
                $"{Localization.Text("CpuUsage")}: {FormatSystemPercent(_systemSnapshot.CpuPercent)}",
                $"{Localization.Text("MemoryUsage")}: {FormatSystemPercent(_systemSnapshot.MemoryPercent)}",
                $"{Localization.Text("TodayTokens")}: {(_snapshot.TodayTokens?.ToString("N0") ?? Localization.Text("Unavailable"))}",
                $"{Localization.Text("LifetimeTokens")}: {(_snapshot.LifetimeTokens?.ToString("N0") ?? Localization.Text("Unavailable"))}",
                $"{Localization.Text("LastUpdated")}: {_snapshot.UpdatedAt:yyyy-MM-dd HH:mm:ss}"
            });
        }

        MessageBox.Show(message, Localization.Text("UsageTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static string FormatSystemPercent(double? value) =>
        value is null ? Localization.Text("Unavailable") : $"{value:0}%";

    private string FormatStatusLine(string title, QuotaWindow? window)
    {
        if (window is null)
            return $"{title}: {Localization.Text("Unavailable")}";
        double value = _settings.PercentageMode == PercentageMode.Remaining
            ? window.RemainingPercent
            : window.UsedPercent;
        string basis = Localization.Text(_settings.PercentageMode == PercentageMode.Remaining ? "Remaining" : "Used");
        string reset = window.ResetsAt is null
            ? Localization.Text("ResetUnknown")
            : Localization.Format("ResetAt", $"{window.ResetsAt:MM-dd HH:mm}");
        return $"{title}: {value:0}% {basis} · {reset}";
    }

    private void ToggleCompactBar()
    {
        _settings.ShowCompactBar = !_settings.ShowCompactBar;
        SettingsStore.Save(_settings);
        UpdateDisplay();
    }

    private void ShowSettings()
    {
        using var form = new SettingsForm(_settings);
        form.PreviewChanged += preview => _compactBar.Preview(preview);
        form.PresetSaved += (slot, preset) =>
        {
            if (slot == 0)
                _settings.Preset1 = preset;
            else if (slot == 1)
                _settings.Preset2 = preset;
            else
                _settings.Preset3 = preset;
            SettingsStore.Save(_settings);
        };
        _compactBar.BeginPreview();
        DialogResult result = DialogResult.Cancel;
        try
        {
            result = form.ShowDialog();
        }
        finally
        {
            _compactBar.EndPreview(result == DialogResult.OK ? form.Result : _settings);
        }

        if (result != DialogResult.OK)
            return;

        bool executableChanged = !string.Equals(
            _settings.CodexExecutable,
            form.Result.CodexExecutable,
            StringComparison.OrdinalIgnoreCase);

        form.Result.WindowPositionX = _settings.WindowPositionX;
        form.Result.WindowPositionY = _settings.WindowPositionY;
        _settings = form.Result;
        Localization.CurrentLanguage = _settings.Language;
        _compactBar.ApplyLanguage();
        ApplyLanguage();
        try
        {
            SettingsStore.Save(_settings);
            StartupService.Apply(_settings.StartWithWindows);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, Localization.Text("SettingsSaveError"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        if (executableChanged)
        {
            _client.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _client = new CodexAppServerClient(_settings.CodexExecutable);
        }

        ApplySettings(startTimer: true);
        UpdateDisplay();
        _ = RefreshAsync();
    }

    private void ApplySettings(bool startTimer)
    {
        _usageTimer.Interval = Math.Clamp(_settings.RefreshIntervalSeconds, 30, 1800) * 1000;
        if (startTimer)
            _usageTimer.Start();
        _compactBar.Apply(_settings, _snapshot, _systemSnapshot);
    }

    private void ApplyLanguage()
    {
        _statusMenu.Text = Localization.Text("Status");
        _refreshMenu.Text = Localization.Text("Refresh");
        _showBarMenu.Text = Localization.Text("ShowCompactBar");
        _settingsMenu.Text = Localization.Text("Settings");
        _exitMenu.Text = Localization.Text("Exit");
    }

    private void Exit()
    {
        _usageTimer.Stop();
        _systemTimer.Stop();
        _trayIcon.Visible = false;
        _compactBar.Dispose();
        _client.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _trayIcon.Dispose();
        _usageTimer.Dispose();
        _systemTimer.Dispose();
        _uiDispatcher.Dispose();
        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        if (_trayIcon.Visible)
            Exit();
        else
            base.ExitThreadCore();
    }

    private static Color ParseColor(string html, Color fallback)
    {
        return HexColor.ParseOrDefault(html, fallback);
    }

    private void PostToUi(Action action)
    {
        if (_uiDispatcher.IsDisposed || !_uiDispatcher.IsHandleCreated)
            return;
        if (_uiDispatcher.InvokeRequired)
            _uiDispatcher.BeginInvoke(action);
        else
            action();
    }
}

internal static class TrayIconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Create(double? percentage, Color accent)
    {
        using var bitmap = new Bitmap(32, 32);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var background = new SolidBrush(Color.FromArgb(35, 37, 43));
        using var border = new Pen(accent, 3f);
        graphics.FillEllipse(background, 2, 2, 28, 28);
        graphics.DrawEllipse(border, 3, 3, 26, 26);

        string text = percentage is null ? "--" : $"{Math.Clamp((int)Math.Round(percentage.Value), 0, 100)}";
        float fontSize = text.Length switch { 1 => 12f, 2 => 10f, _ => 8f };
        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.White);
        SizeF size = graphics.MeasureString(text, font);
        graphics.DrawString(text, font, brush, (32 - size.Width) / 2f, (32 - size.Height) / 2f - 1);

        IntPtr handle = bitmap.GetHicon();
        try
        {
            using Icon temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }
}
