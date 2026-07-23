$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_UI_LANGUAGE = "en-US"

$projectDirectory = $PSScriptRoot
$publishedExecutable = Join-Path $projectDirectory "bin\Release\net8.0-windows\win-x64\publish\CodexUsageMonitor.exe"
& (Join-Path $projectDirectory "build.ps1")

$installDirectory = Join-Path $env:LOCALAPPDATA "Programs\CodexUsageMonitor"
$installedExecutable = Join-Path $installDirectory "CodexUsageMonitor.exe"
New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
Copy-Item -LiteralPath $publishedExecutable -Destination $installedExecutable -Force

$startMenuDirectory = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$shortcutPath = Join-Path $startMenuDirectory "Codex Usage Monitor.lnk"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $installedExecutable
$shortcut.WorkingDirectory = $installDirectory
$shortcut.IconLocation = "$installedExecutable,0"
$shortcut.Save()

Start-Process -FilePath $installedExecutable
Write-Host "Installation complete. You can launch Codex Usage Monitor from the Start menu."
