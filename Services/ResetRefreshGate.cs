using CodexUsageMonitor.Models;

namespace CodexUsageMonitor.Services;

internal sealed class ResetRefreshGate
{
    private static readonly TimeSpan MinimumRetryInterval = TimeSpan.FromMinutes(1);
    private long? _lastAttemptTick;

    public void RecordRefreshAttempt(long monotonicMilliseconds) =>
        _lastAttemptTick = monotonicMilliseconds;

    public bool ShouldRefresh(
        UsageSnapshot snapshot,
        DateTimeOffset now,
        long monotonicMilliseconds,
        bool refreshInProgress)
    {
        if (refreshInProgress || !string.IsNullOrWhiteSpace(snapshot.Error))
            return false;

        bool resetReached = (snapshot.FiveHour?.ResetsAt is { } fiveHour && fiveHour <= now)
                            || (snapshot.Weekly?.ResetsAt is { } weekly && weekly <= now);
        if (!resetReached)
            return false;

        if (_lastAttemptTick is { } last
            && monotonicMilliseconds >= last
            && monotonicMilliseconds - last < MinimumRetryInterval.TotalMilliseconds)
            return false;

        RecordRefreshAttempt(monotonicMilliseconds);
        return true;
    }
}
