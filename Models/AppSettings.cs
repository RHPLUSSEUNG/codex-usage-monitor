namespace CodexUsageMonitor.Models;

public enum PanelId { FiveHour, Weekly, Cpu, Memory, FiveHourReset, WeeklyReset }

public enum MetricPresentation
{
    PercentOnly,
    BarOnly,
    PercentAndBar
}

public enum PercentageMode
{
    Remaining,
    Used
}

public enum CompactBarStyle
{
    DarkMinimal = 0,
    Classic = DarkMinimal,
    LabelBoxes = 1,
    NeonGlow = 2,
    Light = 3,
    Cards = 4,
    CircularGauges = 5,
    CompactRows = 6,
    RoundedCapsules = 7,
    Gradient = 8,
    MinimalIcons = 9
}

public enum ThemeVariant
{
    Dark = 0,
    Light = 1,
    // Kept for settings.json compatibility. SettingsStore migrates these to
    // CodexPalette + Dark/Light.
    CodexDark = 2,
    CodexLight = 3
}

public enum CodexPalette
{
    Absolutely,
    Ayu,
    Catppuccin,
    Codex,
    Dracula,
    Everforest,
    GitHub,
    Gruvbox,
    Linear,
    Lobster,
    Material,
    Matrix,
    Monokai,
    NightOwl,
    Nord,
    Notion,
    One,
    Oscurange,
    Proof,
    Raycast,
    RosePine,
    Sentry,
    Solarized,
    Temple,
    TokyoNight,
    Vercel,
    VSCodePlus,
    Xcode
}

public enum AppLanguage
{
    English,
    Korean,
    ChineseSimplified,
    Japanese
}

public sealed class MetricSettings
{
    public string LabelColor { get; set; } = "";
    public string PercentColor { get; set; } = "";
    public bool ShowResetTime { get; set; } = true;
    public bool ShowResetTimeLabel { get; set; }
    public string ResetTimeColor { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public MetricPresentation Presentation { get; set; } = MetricPresentation.PercentAndBar;
    public string FillColor { get; set; } = "#62D6A7";
    public string TrackColor { get; set; } = "#3A3D45";
}

public sealed class CompactBarPreset
{
    public CompactBarStyle Style { get; set; } = CompactBarStyle.Light;
    public CodexPalette CodexPalette { get; set; } = CodexPalette.Codex;
    public bool FollowSystemTheme { get; set; } = true;
    public PanelId[] PanelOrder { get; set; } = AppSettings.DefaultPanelOrder();
    public ThemeVariant ThemeVariant { get; set; } = ThemeVariant.Light;
    public int ScalePercent { get; set; } = 100;
    public string BackgroundColor { get; set; } = "#0016181C";
    public bool TransparentBackground { get; set; } = true;
    public PercentageMode PercentageMode { get; set; } = PercentageMode.Remaining;
    public MetricSettings FiveHour { get; set; } = new();
    public MetricSettings Weekly { get; set; } = new();
    public MetricSettings Cpu { get; set; } = new();
    public MetricSettings Memory { get; set; } = new();

    public static CompactBarPreset Capture(AppSettings settings) => new()
    {
        Style = settings.CompactBarStyle,
        CodexPalette = settings.CodexPalette,
        FollowSystemTheme = settings.FollowSystemTheme,
        PanelOrder = AppSettings.NormalizePanelOrder(settings.PanelOrder),
        ThemeVariant = settings.ThemeVariant,
        ScalePercent = settings.CompactBarScalePercent,
        BackgroundColor = settings.BackgroundColor,
        TransparentBackground = settings.TransparentBackground,
        PercentageMode = settings.PercentageMode,
        FiveHour = AppSettings.CopyMetric(settings.FiveHour),
        Weekly = AppSettings.CopyMetric(settings.Weekly),
        Cpu = AppSettings.CopyMetric(settings.Cpu),
        Memory = AppSettings.CopyMetric(settings.Memory)
    };

    public CompactBarPreset Copy() => new()
    {
        Style = Style,
        CodexPalette = CodexPalette,
        FollowSystemTheme = FollowSystemTheme,
        PanelOrder = AppSettings.NormalizePanelOrder(PanelOrder),
        ThemeVariant = ThemeVariant,
        ScalePercent = ScalePercent,
        BackgroundColor = BackgroundColor,
        TransparentBackground = TransparentBackground,
        PercentageMode = PercentageMode,
        FiveHour = AppSettings.CopyMetric(FiveHour),
        Weekly = AppSettings.CopyMetric(Weekly),
        Cpu = AppSettings.CopyMetric(Cpu),
        Memory = AppSettings.CopyMetric(Memory)
    };

    public void ApplyTo(AppSettings settings)
    {
        settings.CompactBarStyle = AppSettings.NormalizeStyle(Style);
        settings.CodexPalette = CodexPalette;
        settings.FollowSystemTheme = FollowSystemTheme;
        settings.PanelOrder = AppSettings.NormalizePanelOrder(PanelOrder);
        settings.ThemeVariant = ThemeVariant;
        settings.CompactBarScalePercent = ScalePercent;
        settings.BackgroundColor = BackgroundColor;
        settings.TransparentBackground = TransparentBackground;
        settings.PercentageMode = PercentageMode;
        AppSettings.CopyMetricInto(FiveHour, settings.FiveHour);
        AppSettings.CopyMetricInto(Weekly, settings.Weekly);
        AppSettings.CopyMetricInto(Cpu, settings.Cpu);
        AppSettings.CopyMetricInto(Memory, settings.Memory);
    }
}

public sealed class QuotaAlertState
{
    public DateTimeOffset? ResetsAt { get; set; }
    public bool Notified { get; set; }
    public double LastRemainingPercent { get; set; } = 100;
    public QuotaAlertState Copy() => new() { ResetsAt = ResetsAt, Notified = Notified, LastRemainingPercent = LastRemainingPercent };
}

public sealed class AppSettings
{
    public const int CurrentSettingsVersion = 3;

    public int SettingsVersion { get; set; } = CurrentSettingsVersion;
    public AppLanguage Language { get; set; } = AppLanguage.English;
    public CompactBarStyle CompactBarStyle { get; set; } = CompactBarStyle.Light;
    public CodexPalette CodexPalette { get; set; } = CodexPalette.Codex;
    public bool FollowSystemTheme { get; set; } = true;
    public PanelId[] PanelOrder { get; set; } = DefaultPanelOrder();
    public ThemeVariant ThemeVariant { get; set; } = ThemeVariant.Light;
    public int CompactBarScalePercent { get; set; } = 100;
    public bool ShowCompactBar { get; set; } = true;
    public string BackgroundColor { get; set; } = "#0016181C";
    public bool TransparentBackground { get; set; } = true;
    public int WindowPositionX { get; set; } = -1;
    public int WindowPositionY { get; set; } = -1;
    public bool StartWithWindows { get; set; }
    public bool AlignToTaskbarEnd { get; set; } = true;
    public bool UseCustomTaskbarPosition { get; set; }
    public int TaskbarPositionPercent { get; set; } = 75;
    public int RefreshIntervalSeconds { get; set; } = 60;
    // Retained as the migration source for settings written before thresholds were split.
    public int QuotaNotificationPercent { get; set; } = 20;
    public int FiveHourNotificationPercent { get; set; }
    public int WeeklyNotificationPercent { get; set; }
    public QuotaAlertState FiveHourAlert { get; set; } = new();
    public QuotaAlertState WeeklyAlert { get; set; } = new();
    public bool EnableQuotaNotifications { get; set; } = true;
    public bool QuietHoursEnabled { get; set; }
    public int QuietHoursStart { get; set; } = 22;
    public int QuietHoursEnd { get; set; } = 8;
    public PercentageMode PercentageMode { get; set; } = PercentageMode.Remaining;
    public string CodexExecutable { get; set; } = "codex";
    public MetricSettings FiveHour { get; set; } = new();
    public MetricSettings Weekly { get; set; } = new()
    {
        FillColor = "#77A8FF"
    };
    public MetricSettings Cpu { get; set; } = new()
    {
        Enabled = true,
        FillColor = "#F2C66D"
    };
    public MetricSettings Memory { get; set; } = new()
    {
        Enabled = true,
        FillColor = "#D48BFF"
    };
    public CompactBarPreset? Preset1 { get; set; } = CreateInitialPreset();
    public CompactBarPreset? Preset2 { get; set; }
    public CompactBarPreset? Preset3 { get; set; }

    private static CompactBarPreset CreateInitialPreset() => new()
    {
        Style = CompactBarStyle.Light,
        CodexPalette = CodexPalette.Codex,
        FollowSystemTheme = true,
        ThemeVariant = ThemeVariant.Light,
        BackgroundColor = "#0016181C",
        TransparentBackground = true,
        FiveHour = new MetricSettings(),
        Weekly = new MetricSettings { FillColor = "#77A8FF" },
        Cpu = new MetricSettings { FillColor = "#F2C66D" },
        Memory = new MetricSettings { FillColor = "#D48BFF" }
    };

    public AppSettings Copy() => new()
    {
        SettingsVersion = SettingsVersion,
        Language = Language,
        CompactBarStyle = CompactBarStyle,
        CodexPalette = CodexPalette,
        FollowSystemTheme = FollowSystemTheme,
        PanelOrder = AppSettings.NormalizePanelOrder(PanelOrder),
        ThemeVariant = ThemeVariant,
        CompactBarScalePercent = CompactBarScalePercent,
        ShowCompactBar = ShowCompactBar,
        BackgroundColor = BackgroundColor,
        TransparentBackground = TransparentBackground,
        WindowPositionX = WindowPositionX,
        WindowPositionY = WindowPositionY,
        StartWithWindows = StartWithWindows,
        AlignToTaskbarEnd = AlignToTaskbarEnd,
        UseCustomTaskbarPosition = UseCustomTaskbarPosition,
        TaskbarPositionPercent = TaskbarPositionPercent,
        RefreshIntervalSeconds = RefreshIntervalSeconds,
        QuotaNotificationPercent = QuotaNotificationPercent,
        FiveHourNotificationPercent = FiveHourNotificationPercent,
        WeeklyNotificationPercent = WeeklyNotificationPercent,
        FiveHourAlert = FiveHourAlert.Copy(),
        WeeklyAlert = WeeklyAlert.Copy(),
        EnableQuotaNotifications = EnableQuotaNotifications,
        QuietHoursEnabled = QuietHoursEnabled,
        QuietHoursStart = QuietHoursStart,
        QuietHoursEnd = QuietHoursEnd,
        PercentageMode = PercentageMode,
        CodexExecutable = CodexExecutable,
        FiveHour = CopyMetric(FiveHour),
        Weekly = CopyMetric(Weekly),
        Cpu = CopyMetric(Cpu),
        Memory = CopyMetric(Memory),
        Preset1 = Preset1?.Copy(),
        Preset2 = Preset2?.Copy(),
        Preset3 = Preset3?.Copy()
    };

    public static PanelId[] DefaultPanelOrder() =>
        [PanelId.FiveHourReset, PanelId.FiveHour, PanelId.Cpu,
         PanelId.WeeklyReset, PanelId.Weekly, PanelId.Memory];

    public static PanelId[] NormalizePanelOrder(PanelId[]? order)
    {
        PanelId[] valid = (order ?? []).Where(id => Enum.IsDefined(id)).Distinct().ToArray();
        if (valid.Length == 0) return DefaultPanelOrder();
        if (!valid.Contains(PanelId.FiveHourReset) && !valid.Contains(PanelId.WeeklyReset))
        {
            PanelId[] old = valid.Concat([PanelId.FiveHour, PanelId.Weekly, PanelId.Cpu, PanelId.Memory])
                .Distinct().ToArray();
            return [PanelId.FiveHourReset, old[0], old[2],
                    PanelId.WeeklyReset, old[1], old[3]];
        }
        return valid.Concat(DefaultPanelOrder()).Distinct().ToArray();
    }

    public static CompactBarStyle NormalizeStyle(CompactBarStyle style) => style switch
    {
        CompactBarStyle.DarkMinimal or CompactBarStyle.Cards or CompactBarStyle.CircularGauges
            or CompactBarStyle.RoundedCapsules => CompactBarStyle.Light,
        _ when Enum.IsDefined(style) => style,
        _ => CompactBarStyle.Light
    };

    public MetricSettings Metric(PanelId id) => id switch
    {
        PanelId.FiveHour or PanelId.FiveHourReset => FiveHour,
        PanelId.Weekly or PanelId.WeeklyReset => Weekly,
        PanelId.Cpu => Cpu, _ => Memory
    };

    public int NotificationPercent(PanelId id)
    {
        int configured = id == PanelId.FiveHour ? FiveHourNotificationPercent : WeeklyNotificationPercent;
        return Math.Clamp(configured is >= 1 and <= 99 ? configured : QuotaNotificationPercent, 1, 99);
    }

    public void SwapPanels(PanelId first, PanelId second)
    {
        PanelOrder = NormalizePanelOrder(PanelOrder);
        int a = Array.IndexOf(PanelOrder, first), b = Array.IndexOf(PanelOrder, second);
        (PanelOrder[a], PanelOrder[b]) = (PanelOrder[b], PanelOrder[a]);
    }

    internal static MetricSettings CopyMetric(MetricSettings source) => new()
    {
        LabelColor = source.LabelColor,
        PercentColor = source.PercentColor,
        ShowResetTime = source.ShowResetTime,
        ShowResetTimeLabel = source.ShowResetTimeLabel,
        ResetTimeColor = source.ResetTimeColor,
        Enabled = source.Enabled,
        Presentation = source.Presentation,
        FillColor = source.FillColor,
        TrackColor = source.TrackColor
    };

    internal static void CopyMetricInto(MetricSettings source, MetricSettings destination)
    {
        destination.LabelColor = source.LabelColor;
        destination.PercentColor = source.PercentColor;
        destination.ShowResetTime = source.ShowResetTime;
        destination.ShowResetTimeLabel = source.ShowResetTimeLabel;
        destination.ResetTimeColor = source.ResetTimeColor;
        destination.Enabled = source.Enabled;
        destination.Presentation = source.Presentation;
        destination.FillColor = source.FillColor;
        destination.TrackColor = source.TrackColor;
    }
}
