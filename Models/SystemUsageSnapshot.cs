namespace CodexUsageMonitor.Models;

public sealed record SystemUsageSnapshot(double? CpuPercent, double? MemoryPercent)
{
    public static SystemUsageSnapshot Empty { get; } = new(null, null);
}
