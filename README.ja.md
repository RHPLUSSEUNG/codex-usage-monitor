# Codex Usage Monitor

[English](README.md) | [한국어](README.ko.md) | [简体中文](README.zh-CN.md) | [日本語](README.ja.md)

Codex の短期・週間使用制限と現在の CPU・RAM 使用率を表示する Windows トレイアプリです。

![Codex Usage Monitor Compact Bar](docs/compact-bar.png)

Compact Bar は 2×2 構成です。1列目は上から `5H` と `WK`、2列目は `CPU` と `RAM` を表示します。各項目はパーセント、FillBar、または両方を表示できます。デスクトップ上の任意の位置へドラッグすると、その位置が自動保存されます。

## 機能

- 5時間・週間・CPU・RAM の各項目を個別に表示／非表示
- Codex 制限を残りパーセントまたは使用済みパーセントで表示
- RGB・アルファ・Hex による塗りつぶし色、トラック色、四角形背景色の設定
- 英語・韓国語・簡体字中国語・日本語の設定 UI
- 更新間隔、トレイ操作、Windows 起動時の自動実行
- リセット時刻と今日／累計トークンの表示

## 要件とビルド

- Windows 10/11
- Codex CLI のインストールと ChatGPT へのログイン
- ソースからのビルドには .NET 8 SDK

```powershell
npm install -g @openai/codex
codex login
dotnet build
```

単体配布版は `build.ps1` で作成できます。`install.cmd` をダブルクリックすると `%LOCALAPPDATA%\Programs\CodexUsageMonitor` にインストールし、スタートメニューのショートカットを作成してアプリを起動します。

## 使い方

- トレイアイコンを左クリック：詳細な使用状況
- トレイアイコンを右クリック：更新、Compact Bar の切り替え、設定、終了
- Compact Bar をダブルクリック：設定
- Compact Bar をドラッグ：移動して位置を自動保存

設定は `%LOCALAPPDATA%\CodexUsageMonitor\settings.json` に保存されます。

## データアクセス

Web ページのスクレイピングや認証ファイルの直接読み取りは行いません。ローカルの `codex app-server` を起動し、公式 JSON-RPC メソッド `account/rateLimits/read` と `account/usage/read` を使用します。API キーはアプリに保存されません。

## トラブルシューティング

app-server を開始できない場合は、設定で `codex.exe` のフルパスを指定してください（`where.exe codex`）。ログインが必要な場合は `codex login` と `codex login status` を実行してください。Compact Bar が画面外にある場合は、モニター構成や拡大率を変更後、トレイメニューから一度オフにして再度オンにしてください。
