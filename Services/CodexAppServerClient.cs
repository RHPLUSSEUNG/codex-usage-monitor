using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using CodexUsageMonitor.Models;

namespace CodexUsageMonitor.Services;

public sealed record AppServerDiagnosticInfo(
    bool IsRunning,
    string? LastError,
    int ConsecutiveFailures,
    DateTimeOffset? NextStartAttempt);

public sealed class CodexAppServerClient : IAsyncDisposable
{
    private readonly string _executable;
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly object _processLock = new();
    private Process? _process;
    private Task? _outputTask;
    private Task? _errorTask;
    private int _nextRequestId;
    private int _consecutiveFailures;
    private DateTimeOffset _nextStartAttempt = DateTimeOffset.MinValue;
    private string? _lastError;
    private bool _disposing;

    public CodexAppServerClient(string executable)
    {
        _executable = string.IsNullOrWhiteSpace(executable) ? "codex" : executable.Trim();
    }

    public AppServerDiagnosticInfo GetDiagnosticInfo()
    {
        bool isRunning = GetRunningProcess() is not null;
        lock (_processLock)
        {
            return new AppServerDiagnosticInfo(
                isRunning,
                _lastError,
                _consecutiveFailures,
                _nextStartAttempt == DateTimeOffset.MinValue ? null : _nextStartAttempt);
        }
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
        if (GetRunningProcess() is not null)
            return;

        await _startLock.WaitAsync(cancellationToken);
        Process? process = null;
        try
        {
            if (GetRunningProcess() is not null)
                return;

            await WaitForRestartBackoffAsync(cancellationToken);
            _lastError = null;
            ProcessStartInfo startInfo = CreateStartInfo(_executable);

            process = new Process { StartInfo = startInfo };
            if (!process.Start())
                throw new InvalidOperationException("Codex app-server를 시작할 수 없습니다.");

            lock (_processLock)
                _process = process;
            process.EnableRaisingEvents = true;
            process.Exited += ProcessExited;

            Task outputTask = Task.Run(() => ReadOutputLoopAsync(process));
            Task errorTask = Task.Run(() => ReadErrorLoopAsync(process));
            lock (_processLock)
            {
                _outputTask = outputTask;
                _errorTask = errorTask;
            }

            await RequestAsync("initialize", new
            {
                clientInfo = new
                {
                    name = "codex_usage_monitor",
                    title = "Codex Usage Monitor",
                    version = typeof(CodexAppServerClient).Assembly
                                  .GetName()
                                  .Version?
                                  .ToString(3) ?? "0.0.0"
                }
            }, cancellationToken);
            await NotifyAsync("initialized", new { }, cancellationToken);
            lock (_processLock)
            {
                _consecutiveFailures = 0;
                _nextStartAttempt = DateTimeOffset.MinValue;
            }
            AppLog.Info("Codex app-server initialized.");
        }
        catch (Exception exception)
        {
            if (process is null)
                RecordFailure(exception);
            else
                InvalidateProcess(process, exception);
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
            try
            {
                await WriteMessageAsync(
                    new { method, id, @params = parameters ?? new { } },
                    process,
                    cancellationToken);
            }
            catch (Exception exception) when (
                exception is IOException or InvalidOperationException or ObjectDisposedException)
            {
                InvalidateProcess(process, exception);
                throw;
            }

            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        }
        catch (TimeoutException exception)
        {
            InvalidateProcess(process, exception);
            throw;
        }
        catch (OperationCanceledException exception) when (!_disposing)
        {
            InvalidateProcess(process, exception);
            throw;
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
            var parser = new AppServerOutputParser();
            while (await process.StandardOutput.ReadLineAsync() is { } line)
            {
                JsonDocument? document;
                JsonException? parseError;
                try
                {
                    if (!parser.TryParse(line, out document, out parseError))
                    {
                        if (parseError is not null)
                        {
                            string preview = line.Length <= 500 ? line : line[..500] + "…";
                            AppLog.Warning(
                                $"Ignored non-JSON app-server output "
                                + $"({parser.ConsecutiveMalformedLines}/3): {preview}",
                                parseError);
                        }
                        continue;
                    }
                }
                catch (InvalidDataException exception)
                {
                    string preview = line.Length <= 500 ? line : line[..500] + "…";
                    AppLog.Warning(
                        $"Rejected repeated non-JSON app-server output: {preview}",
                        exception);
                    throw;
                }

                using (JsonDocument parsedDocument = document!)
                {
                    JsonElement root = parsedDocument.RootElement;
                    if (!root.TryGetProperty("id", out JsonElement idElement)
                        || !idElement.TryGetInt32(out int id))
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

            if (!_disposing)
                throw new EndOfStreamException("Codex app-server stdout closed unexpectedly.");
        }
        catch (Exception exception)
        {
            if (!_disposing)
                InvalidateProcess(process, exception);
        }
    }

    private async Task ReadErrorLoopAsync(Process process)
    {
        try
        {
            while (await process.StandardError.ReadLineAsync() is { } line)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                _lastError = line.Trim();
                AppLog.Warning($"Codex app-server stderr: {_lastError}");
            }
        }
        catch (Exception exception)
        {
            if (!_disposing)
                InvalidateProcess(process, exception);
        }
    }

    private void FailPending(Exception exception)
    {
        foreach (TaskCompletionSource<JsonElement> completion in _pending.Values)
            completion.TrySetException(exception);
    }

    internal static UsageSnapshot ParseSnapshot(JsonElement rateResult, JsonElement? usageResult)
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

    private Process? GetRunningProcess()
    {
        Process? process;
        lock (_processLock)
            process = _process;
        if (process is null)
            return null;

        try
        {
            if (!process.HasExited)
                return process;
        }
        catch (InvalidOperationException)
        {
        }

        InvalidateProcess(
            process,
            new InvalidOperationException("Codex app-server is no longer running."));
        return null;
    }

    private void ProcessExited(object? sender, EventArgs eventArgs)
    {
        if (sender is not Process process || _disposing)
            return;
        string detail = string.IsNullOrWhiteSpace(_lastError)
            ? "Codex app-server exited."
            : $"Codex app-server exited. {_lastError}";
        InvalidateProcess(process, new InvalidOperationException(detail));
    }

    private bool InvalidateProcess(Process process, Exception exception)
    {
        lock (_processLock)
        {
            if (!ReferenceEquals(_process, process))
                return false;
            _process = null;
            _outputTask = null;
            _errorTask = null;
        }

        process.Exited -= ProcessExited;
        FailPending(exception);
        if (!_disposing)
            RecordFailure(exception);

        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best-effort shutdown.
        }
        finally
        {
            process.Dispose();
        }
        return true;
    }

    private void RecordFailure(Exception exception)
    {
        TimeSpan delay;
        lock (_processLock)
        {
            _consecutiveFailures = Math.Min(_consecutiveFailures + 1, 6);
            delay = TimeSpan.FromSeconds(Math.Min(30, 1 << (_consecutiveFailures - 1)));
            _nextStartAttempt = DateTimeOffset.UtcNow + delay;
        }
        AppLog.Error($"Codex app-server connection failed; retrying after {delay.TotalSeconds:0}s.", exception);
    }

    private async Task WaitForRestartBackoffAsync(CancellationToken cancellationToken)
    {
        TimeSpan delay;
        lock (_processLock)
            delay = _nextStartAttempt - DateTimeOffset.UtcNow;
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, cancellationToken);
    }

    private void StopProcess()
    {
        Process? process;
        lock (_processLock)
            process = _process;
        if (process is not null)
            InvalidateProcess(process, new ObjectDisposedException(nameof(CodexAppServerClient)));
        else
            FailPending(new ObjectDisposedException(nameof(CodexAppServerClient)));
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

    public async ValueTask DisposeAsync()
    {
        _disposing = true;
        Task? outputTask;
        Task? errorTask;
        lock (_processLock)
        {
            outputTask = _outputTask;
            errorTask = _errorTask;
        }
        StopProcess();
        Task[] readers = new[] { outputTask, errorTask }
            .Where(task => task is not null)
            .Cast<Task>()
            .ToArray();
        await WaitForReaderShutdownAsync(readers, TimeSpan.FromSeconds(2))
            .ConfigureAwait(false);
        _startLock.Dispose();
        _writeLock.Dispose();
    }

    internal static async Task WaitForReaderShutdownAsync(
        IReadOnlyCollection<Task> readers,
        TimeSpan timeout)
    {
        if (readers.Count == 0)
            return;

        try
        {
            await Task.WhenAll(readers)
                .WaitAsync(timeout)
                .ConfigureAwait(false);
        }
        catch
        {
            // The process streams are already closed; reader shutdown is best effort.
        }
    }
}
