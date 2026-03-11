# 푸시 기록 (Push Log)

푸시하기 **전에** 이 파일에 이번 푸시 내용을 간단히 적고, 해당 변경을 커밋에 포함한 뒤 푸시합니다.

---

## 기록 형식

```markdown
### YYYY-MM-DD (커밋 해시 또는 메시지 요약)
- 변경 요약 1
- 변경 요약 2
```

---

## 기록

### 2025-03-11 — 처음부터 끝까지 정리 푸시
- **시야 3단계**: 3=짙은 안개(바닥/벽/오브젝트 미렌더), 2=옅은 안개(구조만), 1=전부 보임. 1단계로 한 번 밝힌 오브젝트는 계속 enable.
- **파티 시야 공유**: 리더·동료가 동일 MapManager로 visited/currentFullView/everFullView 갱신. 안개·오브젝트 표시는 시야 구역만 갱신해 FPS 개선, VisibilityByViewStage 목록 캐시.
- **탐험 완료**: 출구 도착 시에만 완료 처리. IsExplorationComplete로 Idle/로그 하던 부분 제거.
- **이동·모험심**: 기본은 2단계 시야 가장자리(방문 인접 많은 셀)만 선택. 모험심 확률로 3단계(미탐험) 방향 선택. 2단계로 갈 곳 없을 때만 예외로 3단계 진입. MapManager.GetVisitedNeighborCount, ExplorerAI ChooseBestExplorationDirection·재타겟팅 반영.
- **PushLog**: Docs/PushLog.md 추가. 푸시 전 기록 규칙 .cursor/rules/push-log-before-push.mdc 추가.

### 2025-03-11
- 푸시 전 기록용 PushLog.md 추가
- 앞으로 푸시 시 이 파일에 기록 후 커밋·푸시
