using CodexUsageMonitor.Models;
using CodexUsageMonitor.Services;

namespace CodexUsageMonitor.Tests;

public sealed class QuotaNotificationTests
{
    [Fact]
    public void Custom_threshold_is_persisted_and_suppresses_alerts_after_restart()
    {
        using var directory = new TemporaryDirectory();
        var tracker = new QuotaNotificationTracker();
        var settings = new AppSettings { QuotaNotificationPercent = 13 };
        var reset = DateTimeOffset.Now.AddHours(5);
        Assert.Empty(Evaluate(tracker, settings, 86, reset));
        Assert.Equal(13, SingleAlert(tracker, settings, 87, reset).Threshold);
        CodexUsageMonitor.Services.SettingsStore.SaveToDirectory(settings, directory.Path);
        settings = CodexUsageMonitor.Services.SettingsStore.LoadFromDirectory(directory.Path).Settings;
        tracker = new QuotaNotificationTracker();
        Assert.Equal(13, settings.QuotaNotificationPercent);
        Assert.Equal(13, settings.FiveHourNotificationPercent);
        Assert.Equal(13, settings.WeeklyNotificationPercent);
        Assert.Empty(Evaluate(tracker, settings, 99, reset));
        Assert.Empty(Evaluate(tracker, settings, 99, reset.AddSeconds(1)));
        settings.FiveHourNotificationPercent = 25;
        Assert.Empty(Evaluate(tracker, settings, 99, reset));
        Assert.Equal(25, SingleAlert(tracker, settings, 80, reset.AddHours(5)).Threshold);
    }

    [Fact]
    public void Five_hour_and_weekly_windows_use_independent_thresholds()
    {
        var tracker = new QuotaNotificationTracker();
        var settings = new AppSettings
        {
            FiveHourNotificationPercent = 13,
            WeeklyNotificationPercent = 37
        };
        DateTimeOffset now = DateTimeOffset.Now;
        var snapshot = new UsageSnapshot(
            new QuotaWindow(87, 300, now.AddHours(5)),
            new QuotaWindow(63, 10080, now.AddDays(7)),
            null, null, now);

        QuotaThresholdAlert[] alerts = tracker.Evaluate(snapshot, settings, now).ToArray();
        Assert.Contains(alerts, alert => alert.MetricKey == "FiveHour" && alert.Threshold == 13);
        Assert.Contains(alerts, alert => alert.MetricKey == "Weekly" && alert.Threshold == 37);
    }

    [Fact]
    public void Missing_reset_timestamp_does_not_rearm_an_existing_alert()
    {
        var tracker = new QuotaNotificationTracker();
        var settings = new AppSettings();
        var reset = DateTimeOffset.Now.AddHours(5);
        SingleAlert(tracker, settings, 90, reset);
        Assert.Empty(tracker.Evaluate(new(new(95, 300, null), null, null, null, DateTimeOffset.Now), settings, DateTimeOffset.Now));
        Assert.Empty(Evaluate(tracker, settings, 96, reset));
    }
    [Fact]
    public void Tracker_notifies_once_per_window_even_when_usage_keeps_falling()
    {
        var tracker = new QuotaNotificationTracker();
        var settings = new AppSettings();
        DateTimeOffset reset = DateTimeOffset.Now.AddHours(5);

        Assert.Equal(20, SingleAlert(tracker, settings, 81, reset).Threshold);
        Assert.Empty(Evaluate(tracker, settings, 82, reset));
        Assert.Empty(Evaluate(tracker, settings, 91, reset));
        Assert.Empty(Evaluate(tracker, settings, 96, reset));
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
