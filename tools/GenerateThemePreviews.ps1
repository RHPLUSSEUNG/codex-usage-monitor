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
$panelType = $assembly.GetType("CodexUsageMonitor.Models.PanelId", $true)
$quotaType = $assembly.GetType("CodexUsageMonitor.Models.QuotaWindow", $true)
$snapshotType = $assembly.GetType("CodexUsageMonitor.Models.UsageSnapshot", $true)
$systemSnapshotType = $assembly.GetType("CodexUsageMonitor.Models.SystemUsageSnapshot", $true)
$rendererType = $assembly.GetType("CodexUsageMonitor.UI.CompactBarRenderer", $true)
$themeType = $assembly.GetType("CodexUsageMonitor.UI.CompactBarTheme", $true)

$calculateSize = $rendererType.GetMethod("CalculateSize", [Reflection.BindingFlags]"Public,Static")
$draw = $rendererType.GetMethod("Draw", [Reflection.BindingFlags]"Public,Static")
$layout = $rendererType.GetMethod("Layout", [Reflection.BindingFlags]"NonPublic,Static")
$defaultBackground = $themeType.GetMethod("DefaultBackground", [Reflection.BindingFlags]"Public,Static")
$defaultFill = $themeType.GetMethod("DefaultFill", [Reflection.BindingFlags]"Public,Static")
$defaultTrack = $themeType.GetMethod("DefaultTrack", [Reflection.BindingFlags]"Public,Static")
$paletteCatalogType = $assembly.GetType("CodexUsageMonitor.Models.CompactBarPaletteCatalog", $true)
$defaultVariant = $paletteCatalogType.GetMethod("DefaultVariant", [Reflection.BindingFlags]"Public,Static")
$paletteColors = $paletteCatalogType.GetMethod("Get", [Reflection.BindingFlags]"Public,Static")
$paletteDisplayName = $paletteCatalogType.GetMethod("DisplayName", [Reflection.BindingFlags]"Public,Static")

$previewNow = [DateTimeOffset]::Now
$fiveHour = [Activator]::CreateInstance(
    $quotaType, [object[]]@([double]95, [int]300, [Nullable[DateTimeOffset]]$previewNow.AddHours(2).AddMinutes(1)))
$weekly = [Activator]::CreateInstance(
    $quotaType, [object[]]@([double]26, [int]10080, [Nullable[DateTimeOffset]]$previewNow.AddDays(4).AddHours(1)))
$snapshot = [Activator]::CreateInstance(
    $snapshotType,
    [object[]]@($fiveHour, $weekly, $null, $null, [DateTimeOffset]::Now, $null, $null, $null))
$systemSnapshot = [Activator]::CreateInstance(
    $systemSnapshotType,
    [object[]]@([Nullable[double]]36, [Nullable[double]]75))

$styles = @(
    @{ Name = "Light"; File = "light"; Label = "Light" },
    @{ Name = "LabelBoxes"; File = "label-boxes"; Label = "Label boxes" },
    @{ Name = "NeonGlow"; File = "neon-glow"; Label = "Neon glow" },
    @{ Name = "CompactRows"; File = "compact-rows"; Label = "Compact rows" },
    @{ Name = "Gradient"; File = "gradient"; Label = "Gradient" },
    @{ Name = "MinimalIcons"; File = "minimal-icons"; Label = "Minimal icons + text" }
)

function New-Settings([string]$styleName, [string]$variantName, [string]$paletteName = "Codex") {
    $settings = [Activator]::CreateInstance($appSettingsType)
    $settings.CompactBarStyle = [Enum]::Parse($styleType, $styleName)
    $settings.ThemeVariant = [Enum]::Parse($variantType, $variantName)
    $settings.FollowSystemTheme = $false
    $settings.CodexPalette = [Enum]::Parse($paletteType, $paletteName)
    $settings.BackgroundColor = $defaultBackground.Invoke(
        $null,
        [object[]]@($settings.CompactBarStyle, $settings.CodexPalette, $settings.ThemeVariant))
    $fill = $defaultFill.Invoke($null, [object[]]@($settings.CodexPalette, $settings.ThemeVariant))
    $track = $defaultTrack.Invoke($null, [object[]]@($settings.CodexPalette, $settings.ThemeVariant))
    foreach ($metricName in @("FiveHour", "Weekly", "Cpu", "Memory")) {
        $settings.$metricName.FillColor = $fill
        $settings.$metricName.TrackColor = $track
    }
    $settings.FiveHour.ShowResetTime = $false
    $settings.Weekly.ShowResetTime = $false
    $settings.TransparentBackground = $false
    $settings.ShowCompactBar = $true
    return $settings
}

function New-BarBitmapFromSettings([object]$settings) {
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
            [object[]]@($graphics, $size, $settings, $snapshot, $systemSnapshot, $scale, $false))
    }
    finally {
        $graphics.Dispose()
    }
    return $bitmap
}

function New-BarBitmap([string]$styleName, [string]$variantName, [string]$paletteName = "Codex") {
    $settings = New-Settings $styleName $variantName $paletteName | Select-Object -Last 1
    return New-BarBitmapFromSettings $settings
}

function New-StyleCard([hashtable]$style) {
    $bitmap = $null
    try {
        $bitmap = New-BarBitmap $style.Name "Dark" "Everforest" | Select-Object -Last 1
        if ($bitmap -is [Management.Automation.PSObject]) {
            $bitmap = $bitmap.PSObject.BaseObject
        }
        $card = [Drawing.Bitmap]::new(
            $bitmap.Width + 48,
            $bitmap.Height + 70,
            [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [Drawing.Graphics]::FromImage($card)
        try {
            $graphics.Clear([Drawing.Color]::FromArgb(45, 53, 59))
            $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $titleFont = [Drawing.Font]::new("Segoe UI", 15, [Drawing.FontStyle]::Bold)
            $titleBrush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(211, 198, 170))
            $borderPen = [Drawing.Pen]::new([Drawing.Color]::FromArgb(71, 82, 88), 1)
            try {
                $graphics.DrawRectangle($borderPen, 1, 1, $card.Width - 3, $card.Height - 3)
                $graphics.DrawString($style.Label, $titleFont, $titleBrush, 20, 8)
                $graphics.DrawImage(
                    $bitmap,
                    [Drawing.Rectangle]::new(24, 38, $bitmap.Width, $bitmap.Height))
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

function New-PanelOrderPreview {
    $settings = New-Settings "Light" "Dark" "Everforest" | Select-Object -Last 1
    if ($settings -is [Management.Automation.PSObject]) { $settings = $settings.PSObject.BaseObject }
    $settings.FiveHour.ShowResetTime = $true
    $settings.Weekly.ShowResetTime = $true
    $settings.FiveHour.ShowResetTimeLabel = $false
    $settings.Weekly.ShowResetTimeLabel = $false

    $bar = New-BarBitmapFromSettings $settings | Select-Object -Last 1
    if ($bar -is [Management.Automation.PSObject]) { $bar = $bar.PSObject.BaseObject }
    try {
        $panels = $layout.Invoke($null, [object[]]@($settings, [single]2, [int]96))
        $source = $panels | Where-Object { $_.Panel.ToString() -eq "FiveHour" }
        $target = $panels | Where-Object { $_.Panel.ToString() -eq "Cpu" }
        $origin = [Drawing.Point]::new(24, 42)
        $sourceBounds = [Drawing.Rectangle]::new(
            $origin.X + $source.Bounds.X, $origin.Y + $source.Bounds.Y,
            $source.Bounds.Width, $source.Bounds.Height)
        $targetBounds = [Drawing.Rectangle]::new(
            $origin.X + $target.Bounds.X, $origin.Y + $target.Bounds.Y,
            $target.Bounds.Width, $target.Bounds.Height)
        $ghostBounds = $sourceBounds
        $ghostBounds.Offset([int](($targetBounds.X - $sourceBounds.X) * .62), 14)

        $card = [Drawing.Bitmap]::new($bar.Width + 48, $bar.Height + 70,
            [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [Drawing.Graphics]::FromImage($card)
        try {
            $surface = [Drawing.Color]::FromArgb(45, 53, 59)
            $foreground = [Drawing.Color]::FromArgb(211, 198, 170)
            $graphics.Clear($surface)
            $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $titleFont = [Drawing.Font]::new("Segoe UI", 12, [Drawing.FontStyle]::Bold)
            $textBrush = [Drawing.SolidBrush]::new($foreground)
            try {
                $graphics.DrawString("Appearance preview · dragging 5H", $titleFont, $textBrush, 24, 10)
                $graphics.DrawImage($bar,
                    [Drawing.Rectangle]::new($origin.X, $origin.Y, $bar.Width, $bar.Height))

                $fade = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(198, $surface))
                $targetFill = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(42, $foreground))
                $targetOutline = [Drawing.Pen]::new($foreground, 2)
                $ghostOutline = [Drawing.Pen]::new($foreground, 1)
                $attributes = [Drawing.Imaging.ImageAttributes]::new()
                try {
                    $targetOutline.DashStyle = [Drawing.Drawing2D.DashStyle]::Dash
                    $graphics.FillRectangle($fade, $sourceBounds)
                    $graphics.FillRectangle($targetFill, $targetBounds)
                    $graphics.DrawRectangle($targetOutline, $targetBounds)
                    $matrix = [Drawing.Imaging.ColorMatrix]::new()
                    $matrix.Matrix33 = .72
                    $attributes.SetColorMatrix($matrix)
                    $graphics.DrawImage($bar, $ghostBounds,
                        $source.Bounds.X, $source.Bounds.Y, $source.Bounds.Width, $source.Bounds.Height,
                        [Drawing.GraphicsUnit]::Pixel, $attributes)
                    $graphics.DrawRectangle($ghostOutline, $ghostBounds)
                }
                finally {
                    $attributes.Dispose()
                    $ghostOutline.Dispose()
                    $targetOutline.Dispose()
                    $targetFill.Dispose()
                    $fade.Dispose()
                }
            }
            finally {
                $textBrush.Dispose()
                $titleFont.Dispose()
            }
        }
        finally { $graphics.Dispose() }
        return $card
    }
    finally { $bar.Dispose() }
}

function New-ResetTooltipPreview {
    $bar = New-BarBitmap "Light" "Dark" "Everforest" | Select-Object -Last 1
    if ($bar -is [Management.Automation.PSObject]) { $bar = $bar.PSObject.BaseObject }
    try {
        $tooltipText = "5-hour · resets at 2026-10-08 14:30`nWeekly · resets at 2026-10-12 09:00"
        $font = [Drawing.Font]::new("Segoe UI", 9, [Drawing.FontStyle]::Regular)
        $measureBitmap = [Drawing.Bitmap]::new(1, 1)
        $measureBitmap.SetResolution(192, 192)
        $measureGraphics = [Drawing.Graphics]::FromImage($measureBitmap)
        try { $measured = $measureGraphics.MeasureString($tooltipText, $font) }
        finally {
            $measureGraphics.Dispose()
            $measureBitmap.Dispose()
        }
        $margin = 12
        $tooltipWidth = [int][Math]::Ceiling($measured.Width) + 24
        $tooltipHeight = [int][Math]::Ceiling($measured.Height) + 16
        $gap = 2
        $card = [Drawing.Bitmap]::new($bar.Width + $margin * 2, $bar.Height + $tooltipHeight + $gap + 16,
            [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $card.SetResolution(192, 192)
        $graphics = [Drawing.Graphics]::FromImage($card)
        try {
            $graphics.Clear([Drawing.Color]::FromArgb(45, 53, 59))
            $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $barX = $margin
            $barY = 8 + $tooltipHeight + $gap
            $tooltipX = $barX + $bar.Width - $tooltipWidth
            $tooltipY = $barY - $tooltipHeight - $gap
            $tooltipBounds = [Drawing.Rectangle]::new($tooltipX, $tooltipY, $tooltipWidth, $tooltipHeight)
            $fill = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(52, 63, 68))
            $border = [Drawing.Pen]::new([Drawing.Color]::FromArgb(167, 192, 128), 1)
            $text = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(211, 198, 170))
            try {
                $graphics.FillRectangle($fill, $tooltipBounds)
                $graphics.DrawRectangle($border, $tooltipBounds)
                $graphics.DrawString($tooltipText, $font, $text, $tooltipX + 12, $tooltipY + 8)
                $graphics.DrawImage($bar, [Drawing.Rectangle]::new($barX, $barY, $bar.Width, $bar.Height))
            }
            finally {
                $text.Dispose()
                $border.Dispose()
                $fill.Dispose()
            }
        }
        finally { $graphics.Dispose() }
        return $card
    }
    finally {
        if ($font -is [IDisposable]) { $font.Dispose() }
        $bar.Dispose()
    }
}

function New-ColorThemePreview {
    $palettes = [Enum]::GetValues($paletteType)
    $columns = 2
    $rows = [int][Math]::Ceiling($palettes.Count / $columns)
    $columnWidth = 560
    $rowHeight = 112
    $card = [Drawing.Bitmap]::new($columnWidth * $columns, $rowHeight * $rows + 12,
        [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($card)
    try {
        $graphics.Clear([Drawing.Color]::FromArgb(49, 55, 59))
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $nameFont = [Drawing.Font]::new("Segoe UI", 11, [Drawing.FontStyle]::Bold)
        $badgeFont = [Drawing.Font]::new("Segoe UI", 10, [Drawing.FontStyle]::Bold)
        $nameBrush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(226, 219, 198))
        try {
            for ($index = 0; $index -lt $palettes.Count; $index++) {
                $palette = $palettes[$index]
                $variant = $defaultVariant.Invoke($null, [object[]]@($palette))
                $variantName = $variant.ToString()
                $bar = New-BarBitmap "Light" $variantName $palette.ToString() | Select-Object -Last 1
                if ($bar -is [Management.Automation.PSObject]) { $bar = $bar.PSObject.BaseObject }
                try {
                    $column = [int][Math]::Floor($index / [double]$rows)
                    $row = $index % $rows
                    $x = $column * $columnWidth + 16
                    $y = $row * $rowHeight + 10
                    $colors = $paletteColors.Invoke($null, [object[]]@($palette, $variant))
                    $badgeColor = [Drawing.ColorTranslator]::FromHtml($colors.Background)
                    $accentColor = [Drawing.ColorTranslator]::FromHtml($colors.Accent)
                    $badge = [Drawing.Rectangle]::new($x, $y + 2, 32, 32)
                    $badgeBrush = [Drawing.SolidBrush]::new($badgeColor)
                    $badgeText = [Drawing.SolidBrush]::new($accentColor)
                    try {
                        $graphics.FillEllipse($badgeBrush, $badge)
                        $format = [Drawing.StringFormat]::new()
                        try {
                            $format.Alignment = [Drawing.StringAlignment]::Center
                            $format.LineAlignment = [Drawing.StringAlignment]::Center
                            $badgeTextBounds = [Drawing.RectangleF]::new(
                                [single]$badge.X, [single]$badge.Y, [single]$badge.Width, [single]$badge.Height)
                            $graphics.DrawString("Aa", $badgeFont, $badgeText, $badgeTextBounds, $format)
                        }
                        finally { $format.Dispose() }
                    }
                    finally {
                        $badgeText.Dispose()
                        $badgeBrush.Dispose()
                    }
                    $name = $paletteDisplayName.Invoke($null, [object[]]@($palette))
                    $graphics.DrawString($name, $nameFont, $nameBrush, $x + 42, $y + 5)
                    $target = [Drawing.Rectangle]::new($x, $y + 40, 500, 64)
                    $target.Height = 64
                    $target.Width = [int]($bar.Width * ($target.Height / [double]$bar.Height))
                    $graphics.DrawImage($bar, $target)
                }
                finally { $bar.Dispose() }
            }
        }
        finally {
            $nameBrush.Dispose()
            $badgeFont.Dispose()
            $nameFont.Dispose()
        }
    }
    finally { $graphics.Dispose() }
    return $card
}

function New-ScreenModePreview {
    $light = New-BarBitmap "Light" "Light" "Everforest" | Select-Object -Last 1
    $dark = New-BarBitmap "Light" "Dark" "Everforest" | Select-Object -Last 1
    if ($light -is [Management.Automation.PSObject]) { $light = $light.PSObject.BaseObject }
    if ($dark -is [Management.Automation.PSObject]) { $dark = $dark.PSObject.BaseObject }
    try {
        $cardWidth = 320
        $cardHeight = 116
        $gap = 12
        $canvas = [Drawing.Bitmap]::new($cardWidth * 3 + $gap * 2, $cardHeight,
            [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [Drawing.Graphics]::FromImage($canvas)
        try {
            $surface = [Drawing.Color]::FromArgb(45, 53, 59)
            $cardFill = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(52, 63, 68))
            $selectedBorder = [Drawing.Pen]::new([Drawing.Color]::FromArgb(167, 192, 128), 2)
            $labelBrush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(211, 198, 170))
            $labelFont = [Drawing.Font]::new("Segoe UI", 11, [Drawing.FontStyle]::Regular)
            $format = [Drawing.StringFormat]::new()
            try {
                $graphics.Clear($surface)
                $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
                $format.Alignment = [Drawing.StringAlignment]::Center
                $format.LineAlignment = [Drawing.StringAlignment]::Center
                $labels = @("System", "Dark", "Light")
                for ($index = 0; $index -lt 3; $index++) {
                    $x = $index * ($cardWidth + $gap)
                    $bounds = [Drawing.Rectangle]::new($x, 0, $cardWidth, $cardHeight)
                    $graphics.FillRectangle($cardFill, $bounds)
                    if ($index -eq 1) {
                        $graphics.DrawRectangle($selectedBorder, $x + 1, 1, $cardWidth - 3, $cardHeight - 3)
                    }
                    $target = [Drawing.Rectangle]::new($x + 14, 14, $cardWidth - 28, 58)
                    if ($index -eq 0) {
                        $left = [Drawing.Rectangle]::new($target.X, $target.Y, [int]($target.Width / 2), $target.Height)
                        $right = [Drawing.Rectangle]::new($left.Right, $target.Y, $target.Width - $left.Width, $target.Height)
                        $graphics.DrawImage($light, $left, 0, 0, [int]($light.Width / 2), $light.Height, [Drawing.GraphicsUnit]::Pixel)
                        $graphics.DrawImage($dark, $right, [int]($dark.Width / 2), 0, $dark.Width - [int]($dark.Width / 2), $dark.Height, [Drawing.GraphicsUnit]::Pixel)
                    }
                    elseif ($index -eq 1) { $graphics.DrawImage($dark, $target) }
                    else { $graphics.DrawImage($light, $target) }
                    $labelBounds = [Drawing.RectangleF]::new([single]$x, 78, [single]$cardWidth, 30)
                    $graphics.DrawString($labels[$index], $labelFont, $labelBrush, $labelBounds, $format)
                }
            }
            finally {
                $format.Dispose()
                $labelFont.Dispose()
                $labelBrush.Dispose()
                $selectedBorder.Dispose()
                $cardFill.Dispose()
            }
        }
        finally { $graphics.Dispose() }
        return $canvas
    }
    finally {
        $dark.Dispose()
        $light.Dispose()
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

    $representative = New-BarBitmap "Light" "Dark" "Everforest" | Select-Object -Last 1
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

    $panelOrder = New-PanelOrderPreview | Select-Object -Last 1
    try {
        $panelOrder.Save((Join-Path $projectRoot "docs\panel-ordering.png"), [Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $panelOrder.Dispose() }

    $resetTooltip = New-ResetTooltipPreview | Select-Object -Last 1
    try {
        $resetTooltip.Save((Join-Path $projectRoot "docs\reset-tooltip.png"), [Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $resetTooltip.Dispose() }

    $colorThemes = New-ColorThemePreview | Select-Object -Last 1
    try {
        $colorThemes.Save((Join-Path $projectRoot "docs\color-themes.png"), [Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $colorThemes.Dispose() }

    $screenModes = New-ScreenModePreview | Select-Object -Last 1
    try {
        $screenModes.Save((Join-Path $projectRoot "docs\screen-modes.png"), [Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $screenModes.Dispose() }
}
finally {
    foreach ($card in $cards) {
        $card.Dispose()
    }
}

Write-Host "Style previews generated in $outputDirectory"
Write-Host "Representative image generated at $(Join-Path $projectRoot 'docs\compact-bar.png')"
Write-Host "Feature previews generated in $(Join-Path $projectRoot 'docs')"
