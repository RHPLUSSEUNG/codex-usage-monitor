#requires -Version 7.0

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$projectRoot = Split-Path $PSScriptRoot -Parent
$assemblyPath = Join-Path $projectRoot "bin\$Configuration\net8.0-windows\CodexUsageMonitor.dll"
$outputDirectory = Join-Path $projectRoot "docs\themes"

if (-not (Test-Path $assemblyPath)) {
    dotnet build $projectRoot --configuration $Configuration
}

$assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
$appSettingsType = $assembly.GetType("CodexUsageMonitor.Models.AppSettings", $true)
$styleType = $assembly.GetType("CodexUsageMonitor.Models.CompactBarStyle", $true)
$variantType = $assembly.GetType("CodexUsageMonitor.Models.ThemeVariant", $true)
$quotaType = $assembly.GetType("CodexUsageMonitor.Models.QuotaWindow", $true)
$snapshotType = $assembly.GetType("CodexUsageMonitor.Models.UsageSnapshot", $true)
$systemSnapshotType = $assembly.GetType("CodexUsageMonitor.Models.SystemUsageSnapshot", $true)
$rendererType = $assembly.GetType("CodexUsageMonitor.UI.CompactBarRenderer", $true)
$themeType = $assembly.GetType("CodexUsageMonitor.UI.CompactBarTheme", $true)

$calculateSize = $rendererType.GetMethod("CalculateSize", [Reflection.BindingFlags]"Public,Static")
$draw = $rendererType.GetMethod("Draw", [Reflection.BindingFlags]"Public,Static")
$defaultBackground = $themeType.GetMethod("DefaultBackground", [Reflection.BindingFlags]"Public,Static")

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
    $settings.BackgroundColor = $defaultBackground.Invoke(
        $null,
        [object[]]@($settings.CompactBarStyle, $settings.ThemeVariant))
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
    $dark = New-BarBitmap $style.Name "Dark" | Select-Object -Last 1
    $light = New-BarBitmap $style.Name "Light" | Select-Object -Last 1
    if ($dark -is [Management.Automation.PSObject]) {
        $dark = $dark.PSObject.BaseObject
    }
    if ($light -is [Management.Automation.PSObject]) {
        $light = $light.PSObject.BaseObject
    }
    try {
        $contentWidth = [Math]::Max($dark.Width, $light.Width)
        $rowHeight = [Math]::Max($dark.Height, $light.Height)
        $card = [Drawing.Bitmap]::new(
            $contentWidth + 96,
            76 + ($rowHeight * 2) + 44,
            [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [Drawing.Graphics]::FromImage($card)
        try {
            $graphics.Clear([Drawing.Color]::FromArgb(255, 13, 17, 23))
            $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $titleFont = [Drawing.Font]::new("Segoe UI", 15, [Drawing.FontStyle]::Bold)
            $labelFont = [Drawing.Font]::new("Segoe UI", 9, [Drawing.FontStyle]::Bold)
            $titleBrush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(240, 246, 252))
            $labelBrush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(139, 148, 158))
            $borderPen = [Drawing.Pen]::new([Drawing.Color]::FromArgb(48, 54, 61), 2)
            try {
                $graphics.DrawRectangle($borderPen, 1, 1, $card.Width - 3, $card.Height - 3)
                $graphics.DrawString($style.Label, $titleFont, $titleBrush, 28, 18)
                $graphics.DrawString("BLACK", $labelFont, $labelBrush, 28, 58)
                $graphics.DrawImage($dark, 48, 82)
                $lightY = 90 + $rowHeight
                $graphics.DrawString("WHITE", $labelFont, $labelBrush, 28, $lightY)
                $graphics.DrawImage($light, 48, $lightY + 24)
            }
            finally {
                $borderPen.Dispose()
                $labelBrush.Dispose()
                $titleBrush.Dispose()
                $labelFont.Dispose()
                $titleFont.Dispose()
            }
        }
        finally {
            $graphics.Dispose()
        }
        return $card
    }
    finally {
        if ($light -is [IDisposable]) {
            ([IDisposable]$light).Dispose()
        }
        if ($dark -is [IDisposable]) {
            ([IDisposable]$dark).Dispose()
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

    $columns = 2
    $cellWidth = 840
    $cellHeight = 360
    $headerHeight = 170
    $overview = [Drawing.Bitmap]::new(
        $columns * $cellWidth,
        $headerHeight + 5 * $cellHeight,
        [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($overview)
    try {
        $graphics.Clear([Drawing.Color]::FromArgb(255, 8, 12, 18))
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::HighQuality
        $titleFont = [Drawing.Font]::new("Segoe UI", 28, [Drawing.FontStyle]::Bold)
        $subtitleFont = [Drawing.Font]::new("Segoe UI", 13, [Drawing.FontStyle]::Regular)
        $titleBrush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(240, 246, 252))
        $subtitleBrush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(139, 148, 158))
        try {
            $graphics.DrawString("Codex Usage Monitor", $titleFont, $titleBrush, 54, 36)
            $graphics.DrawString(
                "10 Compact Bar styles  |  Black + White themes",
                $subtitleFont,
                $subtitleBrush,
                58,
                92)

            for ($index = 0; $index -lt $cards.Count; $index++) {
                $column = $index % $columns
                $row = [Math]::Floor($index / $columns)
                $card = $cards[$index]
                $availableWidth = $cellWidth - 48
                $availableHeight = $cellHeight - 36
                $ratio = [Math]::Min(
                    $availableWidth / $card.Width,
                    $availableHeight / $card.Height)
                $width = [int]($card.Width * $ratio)
                $height = [int]($card.Height * $ratio)
                $x = ($column * $cellWidth) + [int](($cellWidth - $width) / 2)
                $y = $headerHeight + ($row * $cellHeight) + [int](($cellHeight - $height) / 2)
                $graphics.DrawImage($card, $x, $y, $width, $height)
            }
        }
        finally {
            $subtitleBrush.Dispose()
            $titleBrush.Dispose()
            $subtitleFont.Dispose()
            $titleFont.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
    }

    try {
        $overview.Save(
            (Join-Path $outputDirectory "overview.png"),
            [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $overview.Dispose()
    }
}
finally {
    foreach ($card in $cards) {
        $card.Dispose()
    }
}

Write-Host "Theme previews generated in $outputDirectory"
