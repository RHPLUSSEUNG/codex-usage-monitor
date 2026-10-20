using System.Text.Json;
using System.Text;
using System.Drawing;
using CodexUsageMonitor.Models;
using CodexUsageMonitor.Services;
using CodexUsageMonitor.UI;

namespace CodexUsageMonitor.Tests;

public sealed class ActivityAndResetTests
{
    [Fact]
    public void Activity_uses_event_timestamp_instead_of_file_write_time()
    {
        using var directory = new TemporaryDirectory();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string path = directory.Write("old/session.jsonl", Event(now.AddMinutes(-1), "task_started"));
        File.SetLastWriteTimeUtc(path, now.AddHours(-8).UtcDateTime);

        using var monitor = new CodexActivityMonitor(directory.Path);
        Assert.Equal(CodexActivityState.Working, monitor.Sample(now));
    }

    [Fact]
    public void Activity_handles_concurrent_sessions_and_terminal_events()
    {
        using var directory = new TemporaryDirectory();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        directory.Write("one.jsonl", Event(now.AddMinutes(-5), "task_complete"));
        directory.Write("two.jsonl", Event(now.AddMinutes(-2), "task_started"));

        using var monitor = new CodexActivityMonitor(directory.Path);
        Assert.Equal(CodexActivityState.Working, monitor.Sample(now));

        directory.Write("two.jsonl", Event(now.AddMinutes(-2), "task_started") + "\n"
            + Event(now.AddMinutes(-1), "turn_aborted"));
        Assert.Equal(CodexActivityState.Interrupted, monitor.Sample(now));
    }

    [Fact]
    public void Activity_detects_resumed_old_session_and_ignores_truncated_tail()
    {
        using var directory = new TemporaryDirectory();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        directory.Write("2024/01/old-session.jsonl",
            Event(now.AddDays(-20), "task_complete") + "\n"
            + Event(now.AddMinutes(-3), "task_started") + "\n{\"timestamp\":");

        using var monitor = new CodexActivityMonitor(directory.Path);
        Assert.Equal(CodexActivityState.Working, monitor.Sample(now));
    }

    [Fact]
    public void Activity_keeps_silent_work_but_expires_stale_evidence()
    {
        using var recent = new TemporaryDirectory();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        recent.Write("session.jsonl", Event(now.AddHours(-12), "task_started"));
        using (var monitor = new CodexActivityMonitor(recent.Path))
            Assert.Equal(CodexActivityState.Working, monitor.Sample(now));

        using var stale = new TemporaryDirectory();
        stale.Write("session.jsonl", Event(now.AddHours(-25), "task_started"));
        using var staleMonitor = new CodexActivityMonitor(stale.Path);
        Assert.Equal(CodexActivityState.Unknown, staleMonitor.Sample(now));
    }

    [Fact]
    public void Activity_read_failure_is_reported_as_uncertain()
    {
        FileReadResult result = CodexActivityMonitor.ReadLatestEvidenceFromFile(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jsonl"));
        Assert.False(result.Success);
        Assert.Null(result.Evidence);
    }

    [Fact]
    public void Activity_keeps_unreadable_session_uncertain_until_retry_recovers()
    {
        using var directory = new TemporaryDirectory();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        directory.Write("complete.jsonl", Event(now.AddMinutes(-2), "task_complete"));
        string lockedPath = directory.Write("working.jsonl", Event(now.AddMinutes(-1), "task_started"));
        using var monitor = new CodexActivityMonitor(directory.Path);

        using (FileStream locked = File.Open(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Equal(CodexActivityState.Unknown, monitor.Sample(now));
            Assert.Equal(CodexActivityState.Unknown, monitor.Sample(now.AddSeconds(6)));
        }

        Assert.Equal(CodexActivityState.Working, monitor.Sample(now.AddSeconds(12)));
    }

    [Fact]
    public void Activity_removes_uncertainty_when_unreadable_file_is_deleted()
    {
        using var directory = new TemporaryDirectory();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        directory.Write("complete.jsonl", Event(now.AddMinutes(-2), "task_complete"));
        string lockedPath = directory.Write("missing.jsonl", Event(now.AddMinutes(-1), "task_started"));
        using var monitor = new CodexActivityMonitor(directory.Path);

        using (FileStream locked = File.Open(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.Equal(CodexActivityState.Unknown, monitor.Sample(now));
        File.Delete(lockedPath);

        Assert.Equal(CodexActivityState.Completed, monitor.Sample(now.AddSeconds(6)));
    }

    [Fact]
    public void Trustworthy_working_session_wins_over_unreadable_session()
    {
        using var directory = new TemporaryDirectory();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        directory.Write("working.jsonl", Event(now.AddMinutes(-1), "task_started"));
        string lockedPath = directory.Write("locked.jsonl", Event(now.AddMinutes(-2), "task_complete"));
        using var monitor = new CodexActivityMonitor(directory.Path);

        using FileStream locked = File.Open(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(CodexActivityState.Working, monitor.Sample(now));
    }

    [Fact]
    public void Activity_incrementally_processes_appends_and_split_json_lines()
    {
        using var directory = new TemporaryDirectory();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string path = directory.Write("session.jsonl", Event(now.AddMinutes(-3), "task_started") + "\n");
        using var monitor = new CodexActivityMonitor(directory.Path);
        Assert.Equal(CodexActivityState.Working, monitor.Sample(now));

        File.AppendAllText(path, Event(now.AddMinutes(-2), "task_complete") + "\n");
        Assert.Equal(CodexActivityState.Completed, monitor.Sample(now.AddSeconds(1)));

        string interrupted = Event(now.AddMinutes(-1), "turn_aborted");
        int split = interrupted.Length / 2;
        File.AppendAllText(path, interrupted[..split]);
        Assert.Equal(CodexActivityState.Completed, monitor.Sample(now.AddSeconds(2)));
        File.AppendAllText(path, interrupted[split..] + "\n");
        Assert.Equal(CodexActivityState.Interrupted, monitor.Sample(now.AddSeconds(3)));
    }

    [Fact]
    public void Activity_resets_incremental_cache_for_truncation_replacement_and_deletion()
    {
        using var directory = new TemporaryDirectory();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string path = directory.Write("session.jsonl",
            Event(now.AddMinutes(-3), "task_started") + "\n" + new string(' ', 512));
        using var monitor = new CodexActivityMonitor(directory.Path);
        Assert.Equal(CodexActivityState.Working, monitor.Sample(now));

        File.WriteAllText(path, Event(now.AddMinutes(-2), "task_complete") + "\n");
        Assert.Equal(CodexActivityState.Completed, monitor.Sample(now.AddSeconds(1)));

        string replacement = path + ".replacement";
        File.WriteAllText(replacement, Event(now.AddMinutes(-1), "turn_aborted") + "\n");
        File.Move(replacement, path, overwrite: true);
        Assert.Equal(CodexActivityState.Interrupted, monitor.Sample(now.AddSeconds(2)));

        File.Delete(path);
        Assert.Equal(CodexActivityState.Unknown, monitor.Sample(now.AddSeconds(3)));
    }

    [Fact]
    public void Small_append_to_large_activity_log_does_not_reread_existing_bytes()
    {
        using var directory = new TemporaryDirectory();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string filler = string.Concat(Enumerable.Repeat("{\"type\":\"other\"}\n", 100_000));
        string path = directory.Write("large.jsonl", filler + Event(now.AddMinutes(-2), "task_started") + "\n");
        using var monitor = new CodexActivityMonitor(directory.Path);
        Assert.Equal(CodexActivityState.Working, monitor.Sample(now));
        long initialBytes = monitor.BytesRead;

        string appended = Event(now.AddMinutes(-1), "task_complete") + "\n";
        File.AppendAllText(path, appended);
        Assert.Equal(CodexActivityState.Completed, monitor.Sample(now.AddSeconds(1)));

        Assert.Equal(Encoding.UTF8.GetByteCount(appended), monitor.BytesRead - initialBytes);
    }

    [Fact]
    public void Large_initial_activity_log_buffers_only_one_json_line()
    {
        using var directory = new TemporaryDirectory();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string fillerLine = "{\"type\":\"other\"}\n";
        string path = directory.Write("large.jsonl",
            string.Concat(Enumerable.Repeat(fillerLine, 100_000))
            + Event(now.AddMinutes(-1), "task_started") + "\n");
        long fileLength = new FileInfo(path).Length;

        using var monitor = new CodexActivityMonitor(directory.Path);
        Assert.Equal(CodexActivityState.Working, monitor.Sample(now));

        Assert.Equal(fileLength, monitor.BytesRead);
        Assert.InRange(monitor.LargestLineBytes, 1, 1024);
    }

    [Fact]
    public void Activity_panel_position_and_dot_position_round_trip()
    {
        using var directory = new TemporaryDirectory();
        var settings = new AppSettings
        {
            ActivityPanelPosition = 1,
            ActivityDotPositionPercent = 82,
            ShowActivityPanel = true,
            ActivityWorkingColor = "#FF123456",
            ActivityCompletedColor = "#FF234567",
            ActivityInterruptedColor = "#FF345678",
            ActivityUnknownColor = "#FF456789"
        };
        SettingsStore.SaveToDirectory(settings, directory.Path);

        AppSettings loaded = SettingsStore.LoadFromDirectory(directory.Path).Settings;
        Assert.Equal(1, loaded.ActivityPanelPosition);
        Assert.Equal(82, loaded.ActivityDotPositionPercent);
        Assert.True(loaded.ShowActivityPanel);
        Assert.Equal("#FF123456", loaded.ActivityWorkingColor);
        Assert.Equal("#FF234567", loaded.ActivityCompletedColor);
        Assert.Equal("#FF345678", loaded.ActivityInterruptedColor);
        Assert.Equal("#FF456789", loaded.ActivityUnknownColor);
        Assert.Equal(AppSettings.DefaultPanelOrder(), loaded.PanelOrder);

        List<CompactBarRenderer.PanelLayout> layout = CompactBarRenderer.Layout(loaded, 1, 48);
        CompactBarRenderer.PanelLayout activity = Assert.Single(layout, p => p.Panel == PanelId.CodexActivity);
        Assert.True(activity.Bounds.Height >= 46);
        Assert.True(activity.Bounds.Width < layout.First(p => p.Panel == PanelId.FiveHour).Bounds.Width);

        loaded.FiveHour.Enabled = false;
        loaded.FiveHour.ShowResetTime = false;
        Assert.Contains(CompactBarRenderer.Layout(loaded, 1, 48),
            panel => panel.Panel == PanelId.CodexActivity);
    }

    [Fact]
    public void Existing_settings_without_activity_fields_receive_defaults()
    {
        using var directory = new TemporaryDirectory();
        directory.Write("settings.json", "{\"SettingsVersion\":3,\"PanelOrder\":[\"FiveHourReset\",\"FiveHour\",\"Cpu\",\"WeeklyReset\",\"Weekly\",\"Memory\"]}");

        AppSettings loaded = SettingsStore.LoadFromDirectory(directory.Path).Settings;

        Assert.True(loaded.ShowActivityPanel);
        Assert.Equal(3, loaded.ActivityPanelPosition);
        Assert.Equal(50, loaded.ActivityDotPositionPercent);
    }

    [Fact]
    public void Compact_rows_normalizes_activity_position_to_an_edge_when_saved_and_restored()
    {
        using var directory = new TemporaryDirectory();
        var settings = new AppSettings
        {
            CompactBarStyle = CompactBarStyle.CompactRows,
            ActivityPanelPosition = 1
        };
        settings.Preset2 = CompactBarPreset.Capture(settings);

        SettingsStore.SaveToDirectory(settings, directory.Path);
        AppSettings loaded = SettingsStore.LoadFromDirectory(directory.Path).Settings;

        Assert.Equal(3, loaded.ActivityPanelPosition);
        Assert.Equal(3, loaded.Preset2!.ActivityPanelPosition);
        loaded.Preset2.ApplyTo(loaded);
        Assert.Equal(3, loaded.ActivityPanelPosition);

        loaded.CompactBarStyle = CompactBarStyle.Light;
        loaded.ActivityPanelPosition = 2;
        loaded.NormalizeActivityPanel();
        Assert.Equal(2, loaded.ActivityPanelPosition);
    }

    [Fact]
    public void Working_activity_dot_has_distinct_bright_and_dim_frames()
    {
        var settings = new AppSettings();
        settings.FiveHour.Enabled = false;
        settings.FiveHour.ShowResetTime = false;
        settings.Weekly.Enabled = false;
        settings.Weekly.ShowResetTime = false;
        settings.Cpu.Enabled = false;
        settings.Memory.Enabled = false;
        Size size = CompactBarRenderer.CalculateSize(settings, 1, 48);

        Color Render(bool bright)
        {
            using var bitmap = new Bitmap(size.Width, size.Height);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.Transparent);
            var hits = new List<CompactBarRenderer.HitRegion>();
            CompactBarRenderer.DrawWithRegions(graphics, size, settings, UsageSnapshot.Waiting,
                SystemUsageSnapshot.Empty, 1, hits, activityState: CodexActivityState.Working,
                activityPulseBright: bright);
            Rectangle dot = hits.Single(hit => hit.Panel == PanelId.CodexActivity
                                               && hit.Element == CompactBarRenderer.Element.ActivityDot).Bounds;
            return bitmap.GetPixel(dot.X + dot.Width / 2, dot.Y + dot.Height / 2);
        }

        Assert.True(Render(true).A > Render(false).A);
    }

    [Fact]
    public void Higher_activity_dot_position_moves_toward_the_top()
    {
        var settings = new AppSettings();
        settings.FiveHour.Enabled = false;
        settings.FiveHour.ShowResetTime = false;
        settings.Weekly.Enabled = false;
        settings.Weekly.ShowResetTime = false;
        settings.Cpu.Enabled = false;
        settings.Memory.Enabled = false;
        Size size = CompactBarRenderer.CalculateSize(settings, 1, 48);

        int DotTop(int position)
        {
            settings.ActivityDotPositionPercent = position;
            using var bitmap = new Bitmap(size.Width, size.Height);
            using var graphics = Graphics.FromImage(bitmap);
            var hits = new List<CompactBarRenderer.HitRegion>();
            CompactBarRenderer.DrawWithRegions(graphics, size, settings, UsageSnapshot.Waiting,
                SystemUsageSnapshot.Empty, 1, hits);
            return hits.Single(hit => hit.Panel == PanelId.CodexActivity
                                      && hit.Element == CompactBarRenderer.Element.ActivityDot).Bounds.Top;
        }

        Assert.True(DotTop(100) < DotTop(0));
    }

    [Theory]
    [InlineData(CodexActivityState.Working, "#FF123456")]
    [InlineData(CodexActivityState.Completed, "#FF234567")]
    [InlineData(CodexActivityState.Interrupted, "#FF345678")]
    [InlineData(CodexActivityState.Unknown, "#FF456789")]
    public void Activity_dot_uses_custom_color_for_each_state(CodexActivityState state, string expected)
    {
        var settings = new AppSettings
        {
            ActivityWorkingColor = "#FF123456",
            ActivityCompletedColor = "#FF234567",
            ActivityInterruptedColor = "#FF345678",
            ActivityUnknownColor = "#FF456789"
        };

        Assert.Equal(HexColor.ParseOrDefault(expected, Color.Empty),
            CompactBarRenderer.ActivityColor(settings, state));
    }

    [Fact]
    public void Completed_and_unknown_use_the_swapped_default_dot_emphasis()
    {
        var settings = new AppSettings();

        Color completed = CompactBarRenderer.ActivityColor(settings, CodexActivityState.Completed);
        Color unknown = CompactBarRenderer.ActivityColor(settings, CodexActivityState.Unknown);

        Assert.Equal(115, completed.A);
        Assert.Equal(190, unknown.A);
    }

    [Theory]
    [InlineData(CodexActivityState.Working, "작업 중")]
    [InlineData(CodexActivityState.Completed, "완료·대기")]
    [InlineData(CodexActivityState.Interrupted, "중단")]
    [InlineData(CodexActivityState.Unknown, "확인 불가")]
    public void Reset_tooltip_includes_current_activity_state(CodexActivityState state, string expected)
    {
        string tooltip = CompactBarRenderer.FormatResetTooltip(
            UsageSnapshot.Waiting, AppLanguage.Korean, state);

        Assert.EndsWith($"Codex · {expected}", tooltip);
    }

    [Fact]
    public void Reset_refresh_gate_deduplicates_and_retries_after_sixty_seconds()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var snapshot = new UsageSnapshot(new QuotaWindow(50, 300, now.AddSeconds(-1)),
            new QuotaWindow(50, 10080, now.AddHours(2)), null, null, now);
        var gate = new ResetRefreshGate();

        Assert.True(gate.ShouldRefresh(snapshot, now, 10_000, false));
        Assert.False(gate.ShouldRefresh(snapshot, now, 69_999, false));
        Assert.True(gate.ShouldRefresh(snapshot, now, 70_000, false));
        Assert.False(gate.ShouldRefresh(snapshot, now, 140_000, true));
    }

    [Fact]
    public void Reset_refresh_gate_rechecks_wall_time_after_sleep_or_clock_change()
    {
        DateTimeOffset reset = DateTimeOffset.UtcNow.AddMinutes(5);
        var snapshot = new UsageSnapshot(new QuotaWindow(50, 300, reset), null, null, null, reset.AddMinutes(-10));
        var gate = new ResetRefreshGate();

        Assert.False(gate.ShouldRefresh(snapshot, reset.AddSeconds(-1), 10_000, false));
        Assert.True(gate.ShouldRefresh(snapshot, reset.AddMinutes(30), 20_000, false));
        Assert.False(gate.ShouldRefresh(snapshot, reset.AddMinutes(-1), 30_000, false));
        Assert.True(gate.ShouldRefresh(snapshot, reset.AddMinutes(30), 1_000, false));
    }

    [Fact]
    public void Manual_refresh_throttles_an_immediate_reset_refresh()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var snapshot = new UsageSnapshot(new QuotaWindow(50, 300, now.AddMinutes(-1)),
            null, null, null, now);
        var gate = new ResetRefreshGate();

        gate.RecordRefreshAttempt(50_000);

        Assert.False(gate.ShouldRefresh(snapshot, now, 50_001, false));
        Assert.True(gate.ShouldRefresh(snapshot, now, 110_000, false));
    }

    private static string Event(DateTimeOffset timestamp, string type) => JsonSerializer.Serialize(new
    {
        timestamp,
        type = "event_msg",
        payload = new { type }
    });

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cum-tests-" + Guid.NewGuid());
        public TemporaryDirectory() => Directory.CreateDirectory(Path);
        public string Write(string relativePath, string content)
        {
            string path = System.IO.Path.Combine(Path, relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }
        public void Dispose() => Directory.Delete(Path, true);
    }
}
