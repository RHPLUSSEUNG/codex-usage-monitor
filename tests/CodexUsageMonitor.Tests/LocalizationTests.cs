using CodexUsageMonitor.Models;

namespace CodexUsageMonitor.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void Every_resource_key_has_all_four_translations()
    {
        IReadOnlyList<string> keys = Localization.ResourceKeys();

        Assert.NotEmpty(keys);
        foreach (string key in keys)
        {
            foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
                Assert.True(
                    Localization.HasDirectTranslation(language, key),
                    $"Missing {language} translation for '{key}'.");
        }
    }

    [Fact]
    public void Resource_manager_returns_selected_language()
    {
        Assert.Equal("Settings", Localization.Text(AppLanguage.English, "Settings"));
        Assert.Equal("설정", Localization.Text(AppLanguage.Korean, "Settings"));
        Assert.Equal("设置", Localization.Text(AppLanguage.ChineseSimplified, "Settings"));
        Assert.Equal("設定", Localization.Text(AppLanguage.Japanese, "Settings"));
    }
}
