<p align="center">
  <img src="Resources/CodexUsageMonitor.ico" alt="Codex Usage Monitor icon" width="96">
</p>

<h1 align="center">Codex Usage Monitor</h1>

<p align="center">
  <strong>Your Codex limits and system load, always one glance away.</strong><br>
  A lightweight Windows tray monitor for 5-hour, weekly, CPU, and RAM usage.
</p>

<p align="center">
  <a href="README.md">English</a> ·
  <a href="readme/README.ko.md">한국어</a> ·
  <a href="readme/README.zh-CN.md">简体中文</a> ·
  <a href="readme/README.ja.md">日本語</a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?style=flat-square&logo=windows11&logoColor=white" alt="Windows 10 and 11">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white" alt=".NET 8">
  <img src="https://img.shields.io/badge/UI-WinForms-5C2D91?style=flat-square" alt="WinForms">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-2EA44F?style=flat-square" alt="MIT License"></a>
</p>

<p align="center">
  <a href="#quick-start">Quick start</a> ·
  <a href="#features">Features</a> ·
  <a href="#styles">Styles</a> ·
  <a href="#settings">Settings</a> ·
  <a href="#privacy">Privacy</a> ·
  <a href="#troubleshooting">Troubleshooting</a>
</p>

<p align="center">
  <img src="docs/compact-bar.png" alt="Codex Usage Monitor compact bar">
</p>

<p align="center"><sub>5H and WK on the left · CPU and RAM on the right · drag anywhere to reposition</sub></p>

---

<a id="features"></a>

## ✨ At a glance

| | Feature | What it gives you |
|---|---|---|
| 📊 | **Codex quota** | Live 5-hour and weekly remaining or used percentages |
| 🖥️ | **System usage** | CPU and RAM usage updated independently |
| 🎨 | **10 styles × 2 themes** | Ten Compact Bar styles, each in Black and White |
| 💾 | **3 presets** | Instantly save and restore your preferred appearance |
| 🌍 | **4 languages** | English, Korean, Simplified Chinese, and Japanese |
| 🔒 | **Local and private** | Uses `codex app-server`; no webpage scraping or auth-file access |

The Compact Bar uses two columns: `5H` and `WK` in the first column, and `CPU` and `RAM` in the second. Each item can show a percentage, a fill bar, or both.

<a id="styles"></a>

## 🎨 Style gallery

| | |
|---|---|
| **Dark minimal**<br><img src="docs/themes/dark-minimal.png" alt="Dark minimal style" width="400"> | **Label boxes**<br><img src="docs/themes/label-boxes.png" alt="Label boxes style" width="400"> |
| **Neon glow**<br><img src="docs/themes/neon-glow.png" alt="Neon glow style" width="400"> | **Light**<br><img src="docs/themes/light.png" alt="Light style" width="400"> |
| **Cards**<br><img src="docs/themes/cards.png" alt="Cards style" width="400"> | **Circular gauges**<br><img src="docs/themes/circular-gauges.png" alt="Circular gauges style" width="400"> |
| **Compact rows**<br><img src="docs/themes/compact-rows.png" alt="Compact rows style" width="400"> | **Rounded capsules**<br><img src="docs/themes/rounded-capsules.png" alt="Rounded capsules style" width="400"> |
| **Gradient**<br><img src="docs/themes/gradient.png" alt="Gradient style" width="400"> | **Minimal icons + text**<br><img src="docs/themes/minimal-icons.png" alt="Minimal icons and text style" width="400"> |

<a id="quick-start"></a>

## 🚀 Quick start

### Requirements

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

### Install

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

The installed app checks stable GitHub Releases at startup and every six hours. When a new version is available, a Windows notification and **Update to v…** tray-menu item appear. Selecting it downloads the release, verifies its SHA-256 checksum, replaces the installed app, and restarts it automatically. The settings file is kept.

You can also select **Check for updates** from the tray menu at any time. Automatic installation is available only from the installed path shown above; source and debug builds are not overwritten.

Versions earlier than v1.1.0 do not include the updater and require one manual installation of v1.1.0 or later.

On a first installation, the UI language defaults to English. Preset 1 is preconfigured as Label boxes / Black with background Alpha `0`. Existing `settings.json` files are not overwritten.

<details>
<summary><strong>Build manually</strong></summary>

<br>

```powershell
dotnet build
```

To create the self-contained single-file executable used by the installer:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Output: `bin\Release\net8.0-windows\win-x64\publish\CodexUsageMonitor.exe`

</details>

<a id="controls"></a>

## 🖱️ Running and controls

The app runs in the notification area rather than as a normal taskbar window.

- Left-click the tray icon: show detailed usage status
- Right-click the tray icon: status, refresh, Compact Bar toggle, settings, or exit
- Double-click the tray icon or Compact Bar: open settings
- Right-click the Compact Bar: refresh or settings
- Drag the Compact Bar: move it and save the new position

If the Compact Bar is hidden, right-click the tray icon and enable **Show Compact Bar**.

<a id="settings"></a>

## ⚙️ Settings guide

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

<a id="privacy"></a>

## 🔒 Data and privacy

The app does not scrape webpages or directly read authentication files. It starts the local `codex app-server` and calls the official JSON-RPC methods `account/rateLimits/read` and `account/usage/read`. It does not store an API key.

<a id="troubleshooting"></a>

## 🧰 Troubleshooting

### Codex usage is unavailable

Run `codex login status`. If necessary, run `codex login` again. In Settings, replace `codex` with the full path shown by `where.exe codex`.

### Compact Bar is missing

Check the notification area, then enable **Show Compact Bar** from the tray menu. After changing monitors or display scaling, toggle the bar off and on once.

### Reinstallation fails

Exit Codex Usage Monitor from the tray menu before running `install.cmd` again so the installed executable can be replaced.

## 📄 License

Released under the [MIT License](LICENSE).
