# Codex Usage Monitor

[English](README.md) | [한국어](README.ko.md) | [简体中文](README.zh-CN.md) | [日本語](README.ja.md)

<p align="center">
  <img src="Resources/CodexUsageMonitor.ico" alt="Codex Usage Monitor icon" width="128">
</p>

A Windows 10/11 tray application that shows Codex quota usage together with current CPU and RAM usage.

![Codex Usage Monitor compact bar](docs/compact-bar.png)

The Compact Bar uses two columns: `5H` and `WK` in the first column, and `CPU` and `RAM` in the second. Each item can show a percentage, a fill bar, or both. Drag the bar anywhere on the desktop to save its position.

## Before installation

You need:

- Windows 10 or Windows 11 (64-bit)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build the downloaded source
- Codex CLI installed and signed in to ChatGPT

Open PowerShell and prepare Codex CLI. Skip the installation command if `codex` is already installed.

```powershell
npm install -g @openai/codex
codex login
codex login status
```

## Easy installation

1. On GitHub, select **Code → Download ZIP**, then extract the ZIP. Alternatively, clone the repository.
2. Open the extracted `codex-usage-monitor` folder.
3. Double-click `install.cmd`.
4. Wait for the build to finish. The installer launches the app automatically.
5. For later launches, open the Windows Start menu and search for **Codex Usage Monitor**.

The installer creates:

```text
Application: %LOCALAPPDATA%\Programs\CodexUsageMonitor\CodexUsageMonitor.exe
Shortcut:    Start menu\Programs\Codex Usage Monitor
Settings:    %LOCALAPPDATA%\CodexUsageMonitor\settings.json
```

To update, download the new source, exit the running app from its tray menu, and run `install.cmd` again. Existing settings remain compatible.

## Manual build

```powershell
dotnet build
```

To create the self-contained single-file executable used by the installer:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Output: `bin\Release\net8.0-windows\win-x64\publish\CodexUsageMonitor.exe`

## Running and controls

The app runs in the notification area rather than as a normal taskbar window.

- Left-click the tray icon: show detailed usage status
- Right-click the tray icon: status, refresh, Compact Bar toggle, settings, or exit
- Double-click the tray icon or Compact Bar: open settings
- Right-click the Compact Bar: refresh or settings
- Drag the Compact Bar: move it and save the new position

If the Compact Bar is hidden, right-click the tray icon and enable **Show Compact Bar**.

## Settings guide

### General

| Setting | Description |
|---|---|
| Show Compact Bar | Shows or hides the floating 2×2 monitor. |
| Compact Bar style | Selects Dark minimal, Label boxes, Neon glow, Light, Cards, Circular gauges, Compact rows, Rounded capsules, Gradient, or Minimal icons + text. |
| Start with Windows | Adds or removes the app from Windows startup. |
| Language | Changes the settings UI between English, Korean, Simplified Chinese, and Japanese. The open settings dialog updates immediately; save to apply it to the whole app. |
| Display basis | Shows either remaining quota percentage or used quota percentage. CPU and RAM always show current usage. |
| Refresh interval | Sets Codex quota refresh frequency from 30 to 1,800 seconds. CPU and RAM refresh separately. |
| Codex executable | Usually leave this as `codex`. If startup fails, enter the full path returned by `where.exe codex`. |

Changes are previewed on the Compact Bar immediately while Settings remains open. Three preset slots store and restore the style, Black/White color theme, background, display basis, metric visibility, presentation modes, and colors. **Save slot** writes the preset immediately, independently of the main Save button; press **Load** to apply it.

Settings are organized into **General**, **Appearance**, and **Metrics** tabs. The three preset slots stay visible above every tab. Every style supports Black and White variants. Selecting a style or color theme applies its matching default background color and opacity; the background can still be customized afterward. The Circular gauges style automatically uses the current monitor's taskbar height.

### Compact Bar and metrics

Each `5H`, `WK`, `CPU`, and `RAM` section provides:

| Setting | Description |
|---|---|
| Show | Enables or disables that metric. |
| Percentage only | Shows the value without a fill bar. |
| FillBar only | Shows only the fill bar. |
| Percentage + FillBar | Shows both the value and fill bar. |
| Fill color | Controls the filled part of the bar. |
| Track color | Controls the unfilled track. |

The **Background** setting controls only the rounded Compact Bar background. The color picker supports RGBA channels, an Alpha slider, and a six-digit RGB Hex field. Setting background Alpha to `0` makes only the background transparent; text and graphs remain visible.

## Data and privacy

The app does not scrape webpages or directly read authentication files. It starts the local `codex app-server` and calls the official JSON-RPC methods `account/rateLimits/read` and `account/usage/read`. It does not store an API key.

## Troubleshooting

### Codex usage is unavailable

Run `codex login status`. If necessary, run `codex login` again. In Settings, replace `codex` with the full path shown by `where.exe codex`.

### Compact Bar is missing

Check the notification area, then enable **Show Compact Bar** from the tray menu. After changing monitors or display scaling, toggle the bar off and on once.

### Reinstallation fails

Exit Codex Usage Monitor from the tray menu before running `install.cmd` again so the installed executable can be replaced.
