# Codex Usage Monitor

[English](README.md) | [한국어](README.ko.md) | [简体中文](README.zh-CN.md) | [日本語](README.ja.md)

Windows 화면에서 Codex의 단기·주간 사용 한도와 CPU·RAM 사용률을 확인하는 트레이 앱입니다.

![Codex Usage Monitor Compact Bar](docs/compact-bar.png)

Compact Bar는 2×2 구조입니다. 1열에는 위에서부터 `5H`와 `WK`, 2열에는 `CPU`와 `RAM`이 표시됩니다. 각 항목은 퍼센트, FillBar 또는 둘 다 표시할 수 있으며, 화면 어디로든 드래그하면 위치가 자동 저장됩니다.

## 주요 기능

- 5시간·주간·CPU·RAM 항목을 개별적으로 표시하거나 숨김
- Codex 한도를 남은 퍼센트 또는 사용한 퍼센트로 전환
- RGB·알파·Hex를 이용한 Fill/트랙/사각 배경 색상 설정
- 영어·한국어·중국어(간체)·일본어 설정 UI
- 갱신 주기, 트레이 제어, Windows 시작 시 자동 실행
- 초기화 시각과 오늘/누적 토큰 표시

## 요구 사항 및 빌드

- Windows 10/11
- Codex CLI 설치 및 ChatGPT 로그인
- 소스 빌드 시 .NET 8 SDK

```powershell
npm install -g @openai/codex
codex login
dotnet build
```

단독 실행 배포본은 `build.ps1`로 만듭니다. `install.cmd`를 더블 클릭하면 `%LOCALAPPDATA%\Programs\CodexUsageMonitor`에 설치하고 시작 메뉴 바로가기를 만든 뒤 앱을 실행합니다.

## 사용법

- 트레이 아이콘 왼쪽 클릭: 상세 사용량
- 트레이 아이콘 오른쪽 클릭: 새로고침, Compact Bar 전환, 설정, 종료
- Compact Bar 더블 클릭: 설정
- Compact Bar 드래그: 위치 이동 및 자동 저장

설정은 `%LOCALAPPDATA%\CodexUsageMonitor\settings.json`에 저장됩니다.

## 데이터 연결 방식

웹페이지를 스크래핑하거나 인증 파일을 직접 읽지 않습니다. 로컬 `codex app-server`를 실행하고 공식 JSON-RPC 메서드 `account/rateLimits/read`와 `account/usage/read`를 사용합니다. 앱은 API 키를 저장하지 않습니다.

## 문제 해결

app-server가 시작되지 않으면 설정에서 `codex.exe` 전체 경로를 지정하세요(`where.exe codex`). 로그인이 필요하면 `codex login`과 `codex login status`를 실행하세요. Compact Bar가 화면 밖에 있으면 모니터 구성이나 배율을 바꾼 뒤 트레이 메뉴에서 껐다가 다시 켜세요.
