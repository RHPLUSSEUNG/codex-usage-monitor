using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace CodexUsageMonitor.Services;

internal sealed class UpdateService : IDisposable
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/RHPLUSSEUNG/codex-usage-monitor/releases/latest";
    private const string ProductName = "CodexUsageMonitor";
    private readonly HttpClient _httpClient;

    public UpdateService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"CodexUsageMonitor/{CurrentVersion.ToString(3)}");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public static Version CurrentVersion =>
        typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 0, 0);

    public static string InstalledExecutablePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            ProductName,
            $"{ProductName}.exe");

    public static bool CanSelfUpdate =>
        string.Equals(
            Path.GetFullPath(Environment.ProcessPath ?? string.Empty),
            Path.GetFullPath(InstalledExecutablePath),
            StringComparison.OrdinalIgnoreCase);

    public async Task<UpdateRelease?> CheckAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _httpClient.GetAsync(LatestReleaseUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseRelease(document.RootElement, CurrentVersion);
    }

    internal static UpdateRelease? ParseRelease(JsonElement root, Version currentVersion)
    {
        string tagName = root.GetProperty("tag_name").GetString()
            ?? throw new InvalidDataException("The release tag is missing.");
        if (!TryParseVersion(tagName, out Version? releaseVersion))
            throw new InvalidDataException($"The release tag '{tagName}' is not a valid version.");
        if (releaseVersion is null)
            throw new InvalidDataException($"The release tag '{tagName}' is invalid.");
        if (releaseVersion <= currentVersion)
            return null;

        string packageName = $"{ProductName}-{tagName}-win-x64.zip";
        string checksumName = $"{packageName}.sha256";
        Uri? packageUrl = null;
        Uri? checksumUrl = null;

        foreach (JsonElement asset in root.GetProperty("assets").EnumerateArray())
        {
            string? name = asset.GetProperty("name").GetString();
            string? url = asset.GetProperty("browser_download_url").GetString();
            if (url is null)
                continue;
            if (string.Equals(name, packageName, StringComparison.OrdinalIgnoreCase))
                packageUrl = new Uri(url);
            else if (string.Equals(name, checksumName, StringComparison.OrdinalIgnoreCase))
                checksumUrl = new Uri(url);
        }

        if (packageUrl is null || checksumUrl is null)
            throw new InvalidDataException(
                $"Release assets '{packageName}' and '{checksumName}' are required.");

        string page = root.GetProperty("html_url").GetString()
            ?? $"https://github.com/RHPLUSSEUNG/codex-usage-monitor/releases/tag/{tagName}";
        return new UpdateRelease(releaseVersion, tagName, new Uri(page), packageUrl, checksumUrl);
    }

    public async Task<string> PrepareAsync(
        UpdateRelease release,
        CancellationToken cancellationToken)
    {
        if (!CanSelfUpdate)
            throw new InvalidOperationException(
                "Automatic updates are available only for the installed application.");

        string updateDirectory = Path.Combine(
            UpdateInstaller.DownloadsDirectory,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(updateDirectory);

        string packagePath = Path.Combine(updateDirectory, "package.zip");
        string checksumPath = Path.Combine(updateDirectory, "package.zip.sha256");
        await DownloadAsync(release.PackageUrl, packagePath, cancellationToken);
        await DownloadAsync(release.ChecksumUrl, checksumPath, cancellationToken);
        await VerifyChecksumAsync(packagePath, checksumPath, cancellationToken);

        string updaterPath = Path.Combine(updateDirectory, "CodexUsageMonitor.Updater.exe");
        File.Copy(Environment.ProcessPath!, updaterPath, overwrite: true);
        return updaterPath;
    }

    public static void LaunchInstaller(string updaterPath)
    {
        string updateDirectory = Path.GetDirectoryName(updaterPath)
            ?? throw new InvalidOperationException("The update directory is missing.");
        string packagePath = Path.Combine(updateDirectory, "package.zip");

        var startInfo = new ProcessStartInfo(updaterPath)
        {
            UseShellExecute = false,
            WorkingDirectory = updateDirectory
        };
        startInfo.ArgumentList.Add("--apply-update");
        startInfo.ArgumentList.Add("--parent-pid");
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
        startInfo.ArgumentList.Add("--package");
        startInfo.ArgumentList.Add(packagePath);
        startInfo.ArgumentList.Add("--target");
        startInfo.ArgumentList.Add(InstalledExecutablePath);
        startInfo.ArgumentList.Add("--transaction-id");
        startInfo.ArgumentList.Add(Guid.NewGuid().ToString("N"));
        _ = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The update installer could not be started.");
    }

    public void Dispose() => _httpClient.Dispose();

    private async Task DownloadAsync(
        Uri uri,
        string destination,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using FileStream target = File.Create(destination);
        await source.CopyToAsync(target, cancellationToken);
    }

    private static async Task VerifyChecksumAsync(
        string packagePath,
        string checksumPath,
        CancellationToken cancellationToken)
    {
        string checksumFile = await File.ReadAllTextAsync(checksumPath, cancellationToken);
        string expected = checksumFile
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;
        if (expected.Length != 64 || !expected.All(Uri.IsHexDigit))
            throw new InvalidDataException("The release checksum is invalid.");

        await using FileStream stream = File.OpenRead(packagePath);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        string actual = Convert.ToHexString(hash);
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The downloaded update failed SHA-256 verification.");
    }

    private static bool TryParseVersion(string tagName, out Version? version)
    {
        string normalized = tagName.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
            normalized = normalized[1..];
        int suffix = normalized.IndexOfAny(['-', '+']);
        if (suffix >= 0)
            normalized = normalized[..suffix];
        return Version.TryParse(normalized, out version);
    }
}
