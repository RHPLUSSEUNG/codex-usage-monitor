using System.Drawing;
using CodexUsageMonitor.Services;
using CodexUsageMonitor.UI;

namespace CodexUsageMonitor.Tests;

public sealed class WindowAndInstanceTests
{
    [Theory]
    [InlineData(25, 0.5f)]
    [InlineData(100, 1f)]
    [InlineData(150, 1.5f)]
    [InlineData(300, 2f)]
    public void Compact_bar_user_scale_is_clamped(int percent, float expected)
    {
        Assert.Equal(expected, CompactBarForm.UserScale(percent));
    }

    [Theory]
    [InlineData(50, 200, 40, 100, 20)]
    [InlineData(150, 200, 40, 300, 60)]
    [InlineData(250, 200, 40, 400, 80)]
    public void Compact_bar_size_scales_both_dimensions(
        int percent,
        int width,
        int height,
        int expectedWidth,
        int expectedHeight)
    {
        Size result = CompactBarForm.ScaleSize(new Size(width, height), percent);

        Assert.Equal(new Size(expectedWidth, expectedHeight), result);
    }

    [Fact]
    public void Target_screen_point_uses_window_center_instead_of_top_left()
    {
        var location = new Point(1800, 100);
        var size = new Size(400, 60);

        Point result = CompactBarForm.WindowCenter(location, size);

        Assert.Equal(new Point(2000, 130), result);
    }

    [Fact]
    public void Clamp_keeps_minimum_part_of_window_inside_screen()
    {
        var screenArea = new Rectangle(0, 0, 1920, 1080);
        var size = new Size(400, 60);

        Point result = CompactBarForm.ClampToScreenArea(
            new Point(-5000, 5000),
            size,
            screenArea);

        Assert.Equal(new Point(-368, 1048), result);
    }

    [Fact]
    public void Clamp_allows_bar_below_taskbar_boundary()
    {
        var screenArea = new Rectangle(0, 0, 1536, 864);
        var size = new Size(400, 80);

        Point result = CompactBarForm.ClampToScreenArea(
            new Point(200, 832),
            size,
            screenArea);

        Assert.Equal(new Point(200, 832), result);
    }

    [Fact]
    public void Edge_release_moves_bar_past_cursor_limit()
    {
        var screenArea = new Rectangle(0, 0, 1920, 1080);
        var size = new Size(420, 60);

        Point result = CompactBarForm.ExtendDragAtScreenEdge(
            new Point(100, 1020),
            size,
            new Point(300, 1079),
            screenArea);

        Assert.Equal(new Point(100, 1048), result);
    }

    [Fact]
    public void Clamp_preserves_position_already_inside_screen()
    {
        var position = new Point(100, 200);

        Point result = CompactBarForm.ClampToScreenArea(
            position,
            new Size(400, 60),
            new Rectangle(0, 0, 1920, 1080));

        Assert.Equal(position, result);
    }

    [Theory]
    [InlineData(-20, -20, 0d, 1d)]
    [InlineData(120, 120, 1d, 0d)]
    [InlineData(60, 60, 0.5d, 0.5d)]
    public void Color_square_drag_clamps_to_edges(
        int x,
        int y,
        double expectedSaturation,
        double expectedValue)
    {
        (double saturation, double value) = ColorWheelControl.SaturationValueFromPoint(
            new Point(x, y),
            new RectangleF(10, 10, 100, 100));

        Assert.Equal(expectedSaturation, saturation, 3);
        Assert.Equal(expectedValue, value, 3);
    }

    [Theory]
    [InlineData(100, 50, 0d)]
    [InlineData(50, 100, 90d)]
    [InlineData(0, 50, 180d)]
    [InlineData(50, 0, 270d)]
    public void Hue_drag_tracks_angle_at_any_distance(int x, int y, double expectedHue)
    {
        double hue = ColorWheelControl.HueFromPoint(new Point(x, y), new PointF(50, 50));

        Assert.Equal(expectedHue, hue, 3);
    }

    [Fact]
    public void Second_instance_notifies_primary_instance()
    {
        string id = Guid.NewGuid().ToString("N");
        using var primary = new SingleInstanceCoordinator(
            $@"Local\CodexUsageMonitor.Tests.{id}",
            $"CodexUsageMonitor.Tests.{id}");
        using var activated = new ManualResetEventSlim();
        primary.StartListening(activated.Set);
        using var secondary = new SingleInstanceCoordinator(
            $@"Local\CodexUsageMonitor.Tests.{id}",
            $"CodexUsageMonitor.Tests.{id}");

        Assert.True(primary.IsPrimary);
        Assert.False(secondary.IsPrimary);
        Assert.True(secondary.NotifyPrimary());
        Assert.True(activated.Wait(
            TimeSpan.FromSeconds(3),
            TestContext.Current.CancellationToken));
    }
}
