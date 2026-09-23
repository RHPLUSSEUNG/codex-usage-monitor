using CodexUsageMonitor.Models;
using Microsoft.Win32;

namespace CodexUsageMonitor.UI;

internal static class AppearanceTheme
{
    public static ThemeVariant SystemVariant()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0
                ? ThemeVariant.Dark : ThemeVariant.Light;
        }
        catch { return ThemeVariant.Light; }
    }

    public static AppSettings Resolve(AppSettings settings)
        => Resolve(settings, SystemVariant());

    internal static AppSettings Resolve(AppSettings settings, ThemeVariant systemVariant)
    {
        if (!settings.FollowSystemTheme) return settings;
        ThemeVariant variant = systemVariant;
        if (!CompactBarPaletteCatalog.Supports(settings.CodexPalette, variant))
            variant = CompactBarPaletteCatalog.DefaultVariant(settings.CodexPalette);
        if (variant == settings.ThemeVariant) return settings;
        var copy = settings.Copy();
        copy.ThemeVariant = variant;
        if (SettingsForm.IsThemeDefaultBackground(settings.BackgroundColor,
            CompactBarTheme.DefaultBackground(settings.CompactBarStyle, settings.CodexPalette, settings.ThemeVariant)))
            copy.BackgroundColor = CompactBarTheme.DefaultBackground(copy.CompactBarStyle, copy.CodexPalette, variant);
        foreach (PanelId id in new[] { PanelId.FiveHour, PanelId.Weekly, PanelId.Cpu, PanelId.Memory })
        {
            MetricSettings metric = copy.Metric(id);
            if (SettingsForm.IsThemeDefaultBackground(metric.FillColor, CompactBarTheme.DefaultFill(copy.CodexPalette, settings.ThemeVariant)))
                metric.FillColor = CompactBarTheme.DefaultFill(copy.CodexPalette, variant);
            if (SettingsForm.IsThemeDefaultBackground(metric.TrackColor, CompactBarTheme.DefaultTrack(copy.CodexPalette, settings.ThemeVariant)))
                metric.TrackColor = CompactBarTheme.DefaultTrack(copy.CodexPalette, variant);
        }
        return copy;
    }
}
