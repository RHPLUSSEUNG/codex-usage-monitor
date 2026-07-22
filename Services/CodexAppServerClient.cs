using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using CodexUsageMonitor.Models;

namespace CodexUsageMonitor.Services;

public sealed class CodexAppServerClient : IAsyncDisposable
{
    private readonly string _executable;
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
    private Process? _process;
    private int _nextRequestId;
    private string? _lastError;

    public CodexAppServerClient(string executable)
    {
        _executable = string.IsNullOrWhiteSpace(executable) ? "codex" : executable.Trim();
    }

    public async Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken)
    {
        try
        {
            await EnsureStartedAsync(cancellationToken);
            JsonElement rateResult = await RequestAsync("account/rateLimits/read", null, cancellationToken);

            JsonElement? usageResult = null;
            try
            {
                usageResult = await RequestAsync("account/usage/read", null, cancellationToken);
            }
            catch
            {
                // Some auth modes provide rate limits but not token-activity summaries.
            }

            return ParseSnapshot(rateResult, usageResult);
        }
        catch (Exception exception)
        {
            string detail = string.IsNullOrWhiteSpace(_lastError) ? exception.Message : _lastError!;
            return new UsageSnapshot(null, null, null, null, DateTimeOffset.Now, detail);
        }
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (_process is { HasExited: false })
            return;

        await _startLock.WaitAsync(cancellationToken);
        try
        {
            if (_process is { HasExited: false })
                return;

            _lastError = null;
            ProcessStartInfo startInfo = CreateStartInfo(_executable);

            Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Codex app-server를 시작할 수 없습니다.");
            _process = process;

            _ = Task.Run(() => ReadOutputLoopAsync(process));
            _ = Task.Run(() => ReadErrorLoopAsync(process));

            await RequestAsync("initialize", new
            {
                clientInfo = new
                {
                    name = "codex_usage_monitor",
                    title = "Codex Usage Monitor",
                    version = "0.2.0"
                }
            }, cancellationToken);
            await NotifyAsync("initialized", new { }, cancellationToken);
        }
        catch
        {
            StopProcess();
            throw;
        }
        finally
        {
            _startLock.Release();
        }
    }

    private async Task<JsonElement> RequestAsync(
        string method,
        object? parameters,
        CancellationToken cancellationToken)
    {
        Process process = _process
            ?? throw new InvalidOperationException("Codex app-server가 실행되지 않았습니다.");

        int id = Interlocked.Increment(ref _nextRequestId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = completion;

        try
        {
            await WriteMessageAsync(new { method, id, @params = parameters ?? new { } }, process, cancellationToken);
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    private Task NotifyAsync(string method, object parameters, CancellationToken cancellationToken)
    {
        Process process = _process
            ?? throw new InvalidOperationException("Codex app-server가 실행되지 않았습니다.");
        return WriteMessageAsync(new { method, @params = parameters }, process, cancellationToken);
    }

    private async Task WriteMessageAsync(object message, Process process, CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(message);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await process.StandardInput.WriteLineAsync(json.AsMemory(), cancellationToken);
            await process.StandardInput.FlushAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadOutputLoopAsync(Process process)
    {
        try
        {
            while (await process.StandardOutput.ReadLineAsync() is { } line)
            {
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement root = document.RootElement;
                if (!root.TryGetProperty("id", out JsonElement idElement) || !idElement.TryGetInt32(out int id))
                    continue;
                if (!_pending.TryGetValue(id, out TaskCompletionSource<JsonElement>? completion))
                    continue;

                if (root.TryGetProperty("error", out JsonElement error))
                {
                    string message = error.TryGetProperty("message", out JsonElement text)
                        ? text.GetString() ?? "Codex app-server 오류"
                        : error.ToString();
                    completion.TrySetException(new InvalidOperationException(message));
                }
                else if (root.TryGetProperty("result", out JsonElement result))
                {
                    completion.TrySetResult(result.Clone());
                }
            }
        }
        catch (Exception exception)
        {
            FailPending(exception);
        }
        finally
        {
            if (process.HasExited)
                FailPending(new InvalidOperationException($"Codex app-server가 종료되었습니다. {_lastError}"));
        }
    }

    private async Task ReadErrorLoopAsync(Process process)
    {
        while (await process.StandardError.ReadLineAsync() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
                _lastError = line.Trim();
        }
    }

    private void FailPending(Exception exception)
    {
        foreach (TaskCompletionSource<JsonElement> completion in _pending.Values)
            completion.TrySetException(exception);
    }

    private static UsageSnapshot ParseSnapshot(JsonElement rateResult, JsonElement? usageResult)
    {
        var windows = new List<QuotaWindow>();

        if (rateResult.TryGetProperty("rateLimits", out JsonElement limits))
            AddWindows(limits, windows);

        // The legacy single-bucket view can omit windows that are exposed only
        // through the newer multi-bucket map, so collect both views.
        if (rateResult.TryGetProperty("rateLimitsByLimitId", out JsonElement limitMap))
        {
            foreach (JsonProperty entry in limitMap.EnumerateObject())
                AddWindows(entry.Value, windows);
        }

        List<QuotaWindow> distinct = windows
            .GroupBy(window => (window.WindowDurationMinutes, window.ResetsAt))
            .Select(group => group.First())
            .ToList();

        // Classify by the service-provided window duration. Previously, when
        // only one window was returned, it was always placed in FiveHour even
        // when that single window was actually the weekly quota.
        QuotaWindow? fiveHour = Closest(
            distinct.Where(item => item.WindowDurationMinutes is >= 60 and < 1_440),
            300);
        QuotaWindow? weekly = Closest(
            distinct.Where(item => item.WindowDurationMinutes >= 1_440),
            10_080);

        long? todayTokens = null;
        long? lifetimeTokens = null;
        if (usageResult is { } usage)
        {
            if (usage.TryGetProperty("summary", out JsonElement summary)
                && summary.TryGetProperty("lifetimeTokens", out JsonElement lifetime)
                && lifetime.TryGetInt64(out long lifetimeValue))
                lifetimeTokens = lifetimeValue;

            if (usage.TryGetProperty("dailyUsageBuckets", out JsonElement buckets)
                && buckets.ValueKind == JsonValueKind.Array)
            {
                string today = DateTime.Today.ToString("yyyy-MM-dd");
                foreach (JsonElement bucket in buckets.EnumerateArray())
                {
                    if (bucket.TryGetProperty("startDate", out JsonElement date)
                        && date.GetString() == today
                        && bucket.TryGetProperty("tokens", out JsonElement tokens)
                        && tokens.TryGetInt64(out long tokenValue))
                    {
                        todayTokens = tokenValue;
                        break;
                    }
                }
            }
        }

        return new UsageSnapshot(fiveHour, weekly, todayTokens, lifetimeTokens, DateTimeOffset.Now);
    }

    private static void AddWindows(JsonElement limits, ICollection<QuotaWindow> output)
    {
        AddWindow(limits, "primary", output);
        AddWindow(limits, "secondary", output);
    }

    private static void AddWindow(JsonElement parent, string name, ICollection<QuotaWindow> output)
    {
        if (!parent.TryGetProperty(name, out JsonElement window) || window.ValueKind != JsonValueKind.Object)
            return;
        if (!window.TryGetProperty("usedPercent", out JsonElement percent)
            || !percent.TryGetDouble(out double usedPercent))
            return;

        int duration = window.TryGetProperty("windowDurationMins", out JsonElement mins)
                       && mins.TryGetInt32(out int durationValue)
            ? durationValue
            : 0;

        DateTimeOffset? resetsAt = null;
        if (window.TryGetProperty("resetsAt", out JsonElement reset)
            && reset.TryGetInt64(out long unixSeconds))
            resetsAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime();

        output.Add(new QuotaWindow(usedPercent, duration, resetsAt));
    }

    private static QuotaWindow? Closest(IEnumerable<QuotaWindow> source, int targetMinutes) =>
        source.OrderBy(item => Math.Abs(item.WindowDurationMinutes - targetMinutes)).FirstOrDefault();

    private void StopProcess()
    {
        try
        {
            if (_process is { HasExited: false })
                _process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best-effort shutdown.
        }
        finally
        {
            _process?.Dispose();
            _process = null;
        }
    }

    private static ProcessStartInfo CreateStartInfo(string configuredExecutable)
    {
        string executable = ResolveExecutable(configuredExecutable);
        bool isCommandScript = executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
                               || executable.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);

        var startInfo = new ProcessStartInfo
        {
            FileName = isCommandScript
                ? Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe"
                : executable,
            Arguments = isCommandScript
                ? $"/d /s /c \"\"{executable}\" app-server --listen stdio://\""
                : "app-server --listen stdio://",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        return startInfo;
    }

    private static string ResolveExecutable(string configuredExecutable)
    {
        if (Path.IsPathFullyQualified(configuredExecutable) || configuredExecutable.Contains(Path.DirectorySeparatorChar))
            return configuredExecutable;

        string[] extensions = OperatingSystem.IsWindows()
            ? new[] { ".exe", ".cmd", ".bat", string.Empty }
            : new[] { string.Empty };

        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (string extension in extensions)
            {
                string candidate = Path.Combine(directory.Trim('"'), configuredExecutable + extension);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return configuredExecutable;
    }

    public ValueTask DisposeAsync()
    {
        StopProcess();
        _startLock.Dispose();
        _writeLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
