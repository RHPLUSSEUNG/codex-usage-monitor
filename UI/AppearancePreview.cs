using CodexUsageMonitor.Models;

namespace CodexUsageMonitor.UI;

internal sealed class AppearancePreview : Panel
{
    private readonly PreviewCanvas _canvas;
    private readonly PreviewHorizontalScrollBar _horizontalScroll;
    private int _horizontalOffset;
    internal CompactBarRenderer.HitRegion? Selection { get; private set; }
    public event Action? SelectionChanged;
    public event Action? LayoutChanged;
    internal AppSettings Settings { get; }
    internal UsageSnapshot Snapshot { get; set; } = UsageSnapshot.Waiting;
    internal SystemUsageSnapshot SystemUsage { get; set; } = SystemUsageSnapshot.Empty;

    public AppearancePreview(AppSettings settings)
    {
        Settings = settings;
        Width = 670;
        Height = 160;
        BorderStyle = BorderStyle.None;
        _canvas = new PreviewCanvas(this) { Location = Point.Empty };
        _horizontalScroll = new PreviewHorizontalScrollBar(SetHorizontalOffset) { Height = 12, Visible = false };
        Controls.Add(_canvas);
        Controls.Add(_horizontalScroll);
        _horizontalScroll.BringToFront();
        MouseDown += (_, _) => Select(null);
    }

    internal void RefreshPreview()
    {
        float dpi = DeviceDpi / 96f;
        AppSettings previewSettings = CreatePreviewSettings();
        Size size = CompactBarRenderer.CalculateSize(previewSettings, dpi, (int)(48 * dpi));
        Size bar = CompactBarForm.ScaleSize(size, Settings.CompactBarScalePercent);
        _canvas.Size = new Size(Math.Max(ClientSize.Width - 8, bar.Width + 48), bar.Height + 64);
        int maximum = Math.Max(0, _canvas.Width - ClientSize.Width);
        _horizontalOffset = Math.Clamp(_horizontalOffset, 0, maximum);
        _canvas.Location = new Point(-_horizontalOffset, 0);
        _horizontalScroll.Visible = maximum > 0;
        _horizontalScroll.Bounds = new Rectangle(0, _canvas.Height, ClientSize.Width, 12);
        _horizontalScroll.SetRange(_horizontalOffset, maximum, ClientSize.Width);
        Height = Math.Clamp(_canvas.Height + (_horizontalScroll.Visible ? 16 : 8), 120 * DeviceDpi / 96, 260 * DeviceDpi / 96);
        _canvas.Invalidate();
    }

    private void SetHorizontalOffset(int value)
    {
        int maximum = Math.Max(0, _canvas.Width - ClientSize.Width);
        _horizontalOffset = Math.Clamp(value, 0, maximum);
        _canvas.Location = new Point(-_horizontalOffset, 0);
        _horizontalScroll.SetRange(_horizontalOffset, maximum, ClientSize.Width);
    }

    private AppSettings CreatePreviewSettings()
    {
        var preview = Settings.Copy();
        foreach (PanelId id in new[] { PanelId.FiveHour, PanelId.Weekly, PanelId.Cpu, PanelId.Memory })
        {
            MetricSettings metric = preview.Metric(id);
            metric.Enabled = true;
            metric.Presentation = MetricPresentation.PercentAndBar;
            if (id is PanelId.FiveHour or PanelId.Weekly)
                metric.ShowResetTime = true;
        }
        return preview;
    }

    internal void Select(CompactBarRenderer.HitRegion? hit)
    {
        Selection = hit;
        SelectionChanged?.Invoke();
        _canvas.Invalidate();
    }

    internal sealed class PreviewCanvas : Control
    {
        private readonly AppearancePreview _owner;
        private readonly List<CompactBarRenderer.HitRegion> _hits = [];
        private readonly System.Windows.Forms.Timer _animation = new() { Interval = 16 };
        private Dictionary<PanelId, Rectangle> _from = [];
        private long _animationStart;
        private CompactBarRenderer.HitRegion? _pressed;
        private Point _start, _current = new(-1, -1);
        private bool _dragging;
        internal bool Dragging => _dragging;
        internal bool Animating => _animation.Enabled;
        internal IReadOnlyList<CompactBarRenderer.HitRegion> HitRegions => _hits;
        internal Point Origin => new(24, 24);
        private float UserScale => CompactBarForm.UserScale(_owner.Settings.CompactBarScalePercent);
        private float DpiScale => DeviceDpi / 96f;
        public PreviewCanvas(AppearancePreview owner)
        {
            _owner = owner;
            DoubleBuffered = true;
            TabStop = true;
            SetStyle(ControlStyles.ResizeRedraw, true);
            AccessibleName = "Compact Bar preview";
            _animation.Tick += (_, _) => { if (Environment.TickCount64 - _animationStart >= 200) _animation.Stop(); Invalidate(); };
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) _animation.Dispose();
            base.Dispose(disposing);
        }
        private Rectangle ScreenBounds(Rectangle bounds) => new(
            Origin.X + (int)Math.Round(bounds.X * UserScale), Origin.Y + (int)Math.Round(bounds.Y * UserScale),
            (int)Math.Round(bounds.Width * UserScale), (int)Math.Round(bounds.Height * UserScale));
        private List<CompactBarRenderer.PanelLayout> CreateLayout(AppSettings settings) =>
            CompactBarRenderer.Layout(settings, DpiScale, (int)(48 * DpiScale));
        private Dictionary<PanelId, Rectangle> Panels(IReadOnlyList<CompactBarRenderer.PanelLayout> layout) => layout
            .ToDictionary(p => p.Panel, p => ScreenBounds(p.Bounds));
        private CompactBarRenderer.HitRegion? Hit(Point p)
        {
            var logical = new Point((int)((p.X - Origin.X) / UserScale), (int)((p.Y - Origin.Y) / UserScale));
            return _hits.LastOrDefault(hit => hit.Bounds.Contains(logical));
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            AppSettings previewSettings = _owner.CreatePreviewSettings();
            List<CompactBarRenderer.PanelLayout> layout = CreateLayout(previewSettings);
            Size size = CompactBarRenderer.CalculateSize(layout, DpiScale);
            using var bitmap = new Bitmap(size.Width, size.Height);
            bitmap.SetResolution(DeviceDpi, DeviceDpi);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                _hits.Clear();
                CompactBarRenderer.DrawWithRegions(g, size, previewSettings, _owner.Snapshot, _owner.SystemUsage, DpiScale, _hits);
            }
            var panels = Panels(layout);
            var whole = new Rectangle(Origin, CompactBarForm.ScaleSize(size, _owner.Settings.CompactBarScalePercent));
            if (!_dragging && !_animation.Enabled)
            {
                e.Graphics.DrawImage(bitmap, whole);
                DrawHiddenOverlays(e.Graphics, panels);
            }
            else
            {
                float t = Math.Clamp((Environment.TickCount64 - _animationStart) / 200f, 0, 1);
                t = 1 - MathF.Pow(1 - t, 3);
                foreach (var panel in layout)
                {
                    Rectangle target = panels[panel.Panel];
                    if (_animation.Enabled && _from.TryGetValue(panel.Panel, out var from))
                    {
                        target.X = (int)(from.X + (target.X - from.X) * t);
                        target.Y = (int)(from.Y + (target.Y - from.Y) * t);
                    }
                    float opacity = !CompactBarRenderer.IsPanelVisible(_owner.Settings, panel.Panel) ? .32f :
                        _dragging && panel.Panel == _pressed?.Panel ? .2f : 1f;
                    DrawPanel(e.Graphics, bitmap, panel.Bounds, target, opacity);
                }
                if (_dragging && _pressed is not null && panels.TryGetValue(_pressed.Panel, out var original))
                {
                    var targetHit = Hit(_current);
                    if (targetHit is not null && targetHit.Panel != _pressed.Panel)
                    {
                        using var fill = new SolidBrush(Color.FromArgb(40, ForeColor));
                        using var pen = new Pen(ForeColor, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                        e.Graphics.FillRectangle(fill, panels[targetHit.Panel]);
                        e.Graphics.DrawRectangle(pen, panels[targetHit.Panel]);
                    }
                    var source = _hits.First(h => h.Panel == _pressed.Panel && h.Element == CompactBarRenderer.Element.Panel).Bounds;
                    original.Offset(_current.X - _start.X, _current.Y - _start.Y);
                    using var backing = new SolidBrush(Color.FromArgb(180, BackColor));
                    e.Graphics.FillRectangle(backing, original);
                    DrawPanel(e.Graphics, bitmap, source, original, 0.7f);
                    using var outline = new Pen(ForeColor, 1);
                    e.Graphics.DrawRectangle(outline, original);
                }
            }
            if (!_dragging && !_animation.Enabled)
            {
                var selected = _owner.Selection;
                var region = _hits.LastOrDefault(h => h.Panel == selected?.Panel && h.Element == selected.Element);
                var hover = Hit(_current);
                bool backgroundHover = ClientRectangle.Contains(_current) && hover is null;
                if (backgroundHover)
                {
                    using var fill = new SolidBrush(Color.FromArgb(25, ForeColor));
                    e.Graphics.FillRectangle(fill, whole);
                }
                if (backgroundHover || selected is null)
                {
                    using var pen = new Pen(Color.FromArgb(backgroundHover ? 190 : 90, ForeColor), backgroundHover ? 2 : 1);
                    e.Graphics.DrawRectangle(pen, whole.X, whole.Y, Math.Max(1, whole.Width - 1), Math.Max(1, whole.Height - 1));
                }
                if (hover is not null)
                {
                    using var fill = new SolidBrush(Color.FromArgb(25, ForeColor));
                    e.Graphics.FillRectangle(fill, ScreenBounds(hover.Bounds));
                }
                if (region is not null)
                {
                    using var pen = new Pen(ForeColor, 1);
                    e.Graphics.DrawRectangle(pen, ScreenBounds(region.Bounds));
                }
            }
        }
        private void DrawHiddenOverlays(Graphics graphics, Dictionary<PanelId, Rectangle> panels)
        {
            using var fade = new SolidBrush(Color.FromArgb(158, BackColor));
            using var outline = new Pen(Color.FromArgb(95, ForeColor)) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            foreach (PanelId id in new[] { PanelId.FiveHour, PanelId.Weekly, PanelId.Cpu, PanelId.Memory })
            {
                MetricSettings metric = _owner.Settings.Metric(id);
                if (!metric.Enabled && panels.TryGetValue(id, out var panel))
                {
                    graphics.FillRectangle(fade, panel);
                    graphics.DrawRectangle(outline, panel);
                    continue;
                }
                if (metric.Presentation == MetricPresentation.PercentOnly)
                    FadeElement(id, CompactBarRenderer.Element.Bar);
                else if (metric.Presentation == MetricPresentation.BarOnly)
                    FadeElement(id, CompactBarRenderer.Element.Percent);
            }
            if (!_owner.Settings.FiveHour.ShowResetTime) FadeElement(PanelId.FiveHourReset, CompactBarRenderer.Element.ResetTime);
            if (!_owner.Settings.Weekly.ShowResetTime) FadeElement(PanelId.WeeklyReset, CompactBarRenderer.Element.ResetTime);

            void FadeElement(PanelId id, CompactBarRenderer.Element element)
            {
                CompactBarRenderer.HitRegion? hit = _hits.LastOrDefault(candidate => candidate.Panel == id && candidate.Element == element);
                if (hit is null) return;
                Rectangle bounds = ScreenBounds(hit.Bounds);
                graphics.FillRectangle(fade, bounds);
                graphics.DrawRectangle(outline, bounds);
            }
        }
        private static void DrawPanel(Graphics graphics, Bitmap bitmap, Rectangle source, Rectangle destination, float alpha)
        {
            using var attributes = new System.Drawing.Imaging.ImageAttributes();
            attributes.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = alpha });
            graphics.DrawImage(bitmap, destination, source.X, source.Y, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            _animation.Stop();
            _current = _start = e.Location;
            _pressed = Hit(e.Location);
            _dragging = false;
            Capture = true;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _current = e.Location;
            if (_pressed is not null && Capture && e.Button == MouseButtons.Left)
            {
                Size threshold = SystemInformation.DragSize;
                if (Math.Abs(e.X - _start.X) > threshold.Width / 2 || Math.Abs(e.Y - _start.Y) > threshold.Height / 2)
                    _dragging = true;
            }
            Cursor = _dragging ? Cursors.SizeAll : ClientRectangle.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _current = new Point(-1, -1);
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            var target = Hit(e.Location);
            if (_dragging && _pressed is not null && target is not null && target.Panel != _pressed.Panel)
            {
                AppSettings previewSettings = _owner.CreatePreviewSettings();
                _from = Panels(CreateLayout(previewSettings));
                var ghost = _from[_pressed.Panel];
                ghost.Offset(e.X - _start.X, e.Y - _start.Y);
                _from[_pressed.Panel] = ghost;
                _owner.Settings.SwapPanels(_pressed.Panel, target.Panel);
                _owner.Select(_pressed);
                _owner.LayoutChanged?.Invoke();
                _animationStart = Environment.TickCount64;
                _animation.Start();
            }
            else if (!_dragging) _owner.Select(target);
            CancelDrag();
        }
        private void CancelDrag()
        {
            _pressed = null;
            _dragging = false;
            Capture = false;
            Cursor = Cursors.Default;
            Invalidate();
        }
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture && _pressed is not null) CancelDrag();
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape && _pressed is not null) { CancelDrag(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            Control? current = Parent;
            while (current is not null && current is not SettingsScrollHost)
                current = current.Parent;
            if (current is SettingsScrollHost host)
                host.ScrollBy(-Math.Sign(e.Delta) * Math.Max(1, SystemInformation.MouseWheelScrollLines) * 18);
            base.OnMouseWheel(e);
        }
    }

    private sealed class PreviewHorizontalScrollBar : Control
    {
        private readonly Action<int> _setValue;
        private int _value, _maximum, _viewport;
        private bool _hover, _dragging;
        private int _dragOriginX, _dragOriginValue;
        public PreviewHorizontalScrollBar(Action<int> setValue)
        {
            _setValue = setValue;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }
        internal void SetRange(int value, int maximum, int viewport)
        {
            _value = value; _maximum = maximum; _viewport = viewport; Invalidate();
        }
        private Rectangle Thumb()
        {
            int trackWidth = Math.Max(1, Width - 12);
            int thumbWidth = _maximum <= 0 ? trackWidth : Math.Max(36, trackWidth * _viewport / Math.Max(1, _viewport + _maximum));
            int travel = Math.Max(0, trackWidth - thumbWidth);
            int x = 6 + (_maximum <= 0 ? 0 : travel * _value / _maximum);
            return new Rectangle(x, 4, thumbWidth, Math.Max(4, Height - 7));
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? BackColor);
            using var track = new Pen(SettingsPaint.Blend(BackColor, ForeColor, .10f), 3)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round
            };
            e.Graphics.DrawLine(track, 7, Height / 2, Width - 7, Height / 2);
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
                _dragging = true; _dragOriginX = e.X; _dragOriginValue = _value; Capture = true;
            }
            else
            {
                int travel = Math.Max(1, Width - 12 - thumb.Width);
                _setValue((e.X - 6 - thumb.Width / 2) * _maximum / travel);
            }
            Invalidate();
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging) return;
            int travel = Math.Max(1, Width - 12 - Thumb().Width);
            _setValue(_dragOriginValue + (e.X - _dragOriginX) * _maximum / travel);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            _dragging = false; Capture = false; Invalidate();
        }
    }
}
