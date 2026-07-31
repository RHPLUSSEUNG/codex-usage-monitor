using System.Text.Json;

namespace CodexUsageMonitor.Services;

internal sealed class AppServerOutputParser
{
    public int ConsecutiveMalformedLines { get; private set; }

    public bool TryParse(
        string line,
        out JsonDocument? document,
        out JsonException? error)
    {
        document = null;
        error = null;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        try
        {
            document = JsonDocument.Parse(line);
            ConsecutiveMalformedLines = 0;
            return true;
        }
        catch (JsonException exception)
        {
            error = exception;
            ConsecutiveMalformedLines++;
            if (ConsecutiveMalformedLines >= 3)
            {
                throw new InvalidDataException(
                    "Codex app-server emitted three consecutive non-JSON lines.",
                    exception);
            }
            return false;
        }
    }
}
