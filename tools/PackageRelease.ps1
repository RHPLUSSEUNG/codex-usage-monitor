param(
    [string]$Version,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\artifacts"),
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_UI_LANGUAGE = "en-US"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectPath = Join-Path $repositoryRoot "CodexUsageMonitor.csproj"
if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$project = Get-Content -Raw -LiteralPath $projectPath
    $Version = [string]$project.Project.PropertyGroup.Version
}
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must use major.minor.patch format. Received: $Version"
}

if (-not $SkipBuild) {
    & (Join-Path $repositoryRoot "build.ps1")
    if ($LASTEXITCODE -ne 0) {
        throw "build.ps1 failed with exit code $LASTEXITCODE."
    }
}

$publishDirectory = Join-Path $repositoryRoot "bin\Release\net8.0-windows\win-x64\publish"
$publishedExecutable = Join-Path $publishDirectory "CodexUsageMonitor.exe"
if (-not (Test-Path -LiteralPath $publishedExecutable)) {
    throw "Published executable was not found: $publishedExecutable"
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null
$packageName = "CodexUsageMonitor-v$Version-win-x64"
$stagingDirectory = Join-Path $resolvedOutput $packageName
$zipPath = Join-Path $resolvedOutput "$packageName.zip"
$checksumPath = "$zipPath.sha256"

foreach ($target in @($stagingDirectory, $zipPath, $checksumPath)) {
    $resolvedTarget = [System.IO.Path]::GetFullPath($target)
    if (-not $resolvedTarget.StartsWith(
            $resolvedOutput.TrimEnd('\') + '\',
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to replace a path outside the output directory: $resolvedTarget"
    }
}

if (Test-Path -LiteralPath $stagingDirectory) {
    Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
if (Test-Path -LiteralPath $checksumPath) {
    Remove-Item -LiteralPath $checksumPath -Force
}

try {
    [System.IO.Directory]::CreateDirectory($stagingDirectory) | Out-Null
    Copy-Item -Path (Join-Path $publishDirectory "*") -Destination $stagingDirectory -Recurse
    Compress-Archive -Path $stagingDirectory -DestinationPath $zipPath -CompressionLevel Optimal
    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    [System.IO.File]::WriteAllText(
        $checksumPath,
        "$hash  $packageName.zip`n",
        [System.Text.Encoding]::ASCII)
}
finally {
    if (Test-Path -LiteralPath $stagingDirectory) {
        Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
    }
}

Write-Host "Release package: $zipPath"
Write-Host "SHA-256 file:   $checksumPath"
