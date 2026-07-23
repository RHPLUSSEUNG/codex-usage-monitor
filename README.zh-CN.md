# Codex Usage Monitor

[English](README.md) | [한국어](README.ko.md) | [简体中文](README.zh-CN.md) | [日本語](README.ja.md)

<p align="center">
  <img src="Resources/CodexUsageMonitor.ico" alt="Codex Usage Monitor 图标" width="128">
</p>

一款适用于 Windows 10/11 的托盘程序，可同时显示 Codex 使用限额以及当前 CPU 和 RAM 使用率。

![Codex Usage Monitor Compact Bar](docs/compact-bar.png)

Compact Bar 采用两列布局：第一列从上到下是 `5H` 和 `WK`，第二列是 `CPU` 和 `RAM`。每项可显示百分比、FillBar 或两者。将其拖到桌面任意位置后，位置会自动保存。

## 安装前准备

需要以下环境：

- 64 位 Windows 10 或 Windows 11
- 用于构建下载源码的 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- 已安装 Codex CLI 并登录 ChatGPT

在 PowerShell 中准备 Codex CLI。如果已经安装 `codex`，可跳过安装命令。

```powershell
npm install -g @openai/codex
codex login
codex login status
```

## 快速安装

1. 在 GitHub 中选择 **Code → Download ZIP**，然后解压；也可以克隆仓库。
2. 打开解压后的 `codex-usage-monitor` 文件夹。
3. 双击 `install.cmd`。
4. 等待构建完成。安装结束后程序会自动启动。
5. 以后可在 Windows 开始菜单中搜索 **Codex Usage Monitor** 启动。

安装位置：

```text
程序:     %LOCALAPPDATA%\Programs\CodexUsageMonitor\CodexUsageMonitor.exe
快捷方式: 开始菜单\程序\Codex Usage Monitor
设置文件: %LOCALAPPDATA%\CodexUsageMonitor\settings.json
```

更新时，请下载新源码，在托盘菜单中退出旧版本，然后再次运行 `install.cmd`。现有设置会保留。

## 手动构建

```powershell
dotnet build
```

创建安装程序使用的独立单文件：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

输出：`bin\Release\net8.0-windows\win-x64\publish\CodexUsageMonitor.exe`

## 运行与操作

程序在通知区域运行，不会显示为普通任务栏窗口。

- 左键单击托盘图标：查看详细使用状态
- 右键单击托盘图标：状态、刷新、切换 Compact Bar、设置或退出
- 双击托盘图标或 Compact Bar：打开设置
- 右键单击 Compact Bar：刷新或设置
- 拖动 Compact Bar：移动并自动保存位置

如果 Compact Bar 不可见，请右键单击托盘图标并启用 **显示 Compact Bar**。

## 设置指南

### 常规设置

| 设置 | 说明 |
|---|---|
| 显示 Compact Bar | 显示或隐藏浮动的 2×2 监视器。 |
| Compact Bar 样式 | 选择**经典**或**标签框**。标签框使用各指标的填充颜色，同时保持紧凑尺寸。 |
| Windows 启动时自动运行 | 添加或移除 Windows 启动项。 |
| 语言 | 可选择英语、韩语、简体中文或日语。选择后当前设置窗口会立即翻译；保存后应用到整个程序。 |
| 显示基准 | 以剩余百分比或已用百分比显示 Codex 限额。CPU 和 RAM 始终显示当前使用率。 |
| 刷新间隔 | 设置 Codex 使用量的刷新周期，范围为 30～1,800 秒。CPU 和 RAM 单独刷新。 |
| Codex 可执行文件 | 通常保留为 `codex`。如果启动失败，请输入 `where.exe codex` 返回的完整路径。 |

### Compact Bar 与指标设置

`5H`、`WK`、`CPU` 和 `RAM` 均提供以下设置：

| 设置 | 说明 |
|---|---|
| 显示 | 启用或禁用该指标。 |
| 仅百分比 | 只显示数值，不显示 FillBar。 |
| 仅 FillBar | 只显示 FillBar。 |
| 百分比 + FillBar | 同时显示数值和 FillBar。 |
| 填充颜色 | 设置进度条已填充部分的颜色。 |
| 轨道颜色 | 设置进度条未填充背景的颜色。 |

**矩形背景**只控制 Compact Bar 的背景。颜色选择器支持 RGBA 数值、Alpha 滑块以及六位 RGB Hex。即使将背景 Alpha 设为 `0`，也只会隐藏背景，文字和图表仍然可见。

## 数据与隐私

程序不会抓取网页，也不会直接读取认证文件。它启动本地 `codex app-server`，并调用官方 JSON-RPC 方法 `account/rateLimits/read` 和 `account/usage/read`。程序不会保存 API 密钥。

## 故障排除

### 无法获取 Codex 使用量

运行 `codex login status`。如有需要，请通过 `codex login` 重新登录。也可在设置中将 `Codex 可执行文件` 改为 `where.exe codex` 返回的完整路径。

### Compact Bar 不可见

检查通知区域，并从托盘菜单启用 **显示 Compact Bar**。更改显示器布局或缩放比例后，请将其关闭再重新开启一次。

### 重新安装失败

请先从托盘菜单退出 Codex Usage Monitor，再运行 `install.cmd`，以便替换正在使用的程序文件。
