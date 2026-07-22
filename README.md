# Codex Usage Monitor

[English](README.md) | [한국어](README.ko.md) | [简体中文](README.zh-CN.md) | [日本語](README.ja.md)

A Windows tray application that shows Codex short-term and weekly usage limits alongside current CPU and RAM usage.

![Codex Usage Monitor compact bar](docs/compact-bar.png)

The floating Compact Bar uses a 2×2 layout: `5H` and `WK` in the first column, and `CPU` and `RAM` in the second. Each item can display a percentage, a fill bar, or both. Drag the bar anywhere on the desktop; its position is saved automatically.

## Features

- Show or hide each of the 5-hour, weekly, CPU, and RAM metrics
- Switch quota values between remaining and used percentage
- Configure fill/track colors and the rectangular background with RGB, Alpha, and Hex controls
- English, Korean, Simplified Chinese, and Japanese settings UI
- Adjustable refresh interval, tray controls, and Windows startup
- Display reset times and daily/lifetime token totals

## Requirements and build

- Windows 10/11
- Codex CLI installed and signed in with ChatGPT
- .NET 8 SDK when building from source

```powershell
npm install -g @openai/codex
codex login
dotnet build
```

For a self-contained release, run `build.ps1`. Double-click `install.cmd` to build, install under `%LOCALAPPDATA%\Programs\CodexUsageMonitor`, create a Start menu shortcut, and launch the app.

## Usage

- Left-click the tray icon: detailed usage status
- Right-click the tray icon: refresh, toggle the Compact Bar, settings, or exit
- Double-click the Compact Bar: settings
- Drag the Compact Bar: move and save its position

Settings are stored at `%LOCALAPPDATA%\CodexUsageMonitor\settings.json`.

## Data access

The app does not scrape webpages or read authentication files. It starts the local `codex app-server` and uses the official JSON-RPC methods `account/rateLimits/read` and `account/usage/read`. No API key is stored by the app.

## Troubleshooting

If the app-server cannot start, set the full `codex.exe` path in Settings (`where.exe codex`). If login is required, run `codex login` and `codex login status`. If the Compact Bar is off-screen, toggle it from the tray menu after changing the monitor layout or display scale.
