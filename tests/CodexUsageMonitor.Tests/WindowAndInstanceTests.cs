using System.Drawing;
using CodexUsageMonitor.Services;
using CodexUsageMonitor.UI;

namespace CodexUsageMonitor.Tests;

public sealed class WindowAndInstanceTests
{
    [Fact]
    public void Clamp_keeps_minimum_part_of_window_visible()
    {
        var workingArea = new Rectangle(0, 0, 1920, 1080);
        var size = new Size(400, 60);

        Point result = CompactBarForm.ClampToWorkingArea(
            new Point(-5000, 5000),
            size,
            workingArea);

        Assert.Equal(new Point(-368, 1048), result);
    }

    [Fact]
    public void Clamp_preserves_position_already_in_working_area()
    {
        var position = new Point(100, 200);

        Point result = CompactBarForm.ClampToWorkingArea(
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
