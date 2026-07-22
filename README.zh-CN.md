# Codex Usage Monitor

[English](README.md) | [한국어](README.ko.md) | [简体中文](README.zh-CN.md) | [日本語](README.ja.md)

一款 Windows 托盘应用，用于显示 Codex 的短期限额、每周限额以及当前 CPU 和 RAM 使用率。

![Codex Usage Monitor Compact Bar](docs/compact-bar.png)

Compact Bar 采用 2×2 布局：第一列从上到下是 `5H` 和 `WK`，第二列是 `CPU` 和 `RAM`。每项可显示百分比、FillBar 或两者。可将其拖到桌面任意位置，位置会自动保存。

## 功能

- 分别显示或隐藏 5 小时、每周、CPU 和 RAM 指标
- 在剩余百分比和已用百分比之间切换
- 使用 RGB、Alpha 和 Hex 设置填充色、轨道色及矩形背景色
- 设置界面支持英语、韩语、简体中文和日语
- 可调整刷新间隔、使用托盘控制并随 Windows 启动
- 显示重置时间以及今日/累计令牌数

## 要求与构建

- Windows 10/11
- 已安装 Codex CLI 并登录 ChatGPT
- 从源码构建需要 .NET 8 SDK

```powershell
npm install -g @openai/codex
codex login
dotnet build
```

运行 `build.ps1` 可生成独立发布版。双击 `install.cmd` 会将程序安装到 `%LOCALAPPDATA%\Programs\CodexUsageMonitor`，创建开始菜单快捷方式并启动应用。

## 使用

- 左键单击托盘图标：查看详细使用状态
- 右键单击托盘图标：刷新、切换 Compact Bar、设置或退出
- 双击 Compact Bar：打开设置
- 拖动 Compact Bar：移动并自动保存位置

设置保存在 `%LOCALAPPDATA%\CodexUsageMonitor\settings.json`。

## 数据访问

本应用不会抓取网页，也不会直接读取认证文件。它启动本地 `codex app-server`，并调用官方 JSON-RPC 方法 `account/rateLimits/read` 和 `account/usage/read`。应用不会保存 API 密钥。

## 故障排除

如果 app-server 无法启动，请在设置中指定 `codex.exe` 的完整路径（运行 `where.exe codex`）。如需登录，请运行 `codex login` 和 `codex login status`。若 Compact Bar 位于屏幕外，请在更改显示器布局或缩放后通过托盘菜单关闭再开启。
