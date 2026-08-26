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
    private static readonly CodexPalette[] PaletteValues = CompactBarPaletteCatalog.Values;

    private readonly AppSettings _draft;
    private readonly List<ComboBox> _presentationCombos = [];
    private readonly List<Action> _colorButtonLanguageUpdates = [];
    private readonly List<Action> _refreshDraftEditors = [];
    private AppLanguage _displayLanguage;
    private bool _updatingLanguage;
    private bool _loadingControls;
    private readonly CheckBox _showCompactBar = new() { AutoSize = true };
    private readonly CheckBox _startWithWindows = new() { AutoSize = true };
    private readonly CheckBox _quotaNotifications = new() { AutoSize = true };
    private readonly CheckBox _quietHours = new() { AutoSize = true };
    private readonly ComboBox _language = NewCombo();
    private readonly ComboBox _compactBarStyle = NewCombo();
    private readonly ComboBox _codexPalette = NewCombo();
    private readonly ComboBox _themeVariant = NewCombo();
    private readonly ComboBox _percentageMode = NewCombo();
    private readonly ComboBox _presetSlot = new SettingsComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 125 };
    private readonly Button _presetLoadButton = new() { AutoSize = true };
    private readonly NumericUpDown _compactBarScale = new SettingsNumericUpDown { Minimum = 50, Maximum = 200, Increment = 10, Width = 72 };
    private readonly TrackBar _compactBarScaleSlider = new SettingsTrackBar { Minimum = 50, Maximum = 200, TickFrequency = 10, SmallChange = 10, LargeChange = 20, Width = 155 };
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
    private readonly Button _cancelManagementButton = new() { AutoSize = true, Visible = false };
    private readonly Button _uninstallButton = new() { AutoSize = true };
    private readonly Label _managementStatus = new() { AutoSize = true, MaximumSize = new Size(460, 0), ForeColor = Color.DimGray };
    private readonly ProgressBar _managementProgress = new() { Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 25, Width = 300, Visible = false };
    private readonly Label _presetStatus = new() { AutoSize = true, ForeColor = Color.DimGray };
    private readonly System.Windows.Forms.Timer _previewTimer = new() { Interval = 33 };
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill, Padding = new Point(14, 5) };
    private TabPage? _displayTab;
    private List<Control>? _frozenDisplayControls;
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
        Font = new Font("Segoe UI", 9f);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        StartPosition = FormStartPosition.Manual;
        MinimumSize = new Size(560, 620);
        ClientSize = new Size(600, 700);

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
            RowCount = 1,
            Padding = new Padding(8, 8, 8, 0)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
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

        var styleLayout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Top, Margin = new Padding(0, 4, 0, 10) };
        AddBehaviorRow(styleLayout, "CompactBarStyle", _compactBarStyle, 0);
        AddBehaviorRow(styleLayout, "ThemeVariant", _themeVariant, 1);
        AddBehaviorRow(styleLayout, "CodexPalette", _codexPalette, 2);
        Control scaleEditor = CreateScaleEditor();
        styleLayout.Controls.Add(scaleEditor, 0, 3);
        styleLayout.SetColumnSpan(scaleEditor, 2);
        displayContent.Controls.Add(CreatePresetGroup());
        displayContent.Controls.Add(styleLayout);
        displayContent.Controls.Add(CreateBackgroundGroup());

        var basis = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Top, Margin = new Padding(0, 4, 0, 10) };
        AddBehaviorRow(basis, "DisplayBasis", _percentageMode, 0);
        displayContent.Controls.Add(basis);
        displayContent.Controls.Add(CreateMetricGroup("FiveHourLimit", _draft.FiveHour));
        displayContent.Controls.Add(CreateMetricGroup("WeeklyLimit", _draft.Weekly));
        displayContent.Controls.Add(CreateMetricGroup("CpuUsage", _draft.Cpu));
        displayContent.Controls.Add(CreateMetricGroup("MemoryUsage", _draft.Memory));

        aboutContent.Controls.Add(CreateAboutGroup());
        aboutContent.Controls.Add(CreateManagementGroup());

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
            RefreshFrozenDisplayLayout();
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
        ResumeLayout(performLayout: false);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Screen screen = Screen.FromPoint(Cursor.Position);
        Rectangle area = screen.WorkingArea;
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
            FreezeDisplayLayout();
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

    private void FreezeDisplayLayout()
    {
        if (_displayTab is null || _frozenDisplayControls is not null)
            return;
        _frozenDisplayControls = SuspendLayoutTree(_displayTab);
    }

    private void ThawDisplayLayout(bool performLayout = true)
    {
        if (_frozenDisplayControls is not { } controls)
            return;
        ResumeLayoutTree(controls, performLayout);
        _frozenDisplayControls = null;
    }

    private void RefreshFrozenDisplayLayout()
    {
        if (_frozenDisplayControls is null || _displayTab is null)
            return;
        ThawDisplayLayout();
        _displayTab.PerformLayout();
        FreezeDisplayLayout();
    }

    private TabPage CreateTab(string localizationKey)
    {
        var tab = new TabPage { Padding = new Padding(0), AutoScroll = true };
        tab.Layout += (_, _) => HideHorizontalScroll(tab);
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
        tab.Controls.Add(content);
        return content;
    }

    private GroupBox CreateNotificationGroup()
    {
        var group = new GroupBox { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(10) };
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
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        var toggle = new Button { AutoSize = true };
        SetLocalizationKey(toggle, "ShowAdvancedSettings");
        var advanced = new GroupBox { AutoSize = true, Padding = new Padding(10), Visible = false };
        SetLocalizationKey(advanced, "AdvancedSettings");
        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2 };
        AddBehaviorRow(layout, "CodexExecutable", _codexPath, 0);
        advanced.Controls.Add(layout);
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
        var reset = new Button { AutoSize = true };
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
        var group = new GroupBox { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(10) };
        SetLocalizationKey(group, "CompactBarBackground");
        var content = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        content.Controls.Add(CreateColorButton("RectangleBackground", () => _draft.BackgroundColor, value => _draft.BackgroundColor = value));
        var reset = new Button { AutoSize = true };
        SetLocalizationKey(reset, "UseThemeBackground");
        reset.Click += (_, _) =>
        {
            _draft.BackgroundColor = CompactBarTheme.DefaultBackground(
                _draft.CompactBarStyle,
                _draft.CodexPalette,
                _draft.ThemeVariant);
            RefreshControlsFromDraft();
            RaisePreview();
        };
        content.Controls.Add(reset);
        group.Controls.Add(content);
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
        var group = new GroupBox { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(10) };
        SetLocalizationKey(group, "Presets");
        var content = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, RowCount = 2, Dock = DockStyle.Top };
        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty
        };
        var save = new Button { AutoSize = true };
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
        var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
        Color selected = HexColor.ParseOrDefault(getter(), Color.Gray);
        var choose = new Button { Width = 155, Height = 30, UseVisualStyleBackColor = false };
        void UpdateButton()
        {
            choose.BackColor = Color.FromArgb(255, selected.R, selected.G, selected.B);
            double luminance = selected.R * 0.299 + selected.G * 0.587 + selected.B * 0.114;
            choose.ForeColor = luminance >= 150d ? Color.Black : Color.White;
            choose.Text = Localization.Format(_displayLanguage, "ChooseColor", selected.A);
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
        var label = new Label { AutoSize = true, Width = 80, Anchor = AnchorStyles.Left };
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
        bool refreezeDisplay = _frozenDisplayControls is not null;
        if (refreezeDisplay)
            ThawDisplayLayout(performLayout: false);
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
            foreach (ComboBox presentation in _presentationCombos)
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
            if (refreezeDisplay)
            {
                _displayTab?.PerformLayout();
                FreezeDisplayLayout();
            }
            _updatingLanguage = false;
        }
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
        bool hoursEnabled = _quotaNotifications.Checked && _quietHours.Checked;
        _quietHoursStart.Enabled = hoursEnabled;
        _quietHoursEnd.Enabled = hoursEnabled;
    }

    private static void ScrollParent(Control control, MouseEventArgs e)
    {
        if (e is HandledMouseEventArgs handled)
            handled.Handled = true;

        Control? current = control.Parent;
        while (current is not null && current is not ScrollableControl { AutoScroll: true })
            current = current.Parent;
        if (current is not ScrollableControl scrollHost)
            return;

        int lines = Math.Max(1, SystemInformation.MouseWheelScrollLines);
        int currentY = -scrollHost.AutoScrollPosition.Y;
        int nextY = Math.Max(0, currentY - Math.Sign(e.Delta) * lines * 18);
        scrollHost.AutoScrollPosition = new Point(-scrollHost.AutoScrollPosition.X, nextY);
    }

    private static void HideHorizontalScroll(TabPage? tab)
    {
        if (tab is null)
            return;
        tab.HorizontalScroll.Enabled = false;
        tab.HorizontalScroll.Visible = false;
        if (tab.AutoScrollPosition.X != 0)
            tab.AutoScrollPosition = new Point(0, -tab.AutoScrollPosition.Y);
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
        int index = Array.IndexOf(StyleValues, style);
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

    private static ComboBox NewCombo() => new SettingsComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };

    private sealed class SettingsComboBox : ComboBox
    {
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (DroppedDown)
            {
                base.OnMouseWheel(e);
                return;
            }

            ScrollParent(this, e);
        }
    }

    private sealed class SettingsNumericUpDown : NumericUpDown
    {
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ScrollParent(this, e);
        }
    }

    private sealed class SettingsTrackBar : TrackBar
    {
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ScrollParent(this, e);
        }
    }
}
