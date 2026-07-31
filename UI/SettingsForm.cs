using System.Diagnostics;
using CodexUsageMonitor.Models;
using CodexUsageMonitor.Services;

namespace CodexUsageMonitor.UI;

public sealed class SettingsForm : Form
{
    private static readonly CompactBarStyle[] StyleValues =
    [
        CompactBarStyle.DarkMinimal,
        CompactBarStyle.LabelBoxes,
        CompactBarStyle.NeonGlow,
        CompactBarStyle.Light,
        CompactBarStyle.Cards,
        CompactBarStyle.CircularGauges,
        CompactBarStyle.CompactRows,
        CompactBarStyle.RoundedCapsules,
        CompactBarStyle.Gradient,
        CompactBarStyle.MinimalIcons
    ];

    private readonly AppSettings _draft;
    private readonly List<ComboBox> _presentationCombos = [];
    private readonly List<Action> _colorButtonLanguageUpdates = [];
    private readonly List<Action> _refreshDraftEditors = [];
    private readonly List<Button> _presetLoadButtons = [];
    private AppLanguage _displayLanguage;
    private bool _updatingLanguage;
    private bool _loadingControls;
    private readonly CheckBox _showCompactBar = new() { AutoSize = true };
    private readonly CheckBox _startWithWindows = new() { AutoSize = true };
    private readonly CheckBox _quotaNotifications = new() { AutoSize = true };
    private readonly CheckBox _quietHours = new() { AutoSize = true };
    private readonly ComboBox _language = NewCombo();
    private readonly ComboBox _compactBarStyle = NewCombo();
    private readonly ComboBox _themeVariant = NewCombo();
    private readonly ComboBox _percentageMode = NewCombo();
    private readonly NumericUpDown _refreshSeconds = new SettingsNumericUpDown { Minimum = 30, Maximum = 1800, Increment = 30, Width = 90 };
    private readonly NumericUpDown _quietHoursStart = new SettingsNumericUpDown { Minimum = 0, Maximum = 23, Width = 90 };
    private readonly NumericUpDown _quietHoursEnd = new SettingsNumericUpDown { Minimum = 0, Maximum = 23, Width = 90 };
    private readonly TextBox _codexPath = new() { Width = 280 };
    private readonly Label _aboutDescription = new() { AutoSize = true, MaximumSize = new Size(460, 0) };
    private readonly Label _versionLabel = new() { AutoSize = true };
    private readonly Label _authorLabel = new() { AutoSize = true };
    private readonly Label _licenseLabel = new() { AutoSize = true };
    private readonly LinkLabel _repositoryLink = new() { AutoSize = true };
    private readonly Button _diagnosticsButton = new() { AutoSize = true };
    private readonly Button _updateButton = new() { AutoSize = true };
    private readonly Button _uninstallButton = new() { AutoSize = true };
    private readonly Label _managementStatus = new() { AutoSize = true, MaximumSize = new Size(460, 0), ForeColor = Color.DimGray };
    private readonly System.Windows.Forms.Timer _previewTimer = new() { Interval = 33 };
    private AppSettings? _pendingPreview;
    private bool _checkingUpdate;
    private bool _installingUpdate;
    private Version? _availableUpdateVersion;
    private bool _canUninstall;

    public AppSettings Result => _draft;
    public event Action<AppSettings>? PreviewChanged;
    public event Action<int, CompactBarPreset>? PresetSaved;
    public event EventHandler? DiagnosticsRequested;
    public event EventHandler? UpdateRequested;
    public event EventHandler? UninstallRequested;

    public SettingsForm(AppSettings settings)
    {
        _draft = settings.Copy();
        _displayLanguage = settings.Language;
        Text = T("SettingsTitle");
        Font = new Font("Segoe UI", 9f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(540, 650);

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(14, 5) };
        var generalTab = CreateTab("GeneralTab");
        var appearanceTab = CreateTab("AppearanceTab");
        var metricsTab = CreateTab("MetricsTab");
        var aboutTab = CreateTab("AboutTab");
        tabs.TabPages.Add(generalTab);
        tabs.TabPages.Add(appearanceTab);
        tabs.TabPages.Add(metricsTab);
        tabs.TabPages.Add(aboutTab);

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8, 8, 8, 0)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        Control presetGroup = CreatePresetGroup();
        mainLayout.Controls.Add(presetGroup, 0, 0);
        mainLayout.Controls.Add(tabs, 0, 1);
        Controls.Add(mainLayout);
        tabs.SelectedIndexChanged += (_, _) => presetGroup.Visible = tabs.SelectedTab != aboutTab;

        var generalContent = CreateTabContent(generalTab, 3);
        var appearanceContent = CreateTabContent(appearanceTab, 2);
        var metricsContent = CreateTabContent(metricsTab, 4);
        var aboutContent = CreateTabContent(aboutTab, 2);

        SetLocalizationKey(_showCompactBar, "ShowCompactBar");
        SetLocalizationKey(_startWithWindows, "StartWithWindows");
        SetLocalizationKey(_quotaNotifications, "QuotaNotifications");
        SetLocalizationKey(_quietHours, "QuietHours");
        var general = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Dock = DockStyle.Top };
        general.Controls.Add(_showCompactBar);
        general.Controls.Add(_startWithWindows);
        general.Controls.Add(_quotaNotifications);
        general.Controls.Add(_quietHours);
        generalContent.Controls.Add(general, 0, 0);

        _language.Items.AddRange(["English", "한국어", "中文", "日本語"]);
        _compactBarStyle.Items.AddRange(StyleNames());
        _themeVariant.Items.AddRange([T("DarkTheme"), T("LightTheme")]);
        _percentageMode.Items.AddRange([T("RemainingPercent"), T("UsedPercent")]);
        var behavior = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 12, 0, 8), Dock = DockStyle.Top };
        AddBehaviorRow(behavior, "Language", _language, 0);
        AddBehaviorRow(behavior, "DisplayBasis", _percentageMode, 1);
        AddBehaviorRow(behavior, "RefreshSeconds", _refreshSeconds, 2);
        AddBehaviorRow(behavior, "CodexExecutable", _codexPath, 3);
        AddBehaviorRow(behavior, "QuietHoursStart", _quietHoursStart, 4);
        AddBehaviorRow(behavior, "QuietHoursEnd", _quietHoursEnd, 5);
        generalContent.Controls.Add(behavior, 0, 1);

        var note = new Label { AutoSize = true, MaximumSize = new Size(490, 0), ForeColor = Color.DimGray };
        SetLocalizationKey(note, "SettingsNote");
        generalContent.Controls.Add(note, 0, 2);

        var styleLayout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Top, Margin = new Padding(0, 4, 0, 10) };
        AddBehaviorRow(styleLayout, "CompactBarStyle", _compactBarStyle, 0);
        AddBehaviorRow(styleLayout, "ThemeVariant", _themeVariant, 1);
        appearanceContent.Controls.Add(styleLayout, 0, 0);
        appearanceContent.Controls.Add(CreateBackgroundGroup(), 0, 1);

        metricsContent.Controls.Add(CreateMetricGroup("FiveHourLimit", _draft.FiveHour), 0, 0);
        metricsContent.Controls.Add(CreateMetricGroup("WeeklyLimit", _draft.Weekly), 0, 1);
        metricsContent.Controls.Add(CreateMetricGroup("CpuUsage", _draft.Cpu), 0, 2);
        metricsContent.Controls.Add(CreateMetricGroup("MemoryUsage", _draft.Memory), 0, 3);

        aboutContent.Controls.Add(CreateAboutGroup(), 0, 0);
        aboutContent.Controls.Add(CreateManagementGroup(), 0, 1);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
        var save = new Button { AutoSize = true };
        var cancel = new Button { DialogResult = DialogResult.Cancel, AutoSize = true };
        SetLocalizationKey(save, "Save");
        SetLocalizationKey(cancel, "Cancel");
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;

        _showCompactBar.Checked = settings.ShowCompactBar;
        _startWithWindows.Checked = settings.StartWithWindows;
        _quotaNotifications.Checked = settings.EnableQuotaNotifications;
        _quietHours.Checked = settings.QuietHoursEnabled;
        _language.SelectedIndex = (int)settings.Language;
        _compactBarStyle.SelectedIndex = StyleIndex(settings.CompactBarStyle);
        _themeVariant.SelectedIndex = settings.ThemeVariant == ThemeVariant.Light ? 1 : 0;
        _percentageMode.SelectedIndex = settings.PercentageMode == PercentageMode.Remaining ? 0 : 1;
        _refreshSeconds.Value = Math.Clamp(settings.RefreshIntervalSeconds, 30, 1800);
        _quietHoursStart.Value = Math.Clamp(settings.QuietHoursStart, 0, 23);
        _quietHoursEnd.Value = Math.Clamp(settings.QuietHoursEnd, 0, 23);
        UpdateQuietHoursControls();
        _codexPath.Text = settings.CodexExecutable;
        save.Click += (_, _) => SaveAndClose();
        _language.SelectedIndexChanged += (_, _) => ChangeLanguage();
        _previewTimer.Tick += (_, _) => FlushPreview();
        _repositoryLink.LinkClicked += (_, _) => OpenUrl(
            "https://github.com/RHPLUSSEUNG/codex-usage-monitor");
        _diagnosticsButton.Click += (_, _) => DiagnosticsRequested?.Invoke(this, EventArgs.Empty);
        _updateButton.Click += (_, _) => UpdateRequested?.Invoke(this, EventArgs.Empty);
        _uninstallButton.Click += (_, _) => UninstallRequested?.Invoke(this, EventArgs.Empty);
        FormClosed += (_, _) =>
        {
            _previewTimer.Stop();
            _previewTimer.Dispose();
        };
        _showCompactBar.CheckedChanged += (_, _) =>
        {
            if (_loadingControls)
                return;
            _draft.ShowCompactBar = _showCompactBar.Checked;
            RaisePreview();
        };
        _quietHours.CheckedChanged += (_, _) => UpdateQuietHoursControls();
        _compactBarStyle.SelectedIndexChanged += (_, _) =>
        {
            if (_loadingControls || _updatingLanguage || _compactBarStyle.SelectedIndex < 0)
                return;
            _draft.CompactBarStyle = StyleValues[_compactBarStyle.SelectedIndex];
            _draft.BackgroundColor = CompactBarTheme.DefaultBackground(_draft.CompactBarStyle, _draft.ThemeVariant);
            RefreshControlsFromDraft();
            RaisePreview();
        };
        _themeVariant.SelectedIndexChanged += (_, _) =>
        {
            if (_loadingControls || _updatingLanguage || _themeVariant.SelectedIndex < 0)
                return;
            _draft.ThemeVariant = _themeVariant.SelectedIndex == 1 ? ThemeVariant.Light : ThemeVariant.Dark;
            _draft.BackgroundColor = CompactBarTheme.DefaultBackground(_draft.CompactBarStyle, _draft.ThemeVariant);
            RefreshControlsFromDraft();
            RaisePreview();
        };
        _percentageMode.SelectedIndexChanged += (_, _) =>
        {
            if (_loadingControls || _updatingLanguage || _percentageMode.SelectedIndex < 0)
                return;
            _draft.PercentageMode = _percentageMode.SelectedIndex == 0
                ? PercentageMode.Remaining
                : PercentageMode.Used;
            RaisePreview();
        };
        UpdateAboutText();
        UpdateManagementControls();
    }

    private TabPage CreateTab(string localizationKey)
    {
        var tab = new TabPage { Padding = new Padding(0), AutoScroll = true };
        SetLocalizationKey(tab, localizationKey);
        return tab;
    }

    private static TableLayoutPanel CreateTabContent(TabPage tab, int rows)
    {
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = rows,
            Padding = new Padding(16),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        for (int index = 0; index < rows; index++)
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tab.Controls.Add(content);
        return content;
    }

    private void AddBehaviorRow(TableLayoutPanel layout, string labelKey, Control control, int row)
    {
        var label = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
        SetLocalizationKey(label, labelKey);
        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private GroupBox CreateMetricGroup(string titleKey, MetricSettings metric)
    {
        var group = new GroupBox { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(10) };
        SetLocalizationKey(group, titleKey);
        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
        group.Controls.Add(layout);
        var enabled = new CheckBox { Checked = metric.Enabled, AutoSize = true };
        SetLocalizationKey(enabled, "Show");
        var presentation = NewCombo();
        presentation.Items.AddRange([T("PercentOnly"), T("BarOnly"), T("PercentAndBar")]);
        _presentationCombos.Add(presentation);
        presentation.SelectedIndex = metric.Presentation switch { MetricPresentation.PercentOnly => 0, MetricPresentation.BarOnly => 1, _ => 2 };
        layout.Controls.Add(enabled, 0, 0);
        layout.SetColumnSpan(enabled, 2);
        var presentationLabel = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
        SetLocalizationKey(presentationLabel, "Presentation");
        layout.Controls.Add(presentationLabel, 0, 1);
        layout.Controls.Add(presentation, 1, 1);
        Control fillEditor = CreateColorButton("FillColor", () => metric.FillColor, value => metric.FillColor = value);
        Control trackEditor = CreateColorButton("TrackColor", () => metric.TrackColor, value => metric.TrackColor = value);
        layout.Controls.Add(fillEditor, 0, 2);
        layout.SetColumnSpan(fillEditor, 2);
        layout.Controls.Add(trackEditor, 0, 3);
        layout.SetColumnSpan(trackEditor, 2);
        _refreshDraftEditors.Add(() =>
        {
            enabled.Checked = metric.Enabled;
            presentation.SelectedIndex = metric.Presentation switch
            {
                MetricPresentation.PercentOnly => 0,
                MetricPresentation.BarOnly => 1,
                _ => 2
            };
        });
        enabled.CheckedChanged += (_, _) =>
        {
            if (_loadingControls)
                return;
            metric.Enabled = enabled.Checked;
            RaisePreview();
        };
        presentation.SelectedIndexChanged += (_, _) =>
        {
            if (_updatingLanguage || _loadingControls)
                return;
            metric.Presentation = presentation.SelectedIndex switch
            {
                0 => MetricPresentation.PercentOnly,
                1 => MetricPresentation.BarOnly,
                _ => MetricPresentation.PercentAndBar
            };
            RaisePreview();
        };
        return group;
    }

    private GroupBox CreateBackgroundGroup()
    {
        var group = new GroupBox { Text = "Compact Bar", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(10) };
        group.Controls.Add(CreateColorButton("RectangleBackground", () => _draft.BackgroundColor, value => _draft.BackgroundColor = value));
        return group;
    }

    private GroupBox CreateAboutGroup()
    {
        var group = new GroupBox { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(12) };
        SetLocalizationKey(group, "About");
        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        var title = new Label
        {
            Text = "Codex Usage Monitor",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 14f, FontStyle.Bold)
        };
        layout.Controls.Add(title);
        layout.Controls.Add(_aboutDescription);
        layout.Controls.Add(_versionLabel);
        layout.Controls.Add(_authorLabel);
        layout.Controls.Add(_repositoryLink);
        layout.Controls.Add(_licenseLabel);
        group.Controls.Add(layout);
        return group;
    }

    private GroupBox CreateManagementGroup()
    {
        var group = new GroupBox { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(12) };
        SetLocalizationKey(group, "ManagementActions");
        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };
        buttons.Controls.Add(_diagnosticsButton);
        buttons.Controls.Add(_updateButton);
        buttons.Controls.Add(_uninstallButton);
        layout.Controls.Add(buttons);
        layout.Controls.Add(_managementStatus);
        group.Controls.Add(layout);
        return group;
    }

    private GroupBox CreatePresetGroup()
    {
        var group = new GroupBox { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(10) };
        SetLocalizationKey(group, "Presets");
        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Dock = DockStyle.Fill };
        group.Controls.Add(layout);

        for (int slot = 0; slot < 3; slot++)
        {
            int capturedSlot = slot;
            var label = new Label { AutoSize = true, Width = 90, Anchor = AnchorStyles.Left };
            SetLocalizationKey(label, $"Preset{slot + 1}");
            var load = new Button { AutoSize = true };
            var save = new Button { AutoSize = true };
            SetLocalizationKey(load, "LoadPreset");
            SetLocalizationKey(save, "SavePreset");
            load.Click += (_, _) => LoadPreset(capturedSlot);
            save.Click += (_, _) => SavePreset(capturedSlot);
            layout.Controls.Add(label, 0, slot);
            layout.Controls.Add(load, 1, slot);
            layout.Controls.Add(save, 2, slot);
            _presetLoadButtons.Add(load);
            _refreshDraftEditors.Add(() => load.Enabled = GetPreset(capturedSlot) is not null);
        }

        RefreshPresetButtons();
        return group;
    }

    private Control CreateColorButton(string labelKey, Func<string> getter, Action<string> setter)
    {
        var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
        Color selected = HexColor.ParseOrDefault(getter(), Color.Gray);
        var choose = new Button { Width = 185, Height = 30, UseVisualStyleBackColor = false };
        void UpdateButton()
        {
            choose.BackColor = Color.FromArgb(255, selected.R, selected.G, selected.B);
            double luminance = selected.R * 0.299 + selected.G * 0.587 + selected.B * 0.114;
            choose.ForeColor = luminance >= 150d ? Color.Black : Color.White;
            choose.Text = Localization.Format(_displayLanguage, "ChooseColor", selected.A);
        }
        _colorButtonLanguageUpdates.Add(UpdateButton);
        _refreshDraftEditors.Add(() =>
        {
            selected = HexColor.ParseOrDefault(getter(), Color.Gray);
            UpdateButton();
        });
        choose.Click += (_, _) =>
        {
            using var dialog = new ColorPickerDialog(selected, _displayLanguage);
            Color original = selected;
            dialog.SelectedColorChanged += (_, _) =>
            {
                selected = dialog.SelectedColor;
                setter(HexColor.Format(selected, includeAlpha: true));
                UpdateButton();
                RaisePreview();
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                selected = original;
                setter(HexColor.Format(selected, includeAlpha: true));
                UpdateButton();
                RaisePreview();
                return;
            }
            selected = dialog.SelectedColor;
            setter(HexColor.Format(selected, includeAlpha: true));
            UpdateButton();
            RaisePreview();
        };
        var label = new Label { AutoSize = true, Width = 95, Anchor = AnchorStyles.Left };
        SetLocalizationKey(label, labelKey);
        row.Controls.Add(label);
        row.Controls.Add(choose);
        UpdateButton();
        return row;
    }

    private void SaveAndClose()
    {
        SyncGeneralControlsToDraft();
        _draft.StartWithWindows = _startWithWindows.Checked;
        _draft.Language = (AppLanguage)Math.Max(0, _language.SelectedIndex);
        _draft.RefreshIntervalSeconds = (int)_refreshSeconds.Value;
        _draft.EnableQuotaNotifications = _quotaNotifications.Checked;
        _draft.QuietHoursEnabled = _quietHours.Checked;
        _draft.QuietHoursStart = (int)_quietHoursStart.Value;
        _draft.QuietHoursEnd = (int)_quietHoursEnd.Value;
        _draft.CodexExecutable = string.IsNullOrWhiteSpace(_codexPath.Text) ? "codex" : _codexPath.Text.Trim();
        DialogResult = DialogResult.OK;
        Close();
    }

    private void ChangeLanguage()
    {
        if (_updatingLanguage || _language.SelectedIndex < 0)
            return;
        _draft.Language = (AppLanguage)_language.SelectedIndex;
        _displayLanguage = _draft.Language;
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        _updatingLanguage = true;
        try
        {
            Text = T("SettingsTitle");
            ApplyLocalizedText(this);
            ReplaceItems(_compactBarStyle, StyleNames());
            ReplaceItems(_themeVariant, T("DarkTheme"), T("LightTheme"));
            ReplaceItems(_percentageMode, T("RemainingPercent"), T("UsedPercent"));
            foreach (ComboBox presentation in _presentationCombos)
                ReplaceItems(presentation, T("PercentOnly"), T("BarOnly"), T("PercentAndBar"));
            foreach (Action update in _colorButtonLanguageUpdates)
                update();
            UpdateAboutText();
            UpdateManagementControls();
        }
        finally
        {
            _updatingLanguage = false;
        }
    }

    private void ApplyLocalizedText(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control.Tag is string key)
                control.Text = T(key);
            ApplyLocalizedText(control);
        }
    }

    private static void ReplaceItems(ComboBox combo, params string[] items)
    {
        int selectedIndex = combo.SelectedIndex;
        combo.BeginUpdate();
        combo.Items.Clear();
        combo.Items.AddRange(items);
        combo.SelectedIndex = Math.Clamp(selectedIndex, 0, items.Length - 1);
        combo.EndUpdate();
    }

    private void SetLocalizationKey(Control control, string key)
    {
        control.Tag = key;
        control.Text = T(key);
    }

    private string T(string key) => Localization.Text(_displayLanguage, key);

    public void SetManagementState(
        bool checkingUpdate,
        bool installingUpdate,
        Version? availableUpdateVersion,
        bool canUninstall)
    {
        _checkingUpdate = checkingUpdate;
        _installingUpdate = installingUpdate;
        _availableUpdateVersion = availableUpdateVersion;
        _canUninstall = canUninstall;
        UpdateManagementControls();
    }

    public void ShowManagementMessage(string localizationKey)
    {
        _managementStatus.Text = T(localizationKey);
    }

    private void UpdateAboutText()
    {
        _aboutDescription.Text = T("AboutDescription");
        _versionLabel.Text = Localization.Format(
            _displayLanguage,
            "VersionLabel",
            UpdateService.CurrentVersion.ToString(3));
        _authorLabel.Text = Localization.Format(_displayLanguage, "AuthorLabel", "RHPLUSSEUNG");
        _repositoryLink.Text = T("Repository");
        _licenseLabel.Text = Localization.Format(_displayLanguage, "LicenseLabel", "MIT");
    }

    private void UpdateManagementControls()
    {
        _diagnosticsButton.Text = T("CopyDiagnostics");
        _diagnosticsButton.Enabled = !_installingUpdate;
        _updateButton.Enabled = !_checkingUpdate && !_installingUpdate;
        _updateButton.Text = _installingUpdate
            ? T("DownloadingUpdate")
            : _checkingUpdate
                ? T("CheckingForUpdates")
                : _availableUpdateVersion is null
                    ? T("CheckForUpdates")
                    : Localization.Format(
                        _displayLanguage,
                        "UpdateToVersion",
                        _availableUpdateVersion.ToString(3));
        _uninstallButton.Text = T("Uninstall");
        _uninstallButton.Enabled = !_installingUpdate && _canUninstall;
        _managementStatus.Text = _availableUpdateVersion is null
            ? Localization.Format(
                _displayLanguage,
                "CurrentVersionLabel",
                UpdateService.CurrentVersion.ToString(3))
            : Localization.Format(
                _displayLanguage,
                "UpdateAvailableInline",
                _availableUpdateVersion.ToString(3));
    }

    private static void OpenUrl(string url)
    {
        try
        {
            _ = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // The repository URL remains visible and can still be copied.
        }
    }

    private void SavePreset(int slot)
    {
        SyncGeneralControlsToDraft();
        CompactBarPreset preset = CompactBarPreset.Capture(_draft);
        SetPreset(slot, preset);
        RefreshPresetButtons();
        PresetSaved?.Invoke(slot, preset.Copy());
    }

    private void LoadPreset(int slot)
    {
        CompactBarPreset? preset = GetPreset(slot);
        if (preset is null)
            return;
        preset.ApplyTo(_draft);
        RefreshControlsFromDraft();
        RaisePreview();
    }

    private CompactBarPreset? GetPreset(int slot) => slot switch
    {
        0 => _draft.Preset1,
        1 => _draft.Preset2,
        _ => _draft.Preset3
    };

    private void SetPreset(int slot, CompactBarPreset preset)
    {
        if (slot == 0)
            _draft.Preset1 = preset;
        else if (slot == 1)
            _draft.Preset2 = preset;
        else
            _draft.Preset3 = preset;
    }

    private void RefreshPresetButtons()
    {
        for (int slot = 0; slot < _presetLoadButtons.Count; slot++)
            _presetLoadButtons[slot].Enabled = GetPreset(slot) is not null;
    }

    private void RefreshControlsFromDraft()
    {
        _loadingControls = true;
        try
        {
            _showCompactBar.Checked = _draft.ShowCompactBar;
            _compactBarStyle.SelectedIndex = StyleIndex(_draft.CompactBarStyle);
            _themeVariant.SelectedIndex = _draft.ThemeVariant == ThemeVariant.Light ? 1 : 0;
            _percentageMode.SelectedIndex = _draft.PercentageMode == PercentageMode.Remaining ? 0 : 1;
            foreach (Action refresh in _refreshDraftEditors)
                refresh();
        }
        finally
        {
            _loadingControls = false;
        }
    }

    private void SyncGeneralControlsToDraft()
    {
        _draft.ShowCompactBar = _showCompactBar.Checked;
        if (_compactBarStyle.SelectedIndex >= 0)
            _draft.CompactBarStyle = StyleValues[_compactBarStyle.SelectedIndex];
        _draft.ThemeVariant = _themeVariant.SelectedIndex == 1 ? ThemeVariant.Light : ThemeVariant.Dark;
        _draft.PercentageMode = _percentageMode.SelectedIndex == 0
            ? PercentageMode.Remaining
            : PercentageMode.Used;
    }

    private void UpdateQuietHoursControls()
    {
        _quietHoursStart.Enabled = _quietHours.Checked;
        _quietHoursEnd.Enabled = _quietHours.Checked;
    }

    private void RaisePreview()
    {
        _pendingPreview = _draft.Copy();
        if (!_previewTimer.Enabled)
            _previewTimer.Start();
    }

    private void FlushPreview()
    {
        _previewTimer.Stop();
        AppSettings? preview = _pendingPreview;
        _pendingPreview = null;
        if (preview is not null)
            PreviewChanged?.Invoke(preview);
    }

    private string[] StyleNames() =>
    [
        T("DarkMinimalStyle"),
        T("LabelBoxStyle"),
        T("NeonGlowStyle"),
        T("LightStyle"),
        T("CardsStyle"),
        T("CircularGaugesStyle"),
        T("CompactRowsStyle"),
        T("RoundedCapsulesStyle"),
        T("GradientStyle"),
        T("MinimalIconsStyle")
    ];

    private static int StyleIndex(CompactBarStyle style)
    {
        int index = Array.IndexOf(StyleValues, style);
        return index < 0 ? 0 : index;
    }

    private static ComboBox NewCombo() => new SettingsComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };

    private sealed class SettingsComboBox : ComboBox
    {
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (DroppedDown)
            {
                base.OnMouseWheel(e);
                return;
            }

            if (e is HandledMouseEventArgs handled)
                handled.Handled = true;

            Control? current = Parent;
            while (current is not null && current is not ScrollableControl { AutoScroll: true })
                current = current.Parent;
            if (current is not ScrollableControl scrollHost)
                return;

            int lines = Math.Max(1, SystemInformation.MouseWheelScrollLines);
            int currentY = -scrollHost.AutoScrollPosition.Y;
            int nextY = Math.Max(0, currentY - Math.Sign(e.Delta) * lines * 18);
            scrollHost.AutoScrollPosition = new Point(-scrollHost.AutoScrollPosition.X, nextY);
        }
    }

    private sealed class SettingsNumericUpDown : NumericUpDown
    {
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (e is HandledMouseEventArgs handled)
                handled.Handled = true;
        }
    }
}
