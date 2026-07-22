namespace CodexUsageMonitor.Models;

public sealed record QuotaWindow(double UsedPercent, int WindowDurationMinutes, DateTimeOffset? ResetsAt)
{
    public double RemainingPercent => Math.Clamp(100d - UsedPercent, 0d, 100d);
}

public sealed record UsageSnapshot(
    QuotaWindow? FiveHour,
    QuotaWindow? Weekly,
    long? TodayTokens,
    long? LifetimeTokens,
    DateTimeOffset UpdatedAt,
    string? Error = null)
{
    public static UsageSnapshot Waiting { get; } =
        new(null, null, null, null, DateTimeOffset.MinValue, "사용량을 불러오는 중");
}
