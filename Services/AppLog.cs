using System.Text;

namespace CodexUsageMonitor.Services;

internal static class AppLog
{
    private const long MaximumLogBytes = 1_048_576;
    private static readonly object Sync = new();

    public static string LogPath => Path.Combine(SettingsStore.SettingsDirectory, "CodexUsageMonitor.log");

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warning(string message, Exception? exception = null) =>
        Write("WARN", message, exception);

    public static void Error(string message, Exception exception) =>
        Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(SettingsStore.SettingsDirectory);
                RotateIfNeeded();
                var builder = new StringBuilder()
                    .Append(DateTimeOffset.Now.ToString("O"))
                    .Append(" [")
                    .Append(level)
                    .Append("] ")
                    .AppendLine(message);
                if (exception is not null)
                    builder.AppendLine(exception.ToString());
                File.AppendAllText(LogPath, builder.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Diagnostics must never make the monitor fail.
        }
    }

    private static void RotateIfNeeded()
    {
        if (!File.Exists(LogPath) || new FileInfo(LogPath).Length < MaximumLogBytes)
            return;
        string previous = LogPath + ".1";
        if (File.Exists(previous))
            File.Delete(previous);
        File.Move(LogPath, previous);
    }
}
