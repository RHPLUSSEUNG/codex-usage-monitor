using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace CodexUsageMonitor.Services;

public enum CodexActivityState
{
    Unknown,
    Working,
    Completed,
    Interrupted
}

internal sealed record CodexActivityEvidence(
    CodexActivityState State,
    DateTimeOffset Timestamp);

internal sealed class CodexActivityMonitor : IDisposable
{
    private static readonly TimeSpan EvidenceLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan FullScanInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ReadRetryInterval = TimeSpan.FromSeconds(5);
    private const int FileAnchorLength = 64;
    private readonly string _sessionRoot;
    private readonly SemaphoreSlim _sampleLock = new(1, 1);
    private readonly ConcurrentDictionary<string, byte> _pending =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ObservedFile> _observed =
        new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? _watcher;
    private DateTimeOffset _nextFullScan = DateTimeOffset.MinValue;
    private bool _indexUncertain;
    private bool _disposed;

    public CodexActivityMonitor(string? sessionRoot = null)
    {
        string codexHome = Environment.GetEnvironmentVariable("CODEX_HOME")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".codex");
        _sessionRoot = sessionRoot ?? Path.Combine(codexHome, "sessions");
    }

    public CodexActivityState CurrentState { get; private set; } =
        CodexActivityState.Unknown;

    internal long BytesRead { get; private set; }
    internal long LargestLineBytes { get; private set; }

    public async Task<CodexActivityState> SampleAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!await _sampleLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            return CurrentState;

        try
        {
            CodexActivityState sampled = await Task.Run(
                    () => SampleCore(now, cancellationToken),
                    cancellationToken)
                .ConfigureAwait(false);
            CurrentState = sampled;
            return sampled;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CurrentState;
        }
        finally
        {
            _sampleLock.Release();
        }
    }

    internal CodexActivityState Sample(DateTimeOffset now)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CurrentState = SampleCore(now, CancellationToken.None);
        return CurrentState;
    }

    private CodexActivityState SampleCore(DateTimeOffset now, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(_sessionRoot))
            return CodexActivityState.Unknown;

        EnsureWatcher();
        if (now >= _nextFullScan)
        {
            _indexUncertain = !RefreshFileIndex(cancellationToken);
            _nextFullScan = now + FullScanInterval;
        }

        // Metadata checks recover from missed watcher events without reparsing
        // unchanged files. Unreadable paths are retried on a bounded cadence.
        foreach ((string path, ObservedFile observed) in _observed.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (observed.Uncertain && now < observed.NextRetry)
                continue;
            FilePresence presence = GetFilePresence(path);
            if (presence == FilePresence.Missing)
            {
                _observed.Remove(path);
                _pending.TryRemove(path, out _);
                continue;
            }
            if (presence == FilePresence.Unknown)
            {
                if (observed.Uncertain && now >= observed.NextRetry)
                    _pending[path] = 0;
                else if (!observed.Uncertain)
                    _observed[path] = observed with
                    {
                        Uncertain = true,
                        NextRetry = now + ReadRetryInterval
                    };
                continue;
            }
            if (observed.Uncertain)
            {
                if (now >= observed.NextRetry)
                    _pending[path] = 0;
                continue;
            }
            try
            {
                var info = new FileInfo(path);
                if (info.Length != observed.Length
                    || observed.Offset < info.Length
                    || info.LastWriteTimeUtc != observed.LastWriteTimeUtc
                    || info.CreationTimeUtc != observed.CreationTimeUtc)
                    _pending[path] = 0;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _observed[path] = observed with
                {
                    Uncertain = true,
                    NextRetry = now + ReadRetryInterval
                };
            }
        }

        foreach (string path in DrainPending())
        {
            cancellationToken.ThrowIfCancellationRequested();
            FilePresence presence = GetFilePresence(path);
            if (presence == FilePresence.Missing)
            {
                _observed.Remove(path);
                continue;
            }

            ObservedFile previous = _observed.TryGetValue(path, out ObservedFile? existing)
                ? existing
                : ObservedFile.Empty;
            IncrementalReadResult result = ReadIncrement(path, previous, cancellationToken);
            BytesRead += result.BytesRead;
            LargestLineBytes = Math.Max(LargestLineBytes, result.LargestLineBytes);
            if (!result.Success)
            {
                _observed[path] = previous with
                {
                    Uncertain = true,
                    NextRetry = now + ReadRetryInterval
                };
                continue;
            }

            _observed[path] = result.File! with
            {
                Uncertain = false,
                NextRetry = DateTimeOffset.MinValue
            };
        }

        CodexActivityEvidence[] recent = _observed.Values
            .Where(value => !value.Uncertain)
            .Select(value => value.Evidence)
            .Where(evidence => evidence is not null
                               && evidence.Timestamp <= now.AddMinutes(5)
                               && now - evidence.Timestamp <= EvidenceLifetime)
            .Cast<CodexActivityEvidence>()
            .ToArray();

        // A trustworthy active session wins over an unrelated unreadable file.
        if (recent.Any(evidence => evidence.State == CodexActivityState.Working))
            return CodexActivityState.Working;
        if (_indexUncertain || _observed.Values.Any(value => value.Uncertain))
            return CodexActivityState.Unknown;

        CodexActivityEvidence? latestTerminal = recent
            .Where(evidence => evidence.State is CodexActivityState.Completed
                or CodexActivityState.Interrupted)
            .OrderByDescending(evidence => evidence.Timestamp)
            .FirstOrDefault();
        return latestTerminal?.State ?? CodexActivityState.Unknown;
    }

    private void EnsureWatcher()
    {
        if (_watcher is not null || !Directory.Exists(_sessionRoot))
            return;

        try
        {
            var watcher = new FileSystemWatcher(_sessionRoot, "*.jsonl")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName
                               | NotifyFilters.Size
                               | NotifyFilters.LastWrite
            };
            watcher.Created += QueueFile;
            watcher.Changed += QueueFile;
            watcher.Deleted += QueueFile;
            watcher.Renamed += (_, args) =>
            {
                _pending[args.OldFullPath] = 0;
                _pending[args.FullPath] = 0;
            };
            watcher.Error += (_, _) => _nextFullScan = DateTimeOffset.MinValue;
            watcher.EnableRaisingEvents = true;
            _watcher = watcher;
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or ArgumentException)
        {
            _watcher = null;
        }
    }

    private void QueueFile(object sender, FileSystemEventArgs args) =>
        _pending[args.FullPath] = 0;

    private bool RefreshFileIndex(CancellationToken cancellationToken)
    {
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (string path in Directory.EnumerateFiles(
                         _sessionRoot,
                         "*.jsonl",
                         SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                present.Add(path);
                try
                {
                    var info = new FileInfo(path);
                    if (!_observed.TryGetValue(path, out ObservedFile? observed)
                        || observed.Length != info.Length
                        || observed.Offset < info.Length
                        || observed.LastWriteTimeUtc != info.LastWriteTimeUtc
                        || observed.CreationTimeUtc != info.CreationTimeUtc)
                        _pending[path] = 0;
                }
                catch (Exception exception) when (exception is IOException
                                                   or UnauthorizedAccessException)
                {
                    _pending[path] = 0;
                }
            }
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or ArgumentException)
        {
            return false;
        }

        foreach (string missing in _observed.Keys.Where(path => !present.Contains(path)).ToArray())
        {
            _observed.Remove(missing);
            _pending.TryRemove(missing, out _);
        }
        return true;
    }

    private string[] DrainPending()
    {
        string[] paths = _pending.Keys.ToArray();
        foreach (string path in paths)
            _pending.TryRemove(path, out _);
        return paths;
    }

    private static FilePresence GetFilePresence(string path)
    {
        try
        {
            _ = File.GetAttributes(path);
            return FilePresence.Present;
        }
        catch (FileNotFoundException) { return FilePresence.Missing; }
        catch (DirectoryNotFoundException) { return FilePresence.Missing; }
        catch (UnauthorizedAccessException) { return FilePresence.Unknown; }
        catch (IOException) { return FilePresence.Unknown; }
        catch (ArgumentException) { return FilePresence.Unknown; }
    }

    internal static FileReadResult ReadLatestEvidenceFromFile(string path)
    {
        IncrementalReadResult result = ReadIncrement(path, ObservedFile.Empty, CancellationToken.None);
        return new FileReadResult(result.Success, result.File?.Length ?? 0, result.File?.Evidence);
    }

    private static IncrementalReadResult ReadIncrement(
        string path,
        ObservedFile previous,
        CancellationToken cancellationToken)
    {
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 8192,
                FileOptions.SequentialScan);
            var info = new FileInfo(path);
            long length = stream.Length;
            DateTime lastWrite = info.LastWriteTimeUtc;
            DateTime creation = info.CreationTimeUtc;
            bool reset = length < previous.Offset
                         || creation != previous.CreationTimeUtc && previous.Offset > 0
                         || length == previous.Offset && lastWrite != previous.LastWriteTimeUtc
                         || previous.Offset > 0 && !AnchorMatches(stream, previous.Offset, previous.EndAnchor);
            long offset = reset ? 0 : Math.Min(previous.Offset, length);
            CodexActivityEvidence? latest = reset ? null : previous.Evidence;
            byte[] pendingLine = reset ? [] : previous.PendingLine;
            stream.Position = offset;

            var buffer = new byte[8192];
            using var lineBuffer = new MemoryStream(Math.Max(256, pendingLine.Length));
            if (pendingLine.Length > 0)
                lineBuffer.Write(pendingLine);
            long bytesRead = 0;
            long largestLineBytes = lineBuffer.Length;
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bytesRead += read;
                int lineStart = 0;
                for (int index = 0; index < read; index++)
                {
                    if (buffer[index] != (byte)'\n')
                        continue;

                    int segmentLength = index - lineStart;
                    if (lineBuffer.Length == 0)
                    {
                        largestLineBytes = Math.Max(largestLineBytes, segmentLength);
                        latest = Latest(latest,
                            ParseUtf8Line(buffer.AsSpan(lineStart, segmentLength)));
                    }
                    else
                    {
                        lineBuffer.Write(buffer, lineStart, segmentLength);
                        largestLineBytes = Math.Max(largestLineBytes, lineBuffer.Length);
                        latest = Latest(latest,
                            ParseUtf8Line(lineBuffer.GetBuffer().AsSpan(0, (int)lineBuffer.Length)));
                        lineBuffer.SetLength(0);
                    }
                    lineStart = index + 1;
                }

                if (lineStart < read)
                {
                    lineBuffer.Write(buffer, lineStart, read - lineStart);
                    largestLineBytes = Math.Max(largestLineBytes, lineBuffer.Length);
                }
            }
            long endOffset = stream.Position;
            byte[] tail = lineBuffer.ToArray();
            // A complete final JSON object is useful immediately, but the bytes
            // remain buffered until the JSONL newline arrives.
            latest = Latest(latest, ParseUtf8Line(tail));

            byte[] endAnchor = ReadEndAnchor(stream, endOffset);
            info.Refresh();
            var file = new ObservedFile(
                stream.Length,
                endOffset,
                info.LastWriteTimeUtc,
                info.CreationTimeUtc,
                tail,
                endAnchor,
                latest,
                false,
                DateTimeOffset.MinValue);
            return new IncrementalReadResult(true, file, bytesRead, largestLineBytes);
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or ArgumentException
                                           or OverflowException)
        {
            return new IncrementalReadResult(false, null, 0, 0);
        }
    }

    private static CodexActivityEvidence? ParseUtf8Line(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            return null;
        if (bytes[^1] == (byte)'\r')
            bytes = bytes[..^1];
        if (bytes.Length >= 3
            && bytes[0] == 0xEF
            && bytes[1] == 0xBB
            && bytes[2] == 0xBF)
            bytes = bytes[3..];
        if (bytes.IsEmpty)
            return null;
        try
        {
            var reader = new Utf8JsonReader(bytes, isFinalBlock: true, state: default);
            bool complete = false;
            bool eventMessage = false;
            DateTimeOffset? timestamp = null;
            CodexActivityState? state = null;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == 0)
                {
                    complete = true;
                    break;
                }
                if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != 1)
                    continue;

                if (reader.ValueTextEquals("type"))
                {
                    if (!reader.Read())
                        return null;
                    eventMessage = reader.TokenType == JsonTokenType.String
                                   && reader.ValueTextEquals("event_msg");
                }
                else if (reader.ValueTextEquals("timestamp"))
                {
                    if (!reader.Read())
                        return null;
                    if (reader.TokenType == JsonTokenType.String
                        && reader.TryGetDateTimeOffset(out DateTimeOffset parsed))
                        timestamp = parsed;
                }
                else if (reader.ValueTextEquals("payload"))
                {
                    if (!reader.Read())
                        return null;
                    state = ReadPayloadState(ref reader);
                }
            }
            return complete && eventMessage && timestamp is not null && state is not null
                ? new CodexActivityEvidence(state.Value, timestamp.Value)
                : null;
        }
        catch (JsonException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    private static CodexActivityState? ReadPayloadState(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            return null;
        int payloadDepth = reader.CurrentDepth;
        CodexActivityState? state = null;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == payloadDepth)
                return state;
            if (reader.TokenType != JsonTokenType.PropertyName
                || reader.CurrentDepth != payloadDepth + 1
                || !reader.ValueTextEquals("type"))
                continue;
            if (!reader.Read() || reader.TokenType != JsonTokenType.String)
                return null;
            state = reader.ValueTextEquals("task_started")
                ? CodexActivityState.Working
                : reader.ValueTextEquals("task_complete")
                    ? CodexActivityState.Completed
                    : reader.ValueTextEquals("turn_aborted")
                        ? CodexActivityState.Interrupted
                        : null;
        }
        return null;
    }

    private static bool AnchorMatches(FileStream stream, long offset, byte[] expected)
    {
        if (expected.Length == 0 || offset < expected.Length || stream.Length < offset)
            return expected.Length == 0;
        long original = stream.Position;
        try
        {
            stream.Position = offset - expected.Length;
            byte[] actual = new byte[expected.Length];
            int total = 0;
            while (total < actual.Length)
            {
                int read = stream.Read(actual, total, actual.Length - total);
                if (read == 0)
                    return false;
                total += read;
            }
            return actual.AsSpan().SequenceEqual(expected);
        }
        finally
        {
            stream.Position = original;
        }
    }

    private static byte[] ReadEndAnchor(FileStream stream, long offset)
    {
        int count = (int)Math.Min(FileAnchorLength, offset);
        if (count == 0)
            return [];
        long original = stream.Position;
        try
        {
            stream.Position = offset - count;
            byte[] anchor = new byte[count];
            int total = 0;
            while (total < count)
            {
                int read = stream.Read(anchor, total, count - total);
                if (read == 0)
                    return anchor.AsSpan(0, total).ToArray();
                total += read;
            }
            return anchor;
        }
        finally
        {
            stream.Position = original;
        }
    }

    private static CodexActivityEvidence? Latest(
        CodexActivityEvidence? current,
        CodexActivityEvidence? candidate) =>
        candidate is not null && (current is null || candidate.Timestamp >= current.Timestamp)
            ? candidate
            : current;

    // Only lifecycle kind and timestamp are retained. Prompt and response text
    // are never copied, logged, or transmitted.
    internal static CodexActivityEvidence? InferLatestEvidence(string jsonLines)
    {
        CodexActivityEvidence? latest = null;
        foreach (string line in jsonLines.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            latest = Latest(latest, InferEvidenceFromLine(line));
        }
        return latest;
    }

    private static CodexActivityEvidence? InferEvidenceFromLine(string line)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(line);
            return InferEvidence(document.RootElement);
        }
        catch (JsonException) { return null; }
        catch (InvalidOperationException) { return null; }
    }

    private static CodexActivityEvidence? InferEvidence(JsonElement root)
    {
        if (!root.TryGetProperty("type", out JsonElement kind)
            || kind.GetString() != "event_msg"
            || !root.TryGetProperty("timestamp", out JsonElement timestampElement)
            || timestampElement.ValueKind != JsonValueKind.String
            || !DateTimeOffset.TryParse(timestampElement.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out DateTimeOffset timestamp)
            || !root.TryGetProperty("payload", out JsonElement payload)
            || !payload.TryGetProperty("type", out JsonElement subtype))
            return null;

        CodexActivityState? state = subtype.GetString() switch
        {
            "task_started" => CodexActivityState.Working,
            "task_complete" => CodexActivityState.Completed,
            "turn_aborted" => CodexActivityState.Interrupted,
            _ => null
        };
        return state is null ? null : new CodexActivityEvidence(state.Value, timestamp);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }
        // A background sample may still be unwinding after application
        // cancellation. The process-lifetime semaphore is intentionally left
        // undisposed so its finally block can safely release it.
    }

    private sealed record ObservedFile(
        long Length,
        long Offset,
        DateTime LastWriteTimeUtc,
        DateTime CreationTimeUtc,
        byte[] PendingLine,
        byte[] EndAnchor,
        CodexActivityEvidence? Evidence,
        bool Uncertain,
        DateTimeOffset NextRetry)
    {
        internal static ObservedFile Empty { get; } = new(
            0, 0, DateTime.MinValue, DateTime.MinValue, [], [], null, false, DateTimeOffset.MinValue);
    }

    private sealed record IncrementalReadResult(
        bool Success,
        ObservedFile? File,
        long BytesRead,
        long LargestLineBytes);
    private enum FilePresence { Present, Missing, Unknown }
}

internal sealed record FileReadResult(
    bool Success,
    long Length,
    CodexActivityEvidence? Evidence);
