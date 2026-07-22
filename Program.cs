using CodexUsageMonitor.UI;

namespace CodexUsageMonitor;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new UsageMonitorContext());
    }
}
