using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using CodexUsageMonitor.Services;

namespace CodexUsageMonitor.Tests;

public sealed class UpdateTests
{
    [Fact]
    public void Release_parser_selects_expected_versioned_zip_and_checksum()
    {
        using JsonDocument document = JsonDocument.Parse(
            """
            {
              "tag_name": "v1.3.0",
              "html_url": "https://github.com/RHPLUSSEUNG/codex-usage-monitor/releases/tag/v1.3.0",
              "assets": [
                {
                  "name": "CodexUsageMonitor-v1.3.0-win-x64.zip",
                  "browser_download_url": "https://example.test/app.zip"
                },
                {
                  "name": "CodexUsageMonitor-v1.3.0-win-x64.zip.sha256",
                  "browser_download_url": "https://example.test/app.zip.sha256"
                }
              ]
            }
            """);

        UpdateRelease? release = UpdateService.ParseRelease(
            document.RootElement,
            new Version(1, 2, 0));

        Assert.NotNull(release);
        Assert.Equal(new Version(1, 3, 0), release.Version);
        Assert.Equal("https://example.test/app.zip", release.PackageUrl.ToString());
        Assert.Equal("https://example.test/app.zip.sha256", release.ChecksumUrl.ToString());
    }

    [Fact]
    public void Release_parser_ignores_same_or_older_version()
    {
        using JsonDocument document = JsonDocument.Parse(
            """{"tag_name":"v1.2.0","html_url":"https://example.test","assets":[]}""");

        Assert.Null(UpdateService.ParseRelease(document.RootElement, new Version(1, 2, 0)));
    }

    [Fact]
    public async Task Health_monitor_accepts_matching_signal_from_live_process()
    {
        using var directory = new TemporaryDirectory();
        string transactionId = Guid.NewGuid().ToString("N");
        string healthPath = System.IO.Path.Combine(directory.Path, "healthy.signal");
        using Process process = StartLongRunningProcess();
        Task writer = Task.Run(
            async () =>
            {
                await Task.Delay(150, TestContext.Current.CancellationToken);
                await File.WriteAllTextAsync(
                    healthPath,
                    transactionId,
                    TestContext.Current.CancellationToken);
            },
            TestContext.Current.CancellationToken);

        try
        {
            UpdateInstaller.WaitForHealthyStartup(
                process,
                healthPath,
                transactionId,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(100));
            await writer;
            Assert.False(process.HasExited);
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
    }

    [Fact]
    public void Health_monitor_rejects_process_that_exits_before_signal()
    {
        using Process process = Process.Start(
            new ProcessStartInfo("cmd.exe", "/d /c exit 7")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            })!;

        Assert.Throws<InvalidOperationException>(
            () => UpdateInstaller.WaitForHealthyStartup(
                process,
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".signal"),
                Guid.NewGuid().ToString("N"),
                TimeSpan.FromSeconds(3),
                TimeSpan.Zero));
    }

    [Fact]
    public void Installer_restores_previous_directory_when_new_app_never_becomes_healthy()
    {
        using var directory = new TemporaryDirectory();
        string targetDirectory = System.IO.Path.Combine(directory.Path, "installed");
        Directory.CreateDirectory(targetDirectory);
        string targetExecutable = System.IO.Path.Combine(
            targetDirectory,
            "CodexUsageMonitor.exe");
        File.WriteAllText(targetExecutable, "original installation");

        string payloadDirectory = System.IO.Path.Combine(directory.Path, "payload");
        Directory.CreateDirectory(payloadDirectory);
        File.Copy(
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "cmd.exe"),
            System.IO.Path.Combine(payloadDirectory, "CodexUsageMonitor.exe"));
        string packagePath = System.IO.Path.Combine(directory.Path, "package.zip");
        ZipFile.CreateFromDirectory(payloadDirectory, packagePath);

        Assert.ThrowsAny<Exception>(
            () => UpdateInstaller.Apply(
                packagePath,
                targetExecutable,
                Guid.NewGuid().ToString("N"),
                TimeSpan.FromSeconds(2),
                TimeSpan.Zero));
        Assert.Equal("original installation", File.ReadAllText(targetExecutable));
    }

    private static Process StartLongRunningProcess() =>
        Process.Start(
            new ProcessStartInfo("cmd.exe", "/d /c ping 127.0.0.1 -n 20 > nul")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            })!;
}
