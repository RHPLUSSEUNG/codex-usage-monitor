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
        if (!_dragging)
            RenderAtStoredPosition();
    }

    public void PositionWindow()
    {
        if (Visible && !_dragging)
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
        int padding = Scale(2, scale);
        int marginX = Scale(2, scale);
        int marginY = Scale(1, scale);
        int metricHeight = Scale(20, scale);

        int firstColumn = Math.Max(
            _settings.FiveHour.Enabled ? MetricWidth(_settings.FiveHour, scale) : 0,
            _settings.Weekly.Enabled ? MetricWidth(_settings.Weekly, scale) : 0);
        int secondColumn = Math.Max(
            _settings.Cpu.Enabled ? MetricWidth(_settings.Cpu, scale) : 0,
            _settings.Memory.Enabled ? MetricWidth(_settings.Memory, scale) : 0);
        int columns = (firstColumn > 0 ? firstColumn + marginX * 2 : 0)
                      + (secondColumn > 0 ? secondColumn + marginX * 2 : 0);

        bool firstRow = _settings.FiveHour.Enabled || _settings.Cpu.Enabled;
        bool secondRow = _settings.Weekly.Enabled || _settings.Memory.Enabled;
        int rowHeight = metricHeight + marginY * 2;
        int rows = (firstRow ? rowHeight : 0) + (secondRow ? rowHeight : 0);
        return new Size(Math.Max(1, columns + padding * 2), Math.Max(1, rows + padding * 2));
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
        Color configuredBackground = HexColor.ParseOrDefault(
            _settings.BackgroundColor,
            Color.FromArgb(255, 22, 24, 28));
        // Alpha 1 is visually transparent but keeps the whole rectangle clickable.
        Color background = Color.FromArgb(
            Math.Max(1, (int)configuredBackground.A),
            configuredBackground.R,
            configuredBackground.G,
            configuredBackground.B);
        using (var backgroundBrush = new SolidBrush(background))
            graphics.FillRectangle(backgroundBrush, new Rectangle(Point.Empty, size));

        float scale = DeviceDpi / 96f;
        int padding = Scale(2, scale);
        int marginX = Scale(2, scale);
        int marginY = Scale(1, scale);
        int metricHeight = Scale(20, scale);
        int firstColumnWidth = Math.Max(
            _settings.FiveHour.Enabled ? MetricWidth(_settings.FiveHour, scale) : 0,
            _settings.Weekly.Enabled ? MetricWidth(_settings.Weekly, scale) : 0);
        int secondColumnWidth = Math.Max(
            _settings.Cpu.Enabled ? MetricWidth(_settings.Cpu, scale) : 0,
            _settings.Memory.Enabled ? MetricWidth(_settings.Memory, scale) : 0);
        int firstColumnTotal = firstColumnWidth > 0 ? firstColumnWidth + marginX * 2 : 0;
        int secondX = padding + firstColumnTotal + marginX;
        int firstX = padding + marginX;
        bool firstRowVisible = _settings.FiveHour.Enabled || _settings.Cpu.Enabled;
        int firstY = padding + marginY;
        int secondY = padding + (firstRowVisible ? metricHeight + marginY * 2 : 0) + marginY;

        if (_settings.FiveHour.Enabled)
            DrawQuotaMetric(graphics, new Rectangle(firstX, firstY, MetricWidth(_settings.FiveHour, scale), metricHeight), "5H", _snapshot.FiveHour, _settings.FiveHour, scale);
        if (_settings.Cpu.Enabled)
            DrawMetric(graphics, new Rectangle(secondX, firstY, MetricWidth(_settings.Cpu, scale), metricHeight), "CPU", _systemUsage.CpuPercent, _settings.Cpu, scale);
        if (_settings.Weekly.Enabled)
            DrawQuotaMetric(graphics, new Rectangle(firstX, secondY, MetricWidth(_settings.Weekly, scale), metricHeight), "WK", _snapshot.Weekly, _settings.Weekly, scale);
        if (_settings.Memory.Enabled)
            DrawMetric(graphics, new Rectangle(secondX, secondY, MetricWidth(_settings.Memory, scale), metricHeight), "RAM", _systemUsage.MemoryPercent, _settings.Memory, scale);
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

    private static void DrawMetric(
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

    private static int MetricWidth(MetricSettings settings, float scale) => settings.Presentation switch
    {
        MetricPresentation.PercentOnly => Scale(94, scale),
        MetricPresentation.BarOnly => Scale(124, scale),
        _ => Scale(152, scale)
    };

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
