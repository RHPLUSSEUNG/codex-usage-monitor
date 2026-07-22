using CodexUsageMonitor.Models;

namespace CodexUsageMonitor.UI;

public sealed class SettingsForm : Form
{
    private readonly AppSettings _draft;
    private readonly AppLanguage _displayLanguage;
    private readonly CheckBox _showCompactBar = new() { AutoSize = true };
    private readonly CheckBox _startWithWindows = new() { AutoSize = true };
    private readonly ComboBox _language = NewCombo();
    private readonly ComboBox _percentageMode = NewCombo();
    private readonly NumericUpDown _refreshSeconds = new() { Minimum = 30, Maximum = 1800, Increment = 30, Width = 90 };
    private readonly TextBox _codexPath = new() { Width = 280 };

    public AppSettings Result => _draft;

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
            RowCount = 8,
            Padding = new Padding(16),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        for (int index = 0; index < content.RowCount; index++)
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        scrollHost.Controls.Add(content);

        _showCompactBar.Text = T("ShowCompactBar");
        _startWithWindows.Text = T("StartWithWindows");
        var general = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Dock = DockStyle.Top };
        general.Controls.Add(_showCompactBar);
        general.Controls.Add(_startWithWindows);
        content.Controls.Add(general);

        _language.Items.AddRange(["English", "한국어", "中文", "日本語"]);
        _percentageMode.Items.AddRange([T("RemainingPercent"), T("UsedPercent")]);
        var behavior = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 12, 0, 8) };
        AddBehaviorRow(behavior, T("Language"), _language, 0);
        AddBehaviorRow(behavior, T("DisplayBasis"), _percentageMode, 1);
        AddBehaviorRow(behavior, T("RefreshSeconds"), _refreshSeconds, 2);
        AddBehaviorRow(behavior, T("CodexExecutable"), _codexPath, 3);
        content.Controls.Add(behavior);

        content.Controls.Add(CreateBackgroundGroup(), 0, 2);
        content.Controls.Add(CreateMetricGroup(T("FiveHourLimit"), _draft.FiveHour), 0, 3);
        content.Controls.Add(CreateMetricGroup(T("WeeklyLimit"), _draft.Weekly), 0, 4);
        content.Controls.Add(CreateMetricGroup(T("CpuUsage"), _draft.Cpu), 0, 5);
        content.Controls.Add(CreateMetricGroup(T("MemoryUsage"), _draft.Memory), 0, 6);
        content.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(490, 0), ForeColor = Color.DimGray, Text = T("SettingsNote") }, 0, 7);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
        var save = new Button { Text = T("Save"), AutoSize = true };
        var cancel = new Button { Text = T("Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;

        _showCompactBar.Checked = settings.ShowCompactBar;
        _startWithWindows.Checked = settings.StartWithWindows;
        _language.SelectedIndex = (int)settings.Language;
        _percentageMode.SelectedIndex = settings.PercentageMode == PercentageMode.Remaining ? 0 : 1;
        _refreshSeconds.Value = Math.Clamp(settings.RefreshIntervalSeconds, 30, 1800);
        _codexPath.Text = settings.CodexExecutable;
        save.Click += (_, _) => SaveAndClose();
    }

    private static void AddBehaviorRow(TableLayoutPanel layout, string label, Control control, int row)
    {
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private GroupBox CreateMetricGroup(string title, MetricSettings metric)
    {
        var group = new GroupBox { Text = title, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(10) };
        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill };
        group.Controls.Add(layout);
        var enabled = new CheckBox { Text = T("Show"), Checked = metric.Enabled, AutoSize = true };
        var presentation = NewCombo();
        presentation.Items.AddRange([T("PercentOnly"), T("BarOnly"), T("PercentAndBar")]);
        presentation.SelectedIndex = metric.Presentation switch { MetricPresentation.PercentOnly => 0, MetricPresentation.BarOnly => 1, _ => 2 };
        layout.Controls.Add(enabled, 0, 0);
        layout.SetColumnSpan(enabled, 2);
        layout.Controls.Add(new Label { Text = T("Presentation"), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(presentation, 1, 1);
        Control fillEditor = CreateColorButton(T("FillColor"), metric.FillColor, value => metric.FillColor = value);
        Control trackEditor = CreateColorButton(T("TrackColor"), metric.TrackColor, value => metric.TrackColor = value);
        layout.Controls.Add(fillEditor, 0, 2);
        layout.SetColumnSpan(fillEditor, 2);
        layout.Controls.Add(trackEditor, 0, 3);
        layout.SetColumnSpan(trackEditor, 2);
        enabled.CheckedChanged += (_, _) => metric.Enabled = enabled.Checked;
        presentation.SelectedIndexChanged += (_, _) => metric.Presentation = presentation.SelectedIndex switch
        {
            0 => MetricPresentation.PercentOnly,
            1 => MetricPresentation.BarOnly,
            _ => MetricPresentation.PercentAndBar
        };
        return group;
    }

    private GroupBox CreateBackgroundGroup()
    {
        var group = new GroupBox { Text = "Compact Bar", AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(10) };
        group.Controls.Add(CreateColorButton(T("RectangleBackground"), _draft.BackgroundColor, value => _draft.BackgroundColor = value));
        return group;
    }

    private Control CreateColorButton(string label, string initial, Action<string> setter)
    {
        var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
        Color selected = HexColor.ParseOrDefault(initial, Color.Gray);
        var choose = new Button { Width = 185, Height = 30, UseVisualStyleBackColor = false };
        void UpdateButton()
        {
            choose.BackColor = Color.FromArgb(255, selected.R, selected.G, selected.B);
            double luminance = selected.R * 0.299 + selected.G * 0.587 + selected.B * 0.114;
            choose.ForeColor = luminance >= 150d ? Color.Black : Color.White;
            choose.Text = Localization.Format(_displayLanguage, "ChooseColor", selected.A);
        }
        choose.Click += (_, _) =>
        {
            using var dialog = new ColorPickerDialog(selected);
            if (dialog.ShowDialog() != DialogResult.OK)
                return;
            selected = dialog.SelectedColor;
            setter(HexColor.Format(selected, includeAlpha: true));
            UpdateButton();
        };
        row.Controls.Add(new Label { Text = label, AutoSize = true, Width = 95, Anchor = AnchorStyles.Left });
        row.Controls.Add(choose);
        UpdateButton();
        return row;
    }

    private void SaveAndClose()
    {
        _draft.ShowCompactBar = _showCompactBar.Checked;
        _draft.StartWithWindows = _startWithWindows.Checked;
        _draft.Language = (AppLanguage)Math.Max(0, _language.SelectedIndex);
        _draft.PercentageMode = _percentageMode.SelectedIndex == 0 ? PercentageMode.Remaining : PercentageMode.Used;
        _draft.RefreshIntervalSeconds = (int)_refreshSeconds.Value;
        _draft.CodexExecutable = string.IsNullOrWhiteSpace(_codexPath.Text) ? "codex" : _codexPath.Text.Trim();
        DialogResult = DialogResult.OK;
        Close();
    }

    private string T(string key) => Localization.Text(_displayLanguage, key);

    private static ComboBox NewCombo() => new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
}
