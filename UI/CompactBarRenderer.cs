using System.Drawing.Drawing2D;
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
        DrawBackground(graphics, size, settings, metrics, scale);

        switch (settings.CompactBarStyle)
        {
            case CompactBarStyle.NeonGlow:
                DrawGrid(graphics, metrics, scale, LinearStyle.Neon);
                break;
            case CompactBarStyle.Light:
                DrawGrid(graphics, metrics, scale, LinearStyle.Light);
                break;
            case CompactBarStyle.Cards:
                DrawGrid(graphics, metrics, scale, LinearStyle.Cards);
                break;
            case CompactBarStyle.CircularGauges:
                DrawCircularGauges(graphics, metrics, scale);
                break;
            case CompactBarStyle.CompactRows:
                DrawCompactRows(graphics, metrics, scale);
                break;
            case CompactBarStyle.RoundedCapsules:
                DrawGrid(graphics, metrics, scale, LinearStyle.Capsules);
                break;
            case CompactBarStyle.Gradient:
                DrawGrid(graphics, metrics, scale, LinearStyle.Gradient);
                break;
            case CompactBarStyle.MinimalIcons:
                DrawMinimalIcons(graphics, metrics, scale);
                break;
            case CompactBarStyle.LabelBoxes:
                DrawGrid(graphics, metrics, scale, LinearStyle.LabelBoxes);
                break;
            default:
                DrawGrid(graphics, metrics, scale, LinearStyle.Dark);
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
        int alpha = Math.Max(1, (int)configured.A);
        Rectangle area = new(Point.Empty, size);

        if (settings.CompactBarStyle == CompactBarStyle.Light)
        {
            using var light = new SolidBrush(Color.FromArgb(alpha, 246, 247, 249));
            graphics.FillRectangle(light, area);
            return;
        }

        if (settings.CompactBarStyle == CompactBarStyle.Gradient)
        {
            Color left = MetricColor(metrics[0], Color.FromArgb(52, 130, 150));
            Color right = MetricColor(metrics[3], Color.FromArgb(90, 68, 170));
            using var gradient = new LinearGradientBrush(
                area,
                Color.FromArgb(alpha, Mix(left, Color.FromArgb(19, 42, 48), 0.58f)),
                Color.FromArgb(alpha, Mix(right, Color.FromArgb(34, 27, 68), 0.55f)),
                LinearGradientMode.Horizontal);
            graphics.FillRectangle(gradient, area);
            return;
        }

        using (var background = new SolidBrush(Color.FromArgb(alpha, configured.R, configured.G, configured.B)))
            graphics.FillRectangle(background, area);

        if (settings.CompactBarStyle == CompactBarStyle.NeonGlow && size.Width > 4 && size.Height > 4)
        {
            Color accent = MetricColor(metrics.First(metric => metric.Settings.Enabled), Color.Cyan);
            using var border = new Pen(Color.FromArgb(170, accent), S(1, scale));
            graphics.DrawRectangle(border, S(1, scale), S(1, scale), size.Width - S(3, scale), size.Height - S(3, scale));
        }
    }

    private static void DrawGrid(Graphics graphics, MetricVisual[] metrics, float scale, LinearStyle style)
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
            DrawLinearMetric(graphics, bounds, metric, style, scale);
        }
    }

    private static void DrawLinearMetric(
        Graphics graphics,
        Rectangle bounds,
        MetricVisual metric,
        LinearStyle style,
        float scale)
    {
        Color fillColor = MetricColor(metric, Color.FromArgb(98, 214, 167));
        Color trackColor = HexColor.ParseOrDefault(metric.Settings.TrackColor, Color.FromArgb(58, 61, 69));
        bool light = style == LinearStyle.Light;
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

        using var font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
        using var boldFont = new Font(font, FontStyle.Bold);
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
            graphics.DrawString(metric.Title, style == LinearStyle.Neon ? boldFont : font, titleBrush, contentX, bounds.Y + S(2, scale));
            contentX += S(style == LinearStyle.Cards ? 31 : 34, scale);
        }

        if (metric.Settings.Presentation == MetricPresentation.PercentOnly)
        {
            graphics.DrawString(percentText, boldFont, textBrush, contentX + S(3, scale), bounds.Y + S(2, scale));
            return;
        }

        int valueWidth = 0;
        if (metric.Settings.Presentation == MetricPresentation.PercentAndBar)
        {
            SizeF valueSize = graphics.MeasureString(percentText, boldFont);
            valueWidth = (int)Math.Ceiling(valueSize.Width) + S(4, scale);
            graphics.DrawString(percentText, boldFont, textBrush, bounds.Right - valueSize.Width - S(3, scale), bounds.Y + S(2, scale));
        }

        var track = new Rectangle(
            contentX,
            bounds.Y + (bounds.Height - S(6, scale)) / 2,
            Math.Max(S(8, scale), bounds.Right - contentX - valueWidth - S(4, scale)),
            S(6, scale));
        DrawTrack(graphics, track, metric.Percent, trackColor, fillColor, style == LinearStyle.Neon, scale);
    }

    private static void DrawCircularGauges(Graphics graphics, MetricVisual[] metrics, float scale)
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
            using var font = new Font("Segoe UI", 8f, FontStyle.Regular, GraphicsUnit.Point);
            using var boldFont = new Font("Segoe UI", diameter < S(34, scale) ? 7.5f : 9f, FontStyle.Bold, GraphicsUnit.Point);
            using var titleBrush = new SolidBrush(Color.FromArgb(255, color.R, color.G, color.B));
            using var valueBrush = new SolidBrush(Color.White);
            DrawCenteredText(graphics, metric.Title, font, titleBrush, new Rectangle(x, 0, cellWidth, titleHeight));
            float penWidth = Math.Max(2f, Math.Min(S(6, scale), diameter / 7f));
            using var trackPen = new Pen(Color.FromArgb(70, 255, 255, 255), penWidth);
            using var fillPen = new Pen(color, penWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            graphics.DrawArc(trackPen, gauge, -90, 360);
            if (metric.Percent is not null)
                graphics.DrawArc(fillPen, gauge, -90, (float)(360d * Math.Clamp(metric.Percent.Value, 0d, 100d) / 100d));
            DrawCenteredText(graphics, PercentText(metric.Percent), boldFont, valueBrush, gauge);
            index++;
        }
    }

    private static void DrawCompactRows(Graphics graphics, MetricVisual[] metrics, float scale)
    {
        int row = 0;
        foreach (MetricVisual metric in metrics.Where(metric => metric.Settings.Enabled))
        {
            int y = S(4 + row * 21, scale);
            Color color = MetricColor(metric, Color.CornflowerBlue);
            Color track = HexColor.ParseOrDefault(metric.Settings.TrackColor, Color.FromArgb(58, 61, 69));
            using var font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
            using var boldFont = new Font(font, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.White);
            DrawIcon(graphics, metric.Icon, new Rectangle(S(6, scale), y + S(3, scale), S(14, scale), S(14, scale)), Color.White, scale);
            graphics.DrawString(metric.Title, font, textBrush, S(27, scale), y + S(1, scale));

            int trackX = S(74, scale);
            int valueWidth = metric.Settings.Presentation == MetricPresentation.BarOnly ? 0 : S(42, scale);
            if (metric.Settings.Presentation != MetricPresentation.PercentOnly)
            {
                var trackBounds = new Rectangle(trackX, y + S(7, scale), S(245, scale) - trackX - valueWidth, S(6, scale));
                DrawTrack(graphics, trackBounds, metric.Percent, track, color, false, scale);
            }
            if (metric.Settings.Presentation != MetricPresentation.BarOnly)
                graphics.DrawString(PercentText(metric.Percent), boldFont, textBrush, S(207, scale), y + S(1, scale));
            row++;
        }
    }

    private static void DrawMinimalIcons(Graphics graphics, MetricVisual[] metrics, float scale)
    {
        int index = 0;
        foreach (MetricVisual metric in metrics.Where(metric => metric.Settings.Enabled))
        {
            int x = S(5 + index * 74, scale);
            Color color = MetricColor(metric, Color.CornflowerBlue);
            using var font = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            using var boldFont = new Font(font, FontStyle.Bold);
            using var titleBrush = new SolidBrush(Color.FromArgb(42, 45, 52));
            using var valueBrush = new SolidBrush(color);
            DrawIcon(graphics, metric.Icon, new Rectangle(x, S(7, scale), S(14, scale), S(14, scale)), color, scale);
            graphics.DrawString(metric.Title, font, titleBrush, x + S(19, scale), S(5, scale));
            graphics.DrawString(PercentText(metric.Percent), boldFont, valueBrush, x + S(18, scale), S(25, scale));
            if (index > 0)
            {
                using var divider = new Pen(Color.FromArgb(55, 30, 34, 42), S(1, scale));
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

    private static void DrawCenteredText(Graphics graphics, string text, Font font, Brush brush, Rectangle bounds)
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
    public static string DefaultBackground(CompactBarStyle style, string currentBackground)
    {
        Color current = HexColor.ParseOrDefault(currentBackground, Color.FromArgb(255, 22, 24, 28));
        Color theme = style switch
        {
            CompactBarStyle.NeonGlow => Color.FromArgb(18, 37, 34),
            CompactBarStyle.Light => Color.FromArgb(246, 247, 249),
            CompactBarStyle.Cards => Color.FromArgb(18, 44, 43),
            CompactBarStyle.CircularGauges => Color.FromArgb(26, 29, 34),
            CompactBarStyle.CompactRows => Color.FromArgb(23, 25, 30),
            CompactBarStyle.RoundedCapsules => Color.FromArgb(27, 29, 41),
            CompactBarStyle.Gradient => Color.FromArgb(39, 75, 90),
            CompactBarStyle.MinimalIcons => Color.FromArgb(245, 245, 246),
            _ => Color.FromArgb(22, 24, 28)
        };
        return HexColor.Format(Color.FromArgb(current.A, theme.R, theme.G, theme.B), includeAlpha: true);
    }
}
