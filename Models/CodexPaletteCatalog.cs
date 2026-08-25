namespace CodexUsageMonitor.Models;

public readonly record struct CodexPaletteColors(
    string Background,
    string Surface,
    string Foreground,
    string Accent);

public static class CompactBarPaletteCatalog
{
    // Values mirror the Codex app's resolved chromeTheme seed. Standard
    // themes are resolved from their VS Code tokens; custom themes can
    // override the resulting surface, ink, and accent values.
    public static readonly CodexPalette[] Values = Enum.GetValues<CodexPalette>();

    private static readonly Dictionary<(CodexPalette, ThemeVariant), CodexPaletteColors> Colors = new()
    {
        [(CodexPalette.Absolutely, ThemeVariant.Dark)] = C("#2D2D2B", "#F9F9F7", "#CC7D5E"),
        [(CodexPalette.Absolutely, ThemeVariant.Light)] = C("#F9F9F7", "#2D2D2B", "#CC7D5E"),
        [(CodexPalette.Ayu, ThemeVariant.Dark)] = C("#10141C", "#BFBDB6", "#E6B450"),
        [(CodexPalette.Catppuccin, ThemeVariant.Dark)] = C("#1E1E2E", "#CDD6F4", "#CBA6F7"),
        [(CodexPalette.Catppuccin, ThemeVariant.Light)] = C("#EFF1F5", "#4C4F69", "#8839EF"),
        [(CodexPalette.Codex, ThemeVariant.Dark)] = C("#111111", "#FCFCFC", "#0169CC"),
        [(CodexPalette.Codex, ThemeVariant.Light)] = C("#FFFFFF", "#0D0D0D", "#0169CC"),
        [(CodexPalette.Dracula, ThemeVariant.Dark)] = C("#282A36", "#F8F8F2", "#FF79C6"),
        [(CodexPalette.Everforest, ThemeVariant.Dark)] = C("#2D353B", "#D3C6AA", "#A7C080"),
        [(CodexPalette.Everforest, ThemeVariant.Light)] = C("#FDF6E3", "#5C6A72", "#93B259"),
        [(CodexPalette.GitHub, ThemeVariant.Dark)] = C("#0D1117", "#E6EDF3", "#1F6FEB"),
        [(CodexPalette.GitHub, ThemeVariant.Light)] = C("#FFFFFF", "#1F2328", "#0969DA"),
        [(CodexPalette.Gruvbox, ThemeVariant.Dark)] = C("#282828", "#EBDBB2", "#458588"),
        [(CodexPalette.Gruvbox, ThemeVariant.Light)] = C("#FBF1C7", "#3C3836", "#458588"),
        [(CodexPalette.Linear, ThemeVariant.Dark)] = C("#0F0F11", "#E3E4E6", "#606ACC"),
        [(CodexPalette.Linear, ThemeVariant.Light)] = C("#FCFCFD", "#1B1B1B", "#5E6AD2"),
        [(CodexPalette.Lobster, ThemeVariant.Dark)] = C("#111827", "#E4E4E7", "#FF5C5C"),
        [(CodexPalette.Material, ThemeVariant.Dark)] = C("#212121", "#EEFFFF", "#80CBC4"),
        [(CodexPalette.Matrix, ThemeVariant.Dark)] = C("#040805", "#B8FFCA", "#1EFF5A"),
        [(CodexPalette.Monokai, ThemeVariant.Dark)] = C("#272822", "#F8F8F2", "#99947C"),
        [(CodexPalette.NightOwl, ThemeVariant.Dark)] = C("#011627", "#D6DEEB", "#44596B"),
        [(CodexPalette.Nord, ThemeVariant.Dark)] = C("#2E3440", "#D8DEE9", "#88C0D0"),
        [(CodexPalette.Notion, ThemeVariant.Dark)] = C("#191919", "#D9D9D8", "#3183D8"),
        [(CodexPalette.Notion, ThemeVariant.Light)] = C("#FFFFFF", "#37352F", "#3183D8"),
        [(CodexPalette.One, ThemeVariant.Dark)] = C("#282C34", "#ABB2BF", "#4D78CC"),
        [(CodexPalette.One, ThemeVariant.Light)] = C("#FAFAFA", "#383A42", "#526FFF"),
        [(CodexPalette.Oscurange, ThemeVariant.Dark)] = C("#0B0B0F", "#E6E6E6", "#F9B98C"),
        [(CodexPalette.Proof, ThemeVariant.Light)] = C("#F5F3ED", "#2F312D", "#3D755D"),
        [(CodexPalette.Raycast, ThemeVariant.Dark)] = C("#101010", "#FEFEFE", "#FF6363"),
        [(CodexPalette.Raycast, ThemeVariant.Light)] = C("#FFFFFF", "#030303", "#FF6363"),
        [(CodexPalette.RosePine, ThemeVariant.Dark)] = C("#232136", "#E0DEF4", "#EA9A97"),
        [(CodexPalette.RosePine, ThemeVariant.Light)] = C("#FAF4ED", "#575279", "#D7827E"),
        [(CodexPalette.Sentry, ThemeVariant.Dark)] = C("#2D2935", "#E6DFF9", "#7055F6"),
        [(CodexPalette.Solarized, ThemeVariant.Dark)] = C("#002B36", "#839496", "#D30102"),
        [(CodexPalette.Solarized, ThemeVariant.Light)] = C("#FDF6E3", "#657B83", "#B58900"),
        [(CodexPalette.Temple, ThemeVariant.Dark)] = C("#02120C", "#C7E6DA", "#E4F222"),
        [(CodexPalette.TokyoNight, ThemeVariant.Dark)] = C("#1A1B26", "#A9B1D6", "#3D59A1"),
        [(CodexPalette.Vercel, ThemeVariant.Dark)] = C("#000000", "#EDEDED", "#006EFE"),
        [(CodexPalette.Vercel, ThemeVariant.Light)] = C("#FFFFFF", "#171717", "#006AFF"),
        [(CodexPalette.VSCodePlus, ThemeVariant.Dark)] = C("#1E1E1E", "#D4D4D4", "#007ACC"),
        [(CodexPalette.VSCodePlus, ThemeVariant.Light)] = C("#FFFFFF", "#000000", "#007ACC"),
        [(CodexPalette.Xcode, ThemeVariant.Dark)] = C("#1F1F24", "#FFFFFF", "#5482FF"),
        [(CodexPalette.Xcode, ThemeVariant.Light)] = C("#FFFFFF", "#000000", "#0E0EFF")
    };

    public static bool Supports(CodexPalette palette, ThemeVariant variant) =>
        Colors.ContainsKey((palette, NormalizeVariant(variant)));

    public static ThemeVariant DefaultVariant(CodexPalette palette) =>
        Supports(palette, ThemeVariant.Dark) ? ThemeVariant.Dark : ThemeVariant.Light;

    public static ThemeVariant[] Variants(CodexPalette palette) =>
        new[] { ThemeVariant.Dark, ThemeVariant.Light }
            .Where(variant => Supports(palette, variant))
            .ToArray();

    public static CodexPaletteColors Get(CodexPalette palette, ThemeVariant variant)
    {
        ThemeVariant normalized = NormalizeVariant(variant);
        if (Colors.TryGetValue((palette, normalized), out CodexPaletteColors colors))
            return colors;
        return Colors[(palette, DefaultVariant(palette))];
    }

    public static string EffectiveAccent(CodexPalette palette, ThemeVariant variant)
    {
        return Get(palette, variant).Accent;
    }

    public static string DisplayName(CodexPalette palette) => palette switch
    {
        CodexPalette.NightOwl => "Night Owl",
        CodexPalette.RosePine => "Rose Pine",
        CodexPalette.TokyoNight => "Tokyo Night",
        CodexPalette.VSCodePlus => "VS Code Plus",
        _ => palette.ToString()
    };

    public static ThemeVariant NormalizeVariant(ThemeVariant variant) => variant switch
    {
        ThemeVariant.Light or ThemeVariant.CodexLight => ThemeVariant.Light,
        _ => ThemeVariant.Dark
    };

    private static CodexPaletteColors C(string surface, string ink, string accent) =>
        new(surface, surface, ink, accent);
}
