using CodexUsageMonitor.Models;

namespace CodexUsageMonitor.Services;

internal sealed record QuotaThresholdAlert(string MetricKey, int Threshold, double RemainingPercent);

internal sealed class QuotaNotificationTracker
{
    private static readonly int[] Thresholds = [5, 10, 20];
    private readonly WindowState _fiveHour = new();
    private readonly WindowState _weekly = new();

    public IReadOnlyList<QuotaThresholdAlert> Evaluate(
        UsageSnapshot snapshot,
        AppSettings settings,
        DateTimeOffset now)
    {
        if (!settings.EnableQuotaNotifications || IsQuietTime(settings, now.TimeOfDay))
            return [];

        var alerts = new List<QuotaThresholdAlert>(2);
        EvaluateWindow("FiveHour", snapshot.FiveHour, _fiveHour, alerts);
        EvaluateWindow("Weekly", snapshot.Weekly, _weekly, alerts);
        return alerts;
    }

    internal static bool IsQuietTime(AppSettings settings, TimeSpan localTime)
    {
        if (!settings.QuietHoursEnabled)
            return false;
        int start = Math.Clamp(settings.QuietHoursStart, 0, 23);
        int end = Math.Clamp(settings.QuietHoursEnd, 0, 23);
        if (start == end)
            return false;
        int hour = localTime.Hours;
        return start < end
            ? hour >= start && hour < end
            : hour >= start || hour < end;
    }

    private static void EvaluateWindow(
        string metricKey,
        QuotaWindow? window,
        WindowState state,
        ICollection<QuotaThresholdAlert> alerts)
    {
        if (window is null)
            return;

        bool resetChanged = state.Initialized
                            && state.ResetsAt != window.ResetsAt
                            && (state.ResetsAt is not null || window.ResetsAt is not null);
        bool usageResetWithoutTimestamp = state.Initialized
                                          && state.ResetsAt is null
                                          && window.ResetsAt is null
                                          && window.RemainingPercent > state.LastRemainingPercent + 20;
        if (!state.Initialized || resetChanged || usageResetWithoutTimestamp)
        {
            state.Initialized = true;
            state.ResetsAt = window.ResetsAt;
            state.LastNotifiedThreshold = null;
        }

        state.LastRemainingPercent = window.RemainingPercent;
        int? threshold = Thresholds.FirstOrDefault(
            value => window.RemainingPercent <= value);
        if (threshold == 0)
            threshold = null;
        if (threshold is null)
            return;
        if (state.LastNotifiedThreshold is not null
            && threshold.Value >= state.LastNotifiedThreshold.Value)
        {
            return;
        }

        state.LastNotifiedThreshold = threshold;
        alerts.Add(new QuotaThresholdAlert(metricKey, threshold.Value, window.RemainingPercent));
    }

    private sealed class WindowState
    {
        public bool Initialized { get; set; }
        public DateTimeOffset? ResetsAt { get; set; }
        public double LastRemainingPercent { get; set; }
        public int? LastNotifiedThreshold { get; set; }
    }
}
