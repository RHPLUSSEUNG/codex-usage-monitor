using CodexUsageMonitor.Models;

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
    private readonly ComboBox _language = NewCombo();
    private readonly ComboBox _compactBarStyle = NewCombo();
    private readonly ComboBox _percentageMode = NewCombo();
    private readonly NumericUpDown _refreshSeconds = new() { Minimum = 30, Maximum = 1800, Increment = 30, Width = 90 };
    private readonly TextBox _codexPath = new() { Width = 280 };

    public AppSettings Result => _draft;
    public event Action<AppSettings>? PreviewChanged;

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

        var scrollHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        Controls.Add(scrollHost);
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 9,
            Padding = new Padding(16),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        for (int index = 0; index < content.RowCount; index++)
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        scrollHost.Controls.Add(content);

        SetLocalizationKey(_showCompactBar, "ShowCompactBar");
        SetLocalizationKey(_startWithWindows, "StartWithWindows");
        var general = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Dock = DockStyle.Top };
        general.Controls.Add(_showCompactBar);
        general.Controls.Add(_startWithWindows);
        content.Controls.Add(general);

        _language.Items.AddRange(["English", "한국어", "中文", "日本語"]);
        _compactBarStyle.Items.AddRange(StyleNames());
        _percentageMode.Items.AddRange([T("RemainingPercent"), T("UsedPercent")]);
        var behavior = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 12, 0, 8) };
        AddBehaviorRow(behavior, "Language", _language, 0);
        AddBehaviorRow(behavior, "CompactBarStyle", _compactBarStyle, 1);
        AddBehaviorRow(behavior, "DisplayBasis", _percentageMode, 2);
        AddBehaviorRow(behavior, "RefreshSeconds", _refreshSeconds, 3);
        AddBehaviorRow(behavior, "CodexExecutable", _codexPath, 4);
        content.Controls.Add(behavior);

        content.Controls.Add(CreatePresetGroup(), 0, 2);
        content.Controls.Add(CreateBackgroundGroup(), 0, 3);
        content.Controls.Add(CreateMetricGroup("FiveHourLimit", _draft.FiveHour), 0, 4);
        content.Controls.Add(CreateMetricGroup("WeeklyLimit", _draft.Weekly), 0, 5);
        content.Controls.Add(CreateMetricGroup("CpuUsage", _draft.Cpu), 0, 6);
        content.Controls.Add(CreateMetricGroup("MemoryUsage", _draft.Memory), 0, 7);
        var note = new Label { AutoSize = true, MaximumSize = new Size(490, 0), ForeColor = Color.DimGray };
        SetLocalizationKey(note, "SettingsNote");
        content.Controls.Add(note, 0, 8);

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
        _language.SelectedIndex = (int)settings.Language;
        _compactBarStyle.SelectedIndex = StyleIndex(settings.CompactBarStyle);
        _percentageMode.SelectedIndex = settings.PercentageMode == PercentageMode.Remaining ? 0 : 1;
        _refreshSeconds.Value = Math.Clamp(settings.RefreshIntervalSeconds, 30, 1800);
        _codexPath.Text = settings.CodexExecutable;
        save.Click += (_, _) => SaveAndClose();
        _language.SelectedIndexChanged += (_, _) => ChangeLanguage();
        _showCompactBar.CheckedChanged += (_, _) =>
        {
            if (_loadingControls)
                return;
            _draft.ShowCompactBar = _showCompactBar.Checked;
            RaisePreview();
        };
        _compactBarStyle.SelectedIndexChanged += (_, _) =>
        {
            if (_loadingControls || _updatingLanguage || _compactBarStyle.SelectedIndex < 0)
                return;
            _draft.CompactBarStyle = StyleValues[_compactBarStyle.SelectedIndex];
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
            if (dialog.ShowDialog() != DialogResult.OK)
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
            ReplaceItems(_percentageMode, T("RemainingPercent"), T("UsedPercent"));
            foreach (ComboBox presentation in _presentationCombos)
                ReplaceItems(presentation, T("PercentOnly"), T("BarOnly"), T("PercentAndBar"));
            foreach (Action update in _colorButtonLanguageUpdates)
                update();
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

    private void SavePreset(int slot)
    {
        SyncGeneralControlsToDraft();
        SetPreset(slot, CompactBarPreset.Capture(_draft));
        RefreshPresetButtons();
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
        _draft.PercentageMode = _percentageMode.SelectedIndex == 0
            ? PercentageMode.Remaining
            : PercentageMode.Used;
    }

    private void RaisePreview() => PreviewChanged?.Invoke(_draft.Copy());

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
}
