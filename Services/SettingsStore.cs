using System.Text.Json;
using System.Text.Json.Serialization;
using CodexUsageMonitor.Models;

namespace CodexUsageMonitor.Services;

public enum SettingsLoadStatus
{
    Loaded,
    CreatedDefaults,
    RecoveredFromBackup,
    DefaultsAfterCorruption
}

public sealed record SettingsLoadResult(
    AppSettings Settings,
    SettingsLoadStatus Status,
    string? Detail = null);

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CodexUsageMonitor");

    public static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public static string BackupPath => Path.Combine(SettingsDirectory, "settings.json.bak");

    public static AppSettings Load() => LoadWithResult().Settings;

    public static SettingsLoadResult LoadWithResult() =>
        LoadFromDirectory(SettingsDirectory);

    internal static SettingsLoadResult LoadFromDirectory(string directory)
    {
        string settingsPath = Path.Combine(directory, "settings.json");
        string backupPath = Path.Combine(directory, "settings.json.bak");

        if (!File.Exists(settingsPath))
        {
            bool backupExists = File.Exists(backupPath);
            if (TryRead(backupPath, out AppSettings? backupSettings, out Exception? backupError))
            {
                string? restoreError = TryRestoreBackup(backupPath, settingsPath);
                return new SettingsLoadResult(
                    backupSettings!,
                    SettingsLoadStatus.RecoveredFromBackup,
                    restoreError);
            }

            return new SettingsLoadResult(
                new AppSettings(),
                backupExists
                    ? SettingsLoadStatus.DefaultsAfterCorruption
                    : SettingsLoadStatus.CreatedDefaults,
                backupError?.Message);
        }

        if (TryRead(settingsPath, out AppSettings? settings, out Exception? settingsError))
            return new SettingsLoadResult(settings!, SettingsLoadStatus.Loaded);

        if (TryRead(backupPath, out AppSettings? recovered, out Exception? backupReadError))
        {
            string? restoreError = TryRestoreBackup(backupPath, settingsPath);
            string detail = string.Join(
                Environment.NewLine,
                new[] { settingsError?.Message, restoreError }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            return new SettingsLoadResult(
                recovered!,
                SettingsLoadStatus.RecoveredFromBackup,
                string.IsNullOrWhiteSpace(detail) ? null : detail);
        }

        string failureDetail = string.Join(
            Environment.NewLine,
            new[] { settingsError?.Message, backupReadError?.Message }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        return new SettingsLoadResult(
            new AppSettings(),
            SettingsLoadStatus.DefaultsAfterCorruption,
            string.IsNullOrWhiteSpace(failureDetail) ? null : failureDetail);
    }

    public static void Save(AppSettings settings) =>
        SaveToDirectory(settings, SettingsDirectory);

    internal static void SaveToDirectory(AppSettings settings, string directory)
    {
        Directory.CreateDirectory(directory);
        settings.SettingsVersion = AppSettings.CurrentSettingsVersion;

        string settingsPath = Path.Combine(directory, "settings.json");
        string backupPath = Path.Combine(directory, "settings.json.bak");
        string token = Guid.NewGuid().ToString("N");
        string temporaryPath = Path.Combine(directory, $"settings.{token}.tmp");
        string replacementBackupPath = Path.Combine(directory, $"settings.{token}.bak.tmp");

        try
        {
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions);
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(json);
                stream.Flush(flushToDisk: true);
            }

            if (!TryRead(temporaryPath, out _, out Exception? validationError))
                throw new InvalidDataException(
                    "The temporary settings file could not be validated.",
                    validationError);

            if (!File.Exists(settingsPath))
            {
                File.Move(temporaryPath, settingsPath);
                return;
            }

            if (TryRead(settingsPath, out _, out _))
            {
                File.Replace(
                    temporaryPath,
                    settingsPath,
                    replacementBackupPath,
                    ignoreMetadataErrors: true);
                File.Move(replacementBackupPath, backupPath, overwrite: true);
                return;
            }

            PreserveCorruptFile(settingsPath);
            File.Move(temporaryPath, settingsPath, overwrite: true);
        }
        finally
        {
            TryDelete(temporaryPath);
            TryDelete(replacementBackupPath);
        }
    }

    private static bool TryRead(
        string path,
        out AppSettings? settings,
        out Exception? error)
    {
        settings = null;
        error = null;
        if (!File.Exists(path))
            return false;

        try
        {
            settings = JsonSerializer.Deserialize<AppSettings>(
                           File.ReadAllText(path),
                           JsonOptions)
                       ?? throw new InvalidDataException("The settings file is empty.");
            Normalize(settings);
            return true;
        }
        catch (Exception exception)
        {
            error = exception;
            return false;
        }
    }

    private static void Normalize(AppSettings settings)
    {
        if (settings.SettingsVersion > AppSettings.CurrentSettingsVersion)
        {
            throw new NotSupportedException(
                $"Settings version {settings.SettingsVersion} is newer than supported version "
                + $"{AppSettings.CurrentSettingsVersion}.");
        }

        settings.SettingsVersion = AppSettings.CurrentSettingsVersion;
        NormalizeAppearance(settings);
        settings.CompactBarScalePercent = Math.Clamp(settings.CompactBarScalePercent, 50, 200);
        settings.FiveHour ??= new MetricSettings();
        settings.Weekly ??= new MetricSettings { FillColor = "#77A8FF" };
        settings.Cpu ??= new MetricSettings { FillColor = "#F2C66D" };
        settings.Memory ??= new MetricSettings { FillColor = "#D48BFF" };
        NormalizePreset(settings.Preset1);
        NormalizePreset(settings.Preset2);
        NormalizePreset(settings.Preset3);
    }

    private static void NormalizePreset(CompactBarPreset? preset)
    {
        if (preset is null)
            return;
        NormalizeAppearance(preset);
        preset.ScalePercent = Math.Clamp(preset.ScalePercent, 50, 200);
        preset.FiveHour ??= new MetricSettings();
        preset.Weekly ??= new MetricSettings { FillColor = "#77A8FF" };
        preset.Cpu ??= new MetricSettings { FillColor = "#F2C66D" };
        preset.Memory ??= new MetricSettings { FillColor = "#D48BFF" };
    }

    private static void NormalizeAppearance(AppSettings settings)
    {
        if (!Enum.IsDefined(settings.CodexPalette))
            settings.CodexPalette = CodexPalette.Codex;
        if (settings.ThemeVariant == ThemeVariant.CodexDark)
        {
            settings.CodexPalette = CodexPalette.Codex;
            settings.ThemeVariant = ThemeVariant.Dark;
        }
        else if (settings.ThemeVariant == ThemeVariant.CodexLight)
        {
            settings.CodexPalette = CodexPalette.Codex;
            settings.ThemeVariant = ThemeVariant.Light;
        }
        else if (settings.ThemeVariant is not ThemeVariant.Dark and not ThemeVariant.Light)
        {
            settings.ThemeVariant = ThemeVariant.Dark;
        }

        if (!CompactBarPaletteCatalog.Supports(settings.CodexPalette, settings.ThemeVariant))
            settings.ThemeVariant = CompactBarPaletteCatalog.DefaultVariant(settings.CodexPalette);
    }

    private static void NormalizeAppearance(CompactBarPreset preset)
    {
        if (!Enum.IsDefined(preset.CodexPalette))
            preset.CodexPalette = CodexPalette.Codex;
        if (preset.ThemeVariant == ThemeVariant.CodexDark)
        {
            preset.CodexPalette = CodexPalette.Codex;
            preset.ThemeVariant = ThemeVariant.Dark;
        }
        else if (preset.ThemeVariant == ThemeVariant.CodexLight)
        {
            preset.CodexPalette = CodexPalette.Codex;
            preset.ThemeVariant = ThemeVariant.Light;
        }
        else if (preset.ThemeVariant is not ThemeVariant.Dark and not ThemeVariant.Light)
        {
            preset.ThemeVariant = ThemeVariant.Dark;
        }

        if (!CompactBarPaletteCatalog.Supports(preset.CodexPalette, preset.ThemeVariant))
            preset.ThemeVariant = CompactBarPaletteCatalog.DefaultVariant(preset.CodexPalette);
    }

    private static string? TryRestoreBackup(string backupPath, string settingsPath)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            string temporaryPath = settingsPath + "." + Guid.NewGuid().ToString("N") + ".restore.tmp";
            try
            {
                File.Copy(backupPath, temporaryPath);
                if (!TryRead(temporaryPath, out _, out Exception? validationError))
                    throw new InvalidDataException("The settings backup is invalid.", validationError);
                File.Move(temporaryPath, settingsPath, overwrite: true);
            }
            finally
            {
                TryDelete(temporaryPath);
            }
            return null;
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
    }

    private static void PreserveCorruptFile(string path)
    {
        string preservedPath = path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss")
                               + "-" + Guid.NewGuid().ToString("N")[..8];
        File.Move(path, preservedPath);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Cleanup is best effort; the validated primary or backup remains authoritative.
        }
    }
}
