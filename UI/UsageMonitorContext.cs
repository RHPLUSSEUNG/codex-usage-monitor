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
    private readonly ToolStripMenuItem _resetPositionMenu = new();
    private readonly ToolStripMenuItem _copyDiagnosticsMenu = new();
    private readonly ToolStripMenuItem _updateMenu = new();
    private readonly ToolStripMenuItem _uninstallMenu = new();
    private readonly ToolStripMenuItem _exitMenu = new();
    private readonly NotifyIcon _trayIcon;
    private readonly CompactBarHost _compactBar;
    private readonly Control _uiDispatcher = new();
    private readonly System.Windows.Forms.Timer _usageTimer;
    private readonly System.Windows.Forms.Timer _systemTimer;
    private readonly System.Windows.Forms.Timer _updateTimer;
    private readonly SystemUsageMonitor _systemMonitor = new();
    private readonly UpdateService _updateService = new();
    private readonly QuotaNotificationTracker _quotaNotifications = new();
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly Action? _startupReady;
    private AppSettings _settings;
    private UsageSnapshot _snapshot = UsageSnapshot.Waiting;
    private SystemUsageSnapshot _systemSnapshot = SystemUsageSnapshot.Empty;
    private CodexAppServerClient _client;
    private UpdateRelease? _availableUpdate;
    private Version? _notifiedUpdateVersion;
    private SettingsForm? _settingsForm;
    private bool _refreshing;
    private bool _checkingUpdate;
    private bool _installingUpdate;
    private bool _exiting;
    private bool _startupReadySignaled;
    private DateTimeOffset? _lastSuccessfulRefresh;

    public UsageMonitorContext(Action? startupReady = null)
    {
        _startupReady = startupReady;
        SettingsLoadResult settingsLoad = SettingsStore.LoadWithResult();
        _settings = settingsLoad.Settings;
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
        _resetPositionMenu.Click += (_, _) => _compactBar.ResetPosition();
        _copyDiagnosticsMenu.Click += (_, _) => CopyDiagnostics();
        _updateMenu.Click += async (_, _) => await HandleUpdateMenuAsync();
        _uninstallMenu.Click += (_, _) => Uninstall();
        _exitMenu.Click += (_, _) => Exit();
        menu.Items.Add(_statusMenu);
        menu.Items.Add(_refreshMenu);
        menu.Items.Add(_showBarMenu);
        menu.Items.Add(_settingsMenu);
        menu.Items.Add(_resetPositionMenu);
        menu.Items.Add(_copyDiagnosticsMenu);
        menu.Items.Add(_updateMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_uninstallMenu);
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
        _trayIcon.BalloonTipClicked += async (_, _) =>
        {
            if (_availableUpdate is not null)
                await InstallUpdateAsync();
        };

        _usageTimer = new System.Windows.Forms.Timer();
        _usageTimer.Tick += async (_, _) => await RefreshAsync();
        _systemTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _systemTimer.Tick += (_, _) => RefreshSystemUsage();
        _updateTimer = new System.Windows.Forms.Timer
        {
            Interval = (int)TimeSpan.FromHours(6).TotalMilliseconds
        };
        _updateTimer.Tick += async (_, _) => await CheckForUpdatesAsync(showResult: false);
        _systemSnapshot = _systemMonitor.Sample();
        _systemTimer.Start();
        _updateTimer.Start();
        ApplySettings(startTimer: true);
        ShowSettingsRecovery(settingsLoad);

        _ = RefreshAsync();
        _ = CheckForUpdatesAsync(showResult: false);
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
            if (string.IsNullOrWhiteSpace(_snapshot.Error))
            {
                _lastSuccessfulRefresh = _snapshot.UpdatedAt;
                ShowQuotaAlerts();
            }
            UpdateDisplay();
        }
        finally
        {
            _refreshing = false;
            SignalStartupReady();
        }
    }

    private void SignalStartupReady()
    {
        if (_startupReadySignaled)
            return;
        _startupReadySignaled = true;
        try
        {
            _startupReady?.Invoke();
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not write the post-update health signal.", exception);
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
        SystemUsageSnapshot next = _systemMonitor.Sample();
        if (RoundedSystemPercent(_systemSnapshot.CpuPercent) == RoundedSystemPercent(next.CpuPercent)
            && RoundedSystemPercent(_systemSnapshot.MemoryPercent) == RoundedSystemPercent(next.MemoryPercent))
        {
            return;
        }

        _systemSnapshot = next;
        _compactBar.Apply(_settings, _snapshot, _systemSnapshot);
    }

    private static int RoundedSystemPercent(double? value) =>
        value is null ? -1 : (int)Math.Round(value.Value);

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

    private void ShowQuotaAlerts()
    {
        IReadOnlyList<QuotaThresholdAlert> alerts = _quotaNotifications.Evaluate(
            _snapshot,
            _settings,
            DateTimeOffset.Now);
        if (alerts.Count == 0)
            return;

        _trayIcon.BalloonTipTitle = Localization.Text("QuotaAlertTitle");
        _trayIcon.BalloonTipText = string.Join(
            Environment.NewLine,
            alerts.Select(alert => Localization.Format(
                "QuotaAlertMessage",
                Localization.Text(alert.MetricKey),
                alert.RemainingPercent,
                alert.Threshold)));
        _trayIcon.ShowBalloonTip(10000);
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
        _showBarMenu.Checked = _settings.ShowCompactBar;
        SettingsStore.Save(_settings);
        UpdateDisplay();
    }

    private void ShowSettings()
    {
        if (_settingsForm is { IsDisposed: false } existingForm)
        {
            ActivateExistingWindow(existingForm);
            return;
        }

        using var form = new SettingsForm(_settings);
        _settingsForm = form;
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
            _settingsForm = null;
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
        _showBarMenu.Checked = _settings.ShowCompactBar;
        _settingsMenu.Text = Localization.Text("Settings");
        _resetPositionMenu.Text = Localization.Text("ResetPosition");
        _copyDiagnosticsMenu.Text = Localization.Text("CopyDiagnostics");
        _uninstallMenu.Text = Localization.Text("Uninstall");
        UpdateUpdateMenu();
        _exitMenu.Text = Localization.Text("Exit");
    }

    private void ShowSettingsRecovery(SettingsLoadResult result)
    {
        string? title = null;
        string? message = null;
        if (result.Status == SettingsLoadStatus.RecoveredFromBackup)
        {
            title = Localization.Text("SettingsRecoveredTitle");
            message = Localization.Text("SettingsRecoveredMessage");
        }
        else if (result.Status == SettingsLoadStatus.DefaultsAfterCorruption)
        {
            title = Localization.Text("SettingsDefaultsTitle");
            message = Localization.Text("SettingsDefaultsMessage");
        }

        if (title is null || message is null)
            return;
        if (!string.IsNullOrWhiteSpace(result.Detail))
            AppLog.Warning(message + Environment.NewLine + result.Detail);
        _trayIcon.BalloonTipTitle = title;
        _trayIcon.BalloonTipText = message;
        _trayIcon.ShowBalloonTip(10000);
    }

    private void CopyDiagnostics()
    {
        try
        {
            AppServerDiagnosticInfo server = _client.GetDiagnosticInfo();
            string displays = string.Join(
                "; ",
                Screen.AllScreens.Select(
                    (screen, index) =>
                        $"{index + 1}: bounds={screen.Bounds}, working={screen.WorkingArea}, primary={screen.Primary}"));
            string diagnostics = string.Join(Environment.NewLine, new[]
            {
                $"App version: {UpdateService.CurrentVersion}",
                $"Executable: {Environment.ProcessPath}",
                $"Codex executable: {_settings.CodexExecutable}",
                $"App-server status: {(server.IsRunning ? "Running" : "Stopped")}",
                $"App-server failures: {server.ConsecutiveFailures}",
                $"App-server next retry: {server.NextStartAttempt?.ToString("O") ?? "None"}",
                $"Last successful refresh: {_lastSuccessfulRefresh?.ToString("O") ?? "None"}",
                $"Last error: {_snapshot.Error ?? server.LastError ?? "None"}",
                $"Windows: {Environment.OSVersion}",
                $"Process architecture: {RuntimeInformation.ProcessArchitecture}",
                $"UI DPI: {_uiDispatcher.DeviceDpi}",
                $"Displays: {displays}",
                $"Settings: {SettingsStore.SettingsPath}",
                $"Log: {AppLog.LogPath}"
            });
            Clipboard.SetText(diagnostics);
            _trayIcon.BalloonTipTitle = Localization.Text("DiagnosticsCopiedTitle");
            _trayIcon.BalloonTipText = Localization.Text("DiagnosticsCopiedMessage");
            _trayIcon.ShowBalloonTip(5000);
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not copy diagnostics.", exception);
            MessageBox.Show(
                $"{Localization.Text("DiagnosticsCopyFailed")}{Environment.NewLine}{exception.Message}",
                Localization.Text("DiagnosticsCopyFailed"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private async Task HandleUpdateMenuAsync()
    {
        if (_availableUpdate is null)
            await CheckForUpdatesAsync(showResult: true);
        else
            await InstallUpdateAsync();
    }

    private async Task CheckForUpdatesAsync(bool showResult)
    {
        if (_checkingUpdate || _installingUpdate || _exiting)
            return;

        _checkingUpdate = true;
        UpdateUpdateMenu();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeCancellation.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            UpdateRelease? release = await _updateService.CheckAsync(timeout.Token);
            if (_exiting)
                return;

            _availableUpdate = release;
            if (release is null)
            {
                if (showResult)
                {
                    MessageBox.Show(
                        Localization.Text("UpToDateMessage"),
                        Localization.Text("UpToDateTitle"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                return;
            }

            if (_notifiedUpdateVersion != release.Version)
            {
                _notifiedUpdateVersion = release.Version;
                _trayIcon.BalloonTipTitle = Localization.Text("UpdateAvailableTitle");
                _trayIcon.BalloonTipText =
                    Localization.Format("UpdateAvailableMessage", release.Version.ToString(3));
                _trayIcon.ShowBalloonTip(10000);
            }
        }
        catch (OperationCanceledException) when (_exiting || _lifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (showResult && !_exiting)
            {
                MessageBox.Show(
                    $"{Localization.Text("UpdateCheckFailed")}{Environment.NewLine}{exception.Message}",
                    Localization.Text("UpdateCheckFailed"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        finally
        {
            _checkingUpdate = false;
            if (!_exiting)
                UpdateUpdateMenu();
        }
    }

    private async Task InstallUpdateAsync()
    {
        if (_availableUpdate is null || _installingUpdate || _exiting)
            return;

        _installingUpdate = true;
        UpdateUpdateMenu();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeCancellation.Token);
            timeout.CancelAfter(TimeSpan.FromMinutes(10));
            string updaterPath = await _updateService.PrepareAsync(_availableUpdate, timeout.Token);
            UpdateService.LaunchInstaller(updaterPath);
            Exit();
        }
        catch (Exception exception)
        {
            _installingUpdate = false;
            if (!_exiting)
            {
                UpdateUpdateMenu();
                MessageBox.Show(
                    $"{Localization.Text("UpdateInstallFailed")}{Environment.NewLine}{exception.Message}",
                    Localization.Text("UpdateInstallFailed"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }

    private void UpdateUpdateMenu()
    {
        _updateMenu.Enabled = !_checkingUpdate && !_installingUpdate;
        _uninstallMenu.Enabled = !_installingUpdate;
        _updateMenu.Text = _installingUpdate
            ? Localization.Text("DownloadingUpdate")
            : _checkingUpdate
                ? Localization.Text("CheckingForUpdates")
                : _availableUpdate is null
                    ? Localization.Text("CheckForUpdates")
                    : Localization.Format(
                        "UpdateToVersion",
                        _availableUpdate.Version.ToString(3));
    }

    private void Uninstall()
    {
        if (!UninstallService.IsInstalledApplication())
        {
            MessageBox.Show(
                Localization.Text("UninstallUnavailable"),
                Localization.Text("UninstallTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show(
                Localization.Text("UninstallConfirm"),
                Localization.Text("UninstallTitle"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        DialogResult settingsChoice = MessageBox.Show(
            Localization.Text("UninstallDeleteSettings"),
            Localization.Text("UninstallTitle"),
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (settingsChoice == DialogResult.Cancel)
            return;

        try
        {
            UninstallService.Launch(deleteSettings: settingsChoice == DialogResult.Yes);
            Exit();
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not start uninstall.", exception);
            MessageBox.Show(
                $"{Localization.Text("UninstallFailed")}{Environment.NewLine}{exception.Message}",
                Localization.Text("UninstallTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void Exit()
    {
        if (_exiting)
            return;
        _exiting = true;
        _lifetimeCancellation.Cancel();
        _settingsForm?.Close();
        _usageTimer.Stop();
        _systemTimer.Stop();
        _updateTimer.Stop();
        _trayIcon.Visible = false;
        _compactBar.Dispose();
        _client.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _updateService.Dispose();
        _trayIcon.Dispose();
        _usageTimer.Dispose();
        _systemTimer.Dispose();
        _updateTimer.Dispose();
        _lifetimeCancellation.Dispose();
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

    internal void ActivateSettings() => PostToUi(ShowSettings);

    private static void ActivateExistingWindow(Form form)
    {
        if (form.WindowState == FormWindowState.Minimized)
            form.WindowState = FormWindowState.Normal;

        IntPtr target = GetLastActivePopup(form.Handle);
        if (target == IntPtr.Zero)
            target = form.Handle;
        ShowWindow(target, SwRestore);
        SetForegroundWindow(target);
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

    private const int SwRestore = 9;

    [DllImport("user32.dll")]
    private static extern IntPtr GetLastActivePopup(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);
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
