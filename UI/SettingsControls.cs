using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace CodexUsageMonitor.UI;

internal static class SettingsPaint
{
    public static Color Blend(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    public static GraphicsPath Rounded(Rectangle rectangle, int radius = 8)
    {
        var path = new GraphicsPath();
        int d = Math.Max(1, Math.Min(radius * 2, Math.Min(rectangle.Width, rectangle.Height)));
        path.AddArc(rectangle.Left, rectangle.Top, d, d, 180, 90);
        path.AddArc(rectangle.Right - d, rectangle.Top, d, d, 270, 90);
        path.AddArc(rectangle.Right - d, rectangle.Bottom - d, d, d, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
    public static void Surface(Graphics g, Rectangle bounds, Color color, Color? border = null)
    {
        if (bounds.Width <= 1 || bounds.Height <= 1) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        bounds.Width--; bounds.Height--;
        using var path = Rounded(bounds);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
        if (border is { } outline) { using var pen = new Pen(outline); g.DrawPath(pen, path); }
    }
    public static void Chevron(Graphics g, Rectangle r, Color color, bool up = false)
    {
        using var pen = new Pen(color, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        int x = r.Left + r.Width / 2, y = r.Top + r.Height / 2, sign = up ? -1 : 1;
        g.DrawLines(pen, new Point[] { new Point(x - 4, y - 2 * sign), new Point(x, y + 2 * sign), new Point(x + 4, y - 2 * sign) });
    }
}

internal sealed class SettingsButton : Button
{
    public event PaintEventHandler? OverlayPaint;
    private bool _hover, _pressed;
    internal Color Accent { get; set; }
    public SettingsButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Padding = new Padding(12, 6, 12, 6);
        MinimumSize = new Size(64, 34);
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }
    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? BackColor);
        Color bg = SettingsPaint.Blend(BackColor, ForeColor, _pressed ? .2f : _hover ? .12f : 0);
        SettingsPaint.Surface(e.Graphics, ClientRectangle, bg, Focused ? Accent : null);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle,
            Enabled ? ForeColor : SettingsPaint.Blend(BackColor, ForeColor, .4f),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        // Mode cards paint their miniature above this base surface.
        OverlayPaint?.Invoke(this, e);
    }
}

internal sealed class SettingsToggle : CheckBox
{
    internal Color Accent { get; set; } = Color.SeaGreen;
    private bool _hover;
    public SettingsToggle() { SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); Cursor = Cursors.Hand; }
    public override Size GetPreferredSize(Size proposedSize)
    {
        var size = TextRenderer.MeasureText(Text, Font);
        return new Size(size.Width + 58, Math.Max(34, size.Height + 12));
    }
    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        int h = Math.Min(22, Height - 4), y = (Height - h) / 2;
        Color color = Checked ? Accent : SettingsPaint.Blend(BackColor, ForeColor, _hover ? .3f : .18f);
        if (!Enabled) color = SettingsPaint.Blend(BackColor, color, .4f);
        SettingsPaint.Surface(e.Graphics, new Rectangle(0, y, 40, h), color, Focused ? ForeColor : null);
        using var brush = new SolidBrush(Checked ? BackColor : ForeColor);
        e.Graphics.FillEllipse(brush, Checked ? 22 : 3, y + 3, h - 6, h - 6);
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(50, 0, Math.Max(1, Width - 50), Height), ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }
}

internal sealed class SettingsSection : GroupBox
{
    public SettingsSection()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Margin = new Padding(0, 0, 0, 18);
        Padding = new Padding(18, 14, 18, 16);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? BackColor);
        SettingsPaint.Surface(e.Graphics, ClientRectangle, BackColor);
        using var heading = new Font(Font, FontStyle.Bold);
        TextRenderer.DrawText(e.Graphics, Text, heading, new Rectangle(18, 10, Width - 36, 26), ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }
}

internal sealed class SettingsRow : Panel
{
    public SettingsRow() { DoubleBuffered = true; Height = 54; Margin = Padding.Empty; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(SettingsPaint.Blend(BackColor, ForeColor, .10f));
        e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
    }
}

internal sealed class SettingsScrollHost : Control
{
    private readonly SettingsScrollBar _scrollBar;
    private Control? _content;
    private int _offset;
    private bool _arranging;

    public SettingsScrollHost()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        _scrollBar = new SettingsScrollBar(SetOffset) { Width = 13 };
        Controls.Add(_scrollBar);
    }

    internal void SetContent(Control content)
    {
        if (_content is not null)
        {
            _content.SizeChanged -= ContentChanged;
            _content.Layout -= ContentLayout;
            Controls.Remove(_content);
        }
        _content = content;
        content.Dock = DockStyle.None;
        content.Location = Point.Empty;
        content.SizeChanged += ContentChanged;
        content.Layout += ContentLayout;
        Controls.Add(content);
        _scrollBar.BringToFront();
        RefreshLayout();
    }

    private void ContentChanged(object? sender, EventArgs e) => RefreshLayout();
    private void ContentLayout(object? sender, LayoutEventArgs e) => RefreshLayout();
    protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); RefreshLayout(); }
    protected override void OnMouseWheel(MouseEventArgs e) { ScrollBy(-Math.Sign(e.Delta) * Math.Max(1, SystemInformation.MouseWheelScrollLines) * 18); base.OnMouseWheel(e); }

    internal void ScrollBy(int delta) => SetOffset(_offset + delta);
    private void SetOffset(int value)
    {
        if (_content is null) return;
        int maximum = Math.Max(0, _content.Height - ClientSize.Height);
        _offset = Math.Clamp(value, 0, maximum);
        _content.Location = new Point(0, -_offset);
        _scrollBar.SetRange(_offset, maximum, ClientSize.Height);
    }

    internal void RefreshLayout()
    {
        if (_arranging || _content is null || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        _arranging = true;
        try
        {
            int barWidth = 13;
            int contentWidth = Math.Max(1, ClientSize.Width - barWidth - 4);
            _scrollBar.Bounds = new Rectangle(ClientSize.Width - barWidth, 0, barWidth, ClientSize.Height);
            _content.MaximumSize = new Size(contentWidth, 0);
            _content.PerformLayout();
            Size preferred = _content.GetPreferredSize(new Size(contentWidth, 0));
            _content.Size = new Size(contentWidth, Math.Max(1, preferred.Height));
            SetOffset(_offset);
        }
        finally { _arranging = false; }
    }

    private sealed class SettingsScrollBar : Control
    {
        private readonly Action<int> _setValue;
        private int _value, _maximum, _viewport;
        private bool _hover, _dragging;
        private int _dragOriginY, _dragOriginValue;
        public SettingsScrollBar(Action<int> setValue)
        {
            _setValue = setValue;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }
        internal void SetRange(int value, int maximum, int viewport)
        {
            _value = value; _maximum = maximum; _viewport = viewport;
            Visible = maximum > 0;
            Invalidate();
        }
        private Rectangle Thumb()
        {
            int trackHeight = Math.Max(1, Height - 12);
            int thumbHeight = _maximum <= 0 ? trackHeight : Math.Max(32, trackHeight * _viewport / Math.Max(1, _viewport + _maximum));
            int travel = Math.Max(0, trackHeight - thumbHeight);
            int y = 6 + (_maximum <= 0 ? 0 : travel * _value / _maximum);
            return new Rectangle(4, y, Math.Max(4, Width - 7), thumbHeight);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? BackColor);
            using var track = new Pen(SettingsPaint.Blend(BackColor, ForeColor, .10f), 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            e.Graphics.DrawLine(track, Width / 2, 7, Width / 2, Height - 7);
            Color color = SettingsPaint.Blend(BackColor, ForeColor, _dragging ? .48f : _hover ? .36f : .25f);
            SettingsPaint.Surface(e.Graphics, Thumb(), color);
        }
        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; if (!_dragging) Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Rectangle thumb = Thumb();
            if (thumb.Contains(e.Location))
            {
                _dragging = true; _dragOriginY = e.Y; _dragOriginValue = _value; Capture = true;
            }
            else
            {
                int center = thumb.Height / 2;
                int travel = Math.Max(1, Height - 12 - thumb.Height);
                _setValue((e.Y - 6 - center) * _maximum / travel);
            }
            Invalidate();
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging) return;
            int travel = Math.Max(1, Height - 12 - Thumb().Height);
            _setValue(_dragOriginValue + (e.Y - _dragOriginY) * _maximum / travel);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            _dragging = false; Capture = false; Invalidate();
        }
    }
}

public sealed partial class SettingsForm
{
    private sealed class SettingsComboBox : Control
    {
        private bool _hover, _pressed, _wasDroppedDownOnPress;
        private int _selectedIndex = -1;
        private ToolStripDropDown? _popup;
        internal List<object> Items { get; } = [];
        internal int ItemHeight { get; set; } = 32;
        internal int DropDownHeight { get; set; } = 280;
        internal bool DroppedDown => _popup is { Visible: true };
        internal Color Accent { get; set; }
        internal event DrawItemEventHandler? DrawItem;
        internal event EventHandler? SelectedIndexChanged;
        internal void BeginUpdate() => SuspendLayout();
        internal void EndUpdate() { ResumeLayout(); Invalidate(); }
        internal int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                int next = Items.Count == 0 ? -1 : Math.Clamp(value, -1, Items.Count - 1);
                if (_selectedIndex == next) return;
                _selectedIndex = next;
                Invalidate();
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public SettingsComboBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 34;
            MinimumSize = new Size(80, 34);
            TabStop = true;
            Cursor = Cursors.Hand;
        }
        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _wasDroppedDownOnPress = DroppedDown;
                _pressed = true;
            }
            Invalidate();
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            bool open = e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location);
            _pressed = false;
            Invalidate();
            base.OnMouseUp(e);
            if (open && !_wasDroppedDownOnPress) TogglePopup();
            _wasDroppedDownOnPress = false;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? BackColor);
            Color bg = SettingsPaint.Blend(BackColor, ForeColor, _pressed ? .18f : _hover || DroppedDown ? .12f : 0);
            SettingsPaint.Surface(e.Graphics, ClientRectangle, bg, Focused || DroppedDown ? Accent : null);
            var text = new Rectangle(4, 2, Width - 32, Height - 4);
            DrawItemCore(e.Graphics, text, SelectedIndex, DrawItemState.ComboBoxEdit);
            SettingsPaint.Chevron(e.Graphics, new Rectangle(Width - 28, 0, 24, Height), ForeColor, DroppedDown);
        }
        private void DrawItemCore(Graphics graphics, Rectangle bounds, int index, DrawItemState state)
        {
            if (index < 0 || index >= Items.Count) return;
            if (DrawItem is not null)
            {
                DrawItem(this, new DrawItemEventArgs(graphics, Font, bounds, index, state, ForeColor, BackColor));
                return;
            }
            bool hot = (state & DrawItemState.Selected) != 0;
            using var brush = new SolidBrush(hot ? SettingsPaint.Blend(BackColor, ForeColor, .14f) : BackColor);
            graphics.FillRectangle(brush, bounds);
            int trailing = (state & DrawItemState.ComboBoxEdit) != 0 ? 16 : 38;
            TextRenderer.DrawText(graphics, Items[index]?.ToString() ?? "", Font,
                new Rectangle(bounds.X + 10, bounds.Y, Math.Max(1, bounds.Width - trailing), bounds.Height), ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if ((state & DrawItemState.ComboBoxEdit) == 0 && index == SelectedIndex)
                TextRenderer.DrawText(graphics, "✓", Font, new Rectangle(bounds.Right - 26, bounds.Y, 24, bounds.Height), Accent,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        private void TogglePopup()
        {
            if (DroppedDown) { _popup!.Close(); return; }
            if (Items.Count == 0) return;

            var list = new DropDownList(this)
            {
                Size = new Size(Width, Math.Min(DropDownHeight, Items.Count * ItemHeight))
            };
            var host = new ToolStripControlHost(list)
            {
                AutoSize = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                Size = list.Size
            };
            var popup = new ToolStripDropDown
            {
                AutoClose = true,
                AutoSize = false,
                BackColor = BackColor,
                Padding = new Padding(1),
                Margin = Padding.Empty,
                Size = new Size(list.Width + 2, list.Height + 2),
                DropShadowEnabled = false,
                Renderer = new PopupRenderer(BackColor, ForeColor)
            };
            popup.Items.Add(host);
            popup.Closed += (_, _) => { _popup = null; Invalidate(); };
            _popup = popup;
            popup.Show(PointToScreen(new Point(0, Height + 2)));
            list.Focus();
            Invalidate();
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode is Keys.Enter or Keys.Space or Keys.F4 || e.Alt && e.KeyCode == Keys.Down)
            {
                TogglePopup(); e.Handled = true; return;
            }
            if (e.KeyCode is Keys.Up or Keys.Down && Items.Count > 0)
            {
                int delta = e.KeyCode == Keys.Up ? -1 : 1;
                SelectedIndex = Math.Clamp(Math.Max(0, SelectedIndex) + delta, 0, Items.Count - 1);
                e.Handled = true;
            }
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ScrollParent(this, e);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) _popup?.Dispose();
            base.Dispose(disposing);
        }
        private sealed class DropDownList : Control
        {
            private readonly SettingsComboBox _owner;
            private int _hoverIndex = -1;
            private int _firstVisible;
            public DropDownList(SettingsComboBox owner)
            {
                _owner = owner;
                BackColor = owner.BackColor;
                ForeColor = owner.ForeColor;
                Font = owner.Font;
                TabStop = true;
                Cursor = Cursors.Hand;
                DoubleBuffered = true;
                int visible = Math.Max(1, owner.DropDownHeight / owner.ItemHeight);
                _firstVisible = Math.Max(0, owner.SelectedIndex - visible + 1);
            }
            private int VisibleCount => Math.Max(1, Height / _owner.ItemHeight);
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(BackColor);
                int last = Math.Min(_owner.Items.Count, _firstVisible + VisibleCount);
                for (int index = _firstVisible; index < last; index++)
                {
                    var bounds = new Rectangle(0, (index - _firstVisible) * _owner.ItemHeight, Width, _owner.ItemHeight);
                    DrawItemState state = index == _hoverIndex ? DrawItemState.Selected : DrawItemState.Default;
                    _owner.DrawItemCore(e.Graphics, bounds, index, state);
                }
                if (_owner.Items.Count > VisibleCount)
                {
                    int trackHeight = Height - 8;
                    int thumbHeight = Math.Max(22, trackHeight * VisibleCount / _owner.Items.Count);
                    int maxFirst = Math.Max(1, _owner.Items.Count - VisibleCount);
                    int y = 4 + (trackHeight - thumbHeight) * _firstVisible / maxFirst;
                    Color thumb = SettingsPaint.Blend(BackColor, ForeColor, .28f);
                    SettingsPaint.Surface(e.Graphics, new Rectangle(Width - 7, y, 4, thumbHeight), thumb);
                }
            }
            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int next = _firstVisible + e.Y / Math.Max(1, _owner.ItemHeight);
                next = e.X >= 0 && e.X < Width && next < _owner.Items.Count ? next : -1;
                if (_hoverIndex != next) { _hoverIndex = next; Invalidate(); }
            }
            protected override void OnMouseLeave(EventArgs e) { _hoverIndex = -1; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (e.Button != MouseButtons.Left || _hoverIndex < 0) return;
                _owner.SelectedIndex = _hoverIndex;
                _owner._popup?.Close();
            }
            protected override void OnMouseWheel(MouseEventArgs e)
            {
                int max = Math.Max(0, _owner.Items.Count - VisibleCount);
                _firstVisible = Math.Clamp(_firstVisible - Math.Sign(e.Delta), 0, max);
                _hoverIndex = -1;
                Invalidate();
                base.OnMouseWheel(e);
            }
            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                if (keyData == Keys.Escape) { _owner._popup?.Close(); return true; }
                if (keyData is Keys.Up or Keys.Down)
                {
                    int delta = keyData == Keys.Up ? -1 : 1;
                    int next = Math.Clamp((_hoverIndex < 0 ? _owner.SelectedIndex : _hoverIndex) + delta, 0, _owner.Items.Count - 1);
                    _hoverIndex = next;
                    if (next < _firstVisible) _firstVisible = next;
                    if (next >= _firstVisible + VisibleCount) _firstVisible = next - VisibleCount + 1;
                    Invalidate();
                    return true;
                }
                if (keyData is Keys.Enter or Keys.Space && _hoverIndex >= 0)
                {
                    _owner.SelectedIndex = _hoverIndex;
                    _owner._popup?.Close();
                    return true;
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }
        }
        private sealed class PopupRenderer(Color background, Color foreground) : ToolStripRenderer
        {
            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            {
                using var brush = new SolidBrush(background);
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using var pen = new Pen(SettingsPaint.Blend(background, foreground, .18f));
                var bounds = new Rectangle(Point.Empty, e.ToolStrip.Size);
                bounds.Width--; bounds.Height--;
                e.Graphics.DrawRectangle(pen, bounds);
            }
        }
    }

    private sealed class SettingsTrackBar : TrackBar
    {
        internal Color Accent { get; set; } = Color.SeaGreen;
        private bool _hover;
        public SettingsTrackBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            AutoSize = false; Height = 34; Cursor = Cursors.Hand;
        }
        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnValueChanged(EventArgs e) { Invalidate(); base.OnValueChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            int y = Height / 2, x = 10 + (int)((Width - 20) * (Value - Minimum) / (double)Math.Max(1, Maximum - Minimum));
            using var track = new Pen(SettingsPaint.Blend(BackColor, ForeColor, .2f), 5) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var fill = new Pen(Accent, 5) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawLine(track, 10, y, Width - 10, y);
            e.Graphics.DrawLine(fill, 10, y, x, y);
            using var thumb = new SolidBrush(_hover || Focused ? SettingsPaint.Blend(Accent, ForeColor, .2f) : Accent);
            e.Graphics.FillEllipse(thumb, x - 7, y - 7, 14, 14);
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg is 0x201 or 0x202 || m.Msg == 0x200 && Capture)
            {
                if (m.Msg == 0x201) { Focus(); Capture = true; }
                int x = unchecked((short)(m.LParam.ToInt64() & 0xffff));
                Value = Math.Clamp(Minimum + (int)Math.Round((x - 10) / (double)Math.Max(1, Width - 20) * (Maximum - Minimum)), Minimum, Maximum);
                if (m.Msg == 0x202) Capture = false;
                return;
            }
            base.WndProc(ref m);
        }
        protected override void OnMouseWheel(MouseEventArgs e) => ScrollParent(this, e);
    }

}

internal sealed class SettingsNumericUpDown : Control
{
        private readonly TextBox _editor = new() { BorderStyle = BorderStyle.None, TextAlign = HorizontalAlignment.Left, ImeMode = ImeMode.Disable };
        private decimal _minimum, _maximum = 100, _increment = 1, _value;
        private int _hoverButton;
        private bool _normalizingEditor, _selectAllOnFirstClick;
        internal event EventHandler? ValueChanged;
        internal decimal Minimum { get => _minimum; set { _minimum = value; Value = _value; } }
        internal decimal Maximum { get => _maximum; set { _maximum = value; Value = _value; } }
        internal decimal Increment { get => _increment; set => _increment = Math.Max(1, value); }
        internal decimal Value
        {
            get => _value;
            set
            {
                decimal next = Math.Clamp(value, _minimum, _maximum);
                if (_value == next && _editor.Text == next.ToString()) return;
                bool changed = _value != next;
                _value = next;
                _editor.Text = next.ToString();
                if (changed) ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public SettingsNumericUpDown()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 34;
            MinimumSize = new Size(58, 34);
            Cursor = Cursors.IBeam;
            Controls.Add(_editor);
            _editor.Enter += (_, _) => { _editor.SelectAll(); _selectAllOnFirstClick = true; };
            _editor.MouseUp += (_, _) =>
            {
                if (!_selectAllOnFirstClick) return;
                _editor.SelectAll();
                _selectAllOnFirstClick = false;
            };
            _editor.KeyPress += (_, e) =>
            {
                if (!char.IsControl(e.KeyChar) && e.KeyChar is not (>= '0' and <= '9'))
                    e.Handled = true;
            };
            _editor.TextChanged += (_, _) => NormalizeEditor();
            _editor.KeyDown += (_, e) =>
            {
                _selectAllOnFirstClick = false;
                if (e.KeyCode == Keys.Up) { Step(1); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Down) { Step(-1); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Enter) { CommitEditor(); e.SuppressKeyPress = true; }
            };
            _editor.Leave += (_, _) => CommitEditor();
            _editor.MouseWheel += (_, e) => SettingsForm.ScrollParent(this, e);
            UpdateEditorColors();
        }
        private void Step(int direction)
        {
            decimal startingValue = decimal.TryParse(_editor.Text, out decimal typed)
                ? Math.Clamp(typed, _minimum, _maximum) : _value;
            Value = startingValue + direction * _increment;
            if (_editor.Focused) _editor.SelectAll();
        }
        private void NormalizeEditor()
        {
            if (_normalizingEditor) return;
            string text = _editor.Text;
            string digits = new(text.Where(c => c is >= '0' and <= '9').ToArray());
            if (digits == text) return;
            int cursor = _editor.SelectionStart;
            int digitsBeforeCursor = text.Take(cursor).Count(c => c is >= '0' and <= '9');
            _normalizingEditor = true;
            try
            {
                _editor.Text = digits;
                _editor.SelectionStart = digitsBeforeCursor;
            }
            finally { _normalizingEditor = false; }
        }
        private void CommitEditor()
        {
            if (decimal.TryParse(_editor.Text, out decimal parsed)) Value = parsed;
            else _editor.Text = _value.ToString();
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _editor.Bounds = new Rectangle(8, Math.Max(3, (Height - _editor.PreferredHeight) / 2), Math.Max(1, Width - 34), _editor.PreferredHeight);
        }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); _editor.Font = Font; OnResize(EventArgs.Empty); }
        protected override void OnBackColorChanged(EventArgs e) { base.OnBackColorChanged(e); UpdateEditorColors(); }
        protected override void OnForeColorChanged(EventArgs e) { base.OnForeColorChanged(e); UpdateEditorColors(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); _editor.Enabled = Enabled; Invalidate(); }
        private void UpdateEditorColors() { _editor.BackColor = BackColor; _editor.ForeColor = ForeColor; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? BackColor);
            SettingsPaint.Surface(e.Graphics, ClientRectangle, BackColor,
                Focused || _editor.Focused ? SettingsPaint.Blend(BackColor, ForeColor, .35f) : SettingsPaint.Blend(BackColor, ForeColor, .12f));
            int x = Width - 24, half = Height / 2;
            if (_hoverButton != 0)
            {
                using var hot = new SolidBrush(SettingsPaint.Blend(BackColor, ForeColor, .14f));
                e.Graphics.FillRectangle(hot, new Rectangle(x, _hoverButton == 1 ? 1 : half, 23, half - 1));
            }
            SettingsPaint.Chevron(e.Graphics, new Rectangle(x, 0, 23, half), ForeColor, true);
            SettingsPaint.Chevron(e.Graphics, new Rectangle(x, half, 23, Height - half), ForeColor);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int next = e.X >= Width - 24 ? (e.Y < Height / 2 ? 1 : 2) : 0;
            if (_hoverButton != next) { _hoverButton = next; Cursor = next == 0 ? Cursors.IBeam : Cursors.Hand; Invalidate(); }
        }
        protected override void OnMouseLeave(EventArgs e) { _hoverButton = 0; Cursor = Cursors.IBeam; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && e.X >= Width - 24) Step(e.Y < Height / 2 ? 1 : -1);
            else { _editor.Focus(); _editor.SelectAll(); }
        }
        protected override void OnMouseWheel(MouseEventArgs e) => SettingsForm.ScrollParent(this, e);
}
