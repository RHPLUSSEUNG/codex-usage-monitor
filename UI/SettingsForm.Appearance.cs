using CodexUsageMonitor.Models;
using System.Drawing.Drawing2D;

namespace CodexUsageMonitor.UI;

public sealed partial class SettingsForm
{
    private AppearancePreview _appearancePreview = null!;
    private readonly FlowLayoutPanel _inspector = new() { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
    private readonly Label _selectionTitle = new() { AutoSize = true, Margin = new Padding(3, 10, 3, 8) };
    private readonly SettingsComboBox _selectionPicker = NewCombo();
    private readonly List<GroupBox> _metricInspectors = [];
    private readonly List<GroupBox> _resetInspectors = [];
    private readonly List<Button> _modeCards = [];
    private GroupBox _backgroundInspector = null!;
    private bool _selectingInspector;
    private readonly System.Windows.Forms.Timer _appearanceTimer = new() { Interval = 1000 };
    private ThemeVariant? _lastSystemVariant;
    private (CodexPalette Palette, ThemeVariant Variant, int Background, int Foreground)? _appliedWindowTheme;

    private readonly List<SettingsRow> _appearanceRows = [];
    private readonly List<GroupBox> _appearanceSections = [];

    private Control AppearanceRow(string key, Control editor)
    {
        var row = new SettingsRow { Width = 640 };
        var label = new Label { AutoSize = true, Anchor = AnchorStyles.Left };
        SetLocalizationKey(label, key);
        row.Controls.Add(label);
        row.Controls.Add(editor);
        editor.Margin = Padding.Empty;
        void Arrange()
        {
            label.Location = new Point(0, (row.Height - label.Height) / 2);
            editor.Location = new Point(Math.Max(label.Right + 16, row.Width - editor.Width), (row.Height - editor.Height) / 2);
        }
        row.SizeChanged += (_, _) => Arrange();
        editor.SizeChanged += (_, _) => Arrange();
        label.SizeChanged += (_, _) => Arrange();
        Arrange();
        _appearanceRows.Add(row);
        return row;
    }

    private FlowLayoutPanel AppearanceSection(FlowLayoutPanel parent, string title)
    {
        var section = new SettingsSection { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(18, 12, 18, 14) };
        SetLocalizationKey(section, title);
        var rows = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Top };
        section.Controls.Add(rows);
        parent.Controls.Add(section);
        _appearanceSections.Add(section);
        return rows;
    }

    private void BuildAppearanceTab(FlowLayoutPanel content)
    {
        var presetRows = AppearanceSection(content, "Presets");
        var presets = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false };
        var savePreset = new SettingsButton { AutoSize = true };
        SetLocalizationKey(_presetLoadButton, "LoadPreset");
        SetLocalizationKey(savePreset, "SavePreset");
        _presetLoadButton.Click += (_, _) => LoadPreset(Math.Max(0, _presetSlot.SelectedIndex));
        savePreset.Click += (_, _) => SavePreset(Math.Max(0, _presetSlot.SelectedIndex));
        _presetStatus.Visible = false;
        _presetSlot.SelectedIndexChanged += (_, _) => RefreshPresetButtons();
        presets.Controls.AddRange([_presetSlot, _presetLoadButton, savePreset]);
        presetRows.Controls.Add(AppearanceRow("Presets", presets));
        presetRows.Controls.Add(_presetStatus);
        _refreshDraftEditors.Add(RefreshPresetButtons);
        RefreshPresetButtons();
        var themeRows = AppearanceSection(content, "AppearanceMode");
        var modes = new FlowLayoutPanel { Width = 660, Height = 124, WrapContents = false, Margin = new Padding(0, 8, 0, 12) };
        for (int i = 0; i < 3; i++)
        {
            int mode = i;
            var card = new SettingsButton { Width = 210, Height = 116, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 12, 0) };
            card.OverlayPaint += (_, e) => DrawModeCard(e.Graphics, card, mode);
            card.Click += (_, _) =>
            {
                _draft.FollowSystemTheme = mode == 0;
                ThemeVariant variant = mode == 0 ? AppearanceTheme.SystemVariant() : mode == 1 ? ThemeVariant.Dark : ThemeVariant.Light;
                if (mode == 0)
                    _lastSystemVariant = variant;
                if (!CompactBarPaletteCatalog.Supports(_draft.CodexPalette, variant))
                    variant = CompactBarPaletteCatalog.DefaultVariant(_draft.CodexPalette);
                ApplyColorTheme(_draft.CodexPalette, variant);
                RefreshControlsFromDraft();
                RaisePreview();
            };
            _modeCards.Add(card);
            modes.Controls.Add(card);
        }
        themeRows.Controls.Add(modes);
        _codexPalette.ItemHeight = 34;
        _codexPalette.Width = 244;
        _codexPalette.DrawItem += DrawPaletteItem;
        themeRows.Controls.Add(AppearanceRow("CodexPalette", _codexPalette));
        var barRows = AppearanceSection(content, "CompactBarStyle");
        _compactBarStyle.Width = 244;
        barRows.Controls.Add(AppearanceRow("CompactBarStyle", _compactBarStyle));
        var scaleEditor = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false };
        _compactBarScaleSlider.Width = 150;
        scaleEditor.Controls.AddRange([_compactBarScaleSlider, _compactBarScale]);
        barRows.Controls.Add(AppearanceRow("CompactBarScale", scaleEditor));
        _percentageMode.Width = 244;
        barRows.Controls.Add(AppearanceRow("DisplayBasis", _percentageMode));
        var help = new Label { AutoSize = true, MaximumSize = new Size(660, 0), Margin = new Padding(0, 4, 0, 12) };
        SetLocalizationKey(help, "AppearancePreviewHelp");
        var previewRows = AppearanceSection(content, "PreviewTitle");
        previewRows.Controls.Add(help);
        _appearancePreview = new AppearancePreview(_draft);
        _appearancePreview.SelectionChanged += RefreshInspector;
        _appearancePreview.LayoutChanged += RaisePreview;
        previewRows.Controls.Add(_appearancePreview);
        // This selector also makes hidden panels and hidden countdowns editable again.
        previewRows.Controls.Add(AppearanceRow("SelectedElement", _selectionPicker));
        _selectionPicker.SelectedIndexChanged += (_, _) =>
        {
            if (_selectingInspector) return;
            int index = _selectionPicker.SelectedIndex;
            PanelId id = (PanelId)(index - 1);
            _appearancePreview.Select(index <= 0 ? null : new CompactBarRenderer.HitRegion(id,
                CompactBarRenderer.IsResetPanel(id) ? CompactBarRenderer.Element.ResetTime : CompactBarRenderer.Element.Panel,
                Rectangle.Empty));
        };
        _selectionTitle.Visible = false;
        _inspector.Controls.Add(_selectionTitle);
        _backgroundInspector = CreateBackgroundGroup();
        _inspector.Controls.Add(_backgroundInspector);
        string[] keys = ["FiveHourLimit", "WeeklyLimit", "CpuUsage", "MemoryUsage"];
        foreach (PanelId id in new[] { PanelId.FiveHour, PanelId.Weekly, PanelId.Cpu, PanelId.Memory })
        {
            var group = CreateMetricGroup(keys[(int)id], _draft.Metric(id));
            _metricInspectors.Add(group);
            _inspector.Controls.Add(group);
            if (id is PanelId.FiveHour or PanelId.Weekly)
            {
                MetricSettings metric = _draft.Metric(id);
                var reset = new SettingsSection { AutoSize = true, Padding = new Padding(18), Text = T("ResetTime") };
                var rows = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
                var enabled = new SettingsToggle { AutoSize = true, Checked = metric.ShowResetTime };
                SetLocalizationKey(enabled, "Show");
                enabled.CheckedChanged += (_, _) => { if (!_loadingControls) { metric.ShowResetTime = enabled.Checked; RaisePreview(); } };
                _refreshDraftEditors.Add(() => enabled.Checked = metric.ShowResetTime);
                rows.Controls.Add(AppearanceRow("Show", enabled));
                enabled.Text = "";
                enabled.Tag = null;
                enabled.AccessibleName = T("Show");
                var showLabel = new SettingsToggle { AutoSize = true, Checked = metric.ShowResetTimeLabel };
                showLabel.CheckedChanged += (_, _) => { if (!_loadingControls) { metric.ShowResetTimeLabel = showLabel.Checked; RaisePreview(); } };
                _refreshDraftEditors.Add(() => showLabel.Checked = metric.ShowResetTimeLabel);
                rows.Controls.Add(AppearanceRow("ResetTimeLabel", showLabel));
                showLabel.AccessibleName = T("ResetTimeLabel");
                rows.Controls.Add(CreateColorButton("TimeColor", () => string.IsNullOrEmpty(metric.ResetTimeColor)
                    ? CompactBarPaletteCatalog.Get(_draft.CodexPalette, _draft.ThemeVariant).Foreground : metric.ResetTimeColor,
                    value => metric.ResetTimeColor = value));
                reset.Controls.Add(rows);
                _resetInspectors.Add(reset);
                _inspector.Controls.Add(reset);
            }
        }
        content.Controls.Add(_inspector);
        void ResizeAppearance()
        {
            int available = Math.Max(400, content.ClientSize.Width - content.Padding.Horizontal - 10);
            int inner = available - 36;
            foreach (var row in _appearanceRows) row.Width = inner;
            foreach (var section in _appearanceSections) { section.MinimumSize = new Size(available, 0); section.Width = available; }
            modes.Width = inner;
            foreach (var card in _modeCards) card.Width = Math.Max(80, inner / 3 - card.Margin.Horizontal);
            _appearancePreview.Width = inner;
            help.MaximumSize = new Size(inner, 0);
            _inspector.MinimumSize = new Size(available, 0);
            _inspector.Width = available;
            _backgroundInspector.MinimumSize = new Size(available, 0);
            _backgroundInspector.Width = available;
            foreach (var group in _metricInspectors.Concat(_resetInspectors)) { group.MinimumSize = new Size(available, 0); group.Width = available; }
            _appearancePreview.RefreshPreview();
        }
        content.SizeChanged += (_, _) => ResizeAppearance();
        ResizeAppearance();
        RefreshAppearanceLabels();
        _lastSystemVariant = AppearanceTheme.SystemVariant();
        _appearanceTimer.Tick += (_, _) => RefreshSystemThemeIfChanged();
        _appearanceTimer.Start();
        Shown += (_, _) => RefreshAppearance();
        FormClosed += (_, _) => _appearanceTimer.Stop();
        Disposed += (_, _) => _appearanceTimer.Dispose();
        _tabs.Alignment = TabAlignment.Left;
        _tabs.SizeMode = TabSizeMode.Fixed;
        _tabs.ItemSize = new Size(46, 142);
        _tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        _tabs.DrawItem += (_, e) =>
        {
            Color sidebar = SettingsPaint.Blend(BackColor, ForeColor, .035f);
            using var background = new SolidBrush(sidebar);
            e.Graphics.FillRectangle(background, e.Bounds);
            bool hover = e.Bounds.Contains(_tabs.PointToClient(Cursor.Position));
            if (e.Index == _tabs.SelectedIndex || hover)
                SettingsPaint.Surface(e.Graphics, Rectangle.Inflate(e.Bounds, -8, -4), SettingsPaint.Blend(sidebar, ForeColor, hover ? .12f : .08f));
            TextRenderer.DrawText(e.Graphics, _tabs.TabPages[e.Index].Text, Font, Rectangle.Inflate(e.Bounds, -20, 0), ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        };
    }

    internal void UpdatePreviewData(UsageSnapshot snapshot, SystemUsageSnapshot systemUsage)
    {
        _appearancePreview.Snapshot = snapshot;
        _appearancePreview.SystemUsage = systemUsage;
        if (Visible) _appearancePreview.RefreshPreview();
    }

    private void RefreshAppearanceLabels()
    {
        _selectingInspector = true;
        int selection = Math.Max(0, _selectionPicker.SelectedIndex);
        _selectionPicker.Items.Clear();
        _selectionPicker.Items.AddRange([T("AppearanceBackground"), "5H", "WK", "CPU", "RAM", "5H · " + T("ResetTime"), "WK · " + T("ResetTime")]);
        _selectionPicker.SelectedIndex = selection;
        _selectingInspector = false;
        string[] names = [T("SystemMode"), T("DarkMode"), T("WhiteMode")];
        for (int i = 0; i < _modeCards.Count; i++) _modeCards[i].AccessibleName = names[i];
        RefreshInspector();
        foreach (var card in _modeCards) card.Invalidate();
    }

    private void RefreshInspector()
    {
        var hit = _appearancePreview.Selection;
        _inspector.SuspendLayout();
        _backgroundInspector.Visible = hit is null;
        foreach (var group in _metricInspectors) group.Visible = false;
        foreach (var group in _resetInspectors) group.Visible = false;
        if (hit is null) _selectionTitle.Text = T("AppearanceBackground");
        else
        {
            string key = hit.Element switch
            {
                CompactBarRenderer.Element.Label => "AppearanceLabel",
                CompactBarRenderer.Element.Bar => "AppearanceBar",
                CompactBarRenderer.Element.Percent => "PercentOnly",
                CompactBarRenderer.Element.ResetTime => "ResetTime",
                _ => "AppearancePanel"
            };
            _selectionTitle.Text = CompactBarRenderer.PanelTitle(hit.Panel) + " · " + T(key);
            if (CompactBarRenderer.IsResetPanel(hit.Panel))
                _resetInspectors[(int)hit.Panel - 4].Visible = true;
            else
            {
                var group = _metricInspectors[(int)hit.Panel];
                group.Visible = true;
                foreach (Control row in group.Controls[0].Controls)
                {
                    if (row.Tag is not string tag || !tag.StartsWith("metric-")) continue;
                    row.Visible = hit.Element == CompactBarRenderer.Element.Panel || tag switch
                    {
                        "metric-label" => hit.Element == CompactBarRenderer.Element.Label,
                        "metric-percent" => hit.Element == CompactBarRenderer.Element.Percent,
                        _ => hit.Element == CompactBarRenderer.Element.Bar
                    };
                }
            }
        }
        GroupBox activeInspector = hit is null ? _backgroundInspector
            : CompactBarRenderer.IsResetPanel(hit.Panel) ? _resetInspectors[(int)hit.Panel - 4] : _metricInspectors[(int)hit.Panel];
        activeInspector.Text = _selectionTitle.Text;
        _selectingInspector = true;
        _selectionPicker.SelectedIndex = hit is null ? 0 : 1 + (int)hit.Panel;
        _selectingInspector = false;
        _inspector.ResumeLayout(true);
    }

    private void RefreshAppearance()
    {
        var resolved = AppearanceTheme.Resolve(_draft);
        if (!ReferenceEquals(resolved, _draft))
        {
            _draft.ThemeVariant = resolved.ThemeVariant;
            _draft.BackgroundColor = resolved.BackgroundColor;
            foreach (PanelId id in new[] { PanelId.FiveHour, PanelId.Weekly, PanelId.Cpu, PanelId.Memory })
                AppSettings.CopyMetricInto(resolved.Metric(id), _draft.Metric(id));
            RefreshControlsFromDraft();
        }
        _appearancePreview.RefreshPreview();
        ApplyWindowTheme();
        foreach (var card in _modeCards) card.Invalidate();
    }

    private void RefreshSystemThemeIfChanged()
    {
        if (!Visible || !_draft.FollowSystemTheme)
            return;

        ThemeVariant current = AppearanceTheme.SystemVariant();
        if (_lastSystemVariant == current)
            return;

        _lastSystemVariant = current;
        RefreshAppearance();
        _pendingPreview = _draft.Copy();
        if (!_previewTimer.Enabled)
            _previewTimer.Start();
    }

    private void ApplyWindowTheme()
    {
        var colors = CompactBarPaletteCatalog.Get(_draft.CodexPalette, AppearanceTheme.Resolve(_draft).ThemeVariant);
        Color background = HexColor.ParseOrDefault(colors.Background, Color.Black);
        Color foreground = HexColor.ParseOrDefault(colors.Foreground, Color.White);
        Color surface = SettingsPaint.Blend(background, foreground, .045f);
        Color editor = SettingsPaint.Blend(background, foreground, .08f);
        Color accent = HexColor.ParseOrDefault(colors.Accent, foreground);
        var themeKey = (_draft.CodexPalette, AppearanceTheme.Resolve(_draft).ThemeVariant,
            background.ToArgb(), foreground.ToArgb());
        void Apply(Control control)
        {
            if (control.Tag is not string tag || tag != "color-swatch")
            {
                control.BackColor = control is Button or SettingsComboBox or SettingsNumericUpDown or TextBox ? editor : control is SettingsSection ? surface : control.Parent is TabControl ? background : control.Parent?.BackColor ?? background;
                control.ForeColor = foreground;
            }
            if (control is SettingsButton themedButton) themedButton.Accent = accent;
            if (control is SettingsComboBox combo) combo.Accent = accent;
            if (control is SettingsToggle toggle) toggle.Accent = accent;
            if (control is SettingsTrackBar slider) slider.Accent = accent;
            if (control is Button button) { button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = surface; button.FlatAppearance.MouseOverBackColor = SettingsPaint.Blend(editor, foreground, .12f); button.FlatAppearance.MouseDownBackColor = SettingsPaint.Blend(editor, foreground, .2f); }
            if (control is LinkLabel link) { link.LinkColor = foreground; link.ActiveLinkColor = foreground; }
            foreach (Control child in control.Controls) Apply(child);
        }
        if (_appliedWindowTheme != themeKey)
        {
            SuspendLayout();
            try
            {
                BackColor = background;
                Apply(this);
                _appliedWindowTheme = themeKey;
            }
            finally
            {
                ResumeLayout(performLayout: true);
            }
        }
        if (IsHandleCreated)
        {
            int dark = CompactBarTheme.IsLight(AppearanceTheme.Resolve(_draft).ThemeVariant) ? 0 : 1;
            int caption = ColorTranslator.ToWin32(background);
            int text = ColorTranslator.ToWin32(foreground);
            DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
            DwmSetWindowAttribute(Handle, 35, ref caption, sizeof(int));
            DwmSetWindowAttribute(Handle, 36, ref text, sizeof(int));
        }
    }

    private void DrawPaletteItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var colors = CompactBarPaletteCatalog.Get(PaletteValues[e.Index], AppearanceTheme.Resolve(_draft).ThemeVariant);
        using var background = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? SettingsPaint.Blend(_codexPalette.BackColor, ForeColor, .14f) : _codexPalette.BackColor);
        e.Graphics.FillRectangle(background, e.Bounds);
        var swatch = new Rectangle(e.Bounds.X + 6, e.Bounds.Y + 4, 26, e.Bounds.Height - 8);
        using var fill = new SolidBrush(HexColor.ParseOrDefault(colors.Background, Color.White));
        e.Graphics.FillRectangle(fill, swatch);
        TextRenderer.DrawText(e.Graphics, "Aa", Font, swatch, HexColor.ParseOrDefault(colors.Accent, Color.Green), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(e.Graphics, PaletteNames()[e.Index], Font, new Rectangle(e.Bounds.X + 39, e.Bounds.Y, e.Bounds.Width - 39, e.Bounds.Height), ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        if ((e.State & DrawItemState.ComboBoxEdit) == 0 && e.Index == _codexPalette.SelectedIndex)
            TextRenderer.DrawText(e.Graphics, "✓", Font, new Rectangle(e.Bounds.Right - 24, e.Bounds.Y, 22, e.Bounds.Height), ForeColor, TextFormatFlags.VerticalCenter);
        e.DrawFocusRectangle();
    }

    private void DrawModeCard(Graphics graphics, Button card, int mode)
    {
        bool selected = _draft.FollowSystemTheme ? mode == 0 : mode == (_draft.ThemeVariant == ThemeVariant.Light ? 2 : 1);
        if (selected)
        {
            using var border = new Pen(HexColor.ParseOrDefault(CompactBarPaletteCatalog.Get(_draft.CodexPalette, _draft.ThemeVariant).Accent, Color.Green), 2);
            using var outline = SettingsPaint.Rounded(new Rectangle(2, 2, card.Width - 5, card.Height - 5));
            graphics.DrawPath(border, outline);
        }
        for (int half = 0; half < 2; half++)
        {
            bool light = mode == 2 || mode == 0 && half == 0;
            var colors = CompactBarPaletteCatalog.Get(_draft.CodexPalette, light ? ThemeVariant.Light : ThemeVariant.Dark);
            Color bg = light ? Color.FromArgb(244, 244, 240) : Color.FromArgb(30, 35, 38);
            int x = 14 + half * (card.Width - 28) / 2;
            int width = (card.Width - 28) / 2;
            using var fill = new SolidBrush(bg);
            using var accent = new SolidBrush(HexColor.ParseOrDefault(colors.Accent, Color.Green));
            using var track = new SolidBrush(light ? Color.LightGray : Color.FromArgb(65, 75, 80));
            graphics.FillRectangle(fill, x, 28, width, 38);
            for (int row = 0; row < 2; row++)
            {
                graphics.FillRectangle(accent, x + 6, 36 + row * 13, 13, 6);
                graphics.FillRectangle(track, x + 24, 36 + row * 13, width - 32, 6);
                graphics.FillRectangle(accent, x + 24, 36 + row * 13, (width - 32) / (row + 2), 6);
            }
        }
        string label = mode == 0 ? T("SystemMode") : mode == 1 ? T("DarkMode") : T("WhiteMode");
        TextRenderer.DrawText(graphics, label, Font, new Rectangle(0, 79, card.Width, 26), ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    private sealed class AppearanceTabs : TabControl
    {
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }
        public AppearanceTabs() => SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? BackColor);
            using var sidebar = new SolidBrush(SettingsPaint.Blend(Parent?.BackColor ?? BackColor, ForeColor, .035f));
            e.Graphics.FillRectangle(sidebar, new Rectangle(0, 0, DisplayRectangle.Left, Height));
            for (int i = 0; i < TabCount; i++)
                OnDrawItem(new DrawItemEventArgs(e.Graphics, Font, GetTabRect(i), i,
                    i == SelectedIndex ? DrawItemState.Selected : DrawItemState.Default));
        }
    }
}
