#requires -Version 7.0

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_UI_LANGUAGE = "en-US"
Add-Type -AssemblyName System.Drawing

$projectRoot = Split-Path $PSScriptRoot -Parent
$projectPath = Join-Path $projectRoot "CodexUsageMonitor.csproj"
$assemblyPath = Join-Path $projectRoot "bin\$Configuration\net8.0-windows\CodexUsageMonitor.dll"
$outputDirectory = Join-Path $projectRoot "docs\themes"

& dotnet build $projectPath --configuration $Configuration --no-incremental
if ($LASTEXITCODE -ne 0) {
    throw "Failed to build CodexUsageMonitor before generating previews."
}
if (-not (Test-Path $assemblyPath)) {
    throw "Renderer assembly was not created at $assemblyPath"
}

$assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
$appSettingsType = $assembly.GetType("CodexUsageMonitor.Models.AppSettings", $true)
$styleType = $assembly.GetType("CodexUsageMonitor.Models.CompactBarStyle", $true)
$variantType = $assembly.GetType("CodexUsageMonitor.Models.ThemeVariant", $true)
$paletteType = $assembly.GetType("CodexUsageMonitor.Models.CodexPalette", $true)
$quotaType = $assembly.GetType("CodexUsageMonitor.Models.QuotaWindow", $true)
$snapshotType = $assembly.GetType("CodexUsageMonitor.Models.UsageSnapshot", $true)
$systemSnapshotType = $assembly.GetType("CodexUsageMonitor.Models.SystemUsageSnapshot", $true)
$rendererType = $assembly.GetType("CodexUsageMonitor.UI.CompactBarRenderer", $true)
$themeType = $assembly.GetType("CodexUsageMonitor.UI.CompactBarTheme", $true)

$calculateSize = $rendererType.GetMethod("CalculateSize", [Reflection.BindingFlags]"Public,Static")
$draw = $rendererType.GetMethod("Draw", [Reflection.BindingFlags]"Public,Static")
$defaultBackground = $themeType.GetMethod("DefaultBackground", [Reflection.BindingFlags]"Public,Static")
$defaultFill = $themeType.GetMethod("DefaultFill", [Reflection.BindingFlags]"Public,Static")
$defaultTrack = $themeType.GetMethod("DefaultTrack", [Reflection.BindingFlags]"Public,Static")

$fiveHour = [Activator]::CreateInstance($quotaType, [object[]]@([double]95, [int]300, $null))
$weekly = [Activator]::CreateInstance($quotaType, [object[]]@([double]26, [int]10080, $null))
$snapshot = [Activator]::CreateInstance(
    $snapshotType,
    [object[]]@($fiveHour, $weekly, $null, $null, [DateTimeOffset]::Now, $null))
$systemSnapshot = [Activator]::CreateInstance(
    $systemSnapshotType,
    [object[]]@([Nullable[double]]36, [Nullable[double]]75))

$styles = @(
    @{ Name = "DarkMinimal"; File = "dark-minimal"; Label = "Dark minimal" },
    @{ Name = "LabelBoxes"; File = "label-boxes"; Label = "Label boxes" },
    @{ Name = "NeonGlow"; File = "neon-glow"; Label = "Neon glow" },
    @{ Name = "Light"; File = "light"; Label = "Light" },
    @{ Name = "Cards"; File = "cards"; Label = "Cards" },
    @{ Name = "CircularGauges"; File = "circular-gauges"; Label = "Circular gauges" },
    @{ Name = "CompactRows"; File = "compact-rows"; Label = "Compact rows" },
    @{ Name = "RoundedCapsules"; File = "rounded-capsules"; Label = "Rounded capsules" },
    @{ Name = "Gradient"; File = "gradient"; Label = "Gradient" },
    @{ Name = "MinimalIcons"; File = "minimal-icons"; Label = "Minimal icons + text" }
)

function New-Settings([string]$styleName, [string]$variantName) {
    $settings = [Activator]::CreateInstance($appSettingsType)
    $settings.CompactBarStyle = [Enum]::Parse($styleType, $styleName)
    $settings.ThemeVariant = [Enum]::Parse($variantType, $variantName)
    $settings.CodexPalette = [Enum]::Parse($paletteType, "Codex")
    $settings.BackgroundColor = $defaultBackground.Invoke(
        $null,
        [object[]]@($settings.CompactBarStyle, $settings.CodexPalette, $settings.ThemeVariant))
    $fill = $defaultFill.Invoke($null, [object[]]@($settings.CodexPalette, $settings.ThemeVariant))
    $track = $defaultTrack.Invoke($null, [object[]]@($settings.CodexPalette, $settings.ThemeVariant))
    foreach ($metricName in @("FiveHour", "Weekly", "Cpu", "Memory")) {
        $settings.$metricName.FillColor = $fill
        $settings.$metricName.TrackColor = $track
    }
    $settings.ShowCompactBar = $true
    return $settings
}

function New-BarBitmap([string]$styleName, [string]$variantName) {
    $settings = New-Settings $styleName $variantName | Select-Object -Last 1
    if ($settings -is [Management.Automation.PSObject]) {
        $settings = $settings.PSObject.BaseObject
    }
    $scale = [single]2
    $size = $calculateSize.Invoke($null, [object[]]@($settings, $scale, [int]96))
    $bitmap = [Drawing.Bitmap]::new($size.Width, $size.Height, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bitmap.SetResolution(192, 192)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([Drawing.Color]::Transparent)
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.TextRenderingHint = [Drawing.Text.TextRenderingHint]::ClearTypeGridFit
        $draw.Invoke(
            $null,
            [object[]]@($graphics, $size, $settings, $snapshot, $systemSnapshot, $scale))
    }
    finally {
        $graphics.Dispose()
    }
    return $bitmap
}

function New-StyleCard([hashtable]$style) {
    $bitmap = $null
    try {
        $bitmap = New-BarBitmap $style.Name "Light" | Select-Object -Last 1
        if ($bitmap -is [Management.Automation.PSObject]) {
            $bitmap = $bitmap.PSObject.BaseObject
        }
        $card = [Drawing.Bitmap]::new(
            $bitmap.Width + 96,
            $bitmap.Height + 96,
            [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [Drawing.Graphics]::FromImage($card)
        try {
            $graphics.Clear([Drawing.Color]::White)
            $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $titleFont = [Drawing.Font]::new("Segoe UI", 15, [Drawing.FontStyle]::Bold)
            $titleBrush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(13, 13, 13))
            $borderPen = [Drawing.Pen]::new([Drawing.Color]::FromArgb(217, 217, 217), 2)
            try {
                $graphics.DrawRectangle($borderPen, 1, 1, $card.Width - 3, $card.Height - 3)
                $graphics.DrawString($style.Label, $titleFont, $titleBrush, 28, 18)
                $graphics.DrawImage(
                    $bitmap,
                    [Drawing.Rectangle]::new(48, 60, $bitmap.Width, $bitmap.Height))
            }
            finally {
                $borderPen.Dispose()
                $titleBrush.Dispose()
                $titleFont.Dispose()
            }
        }
        finally {
            $graphics.Dispose()
        }
        return $card
    }
    finally {
        if ($bitmap -is [IDisposable]) {
            ([IDisposable]$bitmap).Dispose()
        }
    }
}

New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$cards = @()

try {
    foreach ($style in $styles) {
        $card = New-StyleCard $style
        $cards += $card
        $card.Save(
            (Join-Path $outputDirectory "$($style.File).png"),
            [Drawing.Imaging.ImageFormat]::Png)
    }

    $representative = New-BarBitmap "LabelBoxes" "Light" | Select-Object -Last 1
    if ($representative -is [Management.Automation.PSObject]) {
        $representative = $representative.PSObject.BaseObject
    }
    try {
        $representative.Save(
            (Join-Path $projectRoot "docs\compact-bar.png"),
            [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        if ($representative -is [IDisposable]) {
            ([IDisposable]$representative).Dispose()
        }
    }
}
finally {
    foreach ($card in $cards) {
        $card.Dispose()
    }
}

Write-Host "Style previews generated in $outputDirectory"
Write-Host "Representative image generated at $(Join-Path $projectRoot 'docs\compact-bar.png')"
