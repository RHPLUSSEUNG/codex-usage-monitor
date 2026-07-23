using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using CodexUsageMonitor.Models;

namespace CodexUsageMonitor.UI;

public sealed class CompactBarForm : Form
{
    private readonly ToolStripMenuItem _refreshMenu = new();
    private readonly ToolStripMenuItem _settingsMenu = new();
    private AppSettings _settings = new();
    private UsageSnapshot _snapshot = UsageSnapshot.Waiting;
    private SystemUsageSnapshot _systemUsage = SystemUsageSnapshot.Empty;
    private bool _dragging;
    private bool _renderingSuspended;
    private Point _dragCursorStart;
    private Point _dragWindowStart;

    public event EventHandler? SettingsRequested;
    public event EventHandler? RefreshRequested;
    public event EventHandler? PositionChanged;

    public CompactBarForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;

        var menu = new ContextMenuStrip();
        _refreshMenu.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        _settingsMenu.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(_refreshMenu);
        menu.Items.Add(_settingsMenu);
        ContextMenuStrip = menu;
        ApplyLanguage();

        MouseDown += BeginDrag;
        MouseMove += ContinueDrag;
        MouseUp += EndDrag;
        MouseDoubleClick += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override bool ShowWithoutActivation => true;

    public void ApplyLanguage()
    {
        _refreshMenu.Text = Localization.Text("Refresh");
        _settingsMenu.Text = Localization.Text("Settings");
    }

    public void SuspendRendering() => _renderingSuspended = true;

    public void ResumeRendering()
    {
        _renderingSuspended = false;
        PositionWindow();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int wsExLayered = 0x00080000;
            const int wsExToolWindow = 0x00000080;
            const int wsExNoActivate = 0x08000000;
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= wsExLayered | wsExToolWindow | wsExNoActivate;
            return parameters;
        }
    }

    public void Apply(AppSettings settings, UsageSnapshot snapshot, SystemUsageSnapshot systemUsage)
    {
        _settings = settings;
        _snapshot = snapshot;
        _systemUsage = systemUsage;

        bool anyMetricEnabled = settings.FiveHour.Enabled
                                || settings.Weekly.Enabled
                                || settings.Cpu.Enabled
                                || settings.Memory.Enabled;
        bool shouldShow = settings.ShowCompactBar && anyMetricEnabled;
        if (!shouldShow)
        {
            Hide();
            return;
        }

        if (!Visible)
            Show();
        if (!_dragging && !_renderingSuspended)
            RenderAtStoredPosition();
    }

    public void Preview(AppSettings settings)
    {
        _settings = settings;
        bool anyMetricEnabled = settings.FiveHour.Enabled
                                || settings.Weekly.Enabled
                                || settings.Cpu.Enabled
                                || settings.Memory.Enabled;
        if (!settings.ShowCompactBar || !anyMetricEnabled)
        {
            Hide();
            return;
        }

        if (!Visible)
            Show();
        if (!_dragging)
            RenderAtStoredPosition();
    }

    public void PositionWindow()
    {
        if (Visible && !_dragging && !_renderingSuspended)
            RenderAtStoredPosition();
    }

    private void RenderAtStoredPosition()
    {
        Size size = CalculateWindowSize();
        Rectangle virtualScreen = SystemInformation.VirtualScreen;
        bool hasStoredPosition = _settings.WindowPositionX != -1 || _settings.WindowPositionY != -1;
        int defaultX = virtualScreen.Left + Math.Max(0, (virtualScreen.Width - size.Width) / 2);
        int defaultY = virtualScreen.Top + Math.Max(0, (virtualScreen.Height - size.Height) / 2);
        int x = hasStoredPosition ? _settings.WindowPositionX : defaultX;
        int y = hasStoredPosition ? _settings.WindowPositionY : defaultY;
        RenderLayeredWindow(new Point(x, y), size);
    }

    private Size CalculateWindowSize()
    {
        float scale = DeviceDpi / 96f;
        return CompactBarRenderer.CalculateSize(_settings, scale);
    }

    private void RenderLayeredWindow(Point location, Size size)
    {
        using var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        bitmap.SetResolution(DeviceDpi, DeviceDpi);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.CompositingMode = CompositingMode.SourceOver;
            DrawContent(graphics, size);
        }

        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memoryDc = CreateCompatibleDC(screenDc);
        IntPtr bitmapHandle = bitmap.GetHbitmap(Color.FromArgb(0));
        IntPtr oldBitmap = SelectObject(memoryDc, bitmapHandle);
        try
        {
            var destination = new NativePoint(location.X, location.Y);
            var source = new NativePoint(0, 0);
            var nativeSize = new NativeSize(size.Width, size.Height);
            var blend = new BlendFunction
            {
                BlendOp = AcSrcOver,
                SourceConstantAlpha = 255,
                AlphaFormat = AcSrcAlpha
            };
            UpdateLayeredWindow(
                Handle,
                screenDc,
                ref destination,
                ref nativeSize,
                memoryDc,
                ref source,
                0,
                ref blend,
                UlwAlpha);
        }
        finally
        {
            SelectObject(memoryDc, oldBitmap);
            DeleteObject(bitmapHandle);
            DeleteDC(memoryDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }

        SetWindowPos(Handle, HwndTopMost, location.X, location.Y, size.Width, size.Height, SwpNoActivate | SwpShowWindow);
    }

    private void DrawContent(Graphics graphics, Size size)
    {
        float scale = DeviceDpi / 96f;
        CompactBarRenderer.Draw(graphics, size, _settings, _snapshot, _systemUsage, scale);
    }

    private void DrawQuotaMetric(
        Graphics graphics,
        Rectangle bounds,
        string title,
        QuotaWindow? window,
        MetricSettings settings,
        float scale)
    {
        double? percent = window is null
            ? null
            : _settings.PercentageMode == PercentageMode.Remaining
                ? window.RemainingPercent
                : window.UsedPercent;
        DrawMetric(graphics, bounds, title, percent, settings, scale);
    }

    private void DrawMetric(
        Graphics graphics,
        Rectangle bounds,
        string title,
        double? percent,
        MetricSettings settings,
        float scale)
    {
        string percentText = percent is null ? "--%" : $"{Math.Round(percent.Value):0}%";
        using var font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
        using var boldFont = new Font(font, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.White);
        float textX = bounds.X + 5f * scale;
        float textY = bounds.Y + 2f * scale;

        if (_settings.CompactBarStyle == CompactBarStyle.LabelBoxes)
        {
            DrawLabelBoxMetric(
                graphics,
                bounds,
                title,
                percent,
                percentText,
                settings,
                boldFont,
                textBrush,
                scale);
            return;
        }

        if (settings.Presentation == MetricPresentation.PercentOnly)
        {
            graphics.DrawString($"{title}  {percentText}", font, textBrush, textX, textY);
            return;
        }

        graphics.DrawString(title, font, textBrush, textX, textY);
        SizeF titleSize = graphics.MeasureString(title, font);
        int valueWidth = 0;
        if (settings.Presentation == MetricPresentation.PercentAndBar)
        {
            SizeF labelSize = graphics.MeasureString(percentText, boldFont);
            valueWidth = (int)Math.Ceiling(labelSize.Width) + Scale(5, scale);
            graphics.DrawString(percentText, boldFont, textBrush, bounds.Right - labelSize.Width - 4f * scale, textY);
        }

        int trackX = bounds.X + (int)Math.Ceiling(titleSize.Width) + Scale(9, scale);
        var track = new Rectangle(
            trackX,
            bounds.Y + Scale(7, scale),
            Math.Max(Scale(8, scale), bounds.Right - trackX - valueWidth - Scale(5, scale)),
            Scale(6, scale));
        Color trackColor = HexColor.ParseOrDefault(settings.TrackColor, Color.FromArgb(58, 61, 69));
        Color fillColor = HexColor.ParseOrDefault(settings.FillColor, Color.FromArgb(98, 214, 167));
        using (var trackBrush = new SolidBrush(trackColor))
        using (GraphicsPath path = RoundedRectangle(track, Scale(3, scale)))
            graphics.FillPath(trackBrush, path);

        int fillWidth = percent is null
            ? 0
            : (int)Math.Round(track.Width * Math.Clamp(percent.Value, 0d, 100d) / 100d);
        if (fillWidth <= 0)
            return;

        var fill = new Rectangle(track.X, track.Y, Math.Min(track.Width, Math.Max(fillWidth, Scale(5, scale))), track.Height);
        using var fillBrush = new SolidBrush(fillColor);
        using GraphicsPath fillPath = RoundedRectangle(fill, Scale(3, scale));
        graphics.FillPath(fillBrush, fillPath);
    }

    private static void DrawLabelBoxMetric(
        Graphics graphics,
        Rectangle bounds,
        string title,
        double? percent,
        string percentText,
        MetricSettings settings,
        Font boldFont,
        Brush valueBrush,
        float scale)
    {
        Color fillColor = HexColor.ParseOrDefault(settings.FillColor, Color.FromArgb(98, 214, 167));
        Color labelColor = Color.FromArgb(255, fillColor.R, fillColor.G, fillColor.B);
        Color labelBackground = Color.FromArgb(
            Math.Clamp(fillColor.A / 4, 24, 64),
            fillColor.R,
            fillColor.G,
            fillColor.B);
        var labelBounds = new Rectangle(
            bounds.X + Scale(1, scale),
            bounds.Y + Scale(1, scale),
            Scale(38, scale),
            Math.Max(Scale(16, scale), bounds.Height - Scale(2, scale)));

        using (var labelBackgroundBrush = new SolidBrush(labelBackground))
        using (GraphicsPath labelPath = RoundedRectangle(labelBounds, Scale(4, scale)))
            graphics.FillPath(labelBackgroundBrush, labelPath);

        using var labelBrush = new SolidBrush(labelColor);
        using var labelFormat = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap
        };
        graphics.DrawString(title, boldFont, labelBrush, labelBounds, labelFormat);

        int contentX = labelBounds.Right + Scale(5, scale);
        float textY = bounds.Y + 2f * scale;
        if (settings.Presentation == MetricPresentation.PercentOnly)
        {
            graphics.DrawString(percentText, boldFont, valueBrush, contentX, textY);
            return;
        }

        int valueWidth = 0;
        if (settings.Presentation == MetricPresentation.PercentAndBar)
        {
            SizeF valueSize = graphics.MeasureString(percentText, boldFont);
            valueWidth = (int)Math.Ceiling(valueSize.Width) + Scale(5, scale);
            graphics.DrawString(percentText, boldFont, valueBrush, bounds.Right - valueSize.Width - 3f * scale, textY);
        }

        var track = new Rectangle(
            contentX,
            bounds.Y + Scale(7, scale),
            Math.Max(Scale(8, scale), bounds.Right - contentX - valueWidth - Scale(4, scale)),
            Scale(6, scale));
        Color trackColor = HexColor.ParseOrDefault(settings.TrackColor, Color.FromArgb(58, 61, 69));
        using (var trackBrush = new SolidBrush(trackColor))
        using (GraphicsPath trackPath = RoundedRectangle(track, Scale(3, scale)))
            graphics.FillPath(trackBrush, trackPath);

        int fillWidth = percent is null
            ? 0
            : (int)Math.Round(track.Width * Math.Clamp(percent.Value, 0d, 100d) / 100d);
        if (fillWidth <= 0)
            return;

        var fill = new Rectangle(track.X, track.Y, Math.Min(track.Width, Math.Max(fillWidth, Scale(5, scale))), track.Height);
        using var fillBrush = new SolidBrush(fillColor);
        using GraphicsPath fillPath = RoundedRectangle(fill, Scale(3, scale));
        graphics.FillPath(fillBrush, fillPath);
    }

    private int MetricWidth(MetricSettings settings, float scale)
    {
        int width = settings.Presentation switch
        {
            MetricPresentation.PercentOnly => 94,
            MetricPresentation.BarOnly => 124,
            _ => 152
        };
        if (_settings.CompactBarStyle == CompactBarStyle.LabelBoxes)
            width += 6;
        return Scale(width, scale);
    }

    private static int Scale(int value, float scale) => Math.Max(1, (int)Math.Round(value * scale));

    private static GraphicsPath RoundedRectangle(Rectangle rectangle, int requestedRadius)
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

    private void BeginDrag(object? sender, MouseEventArgs args)
    {
        if (args.Button != MouseButtons.Left)
            return;
        _dragging = true;
        _dragCursorStart = Cursor.Position;
        _dragWindowStart = GetWindowRect(Handle, out NativeRect rectangle)
            ? new Point(rectangle.Left, rectangle.Top)
            : Location;
        Capture = true;
    }

    private void ContinueDrag(object? sender, MouseEventArgs args)
    {
        if (!_dragging)
            return;

        Point cursor = Cursor.Position;
        int x = _dragWindowStart.X + cursor.X - _dragCursorStart.X;
        int y = _dragWindowStart.Y + cursor.Y - _dragCursorStart.Y;
        SetWindowPos(Handle, HwndTopMost, x, y, 0, 0, SwpNoActivate | SwpNoSize | SwpShowWindow);
    }

    private void EndDrag(object? sender, MouseEventArgs args)
    {
        if (!_dragging || args.Button != MouseButtons.Left)
            return;

        Capture = false;
        _dragging = false;
        if (GetWindowRect(Handle, out NativeRect rectangle))
        {
            _settings.WindowPositionX = rectangle.Left;
            _settings.WindowPositionY = rectangle.Top;
        }
        PositionChanged?.Invoke(this, EventArgs.Empty);
        RenderAtStoredPosition();
    }

    protected override void WndProc(ref Message message)
    {
        const int wmDisplayChange = 0x007E;
        const int wmSettingChange = 0x001A;
        base.WndProc(ref message);
        if (message.Msg is wmDisplayChange or wmSettingChange && IsHandleCreated)
            BeginInvoke(PositionWindow);
    }

    private const byte AcSrcOver = 0;
    private const byte AcSrcAlpha = 1;
    private const uint UlwAlpha = 0x00000002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private static readonly IntPtr HwndTopMost = new(-1);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr value);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateLayeredWindow(
        IntPtr window,
        IntPtr destinationDc,
        ref NativePoint destination,
        ref NativeSize size,
        IntPtr sourceDc,
        ref NativePoint source,
        int colorKey,
        ref BlendFunction blend,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;

        public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Width;
        public int Height;

        public NativeSize(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
