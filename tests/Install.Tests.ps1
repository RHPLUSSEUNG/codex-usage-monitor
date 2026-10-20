$ErrorActionPreference = "Stop"
$installer = Join-Path (Split-Path -Parent $PSScriptRoot) "install.ps1"
$shell = (Get-Process -Id $PID).Path
$root = Join-Path $env:TEMP ("cum-installer-tests-" + [guid]::NewGuid().ToString("N"))
$originalLocalAppData = $env:LOCALAPPDATA
$originalTemp = $env:TEMP
$originalFakeAuth = $env:FAKE_AUTH
$originalFakeLoginMarker = $env:FAKE_LOGIN_MARKER
$originalPath = $env:PATH

function Assert-Equal($Expected, $Actual, [string]$Message) {
    if ($Expected -ne $Actual) { throw "$Message Expected=$Expected Actual=$Actual" }
}

function New-Fixture([string]$Name, [switch]$FailBuild) {
    $case = Join-Path $root $Name
    $source = Join-Path $case "source"
    New-Item -ItemType Directory -Path $source -Force | Out-Null
    $build = if ($FailBuild) {
        'Write-Output "Authorization: Bearer secret-token https://auth.example.test/device"; exit 7'
    } else {
        '$path = Join-Path $PSScriptRoot "bin\Release\net8.0-windows\win-x64\publish"; New-Item -ItemType Directory -Path $path -Force | Out-Null; Copy-Item -LiteralPath (Join-Path $env:SystemRoot "System32\cmd.exe") -Destination (Join-Path $path "CodexUsageMonitor.exe"); exit 0'
    }
    Set-Content -LiteralPath (Join-Path $source "build.ps1") -Value $build
    $codex = Join-Path $case "codex.cmd"
    @'
@echo off
if /i "%1"=="login" if /i "%2"=="status" (
  if /i "%FAKE_AUTH%"=="authenticated" exit /b 0
  if /i "%FAKE_AUTH%"=="unauthenticated" echo Not logged in 1>&2
  if /i "%FAKE_AUTH%"=="timeout" powershell.exe -NoProfile -Command "Start-Sleep -Seconds 30"
  exit /b 1
)
if /i "%1"=="login" (
  if defined FAKE_LOGIN_MARKER echo login>"%FAKE_LOGIN_MARKER%"
  exit /b 5
)
exit /b 2
'@ | Set-Content -LiteralPath $codex
    return [pscustomobject]@{ Root = $case; Source = $source; Codex = $codex }
}

function Add-PowerShellCodexShim($Fixture) {
    $shim = Join-Path $Fixture.Root "codex.ps1"
    @'
if ($args[0] -eq 'login' -and $args[1] -eq 'status') {
    if ($env:FAKE_AUTH -eq 'authenticated') { exit 0 }
    if ($env:FAKE_AUTH -eq 'unauthenticated') { [Console]::Error.WriteLine('Not logged in') }
    exit 1
}
if ($args[0] -eq 'login') { exit 5 }
exit 2
'@ | Set-Content -LiteralPath $shim
    $Fixture.Codex = $shim
    return $Fixture
}

function Invoke-InstallerCase($Fixture, [string]$Choice = "Skip", [string]$InstallDirectory = "",
    [string]$StartMenuDirectory = "", [switch]$IncludeShortcut, [switch]$IncludeLaunch,
    [switch]$UseStoredCodex) {
    $local = Join-Path $Fixture.Root "local"
    $temp = Join-Path $Fixture.Root "temp"
    New-Item -ItemType Directory -Path $local, $temp -Force | Out-Null
    $env:LOCALAPPDATA = $local
    $env:TEMP = $temp
    if ([string]::IsNullOrWhiteSpace($InstallDirectory)) { $InstallDirectory = Join-Path $Fixture.Root "installed" }
    if ([string]::IsNullOrWhiteSpace($StartMenuDirectory)) { $StartMenuDirectory = Join-Path $Fixture.Root "start-menu" }
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $installer,
        '-SourceRoot', $Fixture.Source, '-InstallDirectory', $InstallDirectory,
        '-StartMenuDirectory', $StartMenuDirectory, '-LoginChoice', $Choice,
        '-StartupHealthTimeoutSeconds', '1', '-AuthenticationTimeoutSeconds', '1')
    if ($UseStoredCodex) {
        $settingsDirectory = Join-Path $local 'CodexUsageMonitor'
        New-Item -ItemType Directory -Path $settingsDirectory -Force | Out-Null
        @{ CodexExecutable = $Fixture.Codex } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $settingsDirectory 'settings.json')
    } else { $arguments += @('-CodexExecutable', $Fixture.Codex) }
    if (-not $IncludeShortcut) { $arguments += '-SkipShortcut' }
    if (-not $IncludeLaunch) { $arguments += '-SkipLaunch' }
    & $shell @arguments
    return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Local = $local; Temp = $temp }
}

try {
    New-Item -ItemType Directory -Path $root -Force | Out-Null

    foreach ($state in @("authenticated", "unauthenticated", "unknown")) {
        $fixture = New-Fixture $state
        $env:FAKE_AUTH = $state
        $result = Invoke-InstallerCase $fixture
        Assert-Equal 0 $result.ExitCode "$state authentication must not block installation."
    }

    $fixture = New-Fixture "authentication-timeout"
    $env:FAKE_AUTH = "timeout"
    $env:FAKE_LOGIN_MARKER = Join-Path $fixture.Root "login-started.txt"
    $started = [Diagnostics.Stopwatch]::StartNew()
    $result = Invoke-InstallerCase $fixture "Login"
    $started.Stop()
    Assert-Equal 0 $result.ExitCode "Authentication timeout must not block installation."
    if ($started.Elapsed.TotalSeconds -ge 10) { throw "Authentication timeout did not stop the status process promptly." }
    if (Test-Path -LiteralPath $env:FAKE_LOGIN_MARKER) { throw "Authentication timeout incorrectly started login." }
    $env:FAKE_LOGIN_MARKER = $null

    $fixture = New-Fixture "authentication-start-failure"
    $fixture.Codex = Join-Path $fixture.Root "missing-codex.exe"
    $env:FAKE_AUTH = "unknown"
    $result = Invoke-InstallerCase $fixture
    Assert-Equal 0 $result.ExitCode "Authentication command start failure must remain advisory."

    $fixture = New-Fixture "configured-path"
    $env:FAKE_AUTH = "authenticated"
    $result = Invoke-InstallerCase $fixture "Skip" "" "" -UseStoredCodex
    Assert-Equal 0 $result.ExitCode "The Codex executable stored in settings must be respected."

    $fixture = Add-PowerShellCodexShim (New-Fixture "powershell-shim")
    $env:FAKE_AUTH = "authenticated"
    $result = Invoke-InstallerCase $fixture
    Assert-Equal 0 $result.ExitCode "A codex.ps1 shim must support authentication checks."

    $fixture = New-Fixture "login-cancel"
    $env:FAKE_AUTH = "unauthenticated"
    $result = Invoke-InstallerCase $fixture "Login"
    Assert-Equal 0 $result.ExitCode "A cancelled or failed CLI login must not cancel installation."

    $fixture = New-Fixture "build-failure" -FailBuild
    $result = Invoke-InstallerCase $fixture
    Assert-Equal 30 $result.ExitCode "Build failures must return the build-stage exit code."
    $failureLog = Get-Content -LiteralPath (Join-Path $result.Local "CodexUsageMonitor\install-last-error.log") -Raw
    if ($failureLog -match 'secret-token|auth\.example') { throw "The retained log contains authentication data." }
    if ($failureLog -match 'https?://|Authorization:\s*Bearer') { throw "The retained log contains an unredacted sensitive field." }
    if (Get-ChildItem -LiteralPath (Join-Path $result.Temp "CodexUsageMonitor") -Filter 'install-*.log' -ErrorAction SilentlyContinue) {
        throw "A per-run temporary log was retained."
    }

    $fixture = New-Fixture "replacement-failure"
    $blocked = Join-Path $fixture.Root "blocked"
    Set-Content -LiteralPath $blocked -Value "file"
    $result = Invoke-InstallerCase $fixture "Skip" $blocked
    Assert-Equal 40 $result.ExitCode "Replacement failures must return the replacement-stage exit code."

    $fixture = New-Fixture "shortcut-failure"
    $blocked = Join-Path $fixture.Root "blocked-start-menu"
    Set-Content -LiteralPath $blocked -Value "file"
    $result = Invoke-InstallerCase $fixture "Skip" "" (Join-Path $blocked "child") -IncludeShortcut
    Assert-Equal 50 $result.ExitCode "Shortcut failures must return the shortcut-stage exit code."

    $fixture = New-Fixture "launch-failure"
    $result = Invoke-InstallerCase $fixture "Skip" "" "" -IncludeLaunch
    Assert-Equal 60 $result.ExitCode "Launch failures must return the launch-stage exit code."

    $fixture = New-Fixture "dependency-failure"
    $env:PATH = ""
    $result = Invoke-InstallerCase $fixture
    Assert-Equal 10 $result.ExitCode "Dependency failures must return the dependency-stage exit code."
    $env:PATH = $originalPath

    Write-Host "Installer tests passed."
} finally {
    $env:LOCALAPPDATA = $originalLocalAppData
    $env:TEMP = $originalTemp
    $env:FAKE_AUTH = $originalFakeAuth
    $env:FAKE_LOGIN_MARKER = $originalFakeLoginMarker
    $env:PATH = $originalPath
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}

# Expected failure cases leave a nonzero native exit code. Report the suite's
# successful completion explicitly so CI does not reuse that scenario's code.
exit 0
