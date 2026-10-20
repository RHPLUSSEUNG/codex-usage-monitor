param(
    [string]$CodexExecutable,
    [ValidateSet("Ask", "Login", "Skip")][string]$LoginChoice = "Ask",
    [string]$SourceRoot = $PSScriptRoot,
    [string]$InstallDirectory = (Join-Path $env:LOCALAPPDATA "Programs\CodexUsageMonitor"),
    [string]$StartMenuDirectory = (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"),
    [ValidateRange(1, 120)][int]$StartupHealthTimeoutSeconds = 35,
    [ValidateRange(1, 60)][int]$AuthenticationTimeoutSeconds = 8,
    [switch]$SkipShortcut,
    [switch]$SkipLaunch
)

$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_UI_LANGUAGE = "en-US"

# Exit codes: dependency 10, build 30, replacement 40, shortcut 50,
# launch/health 60, unexpected 99. Authentication is advisory.
$exitCodes = @{ Dependency = 10; Build = 30; Replacement = 40; Shortcut = 50; Launch = 60; Unexpected = 99 }
$stage = "Dependency"
$temporaryLogDirectory = Join-Path $env:TEMP "CodexUsageMonitor"
$temporaryLog = Join-Path $temporaryLogDirectory ("install-" + [guid]::NewGuid().ToString("N") + ".log")
$failureLog = Join-Path $env:LOCALAPPDATA "CodexUsageMonitor\install-last-error.log"

function Protect-DiagnosticText([string]$Text) {
    if ($null -eq $Text) { return "" }
    $safe = $Text -replace '(?i)authorization\s*:\s*\S+.*$', 'Authorization: [REDACTED]'
    $safe = $safe -replace '(?i)\b(bearer|token|access_token|refresh_token)\b\s*[:=]\s*[^\s,;]+', '$1=[REDACTED]'
    return $safe -replace '(?i)https?://\S+', '[REDACTED URL]'
}

function Write-InstallLog([string]$Message) {
    $safe = Protect-DiagnosticText $Message
    Write-Host $safe
    Add-Content -LiteralPath $temporaryLog -Value ((Get-Date -Format o) + " " + $safe) -Encoding UTF8
}

function Quote-ProcessArgument([string]$Value) {
    if ($Value -notmatch '[\s"]') { return $Value }
    return '"' + ($Value -replace '(\\*)"', '$1$1\"' -replace '(\\+)$', '$1$1') + '"'
}

function Invoke-CapturedProcess([string]$FilePath, [string[]]$Arguments, [switch]$WriteOutput,
    [int]$TimeoutMilliseconds = 0) {
    $resolved = Get-Command $FilePath -ErrorAction Stop
    $program = $resolved.Source
    $processArguments = $Arguments
    $extension = [IO.Path]::GetExtension($program)
    if ($extension -match '^\.(cmd|bat)$') {
        $processArguments = @('/d', '/c', $program) + $Arguments
        $program = $env:ComSpec
    } elseif ($extension -ieq '.ps1') {
        $processArguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $program) + $Arguments
        $program = (Get-Process -Id $PID).Path
    }
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = $program
    $info.Arguments = (($processArguments | ForEach-Object { Quote-ProcessArgument $_ }) -join ' ')
    $info.WorkingDirectory = $SourceRoot
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $info
    [void]$process.Start()
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    $timedOut = $false
    if ($TimeoutMilliseconds -gt 0) {
        if (-not $process.WaitForExit($TimeoutMilliseconds)) {
            $timedOut = $true
            try {
                & (Join-Path $env:SystemRoot "System32\taskkill.exe") /PID $process.Id /T /F 2>$null | Out-Null
            } catch {
                try { $process.Kill() } catch {}
            }
        }
    } else {
        $process.WaitForExit()
    }
    if (-not $process.HasExited) {
        try { $process.Kill() } catch {}
    }
    $process.WaitForExit()
    $stdout = $stdoutTask.GetAwaiter().GetResult()
    $stderr = $stderrTask.GetAwaiter().GetResult()
    if ($WriteOutput) {
        @($stdout, $stderr) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            ForEach-Object { (Protect-DiagnosticText $_).TrimEnd() | Tee-Object -FilePath $temporaryLog -Append }
    }
    return [pscustomobject]@{
        ExitCode = $process.ExitCode
        Output = $stdout
        ErrorOutput = $stderr
        TimedOut = $timedOut
    }
}

function Resolve-CodexExecutable {
    if (-not [string]::IsNullOrWhiteSpace($CodexExecutable)) { return $CodexExecutable }
    $settingsPath = Join-Path $env:LOCALAPPDATA "CodexUsageMonitor\settings.json"
    if (Test-Path -LiteralPath $settingsPath) {
        try {
            $configured = (Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json).CodexExecutable
            if (-not [string]::IsNullOrWhiteSpace($configured)) { return [string]$configured }
        } catch {
            Write-InstallLog "Authentication: configured Codex path could not be read; using the default command."
        }
    }
    return "codex"
}

function Get-CodexAuthenticationState([string]$Executable) {
    try {
        $result = Invoke-CapturedProcess $Executable @("login", "status") `
            -TimeoutMilliseconds ($AuthenticationTimeoutSeconds * 1000)
        if ($result.TimedOut) { return "Unknown" }
        if ($result.ExitCode -eq 0) { return "Authenticated" }
        $combined = $result.Output + "`n" + $result.ErrorOutput
        if ($combined -match '(?i)not\s+(logged|signed)\s+in|unauthenticated|login required') { return "Unauthenticated" }
        return "Unknown"
    } catch { return "Unknown" }
}

try {
    New-Item -ItemType Directory -Path $temporaryLogDirectory -Force | Out-Null
    New-Item -ItemType File -Path $temporaryLog -Force | Out-Null

    Write-InstallLog "Dependency: checking the .NET 8 SDK."
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnet) { throw "The dotnet command was not found." }
    $sdks = Invoke-CapturedProcess $dotnet.Source @("--list-sdks")
    if ($sdks.ExitCode -ne 0 -or $sdks.Output -notmatch '(?m)^8\.') { throw "The .NET 8 SDK was not found." }

    $stage = "Authentication"
    $resolvedCodex = Resolve-CodexExecutable
    $authentication = Get-CodexAuthenticationState $resolvedCodex
    Write-InstallLog ("Authentication: " + $authentication.ToLowerInvariant() + ".")
    if ($authentication -eq "Unauthenticated") {
        $choice = $LoginChoice
        if ($choice -eq "Ask") {
            $answer = Read-Host "Codex CLI is not authenticated. Run the official CLI login now? [y/N]"
            $choice = if ($answer -match '^(?i)y(es)?$') { "Login" } else { "Skip" }
        }
        if ($choice -eq "Login") {
            Write-InstallLog "Authentication: starting the official Codex CLI login. Login cancellation does not cancel installation."
            try {
                & $resolvedCodex login
                Write-InstallLog ("Authentication: login command finished with exit code " + $LASTEXITCODE + ".")
            } catch { Write-InstallLog "Authentication: login could not be started; installation will continue." }
        } else { Write-InstallLog "Authentication: login skipped; installation will continue." }
    } elseif ($authentication -eq "Unknown") {
        Write-InstallLog "Authentication: status could not be confirmed; installation will continue without changing authentication."
    }

    $stage = "Build"
    Write-InstallLog "Build: creating the Windows x64 release executable."
    $buildScript = Join-Path $SourceRoot "build.ps1"
    if (-not (Test-Path -LiteralPath $buildScript)) { throw "build.ps1 was not found." }
    $powershell = (Get-Process -Id $PID).Path
    & $powershell -NoProfile -ExecutionPolicy Bypass -File $buildScript 2>&1 |
        ForEach-Object { Write-InstallLog ([string]$_) }
    $buildExitCode = $LASTEXITCODE
    if ($buildExitCode -ne 0) { throw "Build failed with exit code $buildExitCode." }
    $publishedExecutable = Join-Path $SourceRoot "bin\Release\net8.0-windows\win-x64\publish\CodexUsageMonitor.exe"
    if (-not (Test-Path -LiteralPath $publishedExecutable)) { throw "The published executable was not created." }

    $stage = "Replacement"
    Write-InstallLog "Replacement: copying the built executable."
    New-Item -ItemType Directory -Path $InstallDirectory -Force | Out-Null
    $installedExecutable = Join-Path $InstallDirectory "CodexUsageMonitor.exe"
    Copy-Item -LiteralPath $publishedExecutable -Destination $installedExecutable -Force

    if (-not $SkipShortcut) {
        $stage = "Shortcut"
        Write-InstallLog "Shortcut: creating the Start menu shortcut."
        New-Item -ItemType Directory -Path $StartMenuDirectory -Force | Out-Null
        $shortcutPath = Join-Path $StartMenuDirectory "Codex Usage Monitor.lnk"
        $shell = New-Object -ComObject WScript.Shell
        $shortcut = $shell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = $installedExecutable
        $shortcut.WorkingDirectory = $InstallDirectory
        $shortcut.IconLocation = "$installedExecutable,0"
        $shortcut.Save()
    }

    if (-not $SkipLaunch) {
        $stage = "Launch"
        Write-InstallLog "Launch: starting the application and waiting for its startup health signal."
        $healthDirectory = Join-Path $env:LOCALAPPDATA "CodexUsageMonitor\Updates"
        New-Item -ItemType Directory -Path $healthDirectory -Force | Out-Null
        $token = [guid]::NewGuid().ToString("N")
        $healthPath = Join-Path $healthDirectory ("install-health-" + $token + ".ready")
        Start-Process -FilePath $installedExecutable -ArgumentList @("--post-update", $token, $healthPath) | Out-Null
        $deadline = (Get-Date).AddSeconds($StartupHealthTimeoutSeconds)
        while ((Get-Date) -lt $deadline -and -not (Test-Path -LiteralPath $healthPath)) { Start-Sleep -Milliseconds 250 }
        if (-not (Test-Path -LiteralPath $healthPath)) { throw "The application did not report a healthy startup." }
        Remove-Item -LiteralPath $healthPath -Force -ErrorAction SilentlyContinue
    }

    Write-InstallLog "Installation complete. You can launch Codex Usage Monitor from the Start menu."
    Remove-Item -LiteralPath $failureLog -Force -ErrorAction SilentlyContinue
    exit 0
} catch {
    $code = if ($exitCodes.ContainsKey($stage)) { $exitCodes[$stage] } else { $exitCodes.Unexpected }
    Write-InstallLog ("$stage failed: " + $_.Exception.Message)
    New-Item -ItemType Directory -Path (Split-Path -Parent $failureLog) -Force | Out-Null
    Copy-Item -LiteralPath $temporaryLog -Destination $failureLog -Force
    Write-Host ("Sanitized diagnostic log: " + $failureLog)
    exit $code
} finally {
    # Per-run logs are removed. On failure only the sanitized last-error copy remains.
    Remove-Item -LiteralPath $temporaryLog -Force -ErrorAction SilentlyContinue
}
