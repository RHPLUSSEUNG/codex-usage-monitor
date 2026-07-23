# Codex Usage Monitor

[English](README.md) | [한국어](README.ko.md) | [简体中文](README.zh-CN.md) | [日本語](README.ja.md)

<p align="center">
  <img src="Resources/CodexUsageMonitor.ico" alt="Codex Usage Monitor 아이콘" width="128">
</p>

Codex 사용 한도와 현재 CPU·RAM 사용률을 함께 표시하는 Windows 10/11용 트레이 프로그램입니다.

![Codex Usage Monitor Compact Bar](docs/compact-bar.png)

Compact Bar는 두 열로 구성됩니다. 1열에는 위에서부터 `5H`와 `WK`, 2열에는 `CPU`와 `RAM`이 표시됩니다. 각 항목은 퍼센트, FillBar 또는 둘 다 표시할 수 있습니다. 바를 화면 어디로든 드래그하면 위치가 자동 저장됩니다.

## 설치 전 준비

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

## 간편 설치

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

업데이트할 때는 새 소스를 받은 뒤 트레이 메뉴에서 기존 프로그램을 종료하고 `install.cmd`를 다시 실행합니다. 기존 설정은 유지됩니다.

## 직접 빌드

```powershell
dotnet build
```

설치 프로그램과 같은 단독 실행 파일을 만들려면 다음을 실행합니다.

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

결과 파일: `bin\Release\net8.0-windows\win-x64\publish\CodexUsageMonitor.exe`

## 실행 및 조작법

프로그램은 일반 작업 표시줄 창이 아니라 알림 영역의 트레이 프로그램으로 실행됩니다.

- 트레이 아이콘 왼쪽 클릭: 상세 사용량 표시
- 트레이 아이콘 오른쪽 클릭: 상태, 새로고침, Compact Bar 표시 전환, 설정, 종료
- 트레이 아이콘 또는 Compact Bar 더블 클릭: 설정 열기
- Compact Bar 오른쪽 클릭: 새로고침 또는 설정
- Compact Bar 드래그: 위치 이동 및 자동 저장

Compact Bar가 보이지 않으면 트레이 아이콘을 오른쪽 클릭하고 **Compact Bar 표시**를 켭니다.

## 설정 가이드

### 일반 설정

| 설정 | 설명 |
|---|---|
| Compact Bar 표시 | 화면의 2×2 모니터를 표시하거나 숨깁니다. |
| Compact Bar 스타일 | 다크 미니멀, 라벨 박스형, 네온 글로우, 라이트 모드, 카드, 원형 게이지, 컴팩트 바, 둥근 캡슐, 그라데이션, 미니멀 아이콘 + 텍스트 중에서 선택합니다. |
| Windows 시작 시 자동 실행 | Windows 시작 프로그램에 등록하거나 해제합니다. |
| 언어 | 영어, 한국어, 중국어(간체), 일본어를 선택합니다. 선택 즉시 현재 설정창이 번역되며, 저장하면 프로그램 전체에 적용됩니다. |
| 표시 기준 | Codex 한도를 남은 퍼센트 또는 사용한 퍼센트로 표시합니다. CPU와 RAM은 항상 현재 사용률입니다. |
| 갱신 주기 | Codex 사용량 갱신 주기를 30~1,800초로 설정합니다. CPU와 RAM은 별도로 갱신됩니다. |
| Codex 실행 파일 | 일반적으로 `codex`를 유지합니다. 실행에 실패하면 `where.exe codex` 결과의 전체 경로를 입력합니다. |

설정창을 열어둔 상태에서도 변경 내용이 Compact Bar에 즉시 미리보기됩니다. 프리셋 3개에는 스타일, 배경, 표시 기준, 항목 표시 여부, 표현 방식과 색상이 저장됩니다. 현재 모양을 보관하려면 **슬롯 저장**, 적용하려면 **불러오기**를 누릅니다.

### Compact Bar 및 항목 설정

`5H`, `WK`, `CPU`, `RAM` 항목마다 다음 설정을 제공합니다.

| 설정 | 설명 |
|---|---|
| 표시 | 해당 항목을 켜거나 끕니다. |
| 퍼센트만 | FillBar 없이 수치만 표시합니다. |
| FillBar만 | FillBar만 표시합니다. |
| 퍼센트 + FillBar | 수치와 FillBar를 함께 표시합니다. |
| Fill 색상 | 채워진 그래프의 색상을 설정합니다. |
| 트랙 색상 | 채워지지 않은 그래프 배경색을 설정합니다. |

**사각 배경**은 Compact Bar의 배경만 조절합니다. 색상 선택창에서는 RGBA 값, 알파 슬라이더, 6자리 RGB Hex 값을 사용할 수 있습니다. 배경 알파를 `0`으로 설정해도 배경만 투명해지며 글자와 그래프는 계속 표시됩니다.

## 데이터 및 개인정보

웹페이지를 스크래핑하거나 인증 파일을 직접 읽지 않습니다. 로컬 `codex app-server`를 실행하고 공식 JSON-RPC 메서드 `account/rateLimits/read`와 `account/usage/read`를 사용합니다. API 키를 저장하지 않습니다.

## 문제 해결

### Codex 사용량을 가져오지 못함

`codex login status`를 실행합니다. 필요하면 `codex login`으로 다시 로그인하세요. 설정의 `Codex 실행 파일`을 `where.exe codex`에서 확인한 전체 경로로 바꿔보세요.

### Compact Bar가 보이지 않음

알림 영역을 확인하고 트레이 메뉴에서 **Compact Bar 표시**를 켭니다. 모니터 구성이나 화면 배율을 바꾼 뒤에는 바를 한 번 껐다가 다시 켜세요.

### 재설치가 실패함

트레이 메뉴에서 Codex Usage Monitor를 종료한 뒤 `install.cmd`를 다시 실행해야 기존 실행 파일을 교체할 수 있습니다.
