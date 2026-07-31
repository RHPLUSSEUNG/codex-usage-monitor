using System.Drawing;
using CodexUsageMonitor.Services;
using CodexUsageMonitor.UI;

namespace CodexUsageMonitor.Tests;

public sealed class WindowAndInstanceTests
{
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
    public void Clamp_preserves_position_already_inside_screen()
    {
        var position = new Point(100, 200);

        Point result = CompactBarForm.ClampToScreenArea(
            position,
            new Size(400, 60),
            new Rectangle(0, 0, 1920, 1080));

        Assert.Equal(position, result);
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
