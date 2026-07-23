param(
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_UI_LANGUAGE = "en-US"

$projectPath = Join-Path $PSScriptRoot "CodexUsageMonitor.csproj"
$selfContained = (-not $FrameworkDependent).ToString().ToLowerInvariant()
& dotnet restore $projectPath -r win-x64
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE."
}

& dotnet publish $projectPath -c Release -r win-x64 --self-contained $selfContained -p:PublishSingleFile=true --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$output = Join-Path $PSScriptRoot "bin\Release\net8.0-windows\win-x64\publish"
$publishedExecutable = Join-Path $output "CodexUsageMonitor.exe"
if (-not (Test-Path $publishedExecutable)) {
    throw "Published executable was not created at $publishedExecutable"
}

Write-Host "Build complete: $publishedExecutable"
