using System.Diagnostics;
using CodexUsageMonitor.Services;

namespace CodexUsageMonitor.Tests;

public sealed class UninstallTests
{
    [Fact]
    public void Installed_application_check_requires_exact_executable_path()
    {
        string installed = Path.Combine(
            Path.GetTempPath(),
            "Programs",
            "CodexUsageMonitor",
            "CodexUsageMonitor.exe");

        Assert.True(UninstallService.IsInstalledApplication(installed, installed.ToUpperInvariant()));
        Assert.False(UninstallService.IsInstalledApplication(
            Path.Combine(Path.GetTempPath(), "CodexUsageMonitor.exe"),
            installed));
    }

    [Fact]
    public void Uninstall_script_uses_literal_paths_and_removes_startup_entry()
    {
        string script = UninstallService.ScriptForTests;

        Assert.Contains("Remove-Item -LiteralPath $InstallDirectory", script);
        Assert.Contains("Remove-Item -LiteralPath $SettingsDirectory", script);
        Assert.Contains("Remove-ItemProperty", script);
        Assert.Contains("CodexUsageMonitor", script);
        Assert.Contains("$parent.WaitForExit(60000)", script);
    }

    [Fact]
    public async Task Uninstall_script_removes_temp_install_shortcut_and_settings()
    {
        using var directory = new TemporaryDirectory();
        string installDirectory = Path.Combine(directory.Path, "installed");
        string settingsDirectory = Path.Combine(directory.Path, "settings");
        string shortcutPath = Path.Combine(directory.Path, "Codex Usage Monitor.lnk");
        string scriptPath = Path.Combine(directory.Path, "uninstall.ps1");
        Directory.CreateDirectory(installDirectory);
        Directory.CreateDirectory(settingsDirectory);
        File.WriteAllText(Path.Combine(installDirectory, "CodexUsageMonitor.exe"), "test");
        File.WriteAllText(Path.Combine(settingsDirectory, "settings.json"), "{}");
        File.WriteAllText(shortcutPath, "test");
        File.WriteAllText(scriptPath, UninstallService.ScriptForTests);

        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("-ParentProcessId");
        startInfo.ArgumentList.Add(int.MaxValue.ToString());
        startInfo.ArgumentList.Add("-InstallDirectory");
        startInfo.ArgumentList.Add(installDirectory);
        startInfo.ArgumentList.Add("-ShortcutPath");
        startInfo.ArgumentList.Add(shortcutPath);
        startInfo.ArgumentList.Add("-SettingsDirectory");
        startInfo.ArgumentList.Add(settingsDirectory);
        startInfo.ArgumentList.Add("-DeleteSettings");
        startInfo.ArgumentList.Add("-SkipStartupRemoval");

        using Process process = Process.Start(startInfo)!;
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, process.ExitCode);
        Assert.False(Directory.Exists(installDirectory));
        Assert.False(Directory.Exists(settingsDirectory));
        Assert.False(File.Exists(shortcutPath));
        Assert.False(File.Exists(scriptPath));
    }
}
