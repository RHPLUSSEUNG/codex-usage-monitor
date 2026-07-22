param(
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"
$selfContained = (-not $FrameworkDependent).ToString().ToLowerInvariant()
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained $selfContained -p:PublishSingleFile=true

$output = Join-Path $PSScriptRoot "bin\Release\net8.0-windows\win-x64\publish"
Write-Host "Build complete: $output\CodexUsageMonitor.exe"
