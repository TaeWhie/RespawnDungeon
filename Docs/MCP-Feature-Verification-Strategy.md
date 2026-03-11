# 플랜 보관 + 기능 제작 시 MCP 검증 전략

## 1. 디버깅 전략 문서 보관

**「MCP 기반 Unity 디버깅 전략」**은 `Docs/MCP-Debugging-Strategy.md` 에 보관되어 있다.

---

## 2. 기능 제작 시 MCP 검증 전략 개요

새 기능을 구현한 뒤, **에디터 상태 → 컴파일 → 콘솔 → 플레이 → 씬/오브젝트 → 테스트** 순으로 MCP만으로 검증하는 흐름을 따른다.

- 구현 → editor_state(isCompiling) → read_console(errors) → manage_editor(play) → read_console(get) → manage_scene / find_gameobjects → run_tests + get_test_job

---

## 3. 단계별 MCP 검증 체크리스트

### 3.1 구현 직후: 컴파일·에러 확인

| 순서 | 목적 | MCP 사용 | 통과 기준 |
|------|------|----------|-----------|
| 1 | 도메인 리로드/컴파일 완료 여부 | `fetch_mcp_resource` → `mcpforunity://editor/state` | `isCompiling === false` |
| 2 | 컴파일/스크립트 에러 유무 | `read_console`(action=`"get"`, types=`["error"]`, count=`"15"`, format=`"detailed"`, include_stacktrace=`true`) | 에러 0건 |
| 3 | (선택) 기존 로그 제거 | `read_console`(action=`"clear"`) | 이후 로그만 의미 있음 |

`isCompiling === true` 이면 잠시 대기 후 다시 `editor_state` 조회. 에러가 있으면 수정 후 이 단계부터 반복.

### 3.2 플레이 모드 진입 및 런타임 로그

| 순서 | 목적 | MCP 사용 | 통과 기준 |
|------|------|----------|-----------|
| 4 | 재생 전 정리 | `manage_editor`(action=`"stop"`) | 이미 정지 상태면 무시 |
| 5 | 기능이 동작하는 씬 확인 | `manage_scene`(action=`"get_active"`) | 해당 기능 테스트용 씬이 활성인지 확인 |
| 6 | 플레이 모드 진입 | `manage_editor`(action=`"play"`) | 에디터가 플레이 중으로 전환 |
| 7 | 런타임 에러/경고 수집 | `read_console`(action=`"get"`, types=`["error","warning"]`, count=`"30"`, format=`"detailed"`, include_stacktrace=`true`) | 에러 0건, 경고는 기대한 것만 허용 |

필요 시 `filter_text`로 스크립트명/메시지 필터링. 실패 시 `Docs/MCP-Debugging-Strategy.md` 2~3단계(재현·격리) 적용.

### 3.3 씬·오브젝트 기대값 검증

기능이 "특정 오브젝트 생성/활성화/컴포넌트 부착" 등을 전제로 할 때 사용.

| 순서 | 목적 | MCP 사용 | 통과 기준 |
|------|------|----------|-----------|
| 8 | 계층 구조 확인 | `manage_scene`(action=`"get_hierarchy"`, page_size=50, cursor 필요 시 사용) | 예상 부모/자식·이름 존재 |
| 9 | 특정 오브젝트 존재 여부 | `find_gameobjects`(search_term=이름 또는 컴포넌트 타입, search_method=`by_name` 또는 `by_component`) | 기대한 instance ID 반환 |
| 10 | 컴포넌트/프로퍼티 확인 | `fetch_mcp_resource` → `mcpforunity://scene/gameobject/{id}/components` | 필수 컴포넌트·주요 프로퍼티 값 일치 |

### 3.4 자동 테스트로 회귀 검증

| 순서 | 목적 | MCP 사용 | 통과 기준 |
|------|------|----------|-----------|
| 11 | 테스트 목록 확인 | `fetch_mcp_resource` → `mcpforunity://tests` | 관련 테스트 존재 여부 확인 |
| 12 | EditMode/PlayMode 테스트 실행 | `run_tests`(mode=`EditMode` 또는 `PlayMode`, include_details=`true`) | `job_id` 수신 |
| 13 | 결과 대기·실패 분석 | `get_test_job`(job_id, wait_timeout=30~60, include_failed_tests=`true`) | status 완료, failed=0 |

실패가 있으면 스택트레이스 기준으로 코드 수정 후 3.1부터 다시 검증.

### 3.5 (선택) 성능·시각 검증

- **성능**: 기능 적용 전후로 `mcpforunity://rendering/stats` 스냅샷 비교.
- **씬 뷰**: `manage_scene`(action=`"scene_view_frame"`, scene_view_target=id 또는 이름).

---

## 4. 기능별 검증 강도

| 기능 유형 | 필수 검증 | 권장 추가 |
|-----------|-----------|-----------|
| 새 스크립트/로직만 | 3.1, 3.2 | 3.4(해당 테스트 있으면) |
| 씬/프리팹/계층 변경 | 3.1, 3.2, 3.3 | 3.4 |
| 전투/탐험 등 핵심 루프 | 3.1~3.4 전부 | 3.5(렌더링 스탯) |
| UI/이펙트만 | 3.1, 3.2, 3.3(캔버스/카메라 등) | 3.5 |

---

## 5. 실행 시 요약 체크리스트

1. **컴파일**: `editor_state` → `isCompiling` false 확인 후 `read_console`(errors)로 에러 0건 확인.
2. **플레이**: `manage_editor`(stop → play) 후 `read_console`(error/warning, detailed)로 런타임 이상 없음 확인.
3. **씬/오브젝트**: `manage_scene`(get_hierarchy), `find_gameobjects`, `gameobject/{id}/components` 로 기대 구조·컴포넌트 일치 확인.
4. **테스트**: `mcpforunity://tests` → `run_tests` → `get_test_job`(wait_timeout) 로 전부 통과 확인.
5. (선택) **성능**: `mcpforunity://rendering/stats` 로 변경 전후 비교.
