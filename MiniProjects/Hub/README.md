# GuildDialogue Hub (웹 UI)

콘솔 `GuildDialogue`의 메뉴 **1~5**와 동일한 기능을 브라우저에서 쓰기 위한 Vite + React 화면입니다. 백엔드는 **같은 리포의 C# 프로세스**가 `--hub-api`로 HTTP API를 제공합니다 (기본 `http://127.0.0.1:5050`).

**메인 화면**: 콘솔 **메뉴 1(아지트)** 은 카드가 아니라 **진입 시 자동 실행**되며, 트랜스크립트가 상단에 채워집니다. 그 아래 카드 **1~4**는 콘솔 메뉴 **2~5**(길드장 집무실·파티·원정·캐릭터 생성)에 대응합니다.

## 실행 순서

1. **터미널 A** — Hub API (Ollama·임베딩 포함, 대화·원정에 필요):

   ```bash
   cd MiniProjects/GuildDialogue
   dotnet run -- --hub-api
   ```

   또는 Hub 폴더에서: `npm run api`

2. **터미널 B** — 프론트 (Vite가 `/api`를 5050으로 프록시):

   ```bash
   cd MiniProjects/Hub
   npm run dev
   ```

3. 브라우저에서 Vite 주소(보통 `http://localhost:5173`)를 엽니다.

## 이전 bridge.cjs

예전에는 `node bridge.cjs`로 JSON만 읽었습니다. **대화(메뉴 1·2)·캐릭터 생성(5)** 등은 C# `DialogueManager`가 필요하므로, 지금은 **`--hub-api`를 켜는 것이 정식 경로**입니다. `bridge.cjs`는 레거시로 두었을 수 있습니다.

## API 개요

| 영역 | 메서드 | 설명 |
|------|--------|------|
| 상태 | `GET /api/state` | Config 경로, 캐릭터, 파티, 직업, 최근 로그 |
| 대화 준비 | `POST /api/dialogue/init` | 임베딩 인덱스 등 초기화 |
| 아지트 | `POST /api/dialogue/spectator/run` | 메뉴 1 (활동 배정 + 2인 2~4왕복) |
| 집무실 | `POST /api/dialogue/guild-master/begin` · `message` · `switch-buddy` · `end` | 메뉴 2 |
| 파티 | `GET/POST/PUT/DELETE /api/parties` | 메뉴 3 |
| 원정 | `POST /api/expedition` | 메뉴 4 |
| 캐릭터 | `POST /api/character/create` | 메뉴 5 |

포트 변경: `dotnet run -- --hub-api --port 5051`
