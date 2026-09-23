using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using CodexUsageMonitor.Models;

namespace CodexUsageMonitor.UI;

internal static class CompactBarRenderer
{
    private const int GridCellWidth = 158;
    private const int GridRowHeight = 22;
    private const int ResetLabelColumnWidth = 24;
    private static readonly string[] LinearPanelTitles = ["5H", "WK", "CPU", "RAM"];

    internal enum Element { Panel, Label, Bar, Percent, ResetTime }
    internal sealed record HitRegion(PanelId Panel, Element Element, Rectangle Bounds);
    internal sealed record PanelLayout(PanelId Panel, Rectangle Bounds, Rectangle Content, Rectangle Reset);

    internal static string FormatResetTime(DateTimeOffset? resetsAt, bool weekly, DateTimeOffset now)
    {
        if (resetsAt is null) return "--";
        TimeSpan remaining = resetsAt.Value - now;
        if (weekly && remaining.TotalDays >= 1) return $"{(int)remaining.TotalDays:00}d";
        if (remaining.TotalHours >= 1) return $"{(int)remaining.TotalHours:00}h";
        return $"{Math.Max(0, (int)remaining.TotalMinutes):00}m";
    }

    internal static string FormatResetTooltip(UsageSnapshot snapshot, AppLanguage language)
    {
        string Line(string titleKey, DateTimeOffset? resetsAt)
        {
            string reset = resetsAt is null
                ? Localization.Text(language, "ResetUnknown")
                : Localization.Format(language, "ResetAt",
                    resetsAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            return $"{Localization.Text(language, titleKey)} · {reset}";
        }
        return Line("FiveHour", snapshot.FiveHour?.ResetsAt) + Environment.NewLine
            + Line("Weekly", snapshot.Weekly?.ResetsAt);
    }

    internal static string FormatResetPanelText(PanelId panel, AppSettings settings, UsageSnapshot snapshot, DateTimeOffset now)
    {
        bool weekly = panel == PanelId.WeeklyReset;
        string label = settings.Metric(panel).ShowResetTimeLabel ? (weekly ? "WK " : "5H ") : "";
        return label + FormatResetCountdown(panel, snapshot, now);
    }

    private static string FormatResetCountdown(PanelId panel, UsageSnapshot snapshot, DateTimeOffset now)
    {
        bool weekly = panel == PanelId.WeeklyReset;
        DateTimeOffset? resetsAt = (weekly ? snapshot.Weekly : snapshot.FiveHour)?.ResetsAt;
        return "↻ " + FormatResetTime(resetsAt, weekly, now);
    }

    internal static List<PanelLayout> Layout(AppSettings settings, float scale, int taskbarHeight)
    {
        PanelId[] order = HasCompletePanelOrder(settings.PanelOrder)
            ? settings.PanelOrder
            : AppSettings.NormalizePanelOrder(settings.PanelOrder);
        var result = new List<PanelLayout>(6);
        bool rows = settings.CompactBarStyle == CompactBarStyle.CompactRows;
        bool gauges = settings.CompactBarStyle == CompactBarStyle.CircularGauges;
        bool icons = settings.CompactBarStyle == CompactBarStyle.MinimalIcons;
        int width = settings.CompactBarStyle switch
        {
            CompactBarStyle.CompactRows => S(250, scale),
            CompactBarStyle.CircularGauges => Math.Max(S(50, scale), (int)Math.Round(taskbarHeight * 1.15)),
            CompactBarStyle.MinimalIcons => S(74, scale),
            CompactBarStyle.Cards or CompactBarStyle.RoundedCapsules or CompactBarStyle.LabelBoxes => S(164, scale),
            _ => S(GridCellWidth, scale)
        };
        int height = settings.CompactBarStyle switch
        {
            CompactBarStyle.CircularGauges => taskbarHeight,
            CompactBarStyle.MinimalIcons => S(48, scale),
            CompactBarStyle.Cards => S(28, scale),
            CompactBarStyle.RoundedCapsules => S(27, scale),
            _ => S(22, scale)
        };
        bool firstRow = IsPanelVisible(settings, order[0])
                        || IsPanelVisible(settings, order[1])
                        || IsPanelVisible(settings, order[2]);
        int[] rowX = [S(2, scale), S(2, scale)];
        int offset = S(2, scale);
        for (int slot = 0; slot < order.Length; slot++)
        {
            PanelId id = order[slot];
            if (!IsPanelVisible(settings, id))
                continue;
            int row = slot / 3;
            int x, y, panelWidth;
            if (IsResetPanel(id))
            {
                int textWidth = settings.Metric(id).ShowResetTimeLabel ? 70 : 45;
                panelWidth = S(textWidth, scale);
            }
            else panelWidth = width;
            if (rows) { x = S(2, scale); y = offset; offset += height + S(2, scale); }
            else if (gauges || icons) { x = offset; y = S(2, scale); offset += panelWidth + S(4, scale); }
            else
            {
                x = rowX[row];
                y = S(2, scale) + (row == 1 && firstRow ? height + S(2, scale) : 0);
                rowX[row] += panelWidth + S(4, scale);
            }
            var bounds = new Rectangle(x, y, panelWidth, height);
            result.Add(new(id, bounds, bounds, IsResetPanel(id) ? bounds : Rectangle.Empty));
        }
        return result;
    }

    private static bool HasCompletePanelOrder(PanelId[]? order)
    {
        if (order is not { Length: 6 })
            return false;
        int seen = 0;
        foreach (PanelId id in order)
        {
            int value = (int)id;
            if ((uint)value >= 6u || (seen & (1 << value)) != 0)
                return false;
            seen |= 1 << value;
        }
        return seen == 0b11_1111;
    }

    internal static bool IsResetPanel(PanelId id) => id is PanelId.FiveHourReset or PanelId.WeeklyReset;

    internal static bool IsPanelVisible(AppSettings settings, PanelId id) => IsResetPanel(id)
        ? settings.Metric(id).ShowResetTime : settings.Metric(id).Enabled;

    public static Size CalculateSize(AppSettings settings, float scale, int taskbarHeight)
        => CalculateSize(Layout(settings, scale, taskbarHeight), scale);

    internal static Size CalculateSize(IReadOnlyList<PanelLayout> panels, float scale)
    {
        if (panels.Count == 0)
            return new Size(1, 1);
        int right = 0, bottom = 0;
        foreach (PanelLayout panel in panels)
        {
            right = Math.Max(right, panel.Bounds.Right);
            bottom = Math.Max(bottom, panel.Bounds.Bottom);
        }
        return new Size(right + S(2, scale), bottom + S(2, scale));
    }

    public static void Draw(Graphics graphics, Size size, AppSettings settings, UsageSnapshot snapshot,
        SystemUsageSnapshot systemUsage, float scale, bool themeResolved = false) =>
        DrawWithRegions(graphics, size, settings, snapshot, systemUsage, scale, themeResolved: themeResolved);

    internal static void DrawWithRegions(Graphics graphics, Size size, AppSettings settings, UsageSnapshot snapshot,
        SystemUsageSnapshot systemUsage, float scale, List<HitRegion>? regions = null, bool themeResolved = false)
    {
        if (!themeResolved)
            settings = AppearanceTheme.Resolve(settings);
        MetricVisual[] metrics = Metrics(settings, snapshot, systemUsage);
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        graphics.TextContrast = 0;
        DrawBackground(graphics, size, settings, metrics, scale);
        int gaugeHeight = Math.Max(1, size.Height - S(4, scale));
        using var primaryFont = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
        int maxLinearTitleWidth = Math.Max(S(29, scale), LinearPanelTitles.Max(title =>
            TextRenderer.MeasureText(graphics, title, primaryFont, Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width));
        foreach (var panel in Layout(settings, scale, gaugeHeight))
        {
            regions?.Add(new(panel.Panel, Element.Panel, panel.Bounds));
            void Hit(Element element, Rectangle bounds) => regions?.Add(new(panel.Panel, element, bounds));
            if (IsResetPanel(panel.Panel))
            {
                MetricSettings resetSettings = settings.Metric(panel.Panel);
                string resetText = FormatResetCountdown(panel.Panel, snapshot, DateTimeOffset.Now);
                Color resetColor = HexColor.ParseOrDefault(resetSettings.ResetTimeColor,
                    CompactBarTheme.Foreground(settings.CodexPalette, settings.ThemeVariant));
                // GDI+ keeps the countdown glyphs and their panel width on the same DPI scale.
                Rectangle timeBounds = panel.Bounds;
                if (resetSettings.ShowResetTimeLabel)
                {
                    int labelWidth = S(ResetLabelColumnWidth, scale);
                    var labelBounds = new Rectangle(panel.Bounds.X, panel.Bounds.Y, labelWidth, panel.Bounds.Height);
                    DrawThemeText(graphics, panel.Panel == PanelId.WeeklyReset ? "WK" : "5H", primaryFont,
                        labelBounds, resetColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                        TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix, true);
                    timeBounds = new Rectangle(labelBounds.Right, panel.Bounds.Y,
                        panel.Bounds.Width - labelWidth, panel.Bounds.Height);
                }
                DrawThemeText(graphics, resetText, primaryFont, timeBounds, resetColor,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix,
                    true);
                Hit(Element.ResetTime, panel.Reset);
                continue;
            }
            MetricVisual metric = metrics[(int)panel.Panel];
            if (settings.CompactBarStyle is CompactBarStyle.CircularGauges or CompactBarStyle.MinimalIcons)
            {
                var state = graphics.Save();
                graphics.TranslateTransform(panel.Content.X, panel.Content.Y);
                graphics.SetClip(new Rectangle(Point.Empty, panel.Content.Size));
                if (settings.CompactBarStyle == CompactBarStyle.CircularGauges)
                    DrawCircularGauges(graphics, [metric], scale, settings.ThemeVariant, settings.CodexPalette);
                else DrawMinimalIcons(graphics, [metric], scale, settings.ThemeVariant, settings.CodexPalette);
                graphics.Restore(state);
                Hit(Element.Label, new Rectangle(panel.Content.X, panel.Content.Y, panel.Content.Width, S(18, scale)));
                var lower = new Rectangle(panel.Content.X, panel.Content.Y + S(18, scale), panel.Content.Width, Math.Max(1, panel.Content.Height - S(18, scale)));
                if (metric.Settings.Presentation != MetricPresentation.PercentOnly) Hit(Element.Bar, lower);
                if (metric.Settings.Presentation != MetricPresentation.BarOnly)
                {
                    var value = lower;
                    if (settings.CompactBarStyle == CompactBarStyle.CircularGauges) value.Inflate(-S(10, scale), -S(5, scale));
                    else value.Height = Math.Min(value.Height, S(22, scale));
                    Hit(Element.Percent, value);
                }
            }
            else if (settings.CompactBarStyle == CompactBarStyle.CompactRows)
            {
                var state = graphics.Save();
                graphics.TranslateTransform(panel.Content.X, panel.Content.Y - S(4, scale));
                DrawCompactRows(graphics, [metric], scale, settings.ThemeVariant, settings.CodexPalette);
                graphics.Restore(state);
                Hit(Element.Label, new Rectangle(panel.Content.X, panel.Content.Y, S(70, scale), panel.Content.Height));
                if (metric.Settings.Presentation != MetricPresentation.PercentOnly)
                    Hit(Element.Bar, new Rectangle(panel.Content.X + S(74, scale), panel.Content.Y, S(129, scale), panel.Content.Height));
                if (metric.Settings.Presentation != MetricPresentation.BarOnly)
                    Hit(Element.Percent, new Rectangle(panel.Content.X + S(207, scale), panel.Content.Y, S(42, scale), panel.Content.Height));
            }
            else
            {
                LinearStyle style = settings.CompactBarStyle switch
                {
                    CompactBarStyle.LabelBoxes => LinearStyle.LabelBoxes,
                    CompactBarStyle.Cards => LinearStyle.Cards,
                    CompactBarStyle.RoundedCapsules => LinearStyle.Capsules,
                    CompactBarStyle.NeonGlow => LinearStyle.Neon,
                    CompactBarStyle.Light => LinearStyle.Light,
                    CompactBarStyle.Gradient => LinearStyle.Gradient,
                    _ => LinearStyle.Dark
                };
                DrawLinearMetric(graphics, panel.Content, metric, style, settings.ThemeVariant,
                    settings.CodexPalette, scale, primaryFont, maxLinearTitleWidth, Hit);
            }
        }
    }

    internal static string PanelTitle(PanelId id) => id switch
    {
        PanelId.FiveHour => "5H", PanelId.Weekly => "WK", PanelId.Cpu => "CPU",
        PanelId.FiveHourReset => "5H ↻", PanelId.WeeklyReset => "WK ↻", _ => "RAM"
    };

    private static MetricVisual[] Metrics(
        AppSettings settings,
        UsageSnapshot? snapshot,
        SystemUsageSnapshot? systemUsage)
    {
        double? fiveHour = snapshot?.FiveHour is null
            ? null
            : settings.PercentageMode == PercentageMode.Remaining
                ? snapshot.FiveHour.RemainingPercent
                : snapshot.FiveHour.UsedPercent;
        double? weekly = snapshot?.Weekly is null
            ? null
            : settings.PercentageMode == PercentageMode.Remaining
                ? snapshot.Weekly.RemainingPercent
                : snapshot.Weekly.UsedPercent;

        return
        [
            new MetricVisual(0, "5H", fiveHour, settings.FiveHour, MetricIcon.Hourglass),
            new MetricVisual(1, "WK", weekly, settings.Weekly, MetricIcon.Calendar),
            new MetricVisual(2, "CPU", systemUsage?.CpuPercent, settings.Cpu, MetricIcon.Cpu),
            new MetricVisual(3, "RAM", systemUsage?.MemoryPercent, settings.Memory, MetricIcon.Memory)
        ];
    }

    private static void DrawBackground(
        Graphics graphics,
        Size size,
        AppSettings settings,
        MetricVisual[] metrics,
        float scale)
    {
        Color configured = HexColor.ParseOrDefault(settings.BackgroundColor, Color.FromArgb(255, 22, 24, 28));
        // UpdateLayeredWindow lets fully transparent pixels pass mouse input
        // through to windows behind the bar. Alpha 1 remains visually
        // transparent while keeping the complete Compact Bar draggable.
        int alpha = settings.TransparentBackground ? 1 : Math.Max(1, (int)configured.A);
        Rectangle area = new(0, 0, Math.Max(1, size.Width - 1), Math.Max(1, size.Height - 1));
        int radius = S(8, scale);
        using GraphicsPath backgroundPath = Rounded(area, radius);

        if (settings.CompactBarStyle == CompactBarStyle.Gradient)
        {
            Color left = MetricColor(metrics[0], Color.FromArgb(52, 130, 150));
            Color right = MetricColor(metrics[3], Color.FromArgb(90, 68, 170));
            bool lightTheme = CompactBarTheme.IsLight(settings.ThemeVariant);
            Color baseColor = lightTheme
                ? Color.FromArgb(244, 247, 250)
                : configured;
            using var gradient = new LinearGradientBrush(
                area,
                Color.FromArgb(alpha, Mix(left, baseColor, lightTheme ? 0.82f : 0.58f)),
                Color.FromArgb(alpha, Mix(right, baseColor, lightTheme ? 0.82f : 0.55f)),
                LinearGradientMode.Horizontal);
            graphics.FillPath(gradient, backgroundPath);
            return;
        }

        using (var background = new SolidBrush(Color.FromArgb(alpha, configured.R, configured.G, configured.B)))
            graphics.FillPath(background, backgroundPath);

        if (metrics.Any(m => m.Settings.Enabled) && settings.CompactBarStyle == CompactBarStyle.NeonGlow && size.Width > 4 && size.Height > 4)
        {
            Color accent = MetricColor(metrics.FirstOrDefault(metric => metric.Settings.Enabled), Color.Cyan);
            using var border = new Pen(Color.FromArgb(170, accent), S(1, scale));
            graphics.DrawPath(border, backgroundPath);
        }
    }

    private static void DrawLinearMetric(
        Graphics graphics,
        Rectangle bounds,
        MetricVisual metric,
        LinearStyle style,
        ThemeVariant theme,
        CodexPalette palette,
        float scale,
        Font font,
        int maxTitleWidth,
        Action<Element, Rectangle>? hit = null)
    {
        bool light = CompactBarTheme.IsLight(theme);
        Color fillColor = MetricColor(metric, Color.FromArgb(98, 214, 167));
        Color trackColor = CompactBarTheme.Track(metric.Settings.TrackColor, palette, theme);
        Color textColor = CompactBarTheme.Foreground(palette, theme);
        Color titleColor = style is LinearStyle.Neon or LinearStyle.LabelBoxes
            ? fillColor
            : textColor;
        if (style is LinearStyle.Cards or LinearStyle.Capsules)
        {
            int radius = style == LinearStyle.Capsules ? bounds.Height / 2 : S(5, scale);
            using var cellBrush = new SolidBrush(Color.FromArgb(light ? 38 : 30, fillColor.R, fillColor.G, fillColor.B));
            using GraphicsPath cellPath = Rounded(bounds, radius);
            graphics.FillPath(cellBrush, cellPath);
        }

        titleColor = HexColor.ParseOrDefault(metric.Settings.LabelColor, titleColor);
        textColor = HexColor.ParseOrDefault(metric.Settings.PercentColor, textColor);
        using var textBrush = new SolidBrush(textColor);
        using var titleBrush = new SolidBrush(titleColor);
        string percentText = PercentText(metric.Percent);
        int measuredTitleWidth = TextRenderer.MeasureText(graphics, metric.Title, font, Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
        int graphStartX = bounds.X + maxTitleWidth + (style switch
        {
            LinearStyle.Cards => S(5, scale) + S(18, scale) + S(5, scale),
            LinearStyle.LabelBoxes => S(1, scale) + S(10, scale) + S(5, scale),
            _ => S(5, scale) + S(5, scale)
        });
        if (metric.Slot < 2)
            graphStartX -= S(6, scale);
        int contentX;

        if (style == LinearStyle.LabelBoxes)
        {
            var label = new Rectangle(
                bounds.X + S(1, scale),
                bounds.Y + S(1, scale),
                measuredTitleWidth + S(10, scale),
                bounds.Height - S(2, scale));
            using (var labelBackground = new SolidBrush(Color.FromArgb(48, fillColor.R, fillColor.G, fillColor.B)))
            using (GraphicsPath labelPath = Rounded(label, S(4, scale)))
                graphics.FillPath(labelBackground, labelPath);
            DrawCenteredText(graphics, metric.Title, font, titleBrush, label, light);
            contentX = Math.Max(label.Right + S(5, scale), graphStartX);
        }
        else
        {
            contentX = bounds.X + S(5, scale);
            if (style == LinearStyle.Cards)
            {
                DrawIcon(graphics, metric.Icon, new Rectangle(contentX, bounds.Y + S(6, scale), S(13, scale), S(13, scale)), titleColor, scale);
                contentX += S(18, scale);
            }
            int titleWidth = measuredTitleWidth + S(5, scale);
            var titleBounds = new Rectangle(contentX, bounds.Y, titleWidth, bounds.Height);
            DrawThemeText(graphics, metric.Title, font,
                titleBounds, titleBrush.Color,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix, light);
            contentX = Math.Max(contentX + titleWidth, graphStartX);
        }

        hit?.Invoke(Element.Label, new Rectangle(bounds.X, bounds.Y, contentX - bounds.X, bounds.Height));
        // Measure the value as in the original compact bar. A longer value
        // takes space from the track, while hiding it leaves that space reserved.
        int valueWidth = TextRenderer.MeasureText(percentText, font, Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width + S(4, scale);
        var valueBounds = new Rectangle(bounds.Right - valueWidth, bounds.Y,
            valueWidth - S(3, scale), bounds.Height);
        if (metric.Settings.Presentation != MetricPresentation.BarOnly)
        {
            DrawThemeText(graphics, percentText, font, valueBounds, textBrush.Color,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix, light);
            hit?.Invoke(Element.Percent, valueBounds);
        }
        if (metric.Settings.Presentation != MetricPresentation.PercentOnly)
        {
            var track = new Rectangle(contentX, bounds.Y + (bounds.Height - S(6, scale)) / 2,
                Math.Max(S(8, scale), bounds.Right - contentX - valueWidth - S(4, scale)), S(6, scale));
            hit?.Invoke(Element.Bar, new Rectangle(track.X, bounds.Y, track.Width, bounds.Height));
            DrawTrack(graphics, track, metric.Percent, trackColor, fillColor, style == LinearStyle.Neon, scale);
        }
    }

    private static void DrawCircularGauges(Graphics graphics, MetricVisual[] metrics, float scale, ThemeVariant theme, CodexPalette palette)
    {
        bool lightTheme = CompactBarTheme.IsLight(theme);
        int height = Math.Max(1, (int)Math.Round(graphics.VisibleClipBounds.Height));
        int titleHeight = Math.Min(S(17, scale), Math.Max(S(12, scale), height / 3));
        int diameter = Math.Max(S(12, scale), height - titleHeight - S(4, scale));
        int cellWidth = Math.Max(S(50, scale), (int)Math.Round(height * 1.15d));
        int index = 0;
        foreach (MetricVisual metric in metrics.Where(metric => metric.Settings.Enabled))
        {
            Color color = MetricColor(metric, Color.CornflowerBlue);
            int x = S(3, scale) + index * cellWidth;
            var gauge = new Rectangle(
                x + (cellWidth - diameter) / 2,
                titleHeight + S(1, scale),
                diameter,
                diameter);
            using var font = new Font("Segoe UI", 8.5f, FontStyle.Bold, GraphicsUnit.Point);
            using var boldFont = new Font("Segoe UI", diameter < S(34, scale) ? 7.5f : 9f, FontStyle.Bold, GraphicsUnit.Point);
            using var titleBrush = new SolidBrush(HexColor.ParseOrDefault(metric.Settings.LabelColor, color));
            Color textColor = CompactBarTheme.Foreground(palette, theme);
            using var valueBrush = new SolidBrush(HexColor.ParseOrDefault(metric.Settings.PercentColor, textColor));
            DrawCenteredText(graphics, metric.Title, font, titleBrush, new Rectangle(x, 0, cellWidth, titleHeight), lightTheme);
            float penWidth = Math.Max(2f, Math.Min(S(6, scale), diameter / 7f));
            using var trackPen = new Pen(CompactBarTheme.Track(metric.Settings.TrackColor, palette, theme), penWidth);
            using var fillPen = new Pen(color, penWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            if (metric.Settings.Presentation != MetricPresentation.PercentOnly)
            {
                graphics.DrawArc(trackPen, gauge, -90, 360);
                if (metric.Percent is not null)
                    graphics.DrawArc(fillPen, gauge, -90, (float)(360d * Math.Clamp(metric.Percent.Value, 0d, 100d) / 100d));
            }
            if (metric.Settings.Presentation != MetricPresentation.BarOnly)
                DrawCenteredText(graphics, PercentText(metric.Percent), boldFont, valueBrush, gauge, lightTheme);
            index++;
        }
    }

    private static void DrawCompactRows(Graphics graphics, MetricVisual[] metrics, float scale, ThemeVariant theme, CodexPalette palette)
    {
        int row = 0;
        foreach (MetricVisual metric in metrics.Where(metric => metric.Settings.Enabled))
        {
            int y = S(4 + row * 21, scale);
            Color color = MetricColor(metric, Color.CornflowerBlue);
            Color track = CompactBarTheme.Track(metric.Settings.TrackColor, palette, theme);
            Color textColor = CompactBarTheme.Foreground(palette, theme);
            using var font = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
            using var boldFont = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
            using var textBrush = new SolidBrush(textColor);
            DrawIcon(graphics, metric.Icon, new Rectangle(S(6, scale), y + S(3, scale), S(14, scale), S(14, scale)), textColor, scale);
            using var labelBrush = new SolidBrush(HexColor.ParseOrDefault(metric.Settings.LabelColor, textColor));
            using var percentBrush = new SolidBrush(HexColor.ParseOrDefault(metric.Settings.PercentColor, textColor));
            graphics.DrawString(metric.Title, font, labelBrush, S(27, scale), y);

            int trackX = S(74, scale);
            int valueWidth = S(42, scale);
            if (metric.Settings.Presentation != MetricPresentation.PercentOnly)
            {
                var trackBounds = new Rectangle(trackX, y + S(7, scale), S(245, scale) - trackX - valueWidth, S(6, scale));
                DrawTrack(graphics, trackBounds, metric.Percent, track, color, false, scale);
            }
            if (metric.Settings.Presentation != MetricPresentation.BarOnly)
                graphics.DrawString(PercentText(metric.Percent), boldFont, percentBrush, S(207, scale), y);
            row++;
        }
    }

    private static void DrawMinimalIcons(Graphics graphics, MetricVisual[] metrics, float scale, ThemeVariant theme, CodexPalette palette)
    {
        bool lightTheme = CompactBarTheme.IsLight(theme);
        int index = 0;
        foreach (MetricVisual metric in metrics.Where(metric => metric.Settings.Enabled))
        {
            int x = S(5 + index * 74, scale);
            Color color = MetricColor(metric, Color.CornflowerBlue);
            using var font = new Font("Segoe UI", 9f, FontStyle.Bold, GraphicsUnit.Point);
            using var boldFont = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
            Color textColor = CompactBarTheme.Foreground(palette, theme);
            using var titleBrush = new SolidBrush(HexColor.ParseOrDefault(metric.Settings.LabelColor, textColor));
            using var valueBrush = new SolidBrush(HexColor.ParseOrDefault(metric.Settings.PercentColor, color));
            DrawIcon(graphics, metric.Icon, new Rectangle(x, S(7, scale), S(14, scale), S(14, scale)), color, scale);
            graphics.DrawString(metric.Title, font, titleBrush, x + S(19, scale), S(4, scale));
            if (metric.Settings.Presentation != MetricPresentation.BarOnly)
                graphics.DrawString(PercentText(metric.Percent), boldFont, valueBrush, x + S(18, scale), S(24, scale));
            if (metric.Settings.Presentation != MetricPresentation.PercentOnly)
                DrawTrack(graphics, new Rectangle(x + S(4, scale), S(42, scale), S(62, scale), S(4, scale)), metric.Percent,
                    CompactBarTheme.Track(metric.Settings.TrackColor, palette, theme), color, false, scale);
            if (index > 0)
            {
                using var divider = new Pen(lightTheme ? Color.FromArgb(55, 30, 34, 42) : Color.FromArgb(65, 255, 255, 255), S(1, scale));
                graphics.DrawLine(divider, x - S(6, scale), S(8, scale), x - S(6, scale), S(40, scale));
            }
            index++;
        }
    }

    private static void DrawTrack(
        Graphics graphics,
        Rectangle track,
        double? percent,
        Color trackColor,
        Color fillColor,
        bool glow,
        float scale)
    {
        using (var trackBrush = new SolidBrush(trackColor))
        using (GraphicsPath trackPath = Rounded(track, track.Height / 2))
            graphics.FillPath(trackBrush, trackPath);

        int fillWidth = percent is null
            ? 0
            : (int)Math.Round(track.Width * Math.Clamp(percent.Value, 0d, 100d) / 100d);
        if (fillWidth <= 0)
            return;

        var fill = new Rectangle(track.X, track.Y, Math.Min(track.Width, Math.Max(fillWidth, S(5, scale))), track.Height);
        if (glow)
        {
            var glowBounds = Rectangle.Inflate(fill, S(3, scale), S(3, scale));
            using var glowBrush = new SolidBrush(Color.FromArgb(42, fillColor.R, fillColor.G, fillColor.B));
            using GraphicsPath glowPath = Rounded(glowBounds, glowBounds.Height / 2);
            graphics.FillPath(glowBrush, glowPath);
        }
        using var fillBrush = new SolidBrush(fillColor);
        using GraphicsPath fillPath = Rounded(fill, fill.Height / 2);
        graphics.FillPath(fillBrush, fillPath);
    }

    private static void DrawIcon(
        Graphics graphics,
        MetricIcon icon,
        Rectangle bounds,
        Color color,
        float scale)
    {
        using var pen = new Pen(color, Math.Max(1f, 1.3f * scale));
        int cx = bounds.X + bounds.Width / 2;
        int cy = bounds.Y + bounds.Height / 2;
        switch (icon)
        {
            case MetricIcon.Hourglass:
                graphics.DrawLine(pen, bounds.Left + 2, bounds.Top + 1, bounds.Right - 2, bounds.Top + 1);
                graphics.DrawLine(pen, bounds.Left + 2, bounds.Bottom - 1, bounds.Right - 2, bounds.Bottom - 1);
                graphics.DrawLine(pen, bounds.Left + 3, bounds.Top + 2, bounds.Right - 3, bounds.Bottom - 2);
                graphics.DrawLine(pen, bounds.Right - 3, bounds.Top + 2, bounds.Left + 3, bounds.Bottom - 2);
                break;
            case MetricIcon.Calendar:
                graphics.DrawRectangle(pen, bounds.Left + 1, bounds.Top + 3, bounds.Width - 3, bounds.Height - 5);
                graphics.DrawLine(pen, bounds.Left + 2, bounds.Top + 6, bounds.Right - 2, bounds.Top + 6);
                graphics.DrawLine(pen, bounds.Left + 4, bounds.Top + 1, bounds.Left + 4, bounds.Top + 4);
                graphics.DrawLine(pen, bounds.Right - 4, bounds.Top + 1, bounds.Right - 4, bounds.Top + 4);
                break;
            case MetricIcon.Cpu:
                graphics.DrawRectangle(pen, bounds.Left + 3, bounds.Top + 3, bounds.Width - 7, bounds.Height - 7);
                for (int offset = 3; offset <= bounds.Width - 4; offset += Math.Max(3, bounds.Width / 3))
                {
                    graphics.DrawLine(pen, bounds.Left + offset, bounds.Top, bounds.Left + offset, bounds.Top + 3);
                    graphics.DrawLine(pen, bounds.Left + offset, bounds.Bottom - 3, bounds.Left + offset, bounds.Bottom);
                }
                break;
            case MetricIcon.Memory:
                graphics.DrawRectangle(pen, bounds.Left + 1, bounds.Top + 3, bounds.Width - 3, bounds.Height - 6);
                graphics.DrawLine(pen, bounds.Left + 4, cy - 2, bounds.Left + 4, cy + 2);
                graphics.DrawLine(pen, cx, cy - 2, cx, cy + 2);
                graphics.DrawLine(pen, bounds.Right - 5, cy - 2, bounds.Right - 5, cy + 2);
                break;
        }
    }

    private static void DrawCenteredText(
        Graphics graphics,
        string text,
        Font font,
        SolidBrush brush,
        Rectangle bounds,
        bool lightTheme)
    {
        DrawThemeText(graphics, text, font, bounds, brush.Color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix, lightTheme);
    }

    private static void DrawThemeText(Graphics graphics, string text, Font font, Rectangle bounds, Color color,
        TextFormatFlags flags, bool useGdiPlus)
    {
        if (!useGdiPlus)
        {
            TextRenderer.DrawText(graphics, text, font, bounds, color, flags);
            return;
        }

        using var brush = new SolidBrush(color);
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.Alignment = flags.HasFlag(TextFormatFlags.HorizontalCenter) ? StringAlignment.Center
            : flags.HasFlag(TextFormatFlags.Right) ? StringAlignment.Far : StringAlignment.Near;
        format.LineAlignment = StringAlignment.Center;
        format.FormatFlags |= StringFormatFlags.NoWrap;
        graphics.DrawString(text, font, brush, bounds, format);
    }

    private static string PercentText(double? percent) =>
        percent is null ? "--%" : $"{Math.Round(percent.Value):0}%";

    private static Color MetricColor(MetricVisual metric, Color fallback)
    {
        Color color = HexColor.ParseOrDefault(metric.Settings.FillColor, fallback);
        return Color.FromArgb(Math.Max(1, (int)color.A), color.R, color.G, color.B);
    }

    private static Color Mix(Color first, Color second, float secondWeight)
    {
        float weight = Math.Clamp(secondWeight, 0f, 1f);
        return Color.FromArgb(
            (int)Math.Round(first.R * (1f - weight) + second.R * weight),
            (int)Math.Round(first.G * (1f - weight) + second.G * weight),
            (int)Math.Round(first.B * (1f - weight) + second.B * weight));
    }

    private static GraphicsPath Rounded(Rectangle rectangle, int requestedRadius)
    {
        int radius = Math.Max(1, Math.Min(requestedRadius, Math.Min(rectangle.Width, rectangle.Height) / 2));
        int diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static int S(int value, float scale) => Math.Max(1, (int)Math.Round(value * scale));

    private readonly record struct MetricVisual(
        int Slot,
        string Title,
        double? Percent,
        MetricSettings Settings,
        MetricIcon Icon);

    private enum MetricIcon
    {
        Hourglass,
        Calendar,
        Cpu,
        Memory
    }

    private enum LinearStyle
    {
        Dark,
        LabelBoxes,
        Neon,
        Light,
        Cards,
        Capsules,
        Gradient
    }
}

internal static class CompactBarTheme
{
    public static bool IsLight(ThemeVariant variant) =>
        variant is ThemeVariant.Light or ThemeVariant.CodexLight;

    public static Color Foreground(CodexPalette palette, ThemeVariant variant) =>
        ParseColor(CompactBarPaletteCatalog.Get(palette, variant).Foreground, Color.White);

    public static string DefaultFill(CodexPalette palette, ThemeVariant variant)
    {
        Color accent = ParseColor(
            CompactBarPaletteCatalog.EffectiveAccent(palette, variant),
            Color.CornflowerBlue);
        return HexColor.Format(accent, includeAlpha: true);
    }

    public static string DefaultTrack(CodexPalette palette, ThemeVariant variant)
    {
        CodexPaletteColors colors = CompactBarPaletteCatalog.Get(palette, variant);
        Color surface = ParseColor(colors.Surface, Color.FromArgb(58, 61, 69));
        Color foreground = ParseColor(colors.Foreground, Color.White);
        return HexColor.Format(
            Mix(surface, foreground, IsLight(variant) ? 0.16f : 0.2f),
            includeAlpha: true);
    }

    public static Color Track(string configured, CodexPalette palette, ThemeVariant variant)
    {
        if (!string.Equals(configured, "#3A3D45", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(configured, "#FF3A3D45", StringComparison.OrdinalIgnoreCase))
        {
            Color custom = HexColor.ParseOrDefault(configured, Color.FromArgb(58, 61, 69));
            return IsLight(variant) ? Mix(custom, Color.White, 0.72f) : custom;
        }

        return HexColor.ParseOrDefault(
            DefaultTrack(palette, variant),
            Color.FromArgb(58, 61, 69));
    }

    public static string DefaultBackground(CompactBarStyle style, CodexPalette palette, ThemeVariant variant)
    {
        Color background = ParseColor(
            CompactBarPaletteCatalog.Get(palette, variant).Background,
            IsLight(variant) ? Color.White : Color.FromArgb(22, 24, 28));
        return HexColor.Format(background, includeAlpha: true);
    }

    private static Color ParseColor(string value, Color fallback) =>
        HexColor.ParseOrDefault(value, fallback);

    private static Color Mix(Color first, Color second, float secondWeight)
    {
        float weight = Math.Clamp(secondWeight, 0f, 1f);
        return Color.FromArgb(
            (int)Math.Round(first.R * (1f - weight) + second.R * weight),
            (int)Math.Round(first.G * (1f - weight) + second.G * weight),
            (int)Math.Round(first.B * (1f - weight) + second.B * weight));
    }
}
