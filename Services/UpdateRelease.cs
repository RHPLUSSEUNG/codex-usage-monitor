namespace CodexUsageMonitor.Services;

internal sealed record UpdateRelease(
    Version Version,
    string TagName,
    Uri ReleasePage,
    Uri PackageUrl,
    Uri ChecksumUrl);
