using System.Drawing;
using System.Drawing.Imaging;
using CodexUsageMonitor.Models;
using CodexUsageMonitor.Services;
using CodexUsageMonitor.UI;
using System.Reflection;
using System.Windows.Forms;

namespace CodexUsageMonitor.Tests;

public sealed class AppearanceTests
{
    [Fact]
    public void Style_picker_contains_only_supported_styles_with_light_first()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var styles = (CompactBarStyle[])typeof(SettingsForm).GetField("StyleValues", flags)!.GetValue(null)!;

        Assert.Equal(CompactBarStyle.Light, styles[0]);
        Assert.DoesNotContain(CompactBarStyle.DarkMinimal, styles);
        Assert.DoesNotContain(CompactBarStyle.Cards, styles);
        Assert.DoesNotContain(CompactBarStyle.CircularGauges, styles);
        Assert.DoesNotContain(CompactBarStyle.RoundedCapsules, styles);
    }

    [Fact]
    public void Settings_preview_selects_elements_swaps_panels_and_restores_background()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new SettingsForm(new AppSettings { Language = AppLanguage.Korean });
                Assert.True(form.MinimizeBox);
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                T Field<T>(string name) => (T)typeof(SettingsForm).GetField(name, fields)!.GetValue(form)!;
                var tabs = Field<TabControl>("_tabs");
                Assert.Equal("모양", tabs.TabPages[1].Text);
                form.Opacity = 0;
                form.ShowInTaskbar = false;
                form.Show();
                tabs.SelectedIndex = 1;
                Application.DoEvents();
                var preview = Field<AppearancePreview>("_appearancePreview");
                Assert.Null(preview.Selection);
                var backgroundInspector = Field<GroupBox>("_backgroundInspector");
                Assert.True(backgroundInspector.Visible);
                var transparentToggle = backgroundInspector.Controls.OfType<FlowLayoutPanel>().Single()
                    .Controls.OfType<SettingsRow>()
                    .SelectMany(row => row.Controls.OfType<SettingsToggle>())
                    .Single();
                Assert.True(transparentToggle.Checked);
                transparentToggle.Checked = false;
                Assert.False(form.Result.TransparentBackground);
                Assert.NotEqual(0, HexColor.ParseOrDefault(form.Result.BackgroundColor, Color.Transparent).A);
                transparentToggle.Checked = true;
                Assert.True(form.Result.TransparentBackground);
                preview.RefreshPreview();
                Control canvas = preview.Controls.OfType<AppearancePreview.PreviewCanvas>().Single();
                using var bitmap = new Bitmap(canvas.Width, canvas.Height);
                canvas.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                Point backgroundPoint = new(8, canvas.Height - 8);
                canvas.GetType().GetMethod("OnMouseMove", fields)!.Invoke(canvas,
                    [new MouseEventArgs(MouseButtons.None, 0, backgroundPoint.X, backgroundPoint.Y, 0)]);
                Assert.Equal(Cursors.Hand, canvas.Cursor);
                using (var hovered = new Bitmap(canvas.Width, canvas.Height))
                {
                    canvas.DrawToBitmap(hovered, new Rectangle(Point.Empty, hovered.Size));
                    Assert.NotEqual(bitmap.GetPixel(25, 25), hovered.GetPixel(25, 25));
                }
                var settings = form.Result;
                var panels = CompactBarRenderer.Layout(settings, canvas.DeviceDpi / 96f, (int)(48 * canvas.DeviceDpi / 96f));
                var source = panels.Single(p => p.Panel == PanelId.FiveHour);
                var target = panels.Single(p => p.Panel == PanelId.Cpu);
                var reset = panels.Single(p => p.Panel == PanelId.FiveHourReset);
                void Mouse(string method, Point point) => canvas.GetType().GetMethod(method, fields)!.Invoke(canvas,
                    [new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0)]);
                Point Center(Rectangle rect) => new(rect.X + rect.Width / 2 + 24, rect.Y + rect.Height / 2 + 24);
                Point resetPoint = Center(reset.Reset);
                canvas.GetType().GetMethod("OnMouseMove", fields)!.Invoke(canvas,
                    [new MouseEventArgs(MouseButtons.None, 0, resetPoint.X, resetPoint.Y, 0)]);
                Assert.Equal(Cursors.Hand, canvas.Cursor);
                Assert.Null(typeof(AppearancePreview).GetField("_tooltip", fields));
                Mouse("OnMouseDown", Center(reset.Reset));
                Mouse("OnMouseUp", Center(reset.Reset));
                Assert.Equal(CompactBarRenderer.Element.ResetTime, preview.Selection?.Element);
                Assert.Equal(PanelId.FiveHourReset, preview.Selection?.Panel);
                var resetGroup = Field<List<GroupBox>>("_resetInspectors")[0];
                var toggles = resetGroup.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<SettingsRow>()
                    .SelectMany(row => row.Controls.OfType<SettingsToggle>()).ToArray();
                Assert.Equal(2, toggles.Length);
                toggles[1].Checked = false;
                Assert.False(settings.FiveHour.ShowResetTimeLabel);
                Mouse("OnMouseDown", Center(source.Content));
                Mouse("OnMouseMove", Center(target.Content));
                Assert.True(((AppearancePreview.PreviewCanvas)canvas).Dragging);
                Mouse("OnMouseUp", Center(target.Content));
                Assert.True(((AppearancePreview.PreviewCanvas)canvas).Animating);
                Assert.Equal(PanelId.Cpu, settings.PanelOrder[1]);
                Assert.Equal(PanelId.FiveHour, settings.PanelOrder[2]);
                var weeklyReset = panels.Single(p => p.Panel == PanelId.WeeklyReset);
                Mouse("OnMouseDown", Center(reset.Reset));
                Mouse("OnMouseMove", Center(weeklyReset.Reset));
                Mouse("OnMouseUp", Center(weeklyReset.Reset));
                Assert.Equal(PanelId.WeeklyReset, settings.PanelOrder[0]);
                Assert.Equal(PanelId.FiveHourReset, settings.PanelOrder[3]);
                Mouse("OnMouseDown", backgroundPoint);
                Mouse("OnMouseUp", backgroundPoint);
                Assert.Null(preview.Selection);
                Assert.True(Field<GroupBox>("_backgroundInspector").Visible);
                Assert.All(Field<List<GroupBox>>("_metricInspectors"), group => Assert.False(group.Visible));
                form.Close();
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Settings UI did not finish.");
        Assert.Null(failure);
    }

    [Fact]
    public void Settings_minimum_size_keeps_footer_outside_the_scrollable_tab()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new SettingsForm(new AppSettings { Language = AppLanguage.Korean });
                form.Opacity = 0;
                form.ShowInTaskbar = false;
                form.Show();
                form.Size = form.MinimumSize;
                Application.DoEvents();

                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                var tabs = (TabControl)typeof(SettingsForm).GetField("_tabs", fields)!.GetValue(form)!;
                tabs.SelectedIndex = 1;
                Application.DoEvents();

                Control footer = form.Controls[0].Controls.Cast<Control>().Single(control => control is FlowLayoutPanel);
                Rectangle tabBounds = form.RectangleToClient(tabs.RectangleToScreen(tabs.ClientRectangle));
                Rectangle footerBounds = form.RectangleToClient(footer.RectangleToScreen(footer.ClientRectangle));
                Assert.True(tabBounds.Bottom <= footerBounds.Top,
                    $"Tab content {tabBounds} overlaps footer {footerBounds}.");
                Assert.False(tabs.SelectedTab!.HorizontalScroll.Visible);

                using var bitmap = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                form.Close();
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Settings resize UI did not finish.");
        Assert.Null(failure);
    }

    [Fact]
    public void Advanced_cli_path_controls_stay_below_the_section_heading()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new SettingsForm(new AppSettings { Language = AppLanguage.Korean })
                { Opacity = 0, ShowInTaskbar = false };
                form.Show();
                form.Size = form.MinimumSize;
                Application.DoEvents();
                static IEnumerable<Control> Descendants(Control root)
                {
                    foreach (Control child in root.Controls)
                    {
                        yield return child;
                        foreach (Control descendant in Descendants(child)) yield return descendant;
                    }
                }
                Control toggle = Descendants(form).Single(control => control.Tag as string == "ShowAdvancedSettings");
                ((Button)toggle).PerformClick();
                Application.DoEvents();
                Control section = Descendants(form).Single(control => control.Tag as string == "AdvancedSettings");
                Control label = Descendants(section).Single(control => control.Tag as string == "CodexExecutable");
                Control hint = Descendants(section).Single(control => control.Tag as string == "CodexExecutableHint");
                var path = (TextBox)typeof(SettingsForm).GetField("_codexPath", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(form)!;
                Rectangle OnForm(Control control) => form.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
                Assert.True(section.Visible);
                Assert.True(OnForm(label).Top >= OnForm(section).Top + 38);
                Assert.True(OnForm(path).Top >= OnForm(label).Bottom);
                Assert.True(OnForm(hint).Top >= OnForm(path).Bottom);
                Assert.True(OnForm(hint).Bottom <= OnForm(section).Bottom);
                Assert.Contains("Codex CLI", toggle.Text);
                form.Close();
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Advanced CLI path layout did not finish.");
        Assert.Null(failure);
    }

    [Fact]
    public void Appearance_uses_custom_dropdown_and_keeps_hidden_elements_in_preview()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var settings = new AppSettings { Language = AppLanguage.Korean };
                settings.Weekly.Enabled = false;
                settings.FiveHour.ShowResetTime = false;
                using var form = new SettingsForm(settings) { Opacity = 0, ShowInTaskbar = false };
                form.Show();
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                var tabs = (TabControl)typeof(SettingsForm).GetField("_tabs", fields)!.GetValue(form)!;
                tabs.SelectedIndex = 1;
                Application.DoEvents();

                Control palette = (Control)typeof(SettingsForm).GetField("_codexPalette", fields)!.GetValue(form)!;
                Assert.False(palette is ComboBox);
                MethodInfo togglePopup = palette.GetType().GetMethod("TogglePopup", fields)!;
                PropertyInfo droppedDown = palette.GetType().GetProperty("DroppedDown", fields)!;
                togglePopup.Invoke(palette, null);
                Application.DoEvents();
                Assert.True((bool)droppedDown.GetValue(palette)!);
                togglePopup.Invoke(palette, null);
                Application.DoEvents();
                Assert.False((bool)droppedDown.GetValue(palette)!);

                static IEnumerable<Control> Descendants(Control root)
                {
                    foreach (Control child in root.Controls)
                    {
                        yield return child;
                        foreach (Control descendant in Descendants(child)) yield return descendant;
                    }
                }
                Assert.DoesNotContain(Descendants(form), control =>
                    control is ComboBox or NumericUpDown or VScrollBar or HScrollBar);
                Control numeric = (Control)typeof(SettingsForm).GetField("_refreshSeconds", fields)!.GetValue(form)!;
                PropertyInfo numericValue = numeric.GetType().GetProperty("Value", fields)!;
                decimal before = (decimal)numericValue.GetValue(numeric)!;
                numeric.GetType().GetMethod("OnMouseDown", fields)!.Invoke(numeric,
                    [new MouseEventArgs(MouseButtons.Left, 1, numeric.Width - 8, 5, 0)]);
                Assert.True((decimal)numericValue.GetValue(numeric)! > before);
                var scrollHost = tabs.SelectedTab!.Controls.OfType<SettingsScrollHost>().Single();
                Control scrollContent = scrollHost.Controls.OfType<FlowLayoutPanel>().Single();
                scrollHost.ScrollBy(120);
                Assert.True(scrollContent.Top < 0);

                var preview = (AppearancePreview)typeof(SettingsForm).GetField("_appearancePreview", fields)!.GetValue(form)!;
                preview.RefreshPreview();
                var canvas = preview.Controls.OfType<AppearancePreview.PreviewCanvas>().Single();
                using var bitmap = new Bitmap(canvas.Width, canvas.Height);
                canvas.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                Assert.Contains(canvas.HitRegions, hit => hit.Panel == PanelId.Weekly && hit.Element == CompactBarRenderer.Element.Panel);
                Assert.Contains(canvas.HitRegions, hit => hit.Panel == PanelId.FiveHourReset && hit.Element == CompactBarRenderer.Element.ResetTime);
                form.Close();
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Custom settings UI did not finish.");
        Assert.Null(failure);
    }

    [Fact]
    public void Numeric_settings_remove_spacing_and_commit_the_typed_value()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var numeric = new SettingsNumericUpDown { Minimum = 1, Maximum = 99, Value = 10 };
                var editor = numeric.Controls.OfType<TextBox>().Single();
                editor.Text = "1 2";
                Assert.Equal("12", editor.Text);
                typeof(SettingsNumericUpDown).GetMethod("CommitEditor", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(numeric, null);
                Assert.Equal(12, numeric.Value);
                editor.Text = "150";
                typeof(SettingsNumericUpDown).GetMethod("CommitEditor", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(numeric, null);
                Assert.Equal(99, numeric.Value);
                Assert.Equal("99", editor.Text);
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Numeric settings did not finish.");
        Assert.Null(failure);
    }

    [Fact]
    public void Panel_sizes_stay_fixed_while_value_text_can_shorten_the_track()
    {
        foreach (var style in Enum.GetValues<CompactBarStyle>().Distinct())
        {
            var settings = new AppSettings { CompactBarStyle = style };
            var before = CompactBarRenderer.Layout(settings, 1, 48).ToDictionary(p => p.Panel);
            settings.SwapPanels(PanelId.FiveHour, PanelId.Cpu);
            settings.FiveHour.ShowResetTime = false;
            settings.Weekly.Enabled = false;
            settings.Cpu.Presentation = MetricPresentation.BarOnly;
            foreach (var panel in CompactBarRenderer.Layout(settings, 1, 48))
            {
                Assert.Equal(before[panel.Panel].Bounds.Size, panel.Bounds.Size);
                Assert.Equal(before[panel.Panel].Content.Size, panel.Content.Size);
            }
            Size size = CompactBarRenderer.CalculateSize(settings, 1, 48);
            using var bitmap = new Bitmap(size.Width, size.Height);
            using var g = Graphics.FromImage(bitmap);
            var hits = new List<CompactBarRenderer.HitRegion>();
            CompactBarRenderer.DrawWithRegions(g, size, settings, UsageSnapshot.Waiting, new(9, 75), 1, hits);
            var bar = hits.Single(h => h.Panel == PanelId.Cpu && h.Element == CompactBarRenderer.Element.Bar);
            settings.Cpu.Presentation = MetricPresentation.PercentAndBar;
            hits.Clear();
            CompactBarRenderer.DrawWithRegions(g, size, settings, UsageSnapshot.Waiting, new(9, 75), 1, hits);
            Assert.Equal(bar.Bounds, hits.Single(h => h.Panel == PanelId.Cpu && h.Element == CompactBarRenderer.Element.Bar).Bounds);
            hits.Clear();
            CompactBarRenderer.DrawWithRegions(g, size, settings, UsageSnapshot.Waiting, new(100, 75), 1, hits);
            if (style is not (CompactBarStyle.CircularGauges or CompactBarStyle.MinimalIcons or CompactBarStyle.CompactRows))
                Assert.True(hits.Single(h => h.Panel == PanelId.Cpu && h.Element == CompactBarRenderer.Element.Bar).Bounds.Width < bar.Bounds.Width);
        }
    }

    [Fact]
    public void Reset_countdowns_are_independent_panels_before_their_quotas()
    {
        var settings = new AppSettings();
        var panels = CompactBarRenderer.Layout(settings, 1, 48).ToDictionary(panel => panel.Panel);
        foreach ((PanelId resetId, PanelId quotaId) in new[]
            { (PanelId.FiveHourReset, PanelId.FiveHour), (PanelId.WeeklyReset, PanelId.Weekly) })
        {
            Assert.Equal(panels[resetId].Bounds, panels[resetId].Reset);
            Assert.Equal(4, panels[quotaId].Bounds.Left - panels[resetId].Bounds.Right);
            Assert.Equal(panels[quotaId].Bounds.Top, panels[resetId].Bounds.Top);
        }
        Assert.Equal(4, panels[PanelId.Cpu].Bounds.Left - panels[PanelId.FiveHour].Bounds.Right);
        Assert.Equal(2, panels[PanelId.Weekly].Bounds.Top - panels[PanelId.FiveHour].Bounds.Bottom);
        Assert.Equal(panels[PanelId.FiveHour].Bounds.Left, panels[PanelId.Weekly].Bounds.Left);
        Assert.Equal(panels[PanelId.Cpu].Bounds.Left, panels[PanelId.Memory].Bounds.Left);

        settings.SwapPanels(PanelId.FiveHour, PanelId.Cpu);
        panels = CompactBarRenderer.Layout(settings, 1, 48).ToDictionary(panel => panel.Panel);
        Assert.Equal(4, panels[PanelId.FiveHour].Bounds.Left - panels[PanelId.Cpu].Bounds.Right);
        Assert.Equal(4, panels[PanelId.Weekly].Bounds.Left - panels[PanelId.WeeklyReset].Bounds.Right);
        settings.SwapPanels(PanelId.FiveHourReset, PanelId.WeeklyReset);
        Assert.Equal(PanelId.WeeklyReset, settings.PanelOrder[0]);
        Assert.Equal(PanelId.FiveHourReset, settings.PanelOrder[3]);
    }

    [Theory]
    [InlineData(134, false, "02h")]
    [InlineData(14, false, "14m")]
    [InlineData(5894, true, "04d")]
    [InlineData(1439, true, "23h")]
    [InlineData(59, true, "59m")]
    [InlineData(0.5, false, "00m")]
    [InlineData(-1, true, "00m")]
    public void Countdown_uses_largest_whole_unit(double minutes, bool weekly, string expected)
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(expected, CompactBarRenderer.FormatResetTime(now.AddMinutes(minutes), weekly, now));
        Assert.Equal("--", CompactBarRenderer.FormatResetTime(null, weekly, now));
    }

    [Fact]
    public void Every_panel_tooltip_shows_both_local_reset_dates()
    {
        var reset = new DateTimeOffset(2026, 9, 24, 10, 35, 0, TimeSpan.FromHours(9));
        var snapshot = new UsageSnapshot(new(26, 300, reset), new(42, 10080, reset.AddDays(4)),
            null, null, DateTimeOffset.UtcNow);
        string local = reset.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        string weekly = reset.AddDays(4).ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal($"5시간 · {local} 초기화{Environment.NewLine}주간 · {weekly} 초기화",
            CompactBarRenderer.FormatResetTooltip(snapshot, AppLanguage.Korean));
        Assert.Equal($"5시간 · 초기화 시각 미상{Environment.NewLine}주간 · 초기화 시각 미상",
            CompactBarRenderer.FormatResetTooltip(UsageSnapshot.Waiting, AppLanguage.Korean));
    }

    [Fact]
    public void Reset_panel_labels_can_be_hidden_independently_and_shrink_their_panels()
    {
        var settings = new AppSettings();
        var now = DateTimeOffset.UtcNow;
        var snapshot = new UsageSnapshot(new(26, 300, now.AddMinutes(134)),
            new(42, 10080, now.AddDays(4).AddHours(2)), null, null, now);
        settings.FiveHour.ShowResetTimeLabel = true;
        settings.Weekly.ShowResetTimeLabel = true;
        var before = CompactBarRenderer.Layout(settings, 1, 48).ToDictionary(panel => panel.Panel);
        Assert.Equal(70, before[PanelId.FiveHourReset].Bounds.Width);
        Assert.Equal(70, before[PanelId.WeeklyReset].Bounds.Width);
        var darkHighDpi = CompactBarRenderer.Layout(settings, 1.5f, 72).ToDictionary(panel => panel.Panel);
        Assert.Equal(105, darkHighDpi[PanelId.FiveHourReset].Bounds.Width);
        Assert.Equal(105, darkHighDpi[PanelId.WeeklyReset].Bounds.Width);
        settings.ThemeVariant = ThemeVariant.Light;
        var lightHighDpi = CompactBarRenderer.Layout(settings, 1.5f, 72).ToDictionary(panel => panel.Panel);
        Assert.Equal(105, lightHighDpi[PanelId.FiveHourReset].Bounds.Width);
        Assert.Equal(105, lightHighDpi[PanelId.WeeklyReset].Bounds.Width);
        settings.ThemeVariant = ThemeVariant.Dark;
        Assert.Equal("5H ↻ 02h", CompactBarRenderer.FormatResetPanelText(PanelId.FiveHourReset, settings, snapshot, now));
        Assert.Equal("WK ↻ 04d", CompactBarRenderer.FormatResetPanelText(PanelId.WeeklyReset, settings, snapshot, now));
        settings.FiveHour.ShowResetTimeLabel = false;
        Assert.Equal("↻ 02h", CompactBarRenderer.FormatResetPanelText(PanelId.FiveHourReset, settings, snapshot, now));
        Assert.Equal("WK ↻ 04d", CompactBarRenderer.FormatResetPanelText(PanelId.WeeklyReset, settings, snapshot, now));
        settings.Weekly.ShowResetTimeLabel = false;
        Assert.Equal("↻ 04d", CompactBarRenderer.FormatResetPanelText(PanelId.WeeklyReset, settings, snapshot, now));
        var after = CompactBarRenderer.Layout(settings, 1, 48).ToDictionary(panel => panel.Panel);
        Assert.Equal(45, after[PanelId.FiveHourReset].Bounds.Width);
        Assert.Equal(45, after[PanelId.WeeklyReset].Bounds.Width);
        foreach (PanelId id in new[] { PanelId.FiveHour, PanelId.Weekly, PanelId.Cpu, PanelId.Memory })
            Assert.Equal(before[id].Bounds.Size, after[id].Bounds.Size);
        Assert.Equal(4, after[PanelId.FiveHour].Bounds.Left - after[PanelId.FiveHourReset].Bounds.Right);
        Assert.Equal(4, after[PanelId.Weekly].Bounds.Left - after[PanelId.WeeklyReset].Bounds.Right);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void Reset_panel_width_fits_complete_countdown_text_at_each_dpi(float scale)
    {
        var settings = new AppSettings();
        using var font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        using var bitmap = new Bitmap(300, 80);
        bitmap.SetResolution(96 * scale, 96 * scale);
        using var graphics = Graphics.FromImage(bitmap);
        foreach (bool showLabel in new[] { true, false })
        {
            settings.FiveHour.ShowResetTimeLabel = showLabel;
            settings.Weekly.ShowResetTimeLabel = showLabel;
            var panels = CompactBarRenderer.Layout(settings, scale, (int)(48 * scale)).ToDictionary(panel => panel.Panel);
            foreach ((PanelId id, string label) in new[]
                { (PanelId.FiveHourReset, "5H"), (PanelId.WeeklyReset, "WK") })
            {
                int labelColumnWidth = showLabel ? (int)Math.Round(24 * scale) : 0;
                if (showLabel)
                {
                    float labelWidth = graphics.MeasureString(label, font, PointF.Empty, format).Width;
                    Assert.True(labelWidth + 2 * scale <= labelColumnWidth,
                        $"{label} needs {labelWidth:0.0}px but its column has {labelColumnWidth}px at {scale:0.##}x DPI.");
                }
                float timeWidth = graphics.MeasureString("↻ 00m", font, PointF.Empty, format).Width;
                int available = panels[id].Bounds.Width - labelColumnWidth;
                Assert.True(timeWidth + 2 * scale <= available,
                    $"Countdown needs {timeWidth:0.0}px but {id} has {available}px at {scale:0.##}x DPI.");
            }
        }
    }

    [Fact]
    public void Linear_graphs_keep_the_same_left_edge_regardless_of_label_length()
    {
        foreach (var style in Enum.GetValues<CompactBarStyle>().Distinct()
            .Where(style => style is not (CompactBarStyle.CircularGauges or CompactBarStyle.MinimalIcons or CompactBarStyle.CompactRows)))
        foreach (float scale in new[] { 1f, 1.25f, 1.5f, 2f })
        {
            var settings = new AppSettings { CompactBarStyle = style };
            Size size = CompactBarRenderer.CalculateSize(settings, scale, (int)(48 * scale));
            using var bitmap = new Bitmap(size.Width, size.Height);
            bitmap.SetResolution(96 * scale, 96 * scale);
            using var graphics = Graphics.FromImage(bitmap);
            var hits = new List<CompactBarRenderer.HitRegion>();
            CompactBarRenderer.DrawWithRegions(graphics, size, settings, UsageSnapshot.Waiting, SystemUsageSnapshot.Empty, scale, hits);
            int BarX(PanelId id) => hits.Single(hit => hit.Panel == id && hit.Element == CompactBarRenderer.Element.Bar).Bounds.Left;
            int PanelX(PanelId id) => hits.Single(hit => hit.Panel == id && hit.Element == CompactBarRenderer.Element.Panel).Bounds.Left;
            Assert.Equal(BarX(PanelId.FiveHour), BarX(PanelId.Weekly));
            Assert.Equal(BarX(PanelId.Cpu), BarX(PanelId.Memory));
            Assert.True(BarX(PanelId.FiveHour) > PanelX(PanelId.FiveHour), $"{style} at {scale}x");
            Assert.True(BarX(PanelId.FiveHour) - PanelX(PanelId.FiveHour)
                < BarX(PanelId.Cpu) - PanelX(PanelId.Cpu), $"{style} at {scale}x");
        }
    }

    [Fact]
    public void Light_theme_text_has_antialiased_edges_on_the_layered_bitmap()
    {
        var settings = new AppSettings { CompactBarStyle = CompactBarStyle.DarkMinimal };
        SettingsForm.ApplyColorThemeDefaults(settings, CodexPalette.Codex, ThemeVariant.Light);
        settings.TransparentBackground = false;
        Size size = CompactBarRenderer.CalculateSize(settings, 1, 48);
        using var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            CompactBarRenderer.Draw(graphics, size, settings, UsageSnapshot.Waiting, SystemUsageSnapshot.Empty, 1);
        }

        Rectangle reset = CompactBarRenderer.Layout(settings, 1, 48)
            .Single(panel => panel.Panel == PanelId.FiveHourReset).Bounds;
        int blendedEdgePixels = 0;
        for (int y = reset.Top + 3; y < reset.Bottom - 3; y++)
        for (int x = reset.Left + 3; x < reset.Right - 3; x++)
        {
            Color pixel = bitmap.GetPixel(x, y);
            if (pixel.A == 255 && pixel.R is > 30 and < 230 &&
                Math.Abs(pixel.R - pixel.G) <= 2 && Math.Abs(pixel.G - pixel.B) <= 2)
                blendedEdgePixels++;
        }
        Assert.True(blendedEdgePixels > 10, "Light text edges were rendered as only dark and white pixels.");
    }

    [Fact]
    public void Compact_bar_context_menu_closes_after_a_click_outside_it()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new CompactBarForm { Opacity = 0 };
                form.Show();
                ContextMenuStrip menu = form.ContextMenuStrip!;
                menu.Show(form, new Point(1, 1));
                Application.DoEvents();
                Assert.True(menu.Visible);
                IntPtr hook = (IntPtr)typeof(CompactBarForm)
                    .GetField("_outsideClickHook", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(form)!;
                Assert.NotEqual(IntPtr.Zero, hook);
                form.DismissMenuForOutsideClick(new Point(menu.Bounds.Left + 1, menu.Bounds.Top + 1));
                Application.DoEvents();
                Assert.True(menu.Visible);
                form.DismissMenuForOutsideClick(new Point(menu.Bounds.Right + 20, menu.Bounds.Bottom + 20));
                Application.DoEvents();
                Assert.False(menu.Visible);
                form.Close();
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Compact Bar context menu did not finish.");
        Assert.Null(failure);
    }

    [Fact]
    public void Compact_bar_hover_shows_reset_dates_across_the_background()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new CompactBarForm { Opacity = 0 };
                var settings = new AppSettings { Language = AppLanguage.Korean };
                var reset = DateTimeOffset.Now.AddHours(2);
                var snapshot = new UsageSnapshot(new(26, 300, reset), new(42, 10080, reset.AddDays(4)),
                    null, null, DateTimeOffset.UtcNow);
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(CompactBarForm).GetField("_settings", fields)!.SetValue(form, settings);
                typeof(CompactBarForm).GetField("_snapshot", fields)!.SetValue(form, snapshot);
                var tooltip = (ToolTip)typeof(CompactBarForm).GetField("_quotaToolTip", fields)!.GetValue(form)!;
                string VisibleText() => (string)typeof(CompactBarForm)
                    .GetField("_visibleQuotaTooltip", fields)!.GetValue(form)!;
                int popupCount = 0;
                tooltip.Popup += (_, _) => popupCount++;
                form.Show();
                form.ClientSize = CompactBarRenderer.CalculateSize(settings, 1, 48);
                Application.DoEvents();
                MethodInfo move = typeof(Control).GetMethod("OnMouseMove", fields)!;
                var panels = CompactBarRenderer.Layout(settings, 1, 48).ToDictionary(panel => panel.Panel);
                void Hover(PanelId id)
                {
                    Rectangle bounds = panels[id].Content;
                    move.Invoke(form, [new MouseEventArgs(MouseButtons.None, 0,
                        bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2, 0)]);
                }
                foreach (PanelId id in Enum.GetValues<PanelId>())
                {
                    Hover(id);
                    Assert.Contains(reset.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), VisibleText());
                    Assert.Contains(reset.AddDays(4).ToLocalTime().ToString("yyyy-MM-dd HH:mm"), VisibleText());
                }
                Assert.Equal(1, popupCount);
                move.Invoke(form, [new MouseEventArgs(MouseButtons.None, 0, 0, 0, 0)]);
                Assert.Contains("5시간", VisibleText());
                Assert.Equal(1, popupCount);
                typeof(Control).GetMethod("OnMouseLeave", fields)!.Invoke(form, [EventArgs.Empty]);
                Assert.Equal("", VisibleText());
                Assert.Equal("", tooltip.GetToolTip(form));
                int previousPopups = popupCount;
                Hover(PanelId.FiveHourReset);
                Assert.True(popupCount > previousPopups, "The tooltip did not reopen after leaving the bar.");
                move.Invoke(form, [new MouseEventArgs(MouseButtons.None, 0, -1, 0, 0)]);
                Assert.Equal("", VisibleText());
                form.Close();
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Compact Bar hover did not finish.");
        Assert.Null(failure);
    }

    [Fact]
    public void Compact_bar_reset_tooltip_anchors_to_the_whole_bar_and_stays_on_screen()
    {
        Rectangle screen = new(0, 0, 1920, 1080);
        Size bar = new(500, 50), tooltip = new(200, 50);
        Assert.Equal(new Point(150, -58), CompactBarForm.CenteredQuotaTooltipLocation(
            bar, tooltip, new Point(200, 900), screen));
        Assert.Equal(new Point(-80, -58), CompactBarForm.CenteredQuotaTooltipLocation(
            bar, tooltip, new Point(1800, 900), screen));
        Assert.Equal(new Point(150, 54), CompactBarForm.CenteredQuotaTooltipLocation(
            bar, tooltip, new Point(200, 20), screen));
    }

    [Fact]
    public void Swap_and_preset_survive_save_without_aliasing()
    {
        using var directory = new TemporaryDirectory();
        var settings = new AppSettings { FollowSystemTheme = true };
        settings.SwapPanels(PanelId.FiveHour, PanelId.Cpu);
        settings.FiveHour.ShowResetTime = false;
        settings.FiveHour.ShowResetTimeLabel = false;
        settings.Weekly.ShowResetTimeLabel = true;
        settings.Weekly.ResetTimeColor = "#AABBCC";
        settings.Preset2 = CompactBarPreset.Capture(settings);
        settings.SwapPanels(PanelId.Cpu, PanelId.Weekly);
        SettingsStore.SaveToDirectory(settings, directory.Path);
        var loaded = SettingsStore.LoadFromDirectory(directory.Path).Settings;
        loaded.Preset2!.ApplyTo(loaded);
        Assert.Equal(new[] { PanelId.FiveHourReset, PanelId.Cpu, PanelId.FiveHour,
            PanelId.WeeklyReset, PanelId.Weekly, PanelId.Memory }, loaded.PanelOrder);
        Assert.True(loaded.FollowSystemTheme);
        Assert.False(loaded.FiveHour.ShowResetTime);
        Assert.False(loaded.FiveHour.ShowResetTimeLabel);
        Assert.True(loaded.Weekly.ShowResetTimeLabel);
        Assert.Equal("#AABBCC", loaded.Weekly.ResetTimeColor);
        Assert.NotSame(loaded.PanelOrder, loaded.Preset2.PanelOrder);
    }

    [Fact]
    public void Every_style_renders_countdowns_and_hit_regions_inside_its_size()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new UsageSnapshot(new(26, 300, now.AddMinutes(134)), new(42, 10080, now.AddDays(4)), null, null, now);
        foreach (var style in Enum.GetValues<CompactBarStyle>().Distinct())
        foreach (ThemeVariant theme in new[] { ThemeVariant.Dark, ThemeVariant.Light })
        foreach (float dpi in new[] { 1f, 1.5f, 2f })
        foreach (bool hideCpu in new[] { false, true })
        {
            var settings = new AppSettings { CompactBarStyle = style };
            SettingsForm.ApplyColorThemeDefaults(settings, CodexPalette.Codex, theme);
            settings.SwapPanels(PanelId.FiveHour, PanelId.Cpu);
            settings.Cpu.Enabled = !hideCpu;
            Size size = CompactBarRenderer.CalculateSize(settings, dpi, (int)(48 * dpi));
            using var bitmap = new Bitmap(size.Width, size.Height);
            bitmap.SetResolution(96 * dpi, 96 * dpi);
            using var graphics = Graphics.FromImage(bitmap);
            var hits = new List<CompactBarRenderer.HitRegion>();
            CompactBarRenderer.DrawWithRegions(graphics, size, settings, snapshot, new(36, 75), dpi, hits);
            Assert.Equal(2, hits.Count(h => h.Element == CompactBarRenderer.Element.ResetTime));
            Assert.All(hits, hit => Assert.True(new Rectangle(Point.Empty, size).Contains(hit.Bounds), $"{style}: {hit}"));
            var panels = hits.Where(h => h.Element == CompactBarRenderer.Element.Panel).ToArray();
            for (int a = 0; a < panels.Length; a++)
            for (int b = a + 1; b < panels.Length; b++)
                Assert.False(panels[a].Bounds.IntersectsWith(panels[b].Bounds));
        }
    }

    [Fact]
    public void Invalid_or_old_order_keeps_all_six_panels_once()
    {
        Assert.Equal(AppSettings.DefaultPanelOrder(), AppSettings.NormalizePanelOrder(null));
        Assert.Equal(AppSettings.DefaultPanelOrder(), AppSettings.NormalizePanelOrder(
            [PanelId.FiveHour, PanelId.Weekly, PanelId.Cpu, PanelId.Memory]));
        Assert.Equal(new[] { PanelId.FiveHourReset, PanelId.Cpu, PanelId.Weekly,
            PanelId.WeeklyReset, PanelId.FiveHour, PanelId.Memory },
            AppSettings.NormalizePanelOrder([PanelId.Cpu, PanelId.Cpu, (PanelId)99]));
    }
}
