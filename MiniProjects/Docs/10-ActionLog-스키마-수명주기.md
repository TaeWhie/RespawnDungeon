# 10. ActionLog 스키마와 데이터 수명 주기

구현 기준: `Data/TestData.cs`(`ActionLogEntry`, `TestDataRoot`), `ActionLogBuilder`, `Services/ActionLogPersistence.cs`, `DialogueConfigLoader`, `DungeonRunSimulator`, `DialogueManager`.

---

## 10.1 루트 타입

```json
{ "ActionLog": [ /* ActionLogEntry */ ] }
```

- 타입명: `TestDataRoot` — 필드 이름이 `ActionLog`인 **타임라인 루트**.
- 파일: 기본 `Config/ActionLog.json`. 없으면 `LoadTimelineData`가 `TestData.json`을 볼 수 있음(`DialogueConfigLoader`).

---

## 10.2 `ActionLogEntry` 개요

| 필드 | 설명 |
|------|------|
| `Order` | 전체 타임라인 순서(오름차순 처리). |
| `Type` | `"Dungeon"` 또는 `"Base"`. |

- **`Dungeon`**: 던전명, 층, 전투·피해·루팅·함정 등 시뮬이 채우는 필드. 자세한 필드는 `TestData.cs` 주석 참고.
- **`Base`**: 아지트 이벤트. `Location`, `EventType`, `PartyMembers`, 선택 `Dialogue`(대사 줄 배열) 등.

시뮬레이터와 빌더는 **줄글 `Summary` 필드에 의존하지 않는 것이 원칙** — 구조화 JSON이 진실이다. 프롬프트용 줄글은 `ActionLogNarrativeFormatter`가 런타임에 생성한다.

---

## 10.3 `ActionLogBuilder`

- 입력: `ActionLog` 전체 리스트.
- 출력:  
  - 캐릭터별 `DungeonLog` 목록(던전 런 구간을 묶어 파싱),  
  - `BaseLogEntry` 목록.
- `MemoryManager.BuildEpisodicBuffer`는 여기서 나온 던전 로그를 자연어 에피소드로 쓴다.

---

## 10.4 데이터가 쌓이는 경로

| 경로 | 도구 | 결과 |
|------|------|------|
| 메뉴 4 원정 | `PartyManagementConsole.RunExpeditionAsync` → `DungeonRunSimulator` 등 | `SaveActionLogAfterSimulation` — **병합** 시 기존 로그 뒤에 이어 붙이고 `Order` 재번호. |
| CLI `--gen-actionlog` | `Program.cs` | 동일 저장기. `--replace-actionlog`면 덮어쓰기. `--no-sync-chars`면 캐릭터 DB 저장 생략. |
| 메뉴 1·2 대화 종료 | `PersistDialogueSessionToActionLogIfEnabled` | `ActionLogPersistence.AppendGuildDialogueSession` — Base 타입 대화(라벨·위치 추론). |
| (선택) 관점 덤프 | 초기화 시 `PerspectiveMemoryExportPath` | **별도 JSON 파일**. ActionLog와 혼동 금지. |

---

## 10.5 병합·덮어쓰기 (`SaveActionLogAfterSimulation`)

- `replaceExisting == false`: 기존 `ActionLog.json`을 읽어 새 원정 항목을 **append**, 전체 `Order` 재부여.
- `replaceExisting == true` 또는 새 루트만: `SaveActionLog`로 통째 저장.

---

## 10.6 대화 세션 저장 시 위치 추론

`InferBaseLocationFromWorldState(worldState)` — 문자열에 "집무실", "식당", "훈련", "게시", "접수" 등이 있으면 `reception` / `cafeteria` / `training_ground` / `quest_board` / `main_hall` 등으로 매핑. 프롬프트에 쓴 `worldState`와 저장 Location이 연결된다.

---

## 10.7 캐릭터 DB와의 정합

- 원정 후 `--gen-actionlog` 기본 경로는 시뮬 결과로 **`CharactersDatabase.json`(또는 `Characters.json`) 갱신** 가능.
- 대화 세션의 관계 정산은 **`Relationships`에 해당 `TargetId` 엣지가 있을 때만** 델타 적용.

---

## 10.8 빠른 트러블슈팅

| 증상 | 확인 |
|------|------|
| 프롬프트에 원정이 안 보임 | `UseActionLogNarrativeProse` / 꼬리 건수, 길이 제한, 길드장 **일상 턴**에서는 ActionLog 블록이 의도적으로 생략됨([08](08-런타임-시퀀스-상세.md)). |
| 에피소드 비어 있음 | `ActionLog.json`이 비었거나 Dungeon 구간이 파서에 안 잡힘. |
| 관점 덤프가 안 됨 | 경로가 `ActionLog.json`과 같으면 **의도적으로 스킵**. |

다음: [11-길드집무실-가드레일-CLI-탐색.md](11-길드집무실-가드레일-CLI-탐색.md)
