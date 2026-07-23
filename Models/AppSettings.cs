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
    Classic,
    LabelBoxes
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
        Memory = CopyMetric(Memory)
    };

    private static MetricSettings CopyMetric(MetricSettings source) => new()
    {
        Enabled = source.Enabled,
        Presentation = source.Presentation,
        FillColor = source.FillColor,
        TrackColor = source.TrackColor
    };
}
