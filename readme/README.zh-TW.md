<p align="center">
  <img src="../Resources/CodexUsageMonitor.ico" alt="Codex Usage Monitor 圖示" width="96">
</p>

<h1 align="center">Codex Usage Monitor</h1>

<p align="center">
  <strong>隨時一眼查看 Codex 限額與系統負載。</strong><br>
  一款輕量的 Windows 系統匣監視器，顯示 5 小時、每週、CPU 和記憶體使用率。
</p>

<p align="center">
  <a href="../README.md">English</a> ·
  <a href="README.ko.md">한국어</a> ·
  <a href="README.zh-CN.md">簡體中文</a> ·
  <a href="README.zh-TW.md">繁體中文</a> ·
  <a href="README.ja.md">日本語</a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?style=flat-square&logo=windows11&logoColor=white" alt="Windows 10 和 11">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white" alt=".NET 8">
  <img src="https://img.shields.io/badge/UI-WinForms-5C2D91?style=flat-square" alt="WinForms">
  <a href="../LICENSE"><img src="https://img.shields.io/badge/License-MIT-2EA44F?style=flat-square" alt="MIT License"></a>
</p>

<p align="center">
  <a href="#quick-start">快速開始</a> ·
  <a href="#features">主要功能</a> ·
  <a href="#styles">樣式</a> ·
  <a href="#settings">設定</a> ·
  <a href="#privacy">隱私</a> ·
  <a href="#troubleshooting">故障排除</a>
</p>

<p align="center">
  <img src="../docs/compact-bar.png" alt="Codex Usage Monitor Compact Bar">
</p>

<p align="center"><sub>左側顯示 5H·WK · 右側顯示 CPU·RAM · 可拖動到任意位置</sub></p>

---

<a id="features"></a>

## ✨ 一覽

| | 功能 | 提供內容 |
|---|---|---|
| 📊 | **Codex 限額** | 5 小時及每週限額的剩餘或已使用百分比 |
| 🖥️ | **系統使用率** | 獨立重新整理的 CPU 和記憶體使用率 |
| 🔔 | **限額提醒** | 支援勿擾時段的剩餘 20%、10%、5% 單次通知 |
| 🎨 | **6 種樣式 × 28 種色彩主題** | 系統／淺色／深色模式與 Codex 應用程式色彩配置 |
| 💾 | **3 個預設** | 即時儲存並還原偏好的外觀 |
| 🌍 | **4 種語言** | 英語、韓語、簡體中文和日語 |
| 🔒 | **本地且私密** | 使用 `codex app-server`，不抓取網頁或存取認證檔案 |

<a id="compact-bar"></a>

## Compact Bar

Compact Bar 可同時顯示 **5H**、**WK**、**CPU** 和 **RAM**。每項可顯示百分比、進度條或兩者。

### 調整面板順序

在外觀預覽中，將一個面板拖到另一個面板上即可交換位置。調整配置時會同時顯示 5 小時和每週重置倒計時面板；隱藏的面板不會保留空位。

<p align="center">
  <img src="../docs/panel-ordering.png" alt="在外觀預覽中拖動 5H 面板" width="790">
</p>

### 重置時間提示

將滑鼠懸停在 Compact Bar 的任意位置，即可查看 5 小時和每週限額的準確重置日期。提示框會對齊到可見面板最近的一端，並根據螢幕空間顯示在上方或下方。

<p align="center">
  <img src="../docs/reset-tooltip.png" alt="Compact Bar 重置時間提示" width="672">
</p>

<a id="styles"></a>

## 🎨 外觀

### 螢幕模式

**系統**會自動跟隨 Windows 主題，也可以將 Compact Bar 固定為**深色**或**淺色**模式。下方預覽使用 Everforest 配色。

<p align="center">
  <img src="../docs/screen-modes.png" alt="系統、深色和淺色螢幕模式" width="984">
</p>

<details>
<summary><strong>色彩主題</strong> — 28 種色彩配置</summary>

選擇色彩主題會同時更新背景、文字、強調色、進度條填充和軌道色彩。原始主題同時提供深色與淺色版本時，可以在兩者之間切換。

<p align="center">
  <img src="../docs/color-themes.png" alt="Codex Usage Monitor 色彩主題">
</p>

</details>

<details>
<summary><strong>進度條樣式</strong> — 6 種配置</summary>

所有樣式預覽均使用深色模式的 Everforest 配色。

| | |
|---|---|
| **淺色**<br><img src="../docs/themes/light.png" alt="淺色樣式" width="400"> | **標籤框**<br><img src="../docs/themes/label-boxes.png" alt="標籤框樣式" width="400"> |
| **霓虹光效**<br><img src="../docs/themes/neon-glow.png" alt="霓虹光效樣式" width="400"> | **緊湊條**<br><img src="../docs/themes/compact-rows.png" alt="緊湊條樣式" width="400"> |
| **漸變**<br><img src="../docs/themes/gradient.png" alt="漸變樣式" width="400"> | **極簡圖示 + 文字**<br><img src="../docs/themes/minimal-icons.png" alt="極簡圖示加文字樣式" width="400"> |

</details>

<a id="quick-start"></a>

## 🚀 快速開始

### 準備要求

需要以下環境：

- 64 位 Windows 10 或 Windows 11
- 用於建置下載原始碼的 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- 已安裝 Codex CLI 並登入 ChatGPT

在 PowerShell 中準備 Codex CLI。如果已經安裝 `codex`，可跳過安裝命令。

```powershell
npm install -g @openai/codex
codex login
codex login status
```

### 安裝

1. 在 GitHub 中選擇 **Code → Download ZIP**，然後解壓；也可以複製儲存庫。
2. 打開解壓後的 `codex-usage-monitor` 資料夾。
3. 連按兩下 `install.cmd`。
4. 等待建置完成。安裝完成後程式會自動啟動。
5. 以後可在 Windows 開始功能表中搜尋 **Codex Usage Monitor** 啟動。

安裝位置：

```text
程式:     %LOCALAPPDATA%\Programs\CodexUsageMonitor\CodexUsageMonitor.exe
捷徑: 開始功能表\程式\Codex Usage Monitor
設定檔案: %LOCALAPPDATA%\CodexUsageMonitor\settings.json
```

安裝版會在啟動時以及每六小時檢查一次 GitHub 正式版本。發現新版本後會顯示 Windows 通知。在 **設定 → 關於** 中選擇 **更新到 v…** 後，程式會下載發行檔案、驗證 SHA-256 總和檢查碼、替換已安裝的程式並自動重新啟動。總和檢查碼用於確認下載完整性，但不能替代發佈者簽名。設定檔案會保留。

也可以隨時在 **設定 → 關於** 中選擇 **檢查更新**。自動安裝僅適用於從上述安裝路徑執行的程式，不會覆蓋原始碼或偵錯版本。

v1.1.0 之前的版本不包含更新程式，因此需要手動安裝一次 v1.1.0 或更高版本。

若要移除安裝版，請在 **設定 → 關於** 中選擇 **解除安裝**。確認視窗可選擇保留或同時移除設定、預設和記錄檔。解除安裝時也會移除開始功能表捷徑和 Windows 啟動項。

<details>
<summary><strong>手動建置</strong></summary>

<br>

```powershell
dotnet build
```

建立安裝程式使用的獨立單檔案：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

輸出：`bin\Release\net8.0-windows\win-x64\publish\CodexUsageMonitor.exe`

</details>

<a id="controls"></a>

## 🖱️ 執行與操作

程式在通知區域執行，不會顯示為一般工作列視窗。

- 按一下系統匣圖示：查看詳細使用狀態
- 按一下滑鼠右鍵系統匣圖示：狀態、重新整理、切換 Compact Bar、設定或退出
- 連按兩下系統匣圖示或 Compact Bar：打開設定
- 按一下滑鼠右鍵 Compact Bar：重新整理、設定或重置位置
- 拖動 Compact Bar：移動並自動儲存位置
- **設定 → 關於**：版本、作者、儲存庫、診斷資訊、更新和解除安裝

如果 Compact Bar 不可見，請按一下滑鼠右鍵系統匣圖示並啟用 **顯示 Compact Bar**。

<a id="settings"></a>

## ⚙️ 設定指南

### 一般設定

| 設定 | 說明 |
|---|---|
| 顯示 Compact Bar | 顯示或隱藏浮動的 2×2 監視器。 |
| Windows 啟動時自動執行 | 新增或移除 Windows 啟動項。 |
| 語言 | 可選擇英語、韓語、簡體中文或日語。選擇後當前設定視窗會立即翻譯；儲存後套用至整個程式。 |
| 重新整理間隔 | 設定 Codex 使用量的重新整理週期，範圍為 30～1,800 秒。CPU 和 RAM 單獨重新整理。 |
| 限額門檻通知 | 當 5 小時或每週剩餘額度進入 20%、10% 或 5% 區間時各通知一次；額度重置後通知狀態也會重置。 |
| 勿擾時段 | 在設定的本地時間範圍內延後閾值通知，結束後再提示。 |
| 進階設定 | 包含 Codex 可執行檔案路徑。通常保留為 `codex`；如果啟動失敗，請輸入 `where.exe codex` 傳回的完整路徑。 |

設定視窗保持打開時，所有外觀更改都會立即預覽到 Compact Bar。先從下拉選單選擇預設槽位，再使用**載入**或**儲存槽位**。每個槽位會儲存樣式、色彩模式、色彩主題、背景、顯示基準、指標開關、顯示方式及色彩。

色彩配置包括 Absolutely、Ayu、Catppuccin、Codex、Dracula、Everforest、GitHub、Gruvbox、Linear、Lobster、Material、Matrix、Monokai、Night Owl、Nord、Notion、One、Oscurange、Proof、Raycast、Rose Pine、Sentry、Solarized、Temple、Tokyo Night、Vercel、VS Code Plus 和 Xcode。深色／淺色選項只顯示各 Codex 色彩配置實際支援的明暗組合。

設定分為**常規**、**顯示**和**關於**分頁。顯示分頁在一個可滾動頁面中整合了預設、外觀、顯示基準與四個項目的設定，並提供 50～200% 滑桿和 100% 重置按鈕。切換樣式會保留當前色彩；選擇螢幕模式或色彩主題時會套用該主題的背景、填充和軌道色彩。

設定會先寫入並驗證臨時檔案，再以原子方式替換。上一份有效檔案保留為 `settings.json.bak`；如果主 JSON 損壞，程式會還原備份並透過系統匣通知說明。`SettingsVersion` 欄位用於今後的相容遷移。

### Compact Bar 與指標設定

在**顯示**分頁中，可選擇以剩餘百分比或已用百分比顯示 Codex 限額。CPU 和 RAM 始終顯示當前使用率。

`5H`、`WK`、`CPU` 和 `RAM` 均提供以下設定：

| 設定 | 說明 |
|---|---|
| 顯示 | 啟用或禁用該指標。 |
| 僅百分比 | 只顯示數值，不顯示 FillBar。 |
| 僅 FillBar | 只顯示 FillBar。 |
| 百分比 + FillBar | 同時顯示數值和 FillBar。 |
| 填充色彩 | 設定進度條已填充部分的色彩。 |
| 軌道色彩 | 設定進度條未填充背景的色彩。 |

**背景**提供透明背景開關和 RGBA 色彩設定。開啟透明背景後，文字和圖表仍然保持可見。

<a id="privacy"></a>

## 🔒 資料與隱私

程式不會抓取網頁，也不會直接讀取認證檔案。它啟動本地 `codex app-server`，並調用官方 JSON-RPC 方法 `account/rateLimits/read` 和 `account/usage/read`。程式不會儲存 API 金鑰。

<a id="troubleshooting"></a>

## 🧰 故障排除

### 無法取得 Codex 使用量

執行 `codex login status`。如有需要，請透過 `codex login` 重新登入。也可在設定中將 `Codex 可執行檔案` 改為 `where.exe codex` 傳回的完整路徑。

### Compact Bar 不可見

檢查通知區域，並從系統匣功能表啟用 **顯示 Compact Bar**。顯示器配置變化後，程式會自動保證視窗的一部分位于有效螢幕內；必要時請選擇 **重置位置**。

### 收集診斷資訊

在 **設定 → 關於** 中選擇 **複製診斷資訊**。報告會複製應用程式版本、app-server 狀態、最近重新整理與錯誤、Windows/DPI/顯示器資訊以及設定和記錄檔路徑。

### 重新安裝失敗

請先從系統匣功能表退出 Codex Usage Monitor，再執行 `install.cmd`，以便替換正在使用的程式檔案。

## 📄 許可證

本項目基于 [MIT License](../LICENSE) 發布。
