using Microsoft.Win32;

namespace CodexUsageMonitor.Services;

public static class StartupService
{
    private const string RegistryValueName = "CodexUsageMonitor";

    public static void Apply(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true)
            ?? throw new InvalidOperationException("시작 프로그램 레지스트리를 열 수 없습니다.");

        if (enabled)
        {
            string executable = Environment.ProcessPath
                ?? throw new InvalidOperationException("실행 파일 경로를 확인할 수 없습니다.");
            key.SetValue(RegistryValueName, $"\"{executable}\"");
        }
        else
        {
            key.DeleteValue(RegistryValueName, throwOnMissingValue: false);
        }
    }
}
