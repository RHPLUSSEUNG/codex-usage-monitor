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
