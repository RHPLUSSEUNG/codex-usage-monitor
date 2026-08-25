using CodexUsageMonitor.Models;
using CodexUsageMonitor.UI;

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

    [Theory]
    [InlineData(AppLanguage.Korean, null, AppLanguage.Korean)]
    [InlineData(AppLanguage.Korean, AppLanguage.English, AppLanguage.English)]
    [InlineData(AppLanguage.English, AppLanguage.ChineseSimplified, AppLanguage.ChineseSimplified)]
    [InlineData(AppLanguage.ChineseSimplified, AppLanguage.Japanese, AppLanguage.Japanese)]
    public void Management_actions_use_open_settings_language_when_available(
        AppLanguage savedLanguage,
        AppLanguage? openSettingsLanguage,
        AppLanguage expected)
    {
        Assert.Equal(
            expected,
            UsageMonitorContext.ResolveManagementLanguage(savedLanguage, openSettingsLanguage));
    }

    [Fact]
    public void Theme_default_background_detection_is_case_insensitive_but_preserves_custom_colors()
    {
        Assert.True(SettingsForm.IsThemeDefaultBackground("#FF16181C", "#ff16181c"));
        Assert.False(SettingsForm.IsThemeDefaultBackground("#0016181C", "#FF16181C"));
    }

    [Theory]
    [InlineData(ThemeVariant.Dark, "#FF111111", 252, 252, 252)]
    [InlineData(ThemeVariant.Light, "#FFFFFFFF", 13, 13, 13)]
    public void Codex_palette_uses_installed_app_colors(
        ThemeVariant theme,
        string expectedBackground,
        int red,
        int green,
        int blue)
    {
        Assert.Equal(
            expectedBackground,
            CompactBarTheme.DefaultBackground(CompactBarStyle.LabelBoxes, CodexPalette.Codex, theme));
        Assert.Equal(Color.FromArgb(red, green, blue), CompactBarTheme.Foreground(CodexPalette.Codex, theme));
    }

    [Fact]
    public void Palette_catalog_contains_all_28_codex_families_and_supported_variants()
    {
        Assert.Equal(28, CompactBarPaletteCatalog.Values.Length);
        Assert.Equal([ThemeVariant.Dark], CompactBarPaletteCatalog.Variants(CodexPalette.Ayu));
        Assert.Equal([ThemeVariant.Light], CompactBarPaletteCatalog.Variants(CodexPalette.Proof));
        Assert.Equal(
            [ThemeVariant.Dark, ThemeVariant.Light],
            CompactBarPaletteCatalog.Variants(CodexPalette.Catppuccin));
        Assert.Equal(
            43,
            CompactBarPaletteCatalog.Values.Sum(
                palette => CompactBarPaletteCatalog.Variants(palette).Length));
    }

    [Fact]
    public void Selecting_color_theme_replaces_colors_that_would_mask_the_theme()
    {
        var settings = new AppSettings
        {
            BackgroundColor = "#0016181C",
            FiveHour = new MetricSettings { FillColor = "#FFFF0000", TrackColor = "#FF000000" }
        };

        SettingsForm.ApplyColorThemeDefaults(settings, CodexPalette.Codex, ThemeVariant.Dark);

        Assert.Equal("#FF111111", settings.BackgroundColor);
        Assert.Equal("#FF0169CC", settings.FiveHour.FillColor);
        Assert.NotEqual("#FF000000", settings.FiveHour.TrackColor);
        Assert.Equal(settings.FiveHour.FillColor, settings.Memory.FillColor);
    }

    [Theory]
    [InlineData(CodexPalette.Absolutely, ThemeVariant.Dark, "#2D2D2B", "#F9F9F7", "#CC7D5E")]
    [InlineData(CodexPalette.Absolutely, ThemeVariant.Light, "#F9F9F7", "#2D2D2B", "#CC7D5E")]
    [InlineData(CodexPalette.Ayu, ThemeVariant.Dark, "#10141C", "#BFBDB6", "#E6B450")]
    [InlineData(CodexPalette.Catppuccin, ThemeVariant.Dark, "#1E1E2E", "#CDD6F4", "#CBA6F7")]
    [InlineData(CodexPalette.Catppuccin, ThemeVariant.Light, "#EFF1F5", "#4C4F69", "#8839EF")]
    [InlineData(CodexPalette.Codex, ThemeVariant.Dark, "#111111", "#FCFCFC", "#0169CC")]
    [InlineData(CodexPalette.Codex, ThemeVariant.Light, "#FFFFFF", "#0D0D0D", "#0169CC")]
    [InlineData(CodexPalette.Dracula, ThemeVariant.Dark, "#282A36", "#F8F8F2", "#FF79C6")]
    [InlineData(CodexPalette.Everforest, ThemeVariant.Dark, "#2D353B", "#D3C6AA", "#A7C080")]
    [InlineData(CodexPalette.Everforest, ThemeVariant.Light, "#FDF6E3", "#5C6A72", "#93B259")]
    [InlineData(CodexPalette.GitHub, ThemeVariant.Dark, "#0D1117", "#E6EDF3", "#1F6FEB")]
    [InlineData(CodexPalette.GitHub, ThemeVariant.Light, "#FFFFFF", "#1F2328", "#0969DA")]
    [InlineData(CodexPalette.Gruvbox, ThemeVariant.Dark, "#282828", "#EBDBB2", "#458588")]
    [InlineData(CodexPalette.Gruvbox, ThemeVariant.Light, "#FBF1C7", "#3C3836", "#458588")]
    [InlineData(CodexPalette.Linear, ThemeVariant.Dark, "#0F0F11", "#E3E4E6", "#606ACC")]
    [InlineData(CodexPalette.Linear, ThemeVariant.Light, "#FCFCFD", "#1B1B1B", "#5E6AD2")]
    [InlineData(CodexPalette.Lobster, ThemeVariant.Dark, "#111827", "#E4E4E7", "#FF5C5C")]
    [InlineData(CodexPalette.Material, ThemeVariant.Dark, "#212121", "#EEFFFF", "#80CBC4")]
    [InlineData(CodexPalette.Matrix, ThemeVariant.Dark, "#040805", "#B8FFCA", "#1EFF5A")]
    [InlineData(CodexPalette.Monokai, ThemeVariant.Dark, "#272822", "#F8F8F2", "#99947C")]
    [InlineData(CodexPalette.NightOwl, ThemeVariant.Dark, "#011627", "#D6DEEB", "#44596B")]
    [InlineData(CodexPalette.Nord, ThemeVariant.Dark, "#2E3440", "#D8DEE9", "#88C0D0")]
    [InlineData(CodexPalette.Notion, ThemeVariant.Dark, "#191919", "#D9D9D8", "#3183D8")]
    [InlineData(CodexPalette.Notion, ThemeVariant.Light, "#FFFFFF", "#37352F", "#3183D8")]
    [InlineData(CodexPalette.One, ThemeVariant.Dark, "#282C34", "#ABB2BF", "#4D78CC")]
    [InlineData(CodexPalette.One, ThemeVariant.Light, "#FAFAFA", "#383A42", "#526FFF")]
    [InlineData(CodexPalette.Oscurange, ThemeVariant.Dark, "#0B0B0F", "#E6E6E6", "#F9B98C")]
    [InlineData(CodexPalette.Proof, ThemeVariant.Light, "#F5F3ED", "#2F312D", "#3D755D")]
    [InlineData(CodexPalette.Raycast, ThemeVariant.Dark, "#101010", "#FEFEFE", "#FF6363")]
    [InlineData(CodexPalette.Raycast, ThemeVariant.Light, "#FFFFFF", "#030303", "#FF6363")]
    [InlineData(CodexPalette.RosePine, ThemeVariant.Dark, "#232136", "#E0DEF4", "#EA9A97")]
    [InlineData(CodexPalette.RosePine, ThemeVariant.Light, "#FAF4ED", "#575279", "#D7827E")]
    [InlineData(CodexPalette.Sentry, ThemeVariant.Dark, "#2D2935", "#E6DFF9", "#7055F6")]
    [InlineData(CodexPalette.Solarized, ThemeVariant.Dark, "#002B36", "#839496", "#D30102")]
    [InlineData(CodexPalette.Solarized, ThemeVariant.Light, "#FDF6E3", "#657B83", "#B58900")]
    [InlineData(CodexPalette.Temple, ThemeVariant.Dark, "#02120C", "#C7E6DA", "#E4F222")]
    [InlineData(CodexPalette.TokyoNight, ThemeVariant.Dark, "#1A1B26", "#A9B1D6", "#3D59A1")]
    [InlineData(CodexPalette.Vercel, ThemeVariant.Dark, "#000000", "#EDEDED", "#006EFE")]
    [InlineData(CodexPalette.Vercel, ThemeVariant.Light, "#FFFFFF", "#171717", "#006AFF")]
    [InlineData(CodexPalette.VSCodePlus, ThemeVariant.Dark, "#1E1E1E", "#D4D4D4", "#007ACC")]
    [InlineData(CodexPalette.VSCodePlus, ThemeVariant.Light, "#FFFFFF", "#000000", "#007ACC")]
    [InlineData(CodexPalette.Xcode, ThemeVariant.Dark, "#1F1F24", "#FFFFFF", "#5482FF")]
    [InlineData(CodexPalette.Xcode, ThemeVariant.Light, "#FFFFFF", "#000000", "#0E0EFF")]
    public void Palette_catalog_matches_codex_resolved_chrome_theme(
        CodexPalette palette,
        ThemeVariant variant,
        string surface,
        string ink,
        string accent)
    {
        CodexPaletteColors colors = CompactBarPaletteCatalog.Get(palette, variant);

        Assert.Equal(surface, colors.Background);
        Assert.Equal(surface, colors.Surface);
        Assert.Equal(ink, colors.Foreground);
        Assert.Equal(accent, colors.Accent);
    }
}
