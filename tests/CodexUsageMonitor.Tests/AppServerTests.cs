using System.Text.Json;
using CodexUsageMonitor.Services;

namespace CodexUsageMonitor.Tests;

public sealed class AppServerTests
{
    [Fact]
    public async Task Reader_shutdown_does_not_require_captured_ui_context()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await Task.Run(
                async () =>
                {
                    SynchronizationContext? previous = SynchronizationContext.Current;
                    var blockedContext = new NonPumpingSynchronizationContext();
                    var reader = new TaskCompletionSource(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    try
                    {
                        SynchronizationContext.SetSynchronizationContext(blockedContext);
                        Task wait = CodexAppServerClient.WaitForReaderShutdownAsync(
                            [reader.Task],
                            TimeSpan.FromSeconds(2));
                        reader.SetResult();

                        await wait.ConfigureAwait(false);
                        Assert.Equal(0, blockedContext.PostCount);
                    }
                    finally
                    {
                        SynchronizationContext.SetSynchronizationContext(previous);
                    }
                },
                cancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(3), cancellationToken);
    }

    [Fact]
    public void Output_parser_ignores_one_non_json_line_and_resets_after_json()
    {
        var parser = new AppServerOutputParser();

        Assert.False(parser.TryParse("app-server starting", out _, out JsonException? error));
        Assert.NotNull(error);
        Assert.Equal(1, parser.ConsecutiveMalformedLines);
        Assert.True(parser.TryParse("""{"id":1,"result":{}}""", out JsonDocument? document, out _));
        document?.Dispose();
        Assert.Equal(0, parser.ConsecutiveMalformedLines);
    }

    [Fact]
    public void Output_parser_restarts_after_three_consecutive_non_json_lines()
    {
        var parser = new AppServerOutputParser();

        Assert.False(parser.TryParse("first", out _, out _));
        Assert.False(parser.TryParse("second", out _, out _));
        Assert.Throws<InvalidDataException>(
            () => parser.TryParse("third", out _, out _));
    }

    [Fact]
    public void App_server_process_uses_utf8_for_all_redirected_streams()
    {
        var startInfo = CodexAppServerClient.CreateStartInfo(
            @"C:\도구\codex.exe");

        Assert.Equal(65001, startInfo.StandardInputEncoding?.CodePage);
        Assert.Equal(65001, startInfo.StandardOutputEncoding?.CodePage);
        Assert.Equal(65001, startInfo.StandardErrorEncoding?.CodePage);
    }

    [Fact]
    public void Rate_limit_parser_classifies_five_hour_and_weekly_windows_by_duration()
    {
        using JsonDocument document = JsonDocument.Parse(
            """
            {
              "rateLimits": {
                "primary": {
                  "usedPercent": 25,
                  "windowDurationMins": 300,
                  "resetsAt": 1893456000
                }
              },
              "rateLimitsByLimitId": {
                "weekly": {
                  "secondary": {
                    "usedPercent": 60,
                    "windowDurationMins": 10080,
                    "resetsAt": 1893456000
                  }
                }
              }
            }
            """);

        var snapshot = CodexAppServerClient.ParseSnapshot(document.RootElement, null);

        Assert.Equal(25, snapshot.FiveHour?.UsedPercent);
        Assert.Equal(300, snapshot.FiveHour?.WindowDurationMinutes);
        Assert.Equal(60, snapshot.Weekly?.UsedPercent);
        Assert.Equal(10080, snapshot.Weekly?.WindowDurationMinutes);
    }

    private sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        public int PostCount { get; private set; }

        public override void Post(SendOrPostCallback callback, object? state)
        {
            PostCount++;
        }
    }
}
