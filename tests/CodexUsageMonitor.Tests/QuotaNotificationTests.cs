using CodexUsageMonitor.Models;
using CodexUsageMonitor.Services;

namespace CodexUsageMonitor.Tests;

public sealed class QuotaNotificationTests
{
    [Fact]
    public void Tracker_notifies_each_threshold_once_and_resets_with_new_window()
    {
        var tracker = new QuotaNotificationTracker();
        var settings = new AppSettings();
        DateTimeOffset reset = DateTimeOffset.Now.AddHours(5);

        Assert.Equal(20, SingleAlert(tracker, settings, 81, reset).Threshold);
        Assert.Empty(Evaluate(tracker, settings, 82, reset));
        Assert.Equal(10, SingleAlert(tracker, settings, 91, reset).Threshold);
        Assert.Equal(5, SingleAlert(tracker, settings, 96, reset).Threshold);
        Assert.Empty(Evaluate(tracker, settings, 97, reset));
        Assert.Equal(20, SingleAlert(tracker, settings, 81, reset.AddHours(5)).Threshold);
    }

    [Fact]
    public void Tracker_defers_alert_until_quiet_hours_end()
    {
        var tracker = new QuotaNotificationTracker();
        var settings = new AppSettings
        {
            QuietHoursEnabled = true,
            QuietHoursStart = 22,
            QuietHoursEnd = 8
        };
        DateTimeOffset reset = DateTimeOffset.Now.AddHours(5);

        Assert.Empty(tracker.Evaluate(
            Snapshot(85, reset),
            settings,
            new DateTimeOffset(2026, 7, 31, 23, 0, 0, TimeSpan.Zero)));
        QuotaThresholdAlert alert = Assert.Single(tracker.Evaluate(
            Snapshot(85, reset),
            settings,
            new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.Zero)));
        Assert.Equal(20, alert.Threshold);
    }

    private static QuotaThresholdAlert SingleAlert(
        QuotaNotificationTracker tracker,
        AppSettings settings,
        double usedPercent,
        DateTimeOffset reset) =>
        Assert.Single(Evaluate(tracker, settings, usedPercent, reset));

    private static IReadOnlyList<QuotaThresholdAlert> Evaluate(
        QuotaNotificationTracker tracker,
        AppSettings settings,
        double usedPercent,
        DateTimeOffset reset) =>
        tracker.Evaluate(Snapshot(usedPercent, reset), settings, DateTimeOffset.Now);

    private static UsageSnapshot Snapshot(double usedPercent, DateTimeOffset reset) =>
        new(
            new QuotaWindow(usedPercent, 300, reset),
            null,
            null,
            null,
            DateTimeOffset.Now);
}
