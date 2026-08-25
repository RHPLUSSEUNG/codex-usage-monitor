<p align="center">
  <img src="../Resources/CodexUsageMonitor.ico" alt="Codex Usage Monitor 아이콘" width="96">
</p>

<h1 align="center">Codex Usage Monitor</h1>

<p align="center">
  <strong>Codex 한도와 시스템 부하를 언제나 한눈에.</strong><br>
  5시간·주간 한도와 CPU·RAM 사용률을 표시하는 가벼운 Windows 트레이 모니터입니다.
</p>

<p align="center">
  <a href="../README.md">English</a> ·
  <a href="README.ko.md">한국어</a> ·
  <a href="README.zh-CN.md">简体中文</a> ·
  <a href="README.ja.md">日本語</a>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?style=flat-square&logo=windows11&logoColor=white" alt="Windows 10 및 11">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white" alt=".NET 8">
  <img src="https://img.shields.io/badge/UI-WinForms-5C2D91?style=flat-square" alt="WinForms">
  <a href="../LICENSE"><img src="https://img.shields.io/badge/License-MIT-2EA44F?style=flat-square" alt="MIT License"></a>
</p>

<p align="center">
  <a href="#quick-start">빠른 시작</a> ·
  <a href="#features">주요 기능</a> ·
  <a href="#styles">스타일</a> ·
  <a href="#settings">설정</a> ·
  <a href="#privacy">개인정보</a> ·
  <a href="#troubleshooting">문제 해결</a>
</p>

<p align="center">
  <img src="../docs/compact-bar.png" alt="Codex Usage Monitor Compact Bar">
</p>

<p align="center"><sub>왼쪽에는 5H·WK · 오른쪽에는 CPU·RAM · 원하는 위치로 드래그</sub></p>

---

<a id="features"></a>

## ✨ 한눈에 보기

| | 기능 | 제공 내용 |
|---|---|---|
| 📊 | **Codex 한도** | 5시간·주간 한도의 남은 비율 또는 사용 비율 |
| 🖥️ | **시스템 사용량** | 별도로 갱신되는 CPU·RAM 사용률 |
| 🔔 | **한도 알림** | 방해 금지 시간을 지원하는 남은 한도 20%·10%·5% 일회 알림 |
| 🎨 | **10개 스타일 × 28개 컬러 테마** | 블랙·화이트 색상 모드와 Codex 앱 팔레트 제공 |
| 💾 | **프리셋 3개** | 원하는 모양을 즉시 저장하고 불러오기 |
| 🌍 | **4개 언어** | 영어·한국어·중국어(간체)·일본어 |
| 🔒 | **로컬·비공개** | `codex app-server` 사용, 웹 스크래핑과 인증 파일 접근 없음 |

Compact Bar는 두 열로 구성됩니다. 1열에는 `5H`와 `WK`, 2열에는 `CPU`와 `RAM`이 표시됩니다. 각 항목은 퍼센트, FillBar 또는 둘 다 표시할 수 있습니다.

<a id="styles"></a>

## 🎨 스타일 갤러리

아래 미리보기는 모두 기본 **Codex 다크** 컬러 테마로 렌더링했습니다. 다른 컬러 테마는 앱에서 선택할 수 있으며 이 갤러리에는 중복해서 나열하지 않습니다.

| | |
|---|---|
| **다크 미니멀**<br><img src="../docs/themes/dark-minimal.png" alt="다크 미니멀 스타일" width="400"> | **라벨 박스형**<br><img src="../docs/themes/label-boxes.png" alt="라벨 박스형 스타일" width="400"> |
| **네온 글로우**<br><img src="../docs/themes/neon-glow.png" alt="네온 글로우 스타일" width="400"> | **라이트**<br><img src="../docs/themes/light.png" alt="라이트 스타일" width="400"> |
| **카드**<br><img src="../docs/themes/cards.png" alt="카드 스타일" width="400"> | **원형 게이지**<br><img src="../docs/themes/circular-gauges.png" alt="원형 게이지 스타일" width="400"> |
| **컴팩트 바**<br><img src="../docs/themes/compact-rows.png" alt="컴팩트 바 스타일" width="400"> | **둥근 캡슐**<br><img src="../docs/themes/rounded-capsules.png" alt="둥근 캡슐 스타일" width="400"> |
| **그라데이션**<br><img src="../docs/themes/gradient.png" alt="그라데이션 스타일" width="400"> | **미니멀 아이콘 + 텍스트**<br><img src="../docs/themes/minimal-icons.png" alt="미니멀 아이콘과 텍스트 스타일" width="400"> |

<a id="quick-start"></a>

## 🚀 빠른 시작

### 준비 사항

다음 항목이 필요합니다.

- Windows 10 또는 Windows 11 64비트
- 내려받은 소스를 빌드하기 위한 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Codex CLI 설치 및 ChatGPT 로그인

PowerShell에서 Codex CLI를 준비합니다. 이미 `codex`가 설치되어 있다면 설치 명령은 생략할 수 있습니다.

```powershell
npm install -g @openai/codex
codex login
codex login status
```

### 설치

1. GitHub에서 **Code → Download ZIP**을 선택하고 ZIP 파일의 압축을 풉니다. 또는 저장소를 복제합니다.
2. 압축을 푼 `codex-usage-monitor` 폴더를 엽니다.
3. `install.cmd`를 더블 클릭합니다.
4. 빌드가 끝날 때까지 기다립니다. 설치가 끝나면 프로그램이 자동 실행됩니다.
5. 다음부터는 Windows 시작 메뉴에서 **Codex Usage Monitor**를 검색해 실행합니다.

설치되는 위치는 다음과 같습니다.

```text
프로그램: %LOCALAPPDATA%\Programs\CodexUsageMonitor\CodexUsageMonitor.exe
바로가기: 시작 메뉴\프로그램\Codex Usage Monitor
설정 파일: %LOCALAPPDATA%\CodexUsageMonitor\settings.json
```

설치본은 시작할 때와 6시간마다 GitHub의 정식 릴리스를 확인합니다. 새 버전이 있으면 Windows 알림이 표시됩니다. **설정 → 정보**에서 **v…(으)로 업데이트**를 선택하면 릴리스 파일을 다운로드하고 SHA-256 체크섬을 검증한 뒤 설치본을 교체하고 자동으로 다시 실행합니다. 체크섬은 다운로드 무결성을 확인하지만 게시자 서명을 대신하지는 않습니다. 설정 파일은 유지됩니다.

언제든 **설정 → 정보**에서 **업데이트 확인**을 선택해 직접 확인할 수도 있습니다. 자동 설치는 위에 표시된 설치 경로에서 실행한 경우에만 동작하며 소스·디버그 빌드는 덮어쓰지 않습니다.

v1.1.0 이전 버전에는 업데이터가 없으므로 v1.1.0 이상을 한 번 직접 설치해야 합니다.

최초 설치 시 UI 언어는 영어가 기본이며, 프리셋 1은 라벨 박스형 / 블랙 색상 모드 / Codex 컬러 테마 / 배경 Alpha `0`으로 미리 구성됩니다. 기존 `settings.json`은 덮어쓰지 않습니다.

설치본을 제거하려면 **설정 → 정보**에서 **제거**를 선택합니다. 확인창에서 설정, 프리셋 및 로그를 유지하거나 함께 삭제할 수 있습니다. 제거 시 시작 메뉴 바로가기와 Windows 자동 실행 등록도 삭제됩니다.

<details>
<summary><strong>직접 빌드하기</strong></summary>

<br>

```powershell
dotnet build
```

설치 프로그램과 같은 단독 실행 파일을 만들려면 다음을 실행합니다.

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

결과 파일: `bin\Release\net8.0-windows\win-x64\publish\CodexUsageMonitor.exe`

</details>

<a id="controls"></a>

## 🖱️ 실행 및 조작법

프로그램은 일반 작업 표시줄 창이 아니라 알림 영역의 트레이 프로그램으로 실행됩니다.

- 트레이 아이콘 왼쪽 클릭: 상세 사용량 표시
- 트레이 아이콘 오른쪽 클릭: 상태, 새로고침, Compact Bar 표시 전환, 설정, 종료
- 트레이 아이콘 또는 Compact Bar 더블 클릭: 설정 열기
- Compact Bar 오른쪽 클릭: 새로고침, 설정 또는 위치 초기화
- Compact Bar 드래그: 위치 이동 및 자동 저장
- **설정 → 정보**: 버전, 저작자, 저장소, 진단 정보, 업데이트 및 제거

Compact Bar가 보이지 않으면 트레이 아이콘을 오른쪽 클릭하고 **Compact Bar 표시**를 켭니다.

<a id="settings"></a>

## ⚙️ 설정 가이드

### 일반 설정

| 설정 | 설명 |
|---|---|
| Compact Bar 표시 | 화면의 2×2 모니터를 표시하거나 숨깁니다. |
| Windows 시작 시 자동 실행 | Windows 시작 프로그램에 등록하거나 해제합니다. |
| 언어 | 영어, 한국어, 중국어(간체), 일본어를 선택합니다. 선택 즉시 현재 설정창이 번역되며, 저장하면 프로그램 전체에 적용됩니다. |
| 갱신 주기 | Codex 사용량 갱신 주기를 30~1,800초로 설정합니다. CPU와 RAM은 별도로 갱신됩니다. |
| 한도 임계치 알림 | 5시간 또는 주간 남은 한도가 20%·10%·5% 구간에 진입할 때 한 번씩 알립니다. 한도가 초기화되면 알림 상태도 초기화됩니다. |
| 방해 금지 시간 | 지정한 로컬 시간대에는 임계치 알림을 미뤘다가 종료 후 알립니다. |
| 고급 설정 | Codex 실행 파일 경로를 포함합니다. 일반적으로 `codex`를 유지하고, 실행에 실패하면 `where.exe codex` 결과의 전체 경로를 입력합니다. |

설정창을 열어둔 상태에서도 변경 내용이 Compact Bar에 즉시 미리보기됩니다. 드롭다운에서 프리셋 슬롯을 고른 뒤 **불러오기** 또는 **슬롯 저장**을 사용합니다. 각 슬롯에는 스타일, 색상 모드, 컬러 테마, 배경, 표시 기준, 항목 표시 여부, 표현 방식과 색상이 저장됩니다.

팔레트는 Absolutely, Ayu, Catppuccin, Codex, Dracula, Everforest, GitHub, Gruvbox, Linear, Lobster, Material, Matrix, Monokai, Night Owl, Nord, Notion, One, Oscurange, Proof, Raycast, Rose Pine, Sentry, Solarized, Temple, Tokyo Night, Vercel, VS Code Plus, Xcode를 제공합니다. 블랙/화이트 선택지는 각 Codex 팔레트가 실제 제공하는 밝기 조합만 표시됩니다.

설정은 **일반**, **표시**, **정보** 탭으로 구분됩니다. 표시 탭 한곳에서 프리셋, 모양, 표시 기준과 4개 항목 설정을 스크롤하며 편집할 수 있습니다. 50~200% 슬라이더와 100% 초기화 버튼을 제공하며, 스타일 변경은 현재 색상을 유지하고 색상 모드 또는 컬러 테마를 선택하면 해당 테마의 배경·Fill·트랙 색상이 적용됩니다. 원형 게이지 높이는 현재 모니터의 작업 표시줄 높이를 기준으로 합니다. 업데이트 확인과 다운로드에는 진행 표시와 취소 버튼이 제공됩니다.

설정은 검증된 임시 파일을 거쳐 원자적으로 저장됩니다. 이전 정상 파일은 `settings.json.bak`으로 유지되며 기본 JSON이 손상되면 백업을 복구하고 트레이 알림으로 안내합니다. `SettingsVersion` 필드는 향후 호환 마이그레이션에 사용됩니다.

### Compact Bar 및 항목 설정

**표시** 탭에서 Codex 한도를 남은 퍼센트 또는 사용한 퍼센트로 표시할지 선택합니다. CPU와 RAM은 항상 현재 사용률입니다.

`5H`, `WK`, `CPU`, `RAM` 항목마다 다음 설정을 제공합니다.

| 설정 | 설명 |
|---|---|
| 표시 | 해당 항목을 켜거나 끕니다. |
| 퍼센트만 | FillBar 없이 수치만 표시합니다. |
| FillBar만 | FillBar만 표시합니다. |
| 퍼센트 + FillBar | 수치와 FillBar를 함께 표시합니다. |
| Fill 색상 | 채워진 그래프의 색상을 설정합니다. |
| 트랙 색상 | 채워지지 않은 그래프 배경색을 설정합니다. |

**배경**은 둥근 모서리의 Compact Bar 배경만 조절합니다. 색상 선택창에서는 RGBA 값, 알파 슬라이더, 6자리 RGB Hex 값을 사용할 수 있습니다. 배경 알파를 `0`으로 설정해도 배경만 투명해지며 글자와 그래프는 계속 표시됩니다.

<a id="privacy"></a>

## 🔒 데이터 및 개인정보

웹페이지를 스크래핑하거나 인증 파일을 직접 읽지 않습니다. 로컬 `codex app-server`를 실행하고 공식 JSON-RPC 메서드 `account/rateLimits/read`와 `account/usage/read`를 사용합니다. API 키를 저장하지 않습니다.

<a id="troubleshooting"></a>

## 🧰 문제 해결

### Codex 사용량을 가져오지 못함

`codex login status`를 실행합니다. 필요하면 `codex login`으로 다시 로그인하세요. 설정의 `Codex 실행 파일`을 `where.exe codex`에서 확인한 전체 경로로 바꿔보세요.

### Compact Bar가 보이지 않음

알림 영역을 확인하고 트레이 메뉴에서 **Compact Bar 표시**를 켭니다. 모니터 구성이 바뀌면 활성 화면 안에 일부가 보이도록 자동 보정되며, 필요하면 **위치 초기화**를 선택하세요.

### 진단 정보 수집

**설정 → 정보**에서 **진단 정보 복사**를 선택합니다. 앱 버전, app-server 상태, 마지막 갱신·오류, Windows/DPI/모니터 정보와 설정·로그 경로가 클립보드에 복사됩니다.

### 재설치가 실패함

트레이 메뉴에서 Codex Usage Monitor를 종료한 뒤 `install.cmd`를 다시 실행해야 기존 실행 파일을 교체할 수 있습니다.

## 📄 라이선스

[MIT License](../LICENSE)로 배포됩니다.
