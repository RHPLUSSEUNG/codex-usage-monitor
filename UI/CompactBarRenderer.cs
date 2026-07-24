using System.Drawing.Drawing2D;
using System.Drawing.Text;
using CodexUsageMonitor.Models;

namespace CodexUsageMonitor.UI;

internal static class CompactBarRenderer
{
    private const int GridCellWidth = 158;
    private const int GridRowHeight = 22;

    public static Size CalculateSize(AppSettings settings, float scale, int taskbarHeight)
    {
        MetricVisual[] metrics = Metrics(settings, null, null);
        int count = metrics.Count(metric => metric.Settings.Enabled);
        if (count == 0)
            return new Size(1, 1);

        return settings.CompactBarStyle switch
        {
            CompactBarStyle.CircularGauges => CircularGaugeSize(count, scale, taskbarHeight),
            CompactBarStyle.CompactRows => new Size(S(250, scale), S(6 + count * 21, scale)),
            CompactBarStyle.MinimalIcons => new Size(S(8 + count * 74, scale), S(48, scale)),
            CompactBarStyle.Cards => GridSize(metrics, scale, 164, 28),
            CompactBarStyle.RoundedCapsules => GridSize(metrics, scale, 164, 27),
            CompactBarStyle.LabelBoxes => GridSize(metrics, scale, GridCellWidth + 6, GridRowHeight),
            _ => GridSize(metrics, scale, GridCellWidth, GridRowHeight)
        };
    }

    public static void Draw(
        Graphics graphics,
        Size size,
        AppSettings settings,
        UsageSnapshot snapshot,
        SystemUsageSnapshot systemUsage,
        float scale)
    {
        MetricVisual[] metrics = Metrics(settings, snapshot, systemUsage);
        bool lightTheme = settings.ThemeVariant == ThemeVariant.Light;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        graphics.TextContrast = 0;
        DrawBackground(graphics, size, settings, metrics, scale);

        switch (settings.CompactBarStyle)
        {
            case CompactBarStyle.NeonGlow:
                DrawGrid(graphics, metrics, scale, LinearStyle.Neon, lightTheme);
                break;
            case CompactBarStyle.Light:
                DrawGrid(graphics, metrics, scale, LinearStyle.Light, lightTheme);
                break;
            case CompactBarStyle.Cards:
                DrawGrid(graphics, metrics, scale, LinearStyle.Cards, lightTheme);
                break;
            case CompactBarStyle.CircularGauges:
                DrawCircularGauges(graphics, metrics, scale, lightTheme);
                break;
            case CompactBarStyle.CompactRows:
                DrawCompactRows(graphics, metrics, scale, lightTheme);
                break;
            case CompactBarStyle.RoundedCapsules:
                DrawGrid(graphics, metrics, scale, LinearStyle.Capsules, lightTheme);
                break;
            case CompactBarStyle.Gradient:
                DrawGrid(graphics, metrics, scale, LinearStyle.Gradient, lightTheme);
                break;
            case CompactBarStyle.MinimalIcons:
                DrawMinimalIcons(graphics, metrics, scale, lightTheme);
                break;
            case CompactBarStyle.LabelBoxes:
                DrawGrid(graphics, metrics, scale, LinearStyle.LabelBoxes, lightTheme);
                break;
            default:
                DrawGrid(graphics, metrics, scale, LinearStyle.Dark, lightTheme);
                break;
        }
    }

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

    private static Size GridSize(MetricVisual[] metrics, float scale, int cellWidth, int rowHeight)
    {
        bool firstColumn = metrics.Any(metric => metric.Slot < 2 && metric.Settings.Enabled);
        bool secondColumn = metrics.Any(metric => metric.Slot >= 2 && metric.Settings.Enabled);
        bool firstRow = metrics.Any(metric => metric.Slot % 2 == 0 && metric.Settings.Enabled);
        bool secondRow = metrics.Any(metric => metric.Slot % 2 == 1 && metric.Settings.Enabled);
        int columns = (firstColumn ? 1 : 0) + (secondColumn ? 1 : 0);
        int rows = (firstRow ? 1 : 0) + (secondRow ? 1 : 0);
        return new Size(
            S(4 + columns * cellWidth + Math.Max(0, columns - 1) * 4, scale),
            S(4 + rows * rowHeight + Math.Max(0, rows - 1) * 2, scale));
    }

    private static Size CircularGaugeSize(int count, float scale, int taskbarHeight)
    {
        int height = Math.Max(1, taskbarHeight);
        int cellWidth = Math.Max(S(50, scale), (int)Math.Round(height * 1.15d));
        return new Size(S(6, scale) + count * cellWidth, height);
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
        int alpha = Math.Max(1, (int)configured.A);
        Rectangle area = new(0, 0, Math.Max(1, size.Width - 1), Math.Max(1, size.Height - 1));
        int radius = S(8, scale);
        using GraphicsPath backgroundPath = Rounded(area, radius);

        if (settings.CompactBarStyle == CompactBarStyle.Gradient)
        {
            Color left = MetricColor(metrics[0], Color.FromArgb(52, 130, 150));
            Color right = MetricColor(metrics[3], Color.FromArgb(90, 68, 170));
            Color baseColor = settings.ThemeVariant == ThemeVariant.Light
                ? Color.FromArgb(244, 247, 250)
                : configured;
            using var gradient = new LinearGradientBrush(
                area,
                Color.FromArgb(alpha, Mix(left, baseColor, settings.ThemeVariant == ThemeVariant.Light ? 0.82f : 0.58f)),
                Color.FromArgb(alpha, Mix(right, baseColor, settings.ThemeVariant == ThemeVariant.Light ? 0.82f : 0.55f)),
                LinearGradientMode.Horizontal);
            graphics.FillPath(gradient, backgroundPath);
            return;
        }

        using (var background = new SolidBrush(Color.FromArgb(alpha, configured.R, configured.G, configured.B)))
            graphics.FillPath(background, backgroundPath);

        if (settings.CompactBarStyle == CompactBarStyle.NeonGlow && size.Width > 4 && size.Height > 4)
        {
            Color accent = MetricColor(metrics.First(metric => metric.Settings.Enabled), Color.Cyan);
            using var border = new Pen(Color.FromArgb(170, accent), S(1, scale));
            graphics.DrawPath(border, backgroundPath);
        }
    }

    private static void DrawGrid(Graphics graphics, MetricVisual[] metrics, float scale, LinearStyle style, bool lightTheme)
    {
        int cellWidth = style switch
        {
            LinearStyle.Cards or LinearStyle.Capsules => 164,
            LinearStyle.LabelBoxes => GridCellWidth + 6,
            _ => GridCellWidth
        };
        int rowHeight = style switch
        {
            LinearStyle.Cards => 28,
            LinearStyle.Capsules => 27,
            _ => GridRowHeight
        };

        bool firstColumnVisible = metrics.Any(metric => metric.Slot < 2 && metric.Settings.Enabled);
        bool firstRowVisible = metrics.Any(metric => metric.Slot % 2 == 0 && metric.Settings.Enabled);
        foreach (MetricVisual metric in metrics.Where(metric => metric.Settings.Enabled))
        {
            int logicalColumn = metric.Slot < 2 ? 0 : 1;
            int logicalRow = metric.Slot % 2;
            int column = logicalColumn == 1 && !firstColumnVisible ? 0 : logicalColumn;
            int row = logicalRow == 1 && !firstRowVisible ? 0 : logicalRow;
            var bounds = new Rectangle(
                S(2 + column * (cellWidth + 4), scale),
                S(2 + row * (rowHeight + 2), scale),
                S(cellWidth, scale),
                S(rowHeight, scale));
            DrawLinearMetric(graphics, bounds, metric, style, lightTheme, scale);
        }
    }

    private static void DrawLinearMetric(
        Graphics graphics,
        Rectangle bounds,
        MetricVisual metric,
        LinearStyle style,
        bool light,
        float scale)
    {
        Color fillColor = MetricColor(metric, Color.FromArgb(98, 214, 167));
        Color trackColor = HexColor.ParseOrDefault(metric.Settings.TrackColor, Color.FromArgb(58, 61, 69));
        Color textColor = light ? Color.FromArgb(42, 45, 52) : Color.White;
        Color titleColor = style is LinearStyle.Neon or LinearStyle.LabelBoxes
            ? Color.FromArgb(255, fillColor.R, fillColor.G, fillColor.B)
            : textColor;
        if (light)
            trackColor = Mix(trackColor, Color.White, 0.72f);

        if (style is LinearStyle.Cards or LinearStyle.Capsules)
        {
            int radius = style == LinearStyle.Capsules ? bounds.Height / 2 : S(5, scale);
            using var cellBrush = new SolidBrush(Color.FromArgb(light ? 38 : 30, fillColor.R, fillColor.G, fillColor.B));
            using GraphicsPath cellPath = Rounded(bounds, radius);
            graphics.FillPath(cellBrush, cellPath);
        }

        using var font = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
        using var boldFont = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
        using var textBrush = new SolidBrush(textColor);
        using var titleBrush = new SolidBrush(titleColor);
        string percentText = PercentText(metric.Percent);
        int contentX;

        if (style == LinearStyle.LabelBoxes)
        {
            var label = new Rectangle(
                bounds.X + S(1, scale),
                bounds.Y + S(1, scale),
                S(38, scale),
                bounds.Height - S(2, scale));
            using (var labelBackground = new SolidBrush(Color.FromArgb(48, fillColor.R, fillColor.G, fillColor.B)))
            using (GraphicsPath labelPath = Rounded(label, S(4, scale)))
                graphics.FillPath(labelBackground, labelPath);
            DrawCenteredText(graphics, metric.Title, boldFont, titleBrush, label);
            contentX = label.Right + S(5, scale);
        }
        else
        {
            contentX = bounds.X + S(5, scale);
            if (style == LinearStyle.Cards)
            {
                DrawIcon(graphics, metric.Icon, new Rectangle(contentX, bounds.Y + S(6, scale), S(13, scale), S(13, scale)), titleColor, scale);
                contentX += S(18, scale);
            }
            graphics.DrawString(
                metric.Title,
                style == LinearStyle.Neon ? boldFont : font,
                titleBrush,
                contentX,
                bounds.Y + S(1, scale));
            contentX += S(style == LinearStyle.Cards ? 31 : 34, scale);
        }

        if (metric.Settings.Presentation == MetricPresentation.PercentOnly)
        {
            graphics.DrawString(
                percentText,
                boldFont,
                textBrush,
                contentX + S(3, scale),
                bounds.Y + S(1, scale));
            return;
        }

        int valueWidth = 0;
        if (metric.Settings.Presentation == MetricPresentation.PercentAndBar)
        {
            SizeF valueSize = graphics.MeasureString(percentText, boldFont);
            valueWidth = (int)Math.Ceiling(valueSize.Width) + S(4, scale);
            graphics.DrawString(
                percentText,
                boldFont,
                textBrush,
                bounds.Right - valueSize.Width - S(3, scale),
                bounds.Y + S(1, scale));
        }

        var track = new Rectangle(
            contentX,
            bounds.Y + (bounds.Height - S(6, scale)) / 2,
            Math.Max(S(8, scale), bounds.Right - contentX - valueWidth - S(4, scale)),
            S(6, scale));
        DrawTrack(graphics, track, metric.Percent, trackColor, fillColor, style == LinearStyle.Neon, scale);
    }

    private static void DrawCircularGauges(Graphics graphics, MetricVisual[] metrics, float scale, bool lightTheme)
    {
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
            using var titleBrush = new SolidBrush(Color.FromArgb(255, color.R, color.G, color.B));
            Color textColor = lightTheme ? Color.FromArgb(42, 45, 52) : Color.White;
            using var valueBrush = new SolidBrush(textColor);
            DrawCenteredText(graphics, metric.Title, font, titleBrush, new Rectangle(x, 0, cellWidth, titleHeight));
            float penWidth = Math.Max(2f, Math.Min(S(6, scale), diameter / 7f));
            using var trackPen = new Pen(lightTheme ? Color.FromArgb(55, 30, 34, 42) : Color.FromArgb(70, 255, 255, 255), penWidth);
            using var fillPen = new Pen(color, penWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            graphics.DrawArc(trackPen, gauge, -90, 360);
            if (metric.Percent is not null)
                graphics.DrawArc(fillPen, gauge, -90, (float)(360d * Math.Clamp(metric.Percent.Value, 0d, 100d) / 100d));
            DrawCenteredText(graphics, PercentText(metric.Percent), boldFont, valueBrush, gauge);
            index++;
        }
    }

    private static void DrawCompactRows(Graphics graphics, MetricVisual[] metrics, float scale, bool lightTheme)
    {
        int row = 0;
        foreach (MetricVisual metric in metrics.Where(metric => metric.Settings.Enabled))
        {
            int y = S(4 + row * 21, scale);
            Color color = MetricColor(metric, Color.CornflowerBlue);
            Color track = HexColor.ParseOrDefault(metric.Settings.TrackColor, Color.FromArgb(58, 61, 69));
            if (lightTheme)
                track = Mix(track, Color.White, 0.72f);
            Color textColor = lightTheme ? Color.FromArgb(42, 45, 52) : Color.White;
            using var font = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
            using var boldFont = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
            using var textBrush = new SolidBrush(textColor);
            DrawIcon(graphics, metric.Icon, new Rectangle(S(6, scale), y + S(3, scale), S(14, scale), S(14, scale)), textColor, scale);
            graphics.DrawString(metric.Title, font, textBrush, S(27, scale), y);

            int trackX = S(74, scale);
            int valueWidth = metric.Settings.Presentation == MetricPresentation.BarOnly ? 0 : S(42, scale);
            if (metric.Settings.Presentation != MetricPresentation.PercentOnly)
            {
                var trackBounds = new Rectangle(trackX, y + S(7, scale), S(245, scale) - trackX - valueWidth, S(6, scale));
                DrawTrack(graphics, trackBounds, metric.Percent, track, color, false, scale);
            }
            if (metric.Settings.Presentation != MetricPresentation.BarOnly)
                graphics.DrawString(PercentText(metric.Percent), boldFont, textBrush, S(207, scale), y);
            row++;
        }
    }

    private static void DrawMinimalIcons(Graphics graphics, MetricVisual[] metrics, float scale, bool lightTheme)
    {
        int index = 0;
        foreach (MetricVisual metric in metrics.Where(metric => metric.Settings.Enabled))
        {
            int x = S(5 + index * 74, scale);
            Color color = MetricColor(metric, Color.CornflowerBlue);
            using var font = new Font("Segoe UI", 9f, FontStyle.Bold, GraphicsUnit.Point);
            using var boldFont = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
            Color textColor = lightTheme ? Color.FromArgb(42, 45, 52) : Color.White;
            using var titleBrush = new SolidBrush(textColor);
            using var valueBrush = new SolidBrush(color);
            DrawIcon(graphics, metric.Icon, new Rectangle(x, S(7, scale), S(14, scale), S(14, scale)), color, scale);
            graphics.DrawString(metric.Title, font, titleBrush, x + S(19, scale), S(4, scale));
            graphics.DrawString(PercentText(metric.Percent), boldFont, valueBrush, x + S(18, scale), S(24, scale));
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
        Rectangle bounds)
    {
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap
        };
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
    public static string DefaultBackground(CompactBarStyle style, ThemeVariant variant)
    {
        bool light = variant == ThemeVariant.Light;
        Color theme = light ? style switch
        {
            CompactBarStyle.NeonGlow => Color.FromArgb(235, 250, 247),
            CompactBarStyle.Cards => Color.FromArgb(235, 246, 245),
            CompactBarStyle.CircularGauges => Color.FromArgb(245, 246, 248),
            CompactBarStyle.CompactRows => Color.FromArgb(247, 248, 250),
            CompactBarStyle.RoundedCapsules => Color.FromArgb(244, 243, 250),
            CompactBarStyle.Gradient => Color.FromArgb(239, 245, 249),
            CompactBarStyle.MinimalIcons => Color.FromArgb(248, 248, 249),
            _ => Color.FromArgb(246, 247, 249)
        } : style switch
        {
            CompactBarStyle.NeonGlow => Color.FromArgb(18, 37, 34),
            CompactBarStyle.Light => Color.FromArgb(31, 33, 38),
            CompactBarStyle.Cards => Color.FromArgb(18, 44, 43),
            CompactBarStyle.CircularGauges => Color.FromArgb(26, 29, 34),
            CompactBarStyle.CompactRows => Color.FromArgb(23, 25, 30),
            CompactBarStyle.RoundedCapsules => Color.FromArgb(27, 29, 41),
            CompactBarStyle.Gradient => Color.FromArgb(39, 75, 90),
            CompactBarStyle.MinimalIcons => Color.FromArgb(24, 26, 31),
            _ => Color.FromArgb(22, 24, 28)
        };
        int alpha = style switch
        {
            CompactBarStyle.NeonGlow => 238,
            CompactBarStyle.Cards => 245,
            CompactBarStyle.RoundedCapsules => 245,
            CompactBarStyle.Gradient => 238,
            _ => 255
        };
        return HexColor.Format(Color.FromArgb(alpha, theme.R, theme.G, theme.B), includeAlpha: true);
    }
}
