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
