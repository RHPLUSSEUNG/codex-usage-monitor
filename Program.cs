using CodexUsageMonitor.Services;
using CodexUsageMonitor.UI;

namespace CodexUsageMonitor;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (UpdateInstaller.IsUpdateMode(args))
            return UpdateInstaller.Run(args);

        UpdateInstaller.CleanupDownloads();
        ApplicationConfiguration.Initialize();
        Application.Run(new UsageMonitorContext());
        return 0;
    }
}
