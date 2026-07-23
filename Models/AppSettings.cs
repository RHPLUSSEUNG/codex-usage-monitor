namespace CodexUsageMonitor.Models;

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

public enum AppLanguage
{
    English,
    Korean,
    ChineseSimplified,
    Japanese
}

public sealed class MetricSettings
{
    public bool Enabled { get; set; } = true;
    public MetricPresentation Presentation { get; set; } = MetricPresentation.PercentAndBar;
    public string FillColor { get; set; } = "#62D6A7";
    public string TrackColor { get; set; } = "#3A3D45";
}

public sealed class CompactBarPreset
{
    public CompactBarStyle Style { get; set; } = CompactBarStyle.LabelBoxes;
    public string BackgroundColor { get; set; } = "#FF16181C";
    public PercentageMode PercentageMode { get; set; } = PercentageMode.Remaining;
    public MetricSettings FiveHour { get; set; } = new();
    public MetricSettings Weekly { get; set; } = new();
    public MetricSettings Cpu { get; set; } = new();
    public MetricSettings Memory { get; set; } = new();

    public static CompactBarPreset Capture(AppSettings settings) => new()
    {
        Style = settings.CompactBarStyle,
        BackgroundColor = settings.BackgroundColor,
        PercentageMode = settings.PercentageMode,
        FiveHour = AppSettings.CopyMetric(settings.FiveHour),
        Weekly = AppSettings.CopyMetric(settings.Weekly),
        Cpu = AppSettings.CopyMetric(settings.Cpu),
        Memory = AppSettings.CopyMetric(settings.Memory)
    };

    public CompactBarPreset Copy() => new()
    {
        Style = Style,
        BackgroundColor = BackgroundColor,
        PercentageMode = PercentageMode,
        FiveHour = AppSettings.CopyMetric(FiveHour),
        Weekly = AppSettings.CopyMetric(Weekly),
        Cpu = AppSettings.CopyMetric(Cpu),
        Memory = AppSettings.CopyMetric(Memory)
    };

    public void ApplyTo(AppSettings settings)
    {
        settings.CompactBarStyle = Style;
        settings.BackgroundColor = BackgroundColor;
        settings.PercentageMode = PercentageMode;
        AppSettings.CopyMetricInto(FiveHour, settings.FiveHour);
        AppSettings.CopyMetricInto(Weekly, settings.Weekly);
        AppSettings.CopyMetricInto(Cpu, settings.Cpu);
        AppSettings.CopyMetricInto(Memory, settings.Memory);
    }
}

public sealed class AppSettings
{
    public AppLanguage Language { get; set; } = AppLanguage.Korean;
    public CompactBarStyle CompactBarStyle { get; set; } = CompactBarStyle.LabelBoxes;
    public bool ShowCompactBar { get; set; } = true;
    public string BackgroundColor { get; set; } = "#FF16181C";
    public int WindowPositionX { get; set; } = -1;
    public int WindowPositionY { get; set; } = -1;
    public bool StartWithWindows { get; set; }
    public bool AlignToTaskbarEnd { get; set; } = true;
    public bool UseCustomTaskbarPosition { get; set; }
    public int TaskbarPositionPercent { get; set; } = 75;
    public int RefreshIntervalSeconds { get; set; } = 60;
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
    public CompactBarPreset? Preset1 { get; set; }
    public CompactBarPreset? Preset2 { get; set; }
    public CompactBarPreset? Preset3 { get; set; }

    public AppSettings Copy() => new()
    {
        Language = Language,
        CompactBarStyle = CompactBarStyle,
        ShowCompactBar = ShowCompactBar,
        BackgroundColor = BackgroundColor,
        WindowPositionX = WindowPositionX,
        WindowPositionY = WindowPositionY,
        StartWithWindows = StartWithWindows,
        AlignToTaskbarEnd = AlignToTaskbarEnd,
        UseCustomTaskbarPosition = UseCustomTaskbarPosition,
        TaskbarPositionPercent = TaskbarPositionPercent,
        RefreshIntervalSeconds = RefreshIntervalSeconds,
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

    internal static MetricSettings CopyMetric(MetricSettings source) => new()
    {
        Enabled = source.Enabled,
        Presentation = source.Presentation,
        FillColor = source.FillColor,
        TrackColor = source.TrackColor
    };

    internal static void CopyMetricInto(MetricSettings source, MetricSettings destination)
    {
        destination.Enabled = source.Enabled;
        destination.Presentation = source.Presentation;
        destination.FillColor = source.FillColor;
        destination.TrackColor = source.TrackColor;
    }
}
