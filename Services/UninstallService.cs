using System.Diagnostics;
using System.Text;

namespace CodexUsageMonitor.Services;

internal static class UninstallService
{
    private const string Script =
        """
        param(
            [Parameter(Mandatory = $true)][int]$ParentProcessId,
            [Parameter(Mandatory = $true)][string]$InstallDirectory,
            [Parameter(Mandatory = $true)][string]$ShortcutPath,
            [Parameter(Mandatory = $true)][string]$SettingsDirectory,
            [switch]$DeleteSettings,
            [switch]$SkipStartupRemoval
        )

        $ErrorActionPreference = "Stop"
        try {
            $parent = Get-Process -Id $ParentProcessId -ErrorAction SilentlyContinue
            if ($null -ne $parent) {
                $parent.WaitForExit(60000)
                if (-not $parent.HasExited) {
                    throw "Codex Usage Monitor did not exit in time."
                }
            }

            if (-not $SkipStartupRemoval.IsPresent) {
                Remove-ItemProperty `
                    -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" `
                    -Name "CodexUsageMonitor" `
                    -ErrorAction SilentlyContinue
            }
            if (Test-Path -LiteralPath $ShortcutPath) {
                Remove-Item -LiteralPath $ShortcutPath -Force
            }
            if (Test-Path -LiteralPath $InstallDirectory) {
                Remove-Item -LiteralPath $InstallDirectory -Recurse -Force
            }
            if ($DeleteSettings.IsPresent -and (Test-Path -LiteralPath $SettingsDirectory)) {
                Remove-Item -LiteralPath $SettingsDirectory -Recurse -Force
            }
        }
        catch {
            $_ | Out-String | Set-Content `
                -LiteralPath (Join-Path $env:TEMP "CodexUsageMonitor-uninstall-error.log") `
                -Encoding UTF8
        }
        finally {
            Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue
        }
        """;

    public static bool IsInstalledApplication() =>
        IsInstalledApplication(Environment.ProcessPath, UpdateService.InstalledExecutablePath);

    internal static bool IsInstalledApplication(string? processPath, string installedExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(processPath))
            return false;
        return string.Equals(
            Path.GetFullPath(processPath),
            Path.GetFullPath(installedExecutablePath),
            StringComparison.OrdinalIgnoreCase);
    }

    public static void Launch(bool deleteSettings)
    {
        if (!IsInstalledApplication())
        {
            throw new InvalidOperationException(
                "Uninstall is available only for the installed application.");
        }

        string installDirectory = Path.GetDirectoryName(UpdateService.InstalledExecutablePath)
                                  ?? throw new InvalidOperationException(
                                      "The installation directory could not be resolved.");
        string shortcutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs",
            "Codex Usage Monitor.lnk");
        string scriptPath = Path.Combine(
            Path.GetTempPath(),
            $"CodexUsageMonitor.Uninstall.{Guid.NewGuid():N}.ps1");
        File.WriteAllText(scriptPath, Script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        try
        {
            var startInfo = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-WindowStyle");
            startInfo.ArgumentList.Add("Hidden");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.ArgumentList.Add("-ParentProcessId");
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
            startInfo.ArgumentList.Add("-InstallDirectory");
            startInfo.ArgumentList.Add(installDirectory);
            startInfo.ArgumentList.Add("-ShortcutPath");
            startInfo.ArgumentList.Add(shortcutPath);
            startInfo.ArgumentList.Add("-SettingsDirectory");
            startInfo.ArgumentList.Add(SettingsStore.SettingsDirectory);
            if (deleteSettings)
                startInfo.ArgumentList.Add("-DeleteSettings");

            _ = Process.Start(startInfo)
                ?? throw new InvalidOperationException("The uninstaller could not be started.");
        }
        catch
        {
            try
            {
                File.Delete(scriptPath);
            }
            catch
            {
                // Preserve the original launch failure.
            }
            throw;
        }
    }

    internal static string ScriptForTests => Script;
}
