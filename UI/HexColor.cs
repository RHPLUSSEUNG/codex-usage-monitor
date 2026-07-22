using System.Globalization;

namespace CodexUsageMonitor.UI;

internal static class HexColor
{
    public static bool TryParse(string? text, out Color color)
    {
        color = Color.Empty;
        string value = text?.Trim() ?? string.Empty;
        if (!value.StartsWith('#') || value.Length is not (7 or 9))
            return false;
        if (!uint.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hex))
            return false;

        color = value.Length == 7
            ? Color.FromArgb(255, (int)(hex >> 16) & 0xFF, (int)(hex >> 8) & 0xFF, (int)hex & 0xFF)
            : Color.FromArgb((int)(hex >> 24) & 0xFF, (int)(hex >> 16) & 0xFF, (int)(hex >> 8) & 0xFF, (int)hex & 0xFF);
        return true;
    }

    public static Color ParseOrDefault(string? text, Color fallback) =>
        TryParse(text, out Color color) ? color : fallback;

    public static string Normalize(string text) => text.Trim().ToUpperInvariant();

    public static string Format(Color color, bool includeAlpha) => includeAlpha
        ? $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}"
        : $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
