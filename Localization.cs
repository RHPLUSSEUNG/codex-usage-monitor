using System.Collections;
using System.Globalization;
using System.Resources;
using CodexUsageMonitor.Models;

namespace CodexUsageMonitor;

internal static class Localization
{
    private static readonly ResourceManager Resources = new(
        "CodexUsageMonitor.Resources.Strings",
        typeof(Localization).Assembly);

    public static AppLanguage CurrentLanguage { get; set; } = AppLanguage.Korean;

    public static string Text(string key) => Text(CurrentLanguage, key);

    public static string Text(AppLanguage language, string key) =>
        Resources.GetString(key, CultureFor(language)) ?? key;

    public static string Format(string key, params object[] args) =>
        Format(CurrentLanguage, key, args);

    public static string Format(AppLanguage language, string key, params object[] args) =>
        string.Format(CultureFor(language), Text(language, key), args);

    internal static IReadOnlyList<string> ResourceKeys()
    {
        ResourceSet resources = Resources.GetResourceSet(
                                    CultureInfo.InvariantCulture,
                                    createIfNotExists: true,
                                    tryParents: false)
                                ?? throw new MissingManifestResourceException(
                                    "The neutral localization resource is missing.");
        return resources
            .Cast<DictionaryEntry>()
            .Select(entry => (string)entry.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    internal static bool HasDirectTranslation(AppLanguage language, string key)
    {
        CultureInfo culture = CultureFor(language);
        if (language == AppLanguage.English)
            culture = CultureInfo.InvariantCulture;
        ResourceSet? resources = Resources.GetResourceSet(
            culture,
            createIfNotExists: true,
            tryParents: false);
        return resources?.GetString(key) is not null;
    }

    private static CultureInfo CultureFor(AppLanguage language) => language switch
    {
        AppLanguage.Korean => CultureInfo.GetCultureInfo("ko-KR"),
        AppLanguage.ChineseSimplified => CultureInfo.GetCultureInfo("zh-CN"),
        AppLanguage.Japanese => CultureInfo.GetCultureInfo("ja-JP"),
        _ => CultureInfo.GetCultureInfo("en-US")
    };
}
