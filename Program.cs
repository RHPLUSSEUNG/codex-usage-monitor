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

        using var singleInstance = new SingleInstanceCoordinator();
        if (!singleInstance.IsPrimary)
        {
            singleInstance.NotifyPrimary();
            return 0;
        }

        UpdateInstaller.TryGetPostUpdate(args, out PostUpdateSession? postUpdate);
        UpdateInstaller.CleanupDownloads();
        ApplicationConfiguration.Initialize();
        var context = new UsageMonitorContext(
            postUpdate is null
                ? null
                : () => UpdateInstaller.SignalHealthy(postUpdate));
        singleInstance.StartListening(context.ActivateSettings);
        Application.Run(context);
        return 0;
    }
}
