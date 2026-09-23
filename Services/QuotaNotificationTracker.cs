using CodexUsageMonitor.Models;

namespace CodexUsageMonitor.Services;

internal sealed record QuotaThresholdAlert(string MetricKey, int Threshold, double RemainingPercent);

internal sealed class QuotaNotificationTracker
{
    public IReadOnlyList<QuotaThresholdAlert> Evaluate(
        UsageSnapshot snapshot,
        AppSettings settings,
        DateTimeOffset now)
    {
        if (!settings.EnableQuotaNotifications || IsQuietTime(settings, now.TimeOfDay))
            return [];

        var alerts = new List<QuotaThresholdAlert>(2);
        EvaluateWindow("FiveHour", snapshot.FiveHour, settings.FiveHourAlert,
            settings.NotificationPercent(PanelId.FiveHour), now, alerts);
        EvaluateWindow("Weekly", snapshot.Weekly, settings.WeeklyAlert,
            settings.NotificationPercent(PanelId.Weekly), now, alerts);
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

    private static void EvaluateWindow(string metricKey, QuotaWindow? window,
        QuotaAlertState state, int threshold, DateTimeOffset now, ICollection<QuotaThresholdAlert> alerts)
    {
        if (window is null || window.ResetsAt <= now) return;
        // Ignore missing timestamps and small service-side timestamp corrections.
        bool newWindow = state.ResetsAt is { } old && window.ResetsAt is { } next
            && next > old.AddMinutes(2);
        bool recoveredWithoutTimestamp = state.ResetsAt is null && window.ResetsAt is null
            && window.RemainingPercent > state.LastRemainingPercent + 20;
        if (newWindow || recoveredWithoutTimestamp) state.Notified = false;
        if (window.ResetsAt is { } reset) state.ResetsAt = reset;
        state.LastRemainingPercent = window.RemainingPercent;
        threshold = Math.Clamp(threshold, 1, 99);
        if (state.Notified || window.RemainingPercent > threshold) return;
        state.Notified = true;
        alerts.Add(new(metricKey, threshold, window.RemainingPercent));
    }
}
