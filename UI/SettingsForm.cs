using System.Diagnostics;
using CodexUsageMonitor.Models;
using CodexUsageMonitor.Services;

namespace CodexUsageMonitor.UI;

public sealed partial class SettingsForm : Form
{
    private static readonly CompactBarStyle[] StyleValues =
    [
        CompactBarStyle.Light,
        CompactBarStyle.LabelBoxes,
        CompactBarStyle.NeonGlow,
        CompactBarStyle.CompactRows,
        CompactBarStyle.Gradient,
        CompactBarStyle.MinimalIcons
    ];
    private static readonly CodexPalette[] PaletteValues = CompactBarPaletteCatalog.Values;

    private readonly AppSettings _draft;
    private readonly List<SettingsComboBox> _presentationCombos = [];
    private readonly List<Action> _colorButtonLanguageUpdates = [];
    private readonly List<Action> _refreshDraftEditors = [];
    private AppLanguage _displayLanguage;
    private bool _updatingLanguage;
    private bool _loadingControls;
    private readonly CheckBox _showCompactBar = new SettingsToggle() { AutoSize = true };
    private readonly CheckBox _startWithWindows = new SettingsToggle() { AutoSize = true };
    private readonly CheckBox _quotaNotifications = new SettingsToggle() { AutoSize = true };
    private readonly CheckBox _quietHours = new SettingsToggle() { AutoSize = true };
    private readonly SettingsComboBox _language = NewCombo();
    private readonly SettingsComboBox _compactBarStyle = NewCombo();
    private readonly SettingsComboBox _codexPalette = NewCombo();
    private readonly SettingsNumericUpDown _fiveHourNotificationPercent = new() { Minimum = 1, Maximum = 99, Width = 80 };
    private readonly SettingsNumericUpDown _weeklyNotificationPercent = new() { Minimum = 1, Maximum = 99, Width = 80 };
    private readonly SettingsComboBox _themeVariant = NewCombo();
    private readonly SettingsComboBox _percentageMode = NewCombo();
    private readonly SettingsComboBox _presetSlot = new() { Width = 125 };
    private readonly Button _presetLoadButton = new SettingsButton() { AutoSize = true };
    private readonly SettingsNumericUpDown _compactBarScale = new() { Minimum = 50, Maximum = 200, Increment = 10, Width = 72 };
    private readonly TrackBar _compactBarScaleSlider = new SettingsTrackBar { Minimum = 50, Maximum = 200, TickFrequency = 10, SmallChange = 10, LargeChange = 20, Width = 155 };
    private readonly SettingsNumericUpDown _refreshSeconds = new() { Minimum = 30, Maximum = 1800, Increment = 30, Width = 90 };
    private readonly SettingsNumericUpDown _quietHoursStart = new() { Minimum = 0, Maximum = 23, Width = 90 };
    private readonly SettingsNumericUpDown _quietHoursEnd = new() { Minimum = 0, Maximum = 23, Width = 90 };
    private readonly TextBox _codexPath = new() { Width = 420 };
    private readonly Label _aboutDescription = new() { AutoSize = true, MaximumSize = new Size(460, 0) };
    private readonly Label _versionLabel = new() { AutoSize = true };
    private readonly Label _authorLabel = new() { AutoSize = true };
    private readonly Label _licenseLabel = new() { AutoSize = true };
    private readonly LinkLabel _repositoryLink = new() { AutoSize = true };
    private readonly Button _diagnosticsButton = new SettingsButton() { AutoSize = true };
    private readonly Button _updateButton = new SettingsButton() { AutoSize = true };
    private readonly Button _cancelManagementButton = new SettingsButton() { AutoSize = true, Visible = false };
    private readonly Button _uninstallButton = new SettingsButton() { AutoSize = true };
    private readonly Label _managementStatus = new() { AutoSize = true, MaximumSize = new Size(460, 0), ForeColor = Color.DimGray };
    private readonly ProgressBar _managementProgress = new() { Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 25, Width = 300, Visible = false };
    private readonly Label _presetStatus = new() { AutoSize = true, ForeColor = Color.DimGray };
    private readonly System.Windows.Forms.Timer _previewTimer = new() { Interval = 33 };
    private readonly TabControl _tabs = new AppearanceTabs() { Dock = DockStyle.Fill, Padding = new Point(14, 5) };
    private TabPage? _displayTab;
    private AppSettings? _pendingPreview;
    private bool _checkingUpdate;
    private bool _installingUpdate;
    private Version? _availableUpdateVersion;
    private bool _canUninstall;
    private ThemeVariant[] _availableThemeValues = [];

    public AppSettings Result => _draft;
    internal AppLanguage DisplayLanguage => _displayLanguage;
    public event Action<AppSettings>? PreviewChanged;
    public event Action<int, CompactBarPreset>? PresetSaved;
    public event EventHandler? DiagnosticsRequested;
    public event EventHandler? UpdateRequested;
    public event EventHandler? UpdateCancellationRequested;
    public event EventHandler? UninstallRequested;

    public SettingsForm(AppSettings settings)
    {
        SuspendLayout();
        _draft = settings.Copy();
        _displayLanguage = settings.Language;
        Text = T("SettingsTitle");
        Font = new Font("Segoe UI", 10f);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        StartPosition = FormStartPosition.Manual;
        MinimumSize = new Size(820, 640);
        ClientSize = new Size(1040, 900);

        var generalTab = CreateTab("GeneralTab");
        var displayTab = CreateTab("DisplayTab");
        _displayTab = displayTab;
        var aboutTab = CreateTab("AboutTab");
        _tabs.TabPages.Add(generalTab);
        _tabs.TabPages.Add(displayTab);
        _tabs.TabPages.Add(aboutTab);

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8, 8, 8, 0)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.Controls.Add(_tabs, 0, 0);
        Controls.Add(mainLayout);

        var generalContent = CreateTabContent(generalTab, 5);
        var displayContent = CreateTabContent(displayTab, 8);
        var aboutContent = CreateTabContent(aboutTab, 2);

        SetLocalizationKey(_showCompactBar, "ShowCompactBar");
        SetLocalizationKey(_startWithWindows, "StartWithWindows");
        var general = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Dock = DockStyle.Top };
        general.Controls.Add(_showCompactBar);
        general.Controls.Add(_startWithWindows);
        generalContent.Controls.Add(general);

        _language.Items.AddRange(["English", "한국어", "中文", "日本語"]);
        _compactBarStyle.Items.AddRange(StyleNames());
        _codexPalette.Items.AddRange(PaletteNames());
        UpdateThemeVariantItems(settings.CodexPalette, settings.ThemeVariant);
        _percentageMode.Items.AddRange([T("RemainingPercent"), T("UsedPercent")]);
        _presetSlot.Items.AddRange(PresetNames());
        _presetSlot.SelectedIndex = 0;
        var behavior = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 12, 0, 8), Dock = DockStyle.Top };
        AddBehaviorRow(behavior, "Language", _language, 0);
        AddBehaviorRow(behavior, "RefreshSeconds", _refreshSeconds, 1);
        generalContent.Controls.Add(behavior);

        generalContent.Controls.Add(CreateNotificationGroup());
        generalContent.Controls.Add(CreateAdvancedGroup());

        var note = new Label { AutoSize = true, MaximumSize = new Size(490, 0), ForeColor = Color.DimGray };
        SetLocalizationKey(note, "SettingsNote");
        generalContent.Controls.Add(note);

        BuildAppearanceTab(displayContent);

        aboutContent.Controls.Add(CreateAboutGroup());
        aboutContent.Controls.Add(CreateManagementGroup());

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            Padding = new Padding(8),
            Margin = Padding.Empty
        };
        var save = new SettingsButton { AutoSize = true };
        var cancel = new SettingsButton { DialogResult = DialogResult.Cancel, AutoSize = true };
        SetLocalizationKey(save, "Save");
        SetLocalizationKey(cancel, "Cancel");
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        mainLayout.Controls.Add(buttons, 0, 1);
        AcceptButton = save;
        CancelButton = cancel;

        _showCompactBar.Checked = settings.ShowCompactBar;
        _startWithWindows.Checked = settings.StartWithWindows;
        _quotaNotifications.Checked = settings.EnableQuotaNotifications;
        _fiveHourNotificationPercent.Value = settings.NotificationPercent(PanelId.FiveHour);
        _weeklyNotificationPercent.Value = settings.NotificationPercent(PanelId.Weekly);
        _quietHours.Checked = settings.QuietHoursEnabled;
        _language.SelectedIndex = (int)settings.Language;
        _compactBarStyle.SelectedIndex = StyleIndex(settings.CompactBarStyle);
        _codexPalette.SelectedIndex = PaletteIndex(settings.CodexPalette);
        _themeVariant.SelectedIndex = ThemeIndex(settings.ThemeVariant);
        _compactBarScale.Value = Math.Clamp(settings.CompactBarScalePercent, 50, 200);
        _compactBarScaleSlider.Value = (int)_compactBarScale.Value;
        _percentageMode.SelectedIndex = settings.PercentageMode == PercentageMode.Remaining ? 0 : 1;
        _refreshSeconds.Value = Math.Clamp(settings.RefreshIntervalSeconds, 30, 1800);
        _quietHoursStart.Value = Math.Clamp(settings.QuietHoursStart, 0, 23);
        _quietHoursEnd.Value = Math.Clamp(settings.QuietHoursEnd, 0, 23);
        UpdateQuietHoursControls();
        _codexPath.Text = settings.CodexExecutable;
        save.Click += (_, _) => SaveAndClose();
        _language.SelectedIndexChanged += (_, _) => ChangeLanguage();
        _previewTimer.Tick += (_, _) => FlushPreview();
        ResizeEnd += (_, _) =>
        {
            HideHorizontalScroll(_displayTab);
        };
        _repositoryLink.LinkClicked += (_, _) => OpenUrl(
            "https://github.com/RHPLUSSEUNG/codex-usage-monitor");
        _diagnosticsButton.Click += (_, _) => DiagnosticsRequested?.Invoke(this, EventArgs.Empty);
        _updateButton.Click += (_, _) => UpdateRequested?.Invoke(this, EventArgs.Empty);
        _cancelManagementButton.Click += (_, _) => UpdateCancellationRequested?.Invoke(this, EventArgs.Empty);
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
        _quotaNotifications.CheckedChanged += (_, _) => UpdateQuietHoursControls();
        _quietHours.CheckedChanged += (_, _) => UpdateQuietHoursControls();
        _compactBarStyle.SelectedIndexChanged += (_, _) =>
        {
            if (_loadingControls || _updatingLanguage || _compactBarStyle.SelectedIndex < 0)
                return;
            _draft.CompactBarStyle = StyleValues[_compactBarStyle.SelectedIndex];
            RefreshControlsFromDraft();
            RaisePreview();
        };
        _codexPalette.SelectedIndexChanged += (_, _) =>
        {
            if (_loadingControls || _updatingLanguage || _codexPalette.SelectedIndex < 0)
                return;
            CodexPalette palette = PaletteValues[_codexPalette.SelectedIndex];
            ThemeVariant variant = CompactBarPaletteCatalog.Supports(palette, _draft.ThemeVariant)
                ? _draft.ThemeVariant
                : CompactBarPaletteCatalog.DefaultVariant(palette);
            ApplyColorTheme(palette, variant);
            RefreshControlsFromDraft();
            RaisePreview();
        };
        _themeVariant.SelectedIndexChanged += (_, _) =>
        {
            if (_loadingControls || _updatingLanguage || _themeVariant.SelectedIndex < 0)
                return;
            ApplyColorTheme(
                _draft.CodexPalette,
                _availableThemeValues[_themeVariant.SelectedIndex]);
            RefreshControlsFromDraft();
            RaisePreview();
        };
        _compactBarScale.ValueChanged += (_, _) => ChangeCompactBarScale((int)_compactBarScale.Value);
        _compactBarScaleSlider.ValueChanged += (_, _) => ChangeCompactBarScale(_compactBarScaleSlider.Value);
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
        ApplyWindowTheme();
        _appearancePreview.RefreshPreview();
        ResumeLayout(performLayout: false);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Screen screen = Screen.FromPoint(Cursor.Position);
        Rectangle area = screen.WorkingArea;
        Size = new Size(Math.Min(Width, area.Width - 24), Math.Min(Height, area.Height - 24));
        Location = new Point(
            area.Left + Math.Max(0, (area.Width - Width) / 2),
            area.Top + Math.Max(0, (area.Height - Height) / 2));
    }

    internal void PrepareForFirstShow()
    {
        if (IsDisposed || _displayTab is null)
            return;

        int selectedIndex = _tabs.SelectedIndex;
        bool showInTaskbar = ShowInTaskbar;
        double opacity = Opacity;
        try
        {
            ShowInTaskbar = false;
            Opacity = 0d;
            Show();
            _tabs.SelectedTab = _displayTab;
            CreateControlTree(this);
            _displayTab.PerformLayout();
            _tabs.PerformLayout();
            Update();
            _tabs.SelectedIndex = selectedIndex;
            Hide();
        }
        finally
        {
            if (Visible)
                Hide();
            Opacity = opacity;
            ShowInTaskbar = showInTaskbar;
        }
    }

    private static void CreateControlTree(Control root)
    {
        root.CreateControl();
        foreach (Control child in root.Controls)
            CreateControlTree(child);
    }

    private TabPage CreateTab(string localizationKey)
    {
        var tab = new TabPage { Padding = Padding.Empty, AutoScroll = false };
        tab.Controls.Add(new SettingsScrollHost { Dock = DockStyle.Fill });
        SetLocalizationKey(tab, localizationKey);
        return tab;
    }

    private static FlowLayoutPanel CreateTabContent(TabPage tab, int rows)
    {
        var content = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Padding = new Padding(16),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        ((SettingsScrollHost)tab.Controls[0]).SetContent(content);
        return content;
    }

    private GroupBox CreateNotificationGroup()
    {
        var group = new SettingsSection { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(10) };
        SetLocalizationKey(group, "NotificationsSection");
        SetLocalizationKey(_quotaNotifications, "QuotaNotifications");
        SetLocalizationKey(_quietHours, "QuietHours");
        var content = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        content.Controls.Add(_quotaNotifications);
        var threshold = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 10, 0, 10) };
        AddBehaviorRow(threshold, "FiveHourNotificationThreshold", _fiveHourNotificationPercent, 0);
        AddBehaviorRow(threshold, "WeeklyNotificationThreshold", _weeklyNotificationPercent, 1);
        content.Controls.Add(threshold);
        var once = new Label { AutoSize = true, MaximumSize = new Size(600, 0) };
        SetLocalizationKey(once, "NotificationOnce");
        content.Controls.Add(once);
        content.Controls.Add(_quietHours);
        var hours = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = new Padding(20, 2, 0, 0) };
        AddBehaviorRow(hours, "QuietHoursStart", _quietHoursStart, 0);
        AddBehaviorRow(hours, "QuietHoursEnd", _quietHoursEnd, 1);
        content.Controls.Add(hours);
        group.Controls.Add(content);
        return group;
    }

    private Control CreateAdvancedGroup()
    {
        var content = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 4, 0, 0)
        };
        var toggle = new SettingsButton { AutoSize = true };
        SetLocalizationKey(toggle, "ShowAdvancedSettings");
        var advanced = new SettingsSection
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(500, 0),
            Padding = new Padding(18, 44, 18, 18),
            Visible = false
        };
        SetLocalizationKey(advanced, "AdvancedSettings");
        var fields = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = Padding.Empty,
            Location = new Point(18, 44)
        };
        var pathLabel = new Label { AutoSize = true, Margin = new Padding(0, 0, 0, 5) };
        SetLocalizationKey(pathLabel, "CodexExecutable");
        _codexPath.Margin = new Padding(0, 0, 0, 8);
        var hint = new Label { AutoSize = true, MaximumSize = new Size(450, 0), Margin = Padding.Empty };
        SetLocalizationKey(hint, "CodexExecutableHint");
        fields.Controls.Add(pathLabel);
        fields.Controls.Add(_codexPath);
        fields.Controls.Add(hint);
        advanced.Controls.Add(fields);
        toggle.Click += (_, _) =>
        {
            advanced.Visible = !advanced.Visible;
            SetLocalizationKey(toggle, advanced.Visible ? "HideAdvancedSettings" : "ShowAdvancedSettings");
        };
        content.Controls.Add(toggle);
        content.Controls.Add(advanced);
        return content;
    }

    private Control CreateScaleEditor()
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 4, 0, 8)
        };
        var label = new Label { AutoSize = true, Width = 105, Anchor = AnchorStyles.Left };
        SetLocalizationKey(label, "CompactBarScale");
        _compactBarScaleSlider.AccessibleName = T("CompactBarScale");
        _compactBarScale.AccessibleName = T("CompactBarScale");
        var reset = new SettingsButton { AutoSize = true };
        SetLocalizationKey(reset, "ResetTo100Percent");
        reset.Click += (_, _) => ChangeCompactBarScale(100);
        row.Controls.Add(label);
        row.Controls.Add(_compactBarScaleSlider);
        row.Controls.Add(_compactBarScale);
        row.Controls.Add(reset);
        return row;
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
        var group = new SettingsSection { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(18) };
        SetLocalizationKey(group, titleKey);
        var layout = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Top };
        group.Controls.Add(layout);
        var enabled = new SettingsToggle { Checked = metric.Enabled, AutoSize = true };
        SetLocalizationKey(enabled, "Show");
        var presentation = NewCombo();
        presentation.Items.AddRange([T("PercentOnly"), T("BarOnly"), T("PercentAndBar")]);
        _presentationCombos.Add(presentation);
        presentation.SelectedIndex = metric.Presentation switch { MetricPresentation.PercentOnly => 0, MetricPresentation.BarOnly => 1, _ => 2 };
        enabled.Text = "";
        enabled.Tag = null;
        enabled.AccessibleName = T("Show");
        layout.Controls.Add(AppearanceRow("Show", enabled));
        layout.Controls.Add(AppearanceRow("Presentation", presentation));
        Control fillEditor = CreateColorButton("FillColor", () => metric.FillColor, value => metric.FillColor = value);
        Control trackEditor = CreateColorButton("TrackColor", () => metric.TrackColor, value => metric.TrackColor = value);
        Control labelEditor = CreateColorButton("LabelColor", () => string.IsNullOrEmpty(metric.LabelColor) ? metric.FillColor : metric.LabelColor, value => metric.LabelColor = value);
        Control percentEditor = CreateColorButton("PercentColor", () => string.IsNullOrEmpty(metric.PercentColor) ? CompactBarPaletteCatalog.Get(_draft.CodexPalette, _draft.ThemeVariant).Foreground : metric.PercentColor, value => metric.PercentColor = value);
        fillEditor.Tag = "metric-fill";
        trackEditor.Tag = "metric-track";
        labelEditor.Tag = "metric-label";
        percentEditor.Tag = "metric-percent";
        layout.Controls.AddRange([fillEditor, trackEditor, labelEditor, percentEditor]);
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
        var group = new SettingsSection { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(18) };
        SetLocalizationKey(group, "CompactBarBackground");
        var content = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        var transparent = new SettingsToggle { AutoSize = true, Checked = _draft.TransparentBackground };
        SetLocalizationKey(transparent, "TransparentBackground");
        transparent.CheckedChanged += (_, _) =>
        {
            if (_loadingControls)
                return;
            _draft.TransparentBackground = transparent.Checked;
            if (!transparent.Checked && HexColor.ParseOrDefault(_draft.BackgroundColor, Color.Transparent).A == 0)
                _draft.BackgroundColor = CompactBarTheme.DefaultBackground(
                    _draft.CompactBarStyle, _draft.CodexPalette, _draft.ThemeVariant);
            RefreshControlsFromDraft();
            RaisePreview();
        };
        _refreshDraftEditors.Add(() => transparent.Checked = _draft.TransparentBackground);
        content.Controls.Add(AppearanceRow("TransparentBackground", transparent));
        content.Controls.Add(CreateColorButton("RectangleBackground", () => _draft.BackgroundColor, value => _draft.BackgroundColor = value));
        var reset = new SettingsButton { AutoSize = true };
        SetLocalizationKey(reset, "UseThemeBackground");
        reset.Click += (_, _) =>
        {
            _draft.BackgroundColor = CompactBarTheme.DefaultBackground(
                _draft.CompactBarStyle,
                _draft.CodexPalette,
                _draft.ThemeVariant);
            _draft.TransparentBackground = false;
            RefreshControlsFromDraft();
            RaisePreview();
        };
        content.Controls.Add(AppearanceRow("UseThemeBackground", reset));
        reset.Text = T("ResetAction");
        group.Controls.Add(content);
        return group;
    }

    private GroupBox CreateAboutGroup()
    {
        var group = new SettingsSection { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(12) };
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
        var group = new SettingsSection { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(12) };
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
        buttons.Controls.Add(_cancelManagementButton);
        layout.Controls.Add(buttons);
        layout.Controls.Add(_managementProgress);
        layout.Controls.Add(_managementStatus);
        var danger = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 14, 0, 0)
        };
        var dangerLabel = new Label { AutoSize = true, Font = new Font(Font, FontStyle.Bold), ForeColor = Color.Firebrick };
        SetLocalizationKey(dangerLabel, "DangerZone");
        danger.Controls.Add(dangerLabel);
        danger.Controls.Add(_uninstallButton);
        layout.Controls.Add(danger);
        group.Controls.Add(layout);
        return group;
    }

    private GroupBox CreatePresetGroup()
    {
        var group = new SettingsSection { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(10) };
        SetLocalizationKey(group, "Presets");
        var content = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, RowCount = 2, Dock = DockStyle.Top };
        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty
        };
        var save = new SettingsButton { AutoSize = true };
        SetLocalizationKey(_presetLoadButton, "LoadPreset");
        SetLocalizationKey(save, "SavePreset");
        _presetLoadButton.Click += (_, _) => LoadPreset(Math.Max(0, _presetSlot.SelectedIndex));
        save.Click += (_, _) => SavePreset(Math.Max(0, _presetSlot.SelectedIndex));
        _presetSlot.SelectedIndexChanged += (_, _) => RefreshPresetButtons();
        actions.Controls.Add(_presetSlot);
        actions.Controls.Add(_presetLoadButton);
        actions.Controls.Add(save);
        content.Controls.Add(actions, 0, 0);
        content.Controls.Add(_presetStatus, 0, 1);
        group.Controls.Add(content);
        _refreshDraftEditors.Add(RefreshPresetButtons);

        RefreshPresetButtons();
        return group;
    }

    private Control CreateColorButton(string labelKey, Func<string> getter, Action<string> setter)
    {
        Color selected = HexColor.ParseOrDefault(getter(), Color.Gray);
        var choose = new SettingsButton { Width = 155, Height = 30, UseVisualStyleBackColor = false, Tag = "color-swatch" };
        void UpdateButton()
        {
            choose.BackColor = Color.FromArgb(255, selected.R, selected.G, selected.B);
            double luminance = selected.R * 0.299 + selected.G * 0.587 + selected.B * 0.114;
            choose.ForeColor = luminance >= 150d ? Color.Black : Color.White;
            choose.Text = HexColor.Format(selected, includeAlpha: selected.A != 255);
            choose.AccessibleName = $"{T(labelKey)}: {choose.Text}";
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
        Control row = AppearanceRow(labelKey, choose);
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
        _draft.FiveHourNotificationPercent = (int)_fiveHourNotificationPercent.Value;
        _draft.WeeklyNotificationPercent = (int)_weeklyNotificationPercent.Value;
        _draft.QuotaNotificationPercent = _draft.FiveHourNotificationPercent;
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
        List<Control> suspendedControls = SuspendLayoutTree(this);
        try
        {
            Text = T("SettingsTitle");
            ApplyLocalizedText(this);
            ReplaceItems(_compactBarStyle, StyleNames());
            ReplaceItems(_codexPalette, PaletteNames());
            UpdateThemeVariantItems(_draft.CodexPalette, _draft.ThemeVariant);
            ReplaceItems(_percentageMode, T("RemainingPercent"), T("UsedPercent"));
            ReplaceItems(_presetSlot, PresetNames());
            foreach (SettingsComboBox presentation in _presentationCombos)
                ReplaceItems(presentation, T("PercentOnly"), T("BarOnly"), T("PercentAndBar"));
            foreach (Action update in _colorButtonLanguageUpdates)
                update();
            _compactBarScaleSlider.AccessibleName = T("CompactBarScale");
            _compactBarScale.AccessibleName = T("CompactBarScale");
            UpdateAboutText();
            UpdateManagementControls();
        }
        finally
        {
            ResumeLayoutTree(suspendedControls);
            _updatingLanguage = false;
        }
        RefreshAppearanceLabels();
    }

    private static List<Control> SuspendLayoutTree(Control root)
    {
        var controls = new List<Control>();
        var pending = new Stack<Control>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            Control control = pending.Pop();
            control.SuspendLayout();
            controls.Add(control);
            foreach (Control child in control.Controls)
                pending.Push(child);
        }
        return controls;
    }

    private static void ResumeLayoutTree(List<Control> controls, bool performLayout = true)
    {
        for (int index = controls.Count - 1; index >= 0; index--)
            controls[index].ResumeLayout(performLayout: false);
        if (performLayout && controls.Count > 0)
            controls[0].PerformLayout();
    }

    private void ApplyLocalizedText(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control.Tag is string key)
            {
                control.Text = T(key);
                control.AccessibleName = control.Text;
            }
            ApplyLocalizedText(control);
        }
    }

    private static void ReplaceItems(SettingsComboBox combo, params string[] items)
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
        control.AccessibleName = control.Text;
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
        _diagnosticsButton.AccessibleName = _diagnosticsButton.Text;
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
        _updateButton.AccessibleName = _updateButton.Text;
        _cancelManagementButton.Text = T("CancelOperation");
        _cancelManagementButton.AccessibleName = _cancelManagementButton.Text;
        _cancelManagementButton.Visible = _checkingUpdate || _installingUpdate;
        _cancelManagementButton.Enabled = _cancelManagementButton.Visible;
        _uninstallButton.Text = T("Uninstall");
        _uninstallButton.AccessibleName = _uninstallButton.Text;
        _uninstallButton.Enabled = !_installingUpdate && _canUninstall;
        _managementProgress.Visible = _checkingUpdate || _installingUpdate;
        _managementProgress.AccessibleName = T("UpdateProgress");
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
        _presetStatus.Text = Localization.Format(_displayLanguage, "PresetSavedInline", slot + 1);
        _presetStatus.Visible = true;
    }

    private void LoadPreset(int slot)
    {
        CompactBarPreset? preset = GetPreset(slot);
        if (preset is null)
            return;
        preset.ApplyTo(_draft);
        RefreshControlsFromDraft();
        RaisePreview();
        _presetStatus.Text = Localization.Format(_displayLanguage, "PresetLoadedInline", slot + 1);
        _presetStatus.Visible = true;
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
        _presetLoadButton.Enabled = GetPreset(Math.Max(0, _presetSlot.SelectedIndex)) is not null;
    }

    private void RefreshControlsFromDraft()
    {
        _loadingControls = true;
        try
        {
            _showCompactBar.Checked = _draft.ShowCompactBar;
            _compactBarStyle.SelectedIndex = StyleIndex(_draft.CompactBarStyle);
            _codexPalette.SelectedIndex = PaletteIndex(_draft.CodexPalette);
            UpdateThemeVariantItems(_draft.CodexPalette, _draft.ThemeVariant);
            _themeVariant.SelectedIndex = ThemeIndex(_draft.ThemeVariant);
            int scale = Math.Clamp(_draft.CompactBarScalePercent, 50, 200);
            _compactBarScale.Value = scale;
            _compactBarScaleSlider.Value = scale;
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
        if (_codexPalette.SelectedIndex >= 0)
            _draft.CodexPalette = PaletteValues[_codexPalette.SelectedIndex];
        _draft.ThemeVariant = _themeVariant.SelectedIndex >= 0
            ? _availableThemeValues[_themeVariant.SelectedIndex]
            : CompactBarPaletteCatalog.DefaultVariant(_draft.CodexPalette);
        _draft.CompactBarScalePercent = (int)_compactBarScale.Value;
        _draft.PercentageMode = _percentageMode.SelectedIndex == 0
            ? PercentageMode.Remaining
            : PercentageMode.Used;
    }

    private void ApplyColorTheme(CodexPalette palette, ThemeVariant theme)
    {
        ApplyColorThemeDefaults(_draft, palette, theme);
    }

    internal static void ApplyColorThemeDefaults(
        AppSettings settings,
        CodexPalette palette,
        ThemeVariant theme)
    {
        settings.CodexPalette = palette;
        settings.ThemeVariant = theme;
        settings.BackgroundColor = CompactBarTheme.DefaultBackground(
            settings.CompactBarStyle,
            palette,
            theme);
        string fill = CompactBarTheme.DefaultFill(palette, theme);
        string track = CompactBarTheme.DefaultTrack(palette, theme);
        foreach (MetricSettings metric in new[] { settings.FiveHour, settings.Weekly, settings.Cpu, settings.Memory })
        {
            metric.LabelColor = "";
            metric.PercentColor = "";
            metric.ResetTimeColor = "";
            metric.FillColor = fill;
            metric.TrackColor = track;
        }
    }

    internal static bool IsThemeDefaultBackground(string background, string themeDefault) =>
        HexColor.TryParse(background, out Color backgroundColor)
        && HexColor.TryParse(themeDefault, out Color defaultColor)
        && backgroundColor.ToArgb() == defaultColor.ToArgb();

    private void ChangeCompactBarScale(int value)
    {
        if (_loadingControls)
            return;
        int scale = Math.Clamp(value, 50, 200);
        _loadingControls = true;
        try
        {
            _compactBarScale.Value = scale;
            _compactBarScaleSlider.Value = scale;
        }
        finally
        {
            _loadingControls = false;
        }
        _draft.CompactBarScalePercent = scale;
        RaisePreview();
    }

    private void UpdateQuietHoursControls()
    {
        _quietHours.Enabled = _quotaNotifications.Checked;
        _fiveHourNotificationPercent.Enabled = _quotaNotifications.Checked;
        _weeklyNotificationPercent.Enabled = _quotaNotifications.Checked;
        bool hoursEnabled = _quotaNotifications.Checked && _quietHours.Checked;
        _quietHoursStart.Enabled = hoursEnabled;
        _quietHoursEnd.Enabled = hoursEnabled;
    }

    internal static void ScrollParent(Control control, MouseEventArgs e)
    {
        if (e is HandledMouseEventArgs handled)
            handled.Handled = true;

        Control? current = control.Parent;
        while (current is not null && current is not SettingsScrollHost)
            current = current.Parent;
        if (current is not SettingsScrollHost scrollHost)
            return;

        int lines = Math.Max(1, SystemInformation.MouseWheelScrollLines);
        scrollHost.ScrollBy(-Math.Sign(e.Delta) * lines * 18);
    }

    private static void HideHorizontalScroll(TabPage? tab)
    {
        if (tab?.Controls.Count > 0 && tab.Controls[0] is SettingsScrollHost host)
            host.RefreshLayout();
    }

    private void RaisePreview()
    {
        RefreshAppearance();
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
        T("LightStyle"),
        T("LabelBoxStyle"),
        T("NeonGlowStyle"),
        T("CompactRowsStyle"),
        T("GradientStyle"),
        T("MinimalIconsStyle")
    ];

    private string[] PaletteNames() =>
        PaletteValues.Select(CompactBarPaletteCatalog.DisplayName).ToArray();

    private string[] PresetNames() =>
        [T("Preset1"), T("Preset2"), T("Preset3")];

    private string[] ThemeNames(CodexPalette palette) =>
        CompactBarPaletteCatalog.Variants(palette)
            .Select(variant => variant == ThemeVariant.Light ? T("LightTheme") : T("DarkTheme"))
            .ToArray();

    private void UpdateThemeVariantItems(CodexPalette palette, ThemeVariant selected)
    {
        _availableThemeValues = CompactBarPaletteCatalog.Variants(palette);
        ReplaceItems(_themeVariant, ThemeNames(palette));
        ThemeVariant normalized = CompactBarPaletteCatalog.NormalizeVariant(selected);
        int index = Array.IndexOf(_availableThemeValues, normalized);
        _themeVariant.SelectedIndex = index >= 0 ? index : 0;
    }

    private static int StyleIndex(CompactBarStyle style)
    {
        int index = Array.IndexOf(StyleValues, AppSettings.NormalizeStyle(style));
        return index < 0 ? 0 : index;
    }

    private int ThemeIndex(ThemeVariant theme)
    {
        int index = Array.IndexOf(_availableThemeValues, CompactBarPaletteCatalog.NormalizeVariant(theme));
        return index < 0 ? 0 : index;
    }

    private static int PaletteIndex(CodexPalette palette)
    {
        int index = Array.IndexOf(PaletteValues, palette);
        return index < 0 ? Array.IndexOf(PaletteValues, CodexPalette.Codex) : index;
    }

    private static SettingsComboBox NewCombo() => new() { Width = 170 };

}
