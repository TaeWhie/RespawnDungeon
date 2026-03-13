# 푸시 기록 (Push Log)

푸시하기 **전에** 이 파일에 이번 푸시 내용을 간단히 적고, 해당 변경을 커밋에 포함한 뒤 푸시합니다.

---

## 빈 프로젝트 → 현재까지 전체 정리

(프로젝트 전체가 어떤 단계까지 구현됐는지 한눈에 보는 요약.)

### 던전 생성
- **알고리즘**: RoomFirst, CorridorFirst, SimpleRandomWalk 던전 생성기 (AbstractDungeonGenerator 상속).
- **타일**: ProceduralGenerationAlgorithms, WallGenerator, WallTypesHelper. 바닥·벽(기본/코너) 타일맵.
- **데이터**: SimpleRandomWalkSO 등 ScriptableObject 기반 파라미터.
- **특수 오브젝트**: 시작점·출구 배치. TilemapVisualizer에서 바닥/벽/시작·출구 그리기.

### 맵·경로
- **MapManager**: 격자 맵, 이동 가능 셀(walkable), 방문(visited), 도달 불가(unreachable). Bounds, CellToIndex, GetUnvisitedNeighbors, GetFrontierCells, HasLineOfSight.
- **Pathfinder**: A* 경로 탐색.

### 플레이어·탐험 AI
- **ExplorerAI**: 상태(Idle / Exploring / Navigating). 프론티어 기반 탐험, 인접 미방문 셀 선택, 막다른 길 시 재타겟팅(A*로 프론티어까지). 출구 발견 시 출구로 이동. 걷기/뛰기(거리 기준), 애니메이션·스프라이트 플립.
- **시야 갱신**: MarkVisitedInRadius(리더 위치, full반경, structure반경).

### 파티(동료)
- **PartyFollower**: 리더 뒤 포메이션 따라가기. MapManager·Pathfinder로 A* 경로 이동(장애물 우회). 슬롯별 거리·수직 오프셋·겹침 방지(overlap stop). 맨앞 동료는 리더에 더 붙음(firstFollowerDistance). 리더/동료 시야 공유(MarkVisitedInRadius).
- **TilemapVisualizer**: 리더·동료 스폰, PartyFollower 부착, SetLeader/SetSlotIndex. Layer: Player(6), Ally(7). Physics2D에서 Player–Ally 충돌 비활성화.
- **PartyCameraController**: 파티 중심 카메라.

### 장애물·보물
- **PlacePerlinObstaclesAndTreasures**: 펄린 노이즈로 장애물·보물상자 군집 배치. 입구–출구 **최장 경로** 상 셀에는 배치 안 함(GetCellsOnLongestPath, DFS 백트래킹). 장애물 셀은 floor에서 제거(비이동).
- **VisibilityByViewStage**: 장애물·보물은 시야 단계에 따라 표시/비표시.

### 시야 3단계
- **3단계(미탐험)**: 짙은 안개만 표시. 바닥·벽 타일 미렌더, 오브젝트 SetActive(false). ExplorationFogView에서 시야 구역만 갱신.
- **2단계(구조만)**: 옅은 안개, 바닥·벽만 표시, 오브젝트 숨김. structure 반경 > full 반경.
- **1단계(전부 보임)**: 안개 없음, 오브젝트 표시. 한 번 1단계로 밝힌 셀은 everFullView로 계속 표시.
- **파티 시야 공유**: 리더·동료 모두 같은 MapManager에 MarkVisitedInRadius 호출. FPS: 시야 구역만 안개 갱신, VisibilityByViewStage 캐시.

### 탐험 완료·이동 정책
- **탐험 완료**: 출구 셀 도착 시에만 완료(_onReachedExit). IsExplorationComplete로 Idle/로그 하지 않음.
- **이동·모험심**: 기본은 2단계 시야 가장자리(방문 인접 많은 미방문 셀)만 선택. 모험심 확률로 3단계(미탐험) 방향 선택. 2단계로 갈 곳 없을 때만 예외로 3단계 진입. GetVisitedNeighborCount, ChooseBestExplorationDirection·재타겟팅에 반영.

### 기타
- **Input**: InanimateControls 등에서 InputSystem 사용(Keyboard.current).
- **Docs**: MCP-Debugging-Strategy, MCP-Feature-Verification-Strategy, PushLog. Cursor 규칙: unity-mcp-process, push-log-before-push(푸시 전 기록).

---

## 기록 형식

```markdown
### YYYY-MM-DD (커밋 해시 또는 메시지 요약)
- 변경 요약 1
- 변경 요약 2
```

---

## 기록

### 2026-03-13 — 인벤토리 UI 개선, 장비 레이아웃 리팩터, 아이템 데이터 보강
- **InventoryView.uxml 리팩터**: 장비창(EquipmentSection) 레이아웃 재배치 — 헬멧을 상단 중앙으로 분리, BodyContainer에 팔·갑옷·방패를 가로 배치, 악세서리 슬롯을 AccStack으로 묶음. 불필요한 주석·spacer 제거, translate로 미세 위치 조정. InventoryRoot 초기 display를 none으로 설정.
- **InventoryManager**: InventoryRoot 요소를 직접 Q로 찾아 display 제어 추가 (열기·닫기 시 InventoryRoot + root 둘 다 토글).
- **InventoryUIController**: 장비 장착/해제 시 `HideTooltip()` 호출 추가 — 장착·해제 후 툴팁이 남아 있던 문제 수정.
- **아이템 데이터**: Sword(높이 2→3, 설명·스탯 추가), Armor/Potion/Ring/Shield 등 SO에 description·atk/def/str/dex/int 필드 값 채움.
- **KnightData**: 캐릭터 데이터 에셋 업데이트.
- **씬**: MainScene 오브젝트 추가/조정.

### 2026-03-13 — 인벤토리 시스템 신규, HUD UI Toolkit, 에디터 도구
- **인벤토리 시스템** (`_Scripts/Inventory/`): `InventoryManager`(싱글턴, 캐릭터별 가방 생성·열기·닫기, 테스트 아이템 자동 배치), `InventoryData`(그리드 기반 점유·배치·이동), `ItemData`(SO 아이템 원형), `InventoryUIController`(UI Toolkit 그리드 인벤토리 UI), `CharacterEquipment`(장비 슬롯). 아이템 드래그→다른 캐릭터 초상화에 드롭 시 가방 전환·아이템 전송(Follow Drag & Portrait Drop).
- **HUD** (`_Scripts/UI/`, `UI/HUD/`): `CharacterData`(이름·초상화·HP/MP), `UI_CharacterUnit`(유닛별 초상화·HP/MP 바·인벤토리 버튼, 이벤트 기반), `HUDManager`(동적 생성·이벤트 구독), `HUDTestSetup`(테스트 데이터 자동 세팅). UXML/USS로 하단 파티 HUD 레이아웃·스타일 정의(`HUDMain.uxml`, `CharacterUnit.uxml`, `RPG_HUD.uss`).
- **인벤토리 UI** (`UI/Inventory/`): `InventoryView.uxml`, `Inventory.uss` — UI Toolkit 기반 그리드 인벤토리 뷰.
- **에디터 도구** (`Editor/`): `ItemAssetCreator`(샘플 아이템 SO 일괄 생성, 스프라이트 Point Filter 자동 설정), `HUDFixer`(HUD 수정 유틸).
- **PartyFollower**: `_overlapStopRadius` 기본값 0.5→0.8 조정.
- **씬**: MainScene에 PartyHUD_Canvas·HUDManager 오브젝트 추가.
- **버그 수정**: `ItemAssetCreator`에 `using System.IO` 누락 수정, `InventoryManager.AddInitialTestItems` 오버로드 추가로 CS1501 해결.

### 2026-03-12 — 파티 포메이션 리팩터링, 카메라/동료 이동 조정
- **PartyFormationProvider**: 리더에 부착되는 새 컴포넌트. 리더가 새 셀에 2칸 이상 이동했을 때만 슬롯별 포메이션 목표 셀/월드 좌표를 계산하고, 여기서 **비틀기(jitter)**·슬롯 간 충돌 방지(앞 슬롯이 쓴 셀/목표를 뒤 슬롯이 재사용하지 않도록)까지 모두 처리함. 동료는 더 이상 스스로 포메이션을 계산하지 않고, 리더가 준 셀만 따라감.
- **PartyFollower**: 목표 셀 계산을 `PartyFormationProvider`에 완전히 위임. Provider가 줄 때마다 그 셀을 향해 A* 경로를 잡고, 웨이포인트 도착/스냅 로직은 리더와 동일한 작은 반경 기반으로 통일. 달리기 조건을 `_runDistanceThreshold`(기본 2칸 이상) + 리더 속도(리더가 충분히 빠르면 같이 뛰기) 기반으로 조정.
- **PartyCameraController**: 카메라 중심을 항상 파티 중심이 아니라 **리더(Player)** 위치를 우선 사용하도록 변경. 줌(OrthographicSize)은 기존처럼 파티 전체 Bounds + 패딩을 기준으로 맞춰, 리더는 중앙에 두되 동료도 한 화면에 들어오도록 함. `SnapToPartyImmediate`도 동일 규칙 적용.
- **기타/규칙 정리**: Pathfinder의 reserved 셀 기반 경로 분리 실험 코드는 롤백해 기존 시그니처·동작으로 되돌림. 푸시 규칙대로 이번 변경 사항을 PushLog에 기록함(이전 커밋에서 바로 기록하지 않은 점은 이후부터 주의).*** End Patch***} />

### 2025-03-11 — 안개 2단계 벽·오브젝트 셀 적용, 디버그 기즈모
- **ExplorationFogView**: 갱신 대상에 wall 셀 포함(반경 내 셀의 인접 4방 추가). 벽 셀은 인접 floor 방문 여부로 단계 결정해 2/1단계에서 벽 표시. 오브젝트(장애물·상자) 셀도 인접 floor 방문 시 2단계 적용(바닥만 표시, 오브젝트는 1단계에서만 RefreshObjectsVisibility로 표시). RefreshFogAll 동일 로직. DebugReveal 시 장애물/상자 셀에도 바닥 복원.
- **ExplorationDebugPanel**: 안개 단계(1/2/3) 기즈모 표시(Show Fog Stage 토글, Handles.Label, 단계별 색상·반경 슬라이더).

### 2025-03-11 — 모험심·점수 가중치·경로 거리·디버그 패널
- **ExplorerAI 모험심**: 모험심 0일 때 목적지 = 안개 2단계 셀만, 모험심 1일 때 = 2+3단계 풀 합침. 거리와 밝히는 양 둘 다 반영한 가중 점수(brightWeight·distWeight) 사용, 0은 거리 비중·1은 밝히는 양 비중.
- **점수 가중치 필드**: ScoreBrightWeightAdv0/Adv1, ScoreDistWeightAdv0/Adv1, ScoreTotalBrightWeightAdv1 추가(Inspector·런타임 조절). 모험심 0/1 사이는 Lerp로 보간.
- **거리 정의**: 로컬 이동(PickFromCandidatesWithTieBreak)에서 Manhattan 대신 **실제 경로 길이**(GetPath().Count) 사용. 재타겟팅은 기존대로 path.Count 사용.
- **ExplorationDebugPanel**: Adventurousness 토글로 펼치면 모험심 슬라이더 + 점수 가중치 5개 슬라이더 표시. ExplorerAI.Adventurousness 및 가중치 프로퍼티로 런타임 조절.

### 2025-03-11 — 문 배치·황금방 시도 롤백, 디버그 패널/기즈모 통합
- **TilemapVisualizer / 던전 구조**: 황금 방 경계 벽/장애물 실험 코드 제거, 상자/장애물/문 배치만 남기고 맵 구조는 원래대로 유지.
- **문 배치**: 출구·황금상자 필수 경로에서 "양옆이 복도인 복도 셀"만 문 후보로 사용하는 필터 유지, Door 프리팹 추가.
- **디버그 패널**: `ExplorationDebugPanel` 추가로 Reveal Map(전체 시야) 및 MapManager 기즈모 on/off를 한 곳에서 토글.
- **시야/기즈모**: `MapManager.DebugRevealAll` 기반 전체 시야 토글, `ExplorationFogView`에 DebugForceRevealAll/NormalFog 도입, VisibilityByViewStage는 DebugRevealAll 시 항상 보이도록. MapManager 기즈모 옵션 단순화(DrawGizmos만 사용).
- **경로 기즈모**: `ExplorerAI`와 `PartyFollower`에 OnDrawGizmos 추가, 리더/동료의 현재 A* 경로와 목표 셀을 색상별 선·구로 시각화.

### 2025-03-11 — 보물상자 픽·열기, 상자 Animator Bool
- **ExplorerAI**: 보물상자 발견 시 인접 stand 셀으로 이동, **목표 도착 후에만** PickingChest 전환(_navigatingToChestCell). Pick 파라미터 Bool로 켜고, 같은 자리에서 두 번 반복 후 꺼서 Exploring 복귀.
- **ChestOpenable**: 상자 픽 시 Idle=false, Open=true 설정 후 일정 시간 뒤 Destroy. 셀 walkable 복구. Animator GetComponent/GetComponentInChildren으로 캐시.
- **MapManager**: RegisterChests, MarkChestPicked(view.Open()), GetStandCellsNextToChest, SetCellWalkable.
- **TilemapVisualizer**: 골든/일반 상자 개수·간격 제한, chest 셀 floor 제거, GetOrAddChestOpenable·SetChestCell, GetLastPlacedChests.
- **AbstractDungeonGenerator·3개 생성기**: SetMapManagerWalkable 후 RegisterChestCellsToMapManager 호출.

### 2025-03-11 — ReTargeting 2단계/3단계 기준 적용
- ExplorerAI ReTargetingCoroutine: 프론티어 선택 시에도 2단계(방문 인접 많은)·3단계(적은) 분류 적용.
- 모험심이면 3단계 프론티어 풀에서, 아니면 2단계 풀에서 선택 후 경로 짧은 것·동점 시 방문 인접 수로 결정.

### 2025-03-11 — 빈 프로젝트부터 현재까지 전체 정리 반영
- PushLog 상단에 "빈 프로젝트 → 현재까지 전체 정리" 섹션 추가: 던전 생성, 맵·경로, 탐험 AI, 파티, 장애물·보물, 시야 3단계, 탐험 완료·이동 정책, 기타(Docs·규칙) 요약.

### 2025-03-11 — 처음부터 끝까지 정리 푸시
- **시야 3단계**: 3=짙은 안개(바닥/벽/오브젝트 미렌더), 2=옅은 안개(구조만), 1=전부 보임. 1단계로 한 번 밝힌 오브젝트는 계속 enable.
- **파티 시야 공유**: 리더·동료가 동일 MapManager로 visited/currentFullView/everFullView 갱신. 안개·오브젝트 표시는 시야 구역만 갱신해 FPS 개선, VisibilityByViewStage 목록 캐시.
- **탐험 완료**: 출구 도착 시에만 완료 처리. IsExplorationComplete로 Idle/로그 하던 부분 제거.
- **이동·모험심**: 기본은 2단계 시야 가장자리(방문 인접 많은 셀)만 선택. 모험심 확률로 3단계(미탐험) 방향 선택. 2단계로 갈 곳 없을 때만 예외로 3단계 진입. MapManager.GetVisitedNeighborCount, ExplorerAI ChooseBestExplorationDirection·재타겟팅 반영.
- **PushLog**: Docs/PushLog.md 추가. 푸시 전 기록 규칙 .cursor/rules/push-log-before-push.mdc 추가.

### 2025-03-11
- 푸시 전 기록용 PushLog.md 추가
- 앞으로 푸시 시 이 파일에 기록 후 커밋·푸시