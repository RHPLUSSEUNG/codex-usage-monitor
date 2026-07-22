using System.Drawing.Drawing2D;
using CodexUsageMonitor.Models;

namespace CodexUsageMonitor.UI;

internal sealed class ColorPickerDialog : Form
{
    private readonly AppLanguage _language;
    private readonly ColorWheelControl _wheel = new()
    {
        Size = new Size(280, 280),
        BackColor = Color.FromArgb(45, 45, 48)
    };
    private readonly AlphaSliderControl _alpha = new() { Size = new Size(280, 24) };
    private readonly Panel _preview = new() { Size = new Size(280, 38), BorderStyle = BorderStyle.FixedSingle };
    private readonly NumericUpDown _red = NewChannel();
    private readonly NumericUpDown _green = NewChannel();
    private readonly NumericUpDown _blue = NewChannel();
    private readonly NumericUpDown _alphaValue = NewChannel();
    private readonly TextBox _hex = new()
    {
        Width = 120,
        MaxLength = 7,
        CharacterCasing = CharacterCasing.Upper,
        BackColor = Color.FromArgb(32, 32, 32),
        ForeColor = Color.White
    };
    private bool _updating;

    public Color SelectedColor { get; private set; }

    public ColorPickerDialog(Color initial, AppLanguage? language = null)
    {
        _language = language ?? Localization.CurrentLanguage;
        SelectedColor = initial;
        Text = T("ColorPickerTitle");
        Font = new Font("Segoe UI", 9f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(340, 570);
        BackColor = Color.FromArgb(45, 45, 48);
        ForeColor = Color.White;

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(18, 12, 18, 8)
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        Controls.Add(content);

        _preview.Anchor = AnchorStyles.Top;
        _wheel.Anchor = AnchorStyles.Top;
        _alpha.Anchor = AnchorStyles.Top;
        content.Controls.Add(_preview, 0, 0);
        content.Controls.Add(_wheel, 0, 1);
        content.Controls.Add(new Label { Text = T("Opacity"), AutoSize = true, Margin = new Padding(10, 4, 0, 2) }, 0, 2);
        content.Controls.Add(_alpha, 0, 3);

        var channels = new TableLayoutPanel
        {
            AutoSize = true,
            Anchor = AnchorStyles.Top,
            ColumnCount = 4,
            Margin = new Padding(0, 8, 0, 4)
        };
        AddChannel(channels, "R", _red, 0);
        AddChannel(channels, "G", _green, 1);
        AddChannel(channels, "B", _blue, 2);
        AddChannel(channels, "A", _alphaValue, 3);
        content.Controls.Add(channels, 0, 4);

        var hexRow = new FlowLayoutPanel
        {
            AutoSize = true,
            Anchor = AnchorStyles.Top,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 2, 0, 6)
        };
        hexRow.Controls.Add(new Label { Text = "Hex", Width = 45, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft });
        hexRow.Controls.Add(_hex);
        content.Controls.Add(hexRow, 0, 5);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            FlowDirection = FlowDirection.RightToLeft
        };
        var ok = new Button { Text = T("Ok"), AutoSize = true };
        var cancel = new Button { Text = T("Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        content.Controls.Add(buttons, 0, 6);
        AcceptButton = ok;
        CancelButton = cancel;

        _wheel.ColorChanged += (_, _) => SetRgb(_wheel.SelectedColor);
        _alpha.AlphaChanged += (_, _) => SetAlpha(_alpha.Alpha);
        _red.ValueChanged += ChannelChanged;
        _green.ValueChanged += ChannelChanged;
        _blue.ValueChanged += ChannelChanged;
        _alphaValue.ValueChanged += ChannelChanged;
        _hex.TextChanged += HexChanged;
        ok.Click += (_, _) => Confirm();
        SetColor(initial);
    }

    private void HexChanged(object? sender, EventArgs args)
    {
        if (_updating)
            return;

        if (TryParseRgbHex(_hex.Text, out Color rgb))
        {
            _hex.BackColor = Color.FromArgb(32, 32, 32);
            SetColor(Color.FromArgb(SelectedColor.A, rgb.R, rgb.G, rgb.B));
        }
        else
        {
            _hex.BackColor = Color.MistyRose;
            _hex.ForeColor = Color.Black;
        }
    }

    private void Confirm()
    {
        if (!TryParseRgbHex(_hex.Text, out _))
        {
            MessageBox.Show(T("HexError"), T("ColorFormatError"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _hex.Focus();
            _hex.SelectAll();
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private string T(string key) => Localization.Text(_language, key);

    private void ChannelChanged(object? sender, EventArgs args)
    {
        if (_updating)
            return;
        SetColor(Color.FromArgb((int)_alphaValue.Value, (int)_red.Value, (int)_green.Value, (int)_blue.Value));
    }

    private void SetRgb(Color color) =>
        SetColor(Color.FromArgb(SelectedColor.A, color.R, color.G, color.B), updateWheel: false);

    private void SetAlpha(int alpha) =>
        SetColor(Color.FromArgb(alpha, SelectedColor.R, SelectedColor.G, SelectedColor.B), updateAlpha: false);

    private void SetColor(Color color, bool updateWheel = true, bool updateAlpha = true)
    {
        SelectedColor = color;
        _updating = true;
        _red.Value = color.R;
        _green.Value = color.G;
        _blue.Value = color.B;
        _alphaValue.Value = color.A;
        _hex.Text = $"{color.R:X2}{color.G:X2}{color.B:X2}";
        _hex.BackColor = Color.FromArgb(32, 32, 32);
        _hex.ForeColor = Color.White;
        if (updateWheel)
            _wheel.SelectedColor = color;
        if (updateAlpha)
            _alpha.Alpha = color.A;
        _alpha.BaseColor = Color.FromArgb(255, color.R, color.G, color.B);
        _preview.BackColor = BlendOverChecker(color);
        _updating = false;
    }

    private static bool TryParseRgbHex(string text, out Color color)
    {
        color = Color.Empty;
        string value = text.Trim().TrimStart('#');
        if (value.Length != 6 || !int.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out int rgb))
            return false;
        color = Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        return true;
    }

    private static Color BlendOverChecker(Color color)
    {
        const int background = 210;
        double alpha = color.A / 255d;
        return Color.FromArgb(
            255,
            (int)Math.Round(color.R * alpha + background * (1d - alpha)),
            (int)Math.Round(color.G * alpha + background * (1d - alpha)),
            (int)Math.Round(color.B * alpha + background * (1d - alpha)));
    }

    private static NumericUpDown NewChannel() => new()
    {
        Minimum = 0,
        Maximum = 255,
        Width = 54,
        BackColor = Color.FromArgb(32, 32, 32),
        ForeColor = Color.White
    };

    private static void AddChannel(TableLayoutPanel table, string label, Control input, int column)
    {
        var group = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        group.Controls.Add(new Label { Text = label, AutoSize = true });
        group.Controls.Add(input);
        table.Controls.Add(group, column, 0);
    }
}

internal sealed class ColorWheelControl : Control
{
    private double _hue;
    private double _saturation;
    private double _value;
    private Bitmap? _bitmap;

    public event EventHandler? ColorChanged;

    public ColorWheelControl()
    {
        DoubleBuffered = true;
        Cursor = Cursors.Cross;
        ResizeRedraw = true;
    }

    public Color SelectedColor
    {
        get => HsvToColor(_hue, _saturation, _value);
        set
        {
            ColorToHsv(value, out _hue, out _saturation, out _value);
            RebuildBitmap();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_bitmap is null || _bitmap.Size != ClientSize)
            RebuildBitmap();
        if (_bitmap is null)
            return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawImageUnscaled(_bitmap, Point.Empty);
        Geometry(out PointF center, out float outer, out float inner, out RectangleF square);

        double angle = _hue * Math.PI / 180d;
        PointF huePoint = new(
            center.X + (float)(Math.Cos(angle) * (outer + inner) / 2f),
            center.Y + (float)(Math.Sin(angle) * (outer + inner) / 2f));
        DrawMarker(e.Graphics, huePoint, 7f);

        PointF svPoint = new(
            square.Left + (float)_saturation * square.Width,
            square.Bottom - (float)_value * square.Height);
        DrawMarker(e.Graphics, svPoint, 6f);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Capture = true;
        UpdateFromPoint(e.Location);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Capture && e.Button == MouseButtons.Left)
            UpdateFromPoint(e.Location);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        Capture = false;
    }

    private void UpdateFromPoint(Point point)
    {
        Geometry(out PointF center, out float outer, out float inner, out RectangleF square);
        double dx = point.X - center.X;
        double dy = point.Y - center.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        if (distance >= inner && distance <= outer)
        {
            _hue = (Math.Atan2(dy, dx) * 180d / Math.PI + 360d) % 360d;
            RebuildBitmap();
        }
        else if (square.Contains(point))
        {
            _saturation = Math.Clamp((point.X - square.Left) / square.Width, 0d, 1d);
            _value = Math.Clamp((square.Bottom - point.Y) / square.Height, 0d, 1d);
            Invalidate();
        }
        else
        {
            return;
        }

        ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RebuildBitmap()
    {
        _bitmap?.Dispose();
        if (Width <= 0 || Height <= 0)
            return;

        _bitmap = new Bitmap(Width, Height);
        Geometry(out PointF center, out float outer, out float inner, out RectangleF square);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                double dx = x - center.X;
                double dy = y - center.Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance >= inner && distance <= outer)
                {
                    double hue = (Math.Atan2(dy, dx) * 180d / Math.PI + 360d) % 360d;
                    _bitmap.SetPixel(x, y, HsvToColor(hue, 1d, 1d));
                }
                else if (square.Contains(x, y))
                {
                    double saturation = Math.Clamp((x - square.Left) / square.Width, 0d, 1d);
                    double value = Math.Clamp((square.Bottom - y) / square.Height, 0d, 1d);
                    _bitmap.SetPixel(x, y, HsvToColor(_hue, saturation, value));
                }
                else
                {
                    _bitmap.SetPixel(x, y, BackColor);
                }
            }
        }
        Invalidate();
    }

    private void Geometry(out PointF center, out float outer, out float inner, out RectangleF square)
    {
        center = new PointF(Width / 2f, Height / 2f);
        outer = Math.Max(10f, Math.Min(Width, Height) / 2f - 5f);
        inner = Math.Max(4f, outer - 28f);
        float side = inner * 1.36f;
        square = new RectangleF(center.X - side / 2f, center.Y - side / 2f, side, side);
    }

    private static void DrawMarker(Graphics graphics, PointF point, float radius)
    {
        using var dark = new Pen(Color.Black, 3f);
        using var light = new Pen(Color.White, 1.5f);
        RectangleF marker = new(point.X - radius, point.Y - radius, radius * 2f, radius * 2f);
        graphics.DrawEllipse(dark, marker);
        graphics.DrawEllipse(light, marker);
    }

    internal static Color HsvToColor(double hue, double saturation, double value)
    {
        double chroma = value * saturation;
        double section = hue / 60d;
        double x = chroma * (1d - Math.Abs(section % 2d - 1d));
        (double r, double g, double b) = section switch
        {
            < 1d => (chroma, x, 0d),
            < 2d => (x, chroma, 0d),
            < 3d => (0d, chroma, x),
            < 4d => (0d, x, chroma),
            < 5d => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };
        double match = value - chroma;
        return Color.FromArgb(
            255,
            (int)Math.Round((r + match) * 255d),
            (int)Math.Round((g + match) * 255d),
            (int)Math.Round((b + match) * 255d));
    }

    private static void ColorToHsv(Color color, out double hue, out double saturation, out double value)
    {
        double r = color.R / 255d;
        double g = color.G / 255d;
        double b = color.B / 255d;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        hue = delta == 0d
            ? 0d
            : max == r
                ? 60d * (((g - b) / delta) % 6d)
                : max == g
                    ? 60d * ((b - r) / delta + 2d)
                    : 60d * ((r - g) / delta + 4d);
        if (hue < 0d)
            hue += 360d;
        saturation = max == 0d ? 0d : delta / max;
        value = max;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _bitmap?.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class AlphaSliderControl : Control
{
    private int _alpha = 255;
    private Color _baseColor = Color.White;

    public event EventHandler? AlphaChanged;

    public AlphaSliderControl()
    {
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
        ResizeRedraw = true;
    }

    public int Alpha
    {
        get => _alpha;
        set
        {
            _alpha = Math.Clamp(value, 0, 255);
            Invalidate();
        }
    }

    public Color BaseColor
    {
        get => _baseColor;
        set
        {
            _baseColor = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        DrawCheckerboard(e.Graphics, ClientRectangle);
        using var gradient = new LinearGradientBrush(
            ClientRectangle,
            Color.FromArgb(0, _baseColor),
            Color.FromArgb(255, _baseColor),
            LinearGradientMode.Horizontal);
        e.Graphics.FillRectangle(gradient, ClientRectangle);

        int x = (int)Math.Round((Width - 1) * _alpha / 255d);
        using var dark = new Pen(Color.Black, 3f);
        using var light = new Pen(Color.White, 1f);
        e.Graphics.DrawLine(dark, x, 0, x, Height - 1);
        e.Graphics.DrawLine(light, x, 1, x, Height - 2);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Capture = true;
        UpdateAlpha(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Capture && e.Button == MouseButtons.Left)
            UpdateAlpha(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        Capture = false;
    }

    private void UpdateAlpha(int x)
    {
        Alpha = Width <= 1 ? 255 : (int)Math.Round(Math.Clamp(x, 0, Width - 1) * 255d / (Width - 1));
        AlphaChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void DrawCheckerboard(Graphics graphics, Rectangle area)
    {
        const int cell = 8;
        using var light = new SolidBrush(Color.White);
        using var dark = new SolidBrush(Color.LightGray);
        for (int y = 0; y < area.Height; y += cell)
        for (int x = 0; x < area.Width; x += cell)
            graphics.FillRectangle(((x / cell + y / cell) & 1) == 0 ? light : dark, x, y, cell, cell);
    }
}
