using System.Text.Json;
using CodexUsageMonitor.Models;
using CodexUsageMonitor.Services;

namespace CodexUsageMonitor.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public void Save_writes_versioned_primary_and_previous_valid_backup()
    {
        using var directory = new TemporaryDirectory();
        var settings = new AppSettings { Language = AppLanguage.Korean };

        SettingsStore.SaveToDirectory(settings, directory.Path);
        settings.Language = AppLanguage.Japanese;
        SettingsStore.SaveToDirectory(settings, directory.Path);

        using JsonDocument primary = JsonDocument.Parse(
            File.ReadAllText(System.IO.Path.Combine(directory.Path, "settings.json")));
        using JsonDocument backup = JsonDocument.Parse(
            File.ReadAllText(System.IO.Path.Combine(directory.Path, "settings.json.bak")));
        Assert.Equal(
            AppSettings.CurrentSettingsVersion,
            primary.RootElement.GetProperty("SettingsVersion").GetInt32());
        Assert.Equal("Japanese", primary.RootElement.GetProperty("Language").GetString());
        Assert.Equal("Korean", backup.RootElement.GetProperty("Language").GetString());
        Assert.Empty(Directory.EnumerateFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public void Load_recovers_valid_backup_and_repairs_primary()
    {
        using var directory = new TemporaryDirectory();
        var settings = new AppSettings { Language = AppLanguage.Korean };
        SettingsStore.SaveToDirectory(settings, directory.Path);
        settings.Language = AppLanguage.Japanese;
        SettingsStore.SaveToDirectory(settings, directory.Path);
        string primaryPath = System.IO.Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(primaryPath, "{ broken");

        SettingsLoadResult result = SettingsStore.LoadFromDirectory(directory.Path);

        Assert.Equal(SettingsLoadStatus.RecoveredFromBackup, result.Status);
        Assert.Equal(AppLanguage.Korean, result.Settings.Language);
        using JsonDocument repaired = JsonDocument.Parse(File.ReadAllText(primaryPath));
        Assert.Equal("Korean", repaired.RootElement.GetProperty("Language").GetString());
    }

    [Fact]
    public void Load_uses_defaults_without_overwriting_unrecoverable_primary()
    {
        using var directory = new TemporaryDirectory();
        string primaryPath = System.IO.Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(primaryPath, "{ broken");

        SettingsLoadResult result = SettingsStore.LoadFromDirectory(directory.Path);

        Assert.Equal(SettingsLoadStatus.DefaultsAfterCorruption, result.Status);
        Assert.Equal("{ broken", File.ReadAllText(primaryPath));
        Assert.Equal(AppSettings.CurrentSettingsVersion, result.Settings.SettingsVersion);
    }

    [Fact]
    public void Load_migrates_legacy_json_without_settings_version()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllText(
            System.IO.Path.Combine(directory.Path, "settings.json"),
            """{"Language":"Korean"}""");

        SettingsLoadResult result = SettingsStore.LoadFromDirectory(directory.Path);

        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        Assert.Equal(AppLanguage.Korean, result.Settings.Language);
        Assert.Equal(AppSettings.CurrentSettingsVersion, result.Settings.SettingsVersion);
        Assert.Equal(100, result.Settings.CompactBarScalePercent);
    }

    [Theory]
    [InlineData("Dark", ThemeVariant.Dark)]
    [InlineData("Light", ThemeVariant.Light)]
    [InlineData("CodexDark", ThemeVariant.Dark)]
    [InlineData("CodexLight", ThemeVariant.Light)]
    public void Load_supports_existing_and_legacy_codex_theme_names(string stored, ThemeVariant expected)
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllText(
            System.IO.Path.Combine(directory.Path, "settings.json"),
            $$"""{"ThemeVariant":"{{stored}}"}""");

        SettingsLoadResult result = SettingsStore.LoadFromDirectory(directory.Path);

        Assert.Equal(expected, result.Settings.ThemeVariant);
        Assert.Equal(CodexPalette.Codex, result.Settings.CodexPalette);
    }

    [Fact]
    public void Preset_captures_and_restores_palette_independently_from_brightness()
    {
        var source = new AppSettings
        {
            CodexPalette = CodexPalette.RosePine,
            ThemeVariant = ThemeVariant.Light
        };
        CompactBarPreset preset = CompactBarPreset.Capture(source);
        var destination = new AppSettings
        {
            CodexPalette = CodexPalette.Codex,
            ThemeVariant = ThemeVariant.Dark
        };

        preset.ApplyTo(destination);

        Assert.Equal(CodexPalette.RosePine, destination.CodexPalette);
        Assert.Equal(ThemeVariant.Light, destination.ThemeVariant);
    }

    [Theory]
    [InlineData(10, 50)]
    [InlineData(125, 125)]
    [InlineData(500, 200)]
    public void Load_clamps_compact_bar_scale_to_supported_range(int stored, int expected)
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllText(
            System.IO.Path.Combine(directory.Path, "settings.json"),
            $$"""{"CompactBarScalePercent":{{stored}}}""");

        SettingsLoadResult result = SettingsStore.LoadFromDirectory(directory.Path);

        Assert.Equal(expected, result.Settings.CompactBarScalePercent);
    }

    [Fact]
    public void Load_reports_corruption_when_only_backup_exists_and_is_invalid()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllText(
            System.IO.Path.Combine(directory.Path, "settings.json.bak"),
            "{ broken");

        SettingsLoadResult result = SettingsStore.LoadFromDirectory(directory.Path);

        Assert.Equal(SettingsLoadStatus.DefaultsAfterCorruption, result.Status);
    }
}
