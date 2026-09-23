<p align="center">
  <img src="../Resources/CodexUsageMonitor.ico" alt="Codex Usage Monitor 아이콘" width="96">
</p>

<h1 align="center">Codex Usage Monitor</h1>

<p align="center">
  Codex의 5시간·주간 한도와 CPU·RAM 사용률을 표시하는 가벼운 Windows 트레이 모니터입니다.
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
  <a href="../LICENSE"><img src="https://img.shields.io/badge/License-MIT-2EA44F?style=flat-square" alt="MIT License"></a>
</p>

<p align="center">
  <img src="../docs/compact-bar.png" alt="Codex Usage Monitor Compact Bar">
</p>

## 주요 기능

- Codex 5시간·주간 한도 비율과 초기화까지 남은 시간
- CPU·RAM 사용률
- 5시간·주간별 한도 알림과 방해 금지 시간
- 스타일 6개, Codex 컬러 팔레트 28개, 시스템·라이트·다크 화면 모드
- 드래그로 패널 순서 변경, 모양 프리셋 3개
- 영어·한국어·중국어(간체)·일본어
- 웹 스크래핑이나 인증 파일 접근 없이 로컬 `codex app-server` 사용

## 최초 실행 기본값

새로 설치하면 다음 값으로 시작합니다.

- **라이트** 스타일과 **Codex** 컬러 테마
- **시스템** 화면 모드
- 투명 배경 켜짐, 배경 알파 `0`
- 초기화 시간은 별도 `5H`·`WK` 접두사 없이 `↻ 02h`, `↻ 05d` 형식으로 표시

기존 설정은 유지됩니다. 삭제된 다크 미니멀·카드·원형 게이지·둥근 캡슐 스타일을 사용 중이었다면 라이트 스타일로 전환됩니다.

## 스타일

| | |
|---|---|
| **라이트**<br><img src="../docs/themes/light.png" alt="라이트 스타일" width="400"> | **라벨 박스형**<br><img src="../docs/themes/label-boxes.png" alt="라벨 박스형 스타일" width="400"> |
| **네온 글로우**<br><img src="../docs/themes/neon-glow.png" alt="네온 글로우 스타일" width="400"> | **컴팩트 바**<br><img src="../docs/themes/compact-rows.png" alt="컴팩트 바 스타일" width="400"> |
| **그라데이션**<br><img src="../docs/themes/gradient.png" alt="그라데이션 스타일" width="400"> | **미니멀 아이콘 + 텍스트**<br><img src="../docs/themes/minimal-icons.png" alt="미니멀 아이콘과 텍스트 스타일" width="400"> |

## 설치

필요한 항목:

- Windows 10 또는 11 64비트
- 소스 빌드 시 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Codex CLI 설치 및 로그인

```powershell
npm install -g @openai/codex
codex login
codex login status
```

저장소를 내려받거나 복제한 뒤 `install.cmd`를 실행합니다. 프로그램은 `%LOCALAPPDATA%\Programs\CodexUsageMonitor`에 설치되고 시작 메뉴 바로가기가 생성됩니다.

설정 파일은 `%LOCALAPPDATA%\CodexUsageMonitor\settings.json`에 저장됩니다. 시작할 때와 6시간마다 GitHub 정식 릴리스를 확인하며, **설정 → 정보**에서도 직접 업데이트를 확인할 수 있습니다.

## 조작 및 설정

- 트레이 아이콘 왼쪽 클릭: 상세 사용량 표시
- 트레이 아이콘 오른쪽 클릭: 새로고침, Compact Bar 표시 전환, 설정, 종료
- Compact Bar 드래그: 위치 이동
- Compact Bar 오른쪽 클릭: 새로고침, 설정, 위치 초기화
- 트레이 아이콘 또는 Compact Bar 더블 클릭: 설정 열기

**모양** 탭은 실제 Compact Bar 미리보기를 제공합니다. 미리보기에서 패널을 드래그하면 위치가 교환되고, 배경·패널·패널 안 요소를 클릭하면 해당 항목 설정이 열립니다. 항목은 퍼센트, 그래프 또는 둘 다 표시할 수 있으며, 숨긴 요소는 미리보기에서 반투명하게 보입니다.

배경 설정에는 투명 배경 토글과 RGBA 색상 편집기가 있습니다. 투명 배경을 켜도 글자와 그래프는 계속 보입니다. 초기화 패널에서는 `5H`와 `WK` 접두사 표시 여부를 각각 선택할 수 있습니다.

고급 설정에는 Codex 실행 파일 경로만 있습니다. 일반적으로 `codex`를 유지하고, 실행에 실패할 때만 `where.exe codex` 결과의 전체 경로를 입력합니다.

## 빌드

```powershell
dotnet build CodexUsageMonitor.csproj
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

결과 파일은 `bin\Release\net8.0-windows\win-x64\publish`에 생성됩니다.

## 개인정보 및 문제 해결

프로그램은 로컬 `codex app-server`를 통해 `account/rateLimits/read`와 `account/usage/read`를 호출하며 API 키를 저장하지 않습니다.

Codex 사용량이 나오지 않으면 `codex login status`를 확인하고 필요하면 다시 로그인하세요. Compact Bar가 보이지 않으면 트레이 메뉴에서 표시를 켜거나 위치를 초기화하세요. **설정 → 정보 → 진단 정보 복사**에서 문제 해결에 필요한 정보를 복사할 수 있습니다.

## 라이선스

[MIT License](../LICENSE)로 배포됩니다.
