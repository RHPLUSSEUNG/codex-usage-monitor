using System.Globalization;
using CodexUsageMonitor.Models;

namespace CodexUsageMonitor;

internal static class Localization
{
    private static readonly Dictionary<string, string[]> Strings = new(StringComparer.Ordinal)
    {
        ["SettingsTitle"] = ["Codex Usage Monitor Settings", "Codex Usage Monitor 설정", "Codex Usage Monitor 设置", "Codex Usage Monitor 設定"],
        ["ShowCompactBar"] = ["Show Compact Bar", "Compact Bar 표시", "显示 Compact Bar", "Compact Bar を表示"],
        ["StartWithWindows"] = ["Start with Windows", "Windows 시작 시 자동 실행", "Windows 启动时自动运行", "Windows 起動時に自動実行"],
        ["Language"] = ["Language", "언어", "语言", "言語"],
        ["CompactBarStyle"] = ["Compact Bar style", "Compact Bar 스타일", "Compact Bar 样式", "Compact Bar スタイル"],
        ["ThemeVariant"] = ["Color theme", "색상 테마", "颜色主题", "カラーテーマ"],
        ["DarkTheme"] = ["Black", "블랙", "黑色", "ブラック"],
        ["LightTheme"] = ["White", "화이트", "白色", "ホワイト"],
        ["DarkMinimalStyle"] = ["Dark minimal", "다크 미니멀", "深色极简", "ダークミニマル"],
        ["LabelBoxStyle"] = ["Label boxes", "라벨 박스형", "标签框", "ラベルボックス"],
        ["NeonGlowStyle"] = ["Neon glow", "네온 글로우", "霓虹光效", "ネオングロー"],
        ["LightStyle"] = ["Light", "라이트 모드", "浅色", "ライトモード"],
        ["CardsStyle"] = ["Cards", "카드 스타일", "卡片", "カード"],
        ["CircularGaugesStyle"] = ["Circular gauges", "원형 게이지", "圆形仪表", "円形ゲージ"],
        ["CompactRowsStyle"] = ["Compact rows", "컴팩트 바", "紧凑条", "コンパクトバー"],
        ["RoundedCapsulesStyle"] = ["Rounded capsules", "둥근 캡슐", "圆角胶囊", "丸型カプセル"],
        ["GradientStyle"] = ["Gradient", "그라데이션", "渐变", "グラデーション"],
        ["MinimalIconsStyle"] = ["Minimal icons + text", "미니멀 아이콘 + 텍스트", "极简图标 + 文本", "ミニマルアイコン + テキスト"],
        ["Presets"] = ["Presets", "프리셋", "预设", "プリセット"],
        ["GeneralTab"] = ["General", "일반", "常规", "一般"],
        ["AppearanceTab"] = ["Appearance", "모양", "外观", "外観"],
        ["MetricsTab"] = ["Metrics", "표시 항목", "指标", "表示項目"],
        ["Preset1"] = ["Preset 1", "프리셋 1", "预设 1", "プリセット 1"],
        ["Preset2"] = ["Preset 2", "프리셋 2", "预设 2", "プリセット 2"],
        ["Preset3"] = ["Preset 3", "프리셋 3", "预设 3", "プリセット 3"],
        ["LoadPreset"] = ["Load", "불러오기", "加载", "読込"],
        ["SavePreset"] = ["Save slot", "슬롯 저장", "保存槽位", "スロット保存"],
        ["RemainingPercent"] = ["Remaining percentage", "남은 퍼센트", "剩余百分比", "残りパーセント"],
        ["UsedPercent"] = ["Used percentage", "사용한 퍼센트", "已用百分比", "使用済みパーセント"],
        ["DisplayBasis"] = ["Display basis", "표시 기준", "显示基准", "表示基準"],
        ["RefreshSeconds"] = ["Refresh interval (sec)", "갱신 주기(초)", "刷新间隔（秒）", "更新間隔（秒）"],
        ["CodexExecutable"] = ["Codex executable", "Codex 실행 파일", "Codex 可执行文件", "Codex 実行ファイル"],
        ["FiveHourLimit"] = ["5-hour limit", "5시간 한도", "5 小时限额", "5時間制限"],
        ["WeeklyLimit"] = ["Weekly limit", "주간 한도", "每周限额", "週間制限"],
        ["CpuUsage"] = ["CPU usage", "CPU 사용률", "CPU 使用率", "CPU 使用率"],
        ["MemoryUsage"] = ["Memory usage", "메모리 사용률", "内存使用率", "メモリ使用率"],
        ["SettingsNote"] = ["Drag the Compact Bar anywhere on the screen. Color controls support RGB and alpha transparency. CPU and RAM always show current usage.", "Compact Bar는 드래그해 화면 어디로든 이동할 수 있습니다. 색상 버튼에서 RGB와 알파 투명도를 조절할 수 있습니다. CPU와 RAM은 항상 현재 사용률을 표시합니다.", "可将 Compact Bar 拖到屏幕上的任意位置。颜色控件支持 RGB 和 Alpha 透明度。CPU 和 RAM 始终显示当前使用率。", "Compact Bar は画面上の任意の場所へドラッグできます。色では RGB とアルファ透明度を調整できます。CPU と RAM は常に現在の使用率を表示します。"],
        ["Save"] = ["Save", "저장", "保存", "保存"],
        ["Cancel"] = ["Cancel", "취소", "取消", "キャンセル"],
        ["Show"] = ["Show", "표시", "显示", "表示"],
        ["PercentOnly"] = ["Percentage only (hide bar)", "퍼센트만 (그래프 숨김)", "仅百分比（隐藏进度条）", "パーセントのみ（バー非表示）"],
        ["BarOnly"] = ["Fill bar only", "FillBar만", "仅 FillBar", "FillBar のみ"],
        ["PercentAndBar"] = ["Percentage + Fill bar", "퍼센트 + FillBar", "百分比 + FillBar", "パーセント + FillBar"],
        ["Presentation"] = ["Presentation", "표현 방식", "显示方式", "表示方法"],
        ["FillColor"] = ["Fill color", "Fill 색상", "填充颜色", "塗りつぶし色"],
        ["TrackColor"] = ["Track color", "트랙 색상", "轨道颜色", "トラック色"],
        ["RectangleBackground"] = ["Background", "배경", "背景", "背景"],
        ["ChooseColor"] = ["Choose color · A {0}", "색상 선택 · A {0}", "选择颜色 · A {0}", "色を選択 · A {0}"],
        ["ColorPickerTitle"] = ["Choose color", "색상 선택", "选择颜色", "色を選択"],
        ["Opacity"] = ["Opacity", "투명도", "透明度", "透明度"],
        ["Ok"] = ["OK", "확인", "确定", "OK"],
        ["HexError"] = ["Enter the Hex color in RRGGBB format.", "Hex 색상은 RRGGBB 형식으로 입력하세요.", "请以 RRGGBB 格式输入 Hex 颜色。", "Hex 色を RRGGBB 形式で入力してください。"],
        ["ColorFormatError"] = ["Color format error", "색상 형식 오류", "颜色格式错误", "色形式エラー"],
        ["Status"] = ["Status", "상태 보기", "查看状态", "状態を表示"],
        ["Refresh"] = ["Refresh", "새로고침", "刷新", "更新"],
        ["Settings"] = ["Settings", "설정", "设置", "設定"],
        ["Exit"] = ["Exit", "종료", "退出", "終了"],
        ["Remaining"] = ["remaining", "남음", "剩余", "残り"],
        ["Used"] = ["used", "사용", "已用", "使用済み"],
        ["FiveHour"] = ["5-hour", "5시간", "5 小时", "5時間"],
        ["Weekly"] = ["Weekly", "주간", "每周", "週間"],
        ["LoginHint"] = ["Check that Codex CLI is installed and you are signed in to ChatGPT.", "Codex CLI 설치 및 ChatGPT 로그인을 확인하세요.", "请确认已安装 Codex CLI 并已登录 ChatGPT。", "Codex CLI のインストールと ChatGPT へのログインを確認してください。"],
        ["TodayTokens"] = ["Tokens today", "오늘 토큰", "今日令牌数", "今日のトークン"],
        ["LifetimeTokens"] = ["Lifetime tokens", "누적 토큰", "累计令牌数", "累計トークン"],
        ["LastUpdated"] = ["Last updated", "마지막 갱신", "最后更新", "最終更新"],
        ["Unavailable"] = ["Unavailable", "확인 불가", "无法获取", "取得不可"],
        ["ResetUnknown"] = ["reset time unknown", "초기화 시각 미상", "重置时间未知", "リセット時刻不明"],
        ["ResetAt"] = ["resets at {0}", "{0} 초기화", "{0} 重置", "{0} リセット"],
        ["UsageTitle"] = ["Codex usage", "Codex 사용량", "Codex 使用量", "Codex 使用量"],
        ["SettingsSaveError"] = ["Settings save error", "설정 저장 오류", "设置保存错误", "設定保存エラー"]
    };

    public static AppLanguage CurrentLanguage { get; set; } = AppLanguage.Korean;

    public static string Text(string key) => Text(CurrentLanguage, key);

    public static string Text(AppLanguage language, string key)
    {
        if (!Strings.TryGetValue(key, out string[]? values))
            return key;
        int index = Math.Clamp((int)language, 0, values.Length - 1);
        return values[index];
    }

    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Text(key), args);

    public static string Format(AppLanguage language, string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Text(language, key), args);
}
