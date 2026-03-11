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
