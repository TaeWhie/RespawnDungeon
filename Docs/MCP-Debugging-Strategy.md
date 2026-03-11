# MCP 도구 활용 Unity 디버깅 전략

## 사용 가능한 MCP 도구 요약

| 구분            | 리소스/도구                                                                                      | 용도                            |
| ------------- | ------------------------------------------------------------------------------------------- | ----------------------------- |
| **에디터 상태**    | `mcpforunity://editor/state`                                                                | isCompiling, 플레이 모드, 조언/스테일니스 |
| **콘솔**        | `read_console` (action=get, types, filter_text, include_stacktrace, format=detailed)        | 에러/경고/로그·스택트레이스·필터 조회         |
| **플레이 제어**    | `manage_editor` (play/pause/stop)                                                           | 재생/일시정지/정지로 재현 제어             |
| **씬/계층**      | `manage_scene` (get_hierarchy, get_active), `find_gameobjects`                              | 활성 씬, 계층 구조, 이름/태그/컴포넌트 검색    |
| **오브젝트/컴포넌트** | `mcpforunity://scene/gameobject/{id}`, `.../components`, `manage_components` (set_property) | GO 메타·컴포넌트 읽기/쓰기              |
| **선택**        | `mcpforunity://editor/selection`                                                            | 현재 선택된 오브젝트/에셋 상세             |
| **테스트**       | `mcpforunity://tests`, `run_tests` + `get_test_job` (wait_timeout)                          | 테스트 목록·실행·폴링·실패 스택            |
| **코드 검색**     | `find_in_file` (uri, pattern), Cursor `Grep`/`SemanticSearch`                               | 스크립트 내 정규식/의미 검색              |
| **렌더링**       | `mcpforunity://rendering/stats`                                                             | 드로우콜·배치·트라이앵글·프레임 타임          |
| **프로젝트 특화**   | `mcpforunity://custom-tools`                                                                | 프로젝트별 커스텀 도구 확인               |

---

## 1단계: 디버깅 전 사전 점검

- **에디터 준비**
  - `mcpforunity://editor/state` 로 `isCompiling` 확인. `true`면 컴파일 완료까지 대기 후 다음 단계 진행.
  - `mcpforunity://instances` 로 연결된 Unity 인스턴스 확인. 복수 인스턴스면 `set_active_instance` 로 대상 지정.
- **콘솔 정리**
  - `read_console`(action=`"clear"`) 로 기존 로그 제거 후 재현 시 새 로그만 수집.
- **컴파일 에러 여부**
  - `read_console`(action=`"get"`, types=`["error"]`, count=`"20"`, format=`"detailed"`, include_stacktrace=`true`) 로 최근 에러·스택 확인. 에러가 있으면 먼저 해결.

이 단계에서 **리소스는 `fetch_mcp_resource`, 도구는 `call_mcp_tool`(server=`user-unityMCP`)로 호출**.

---

## 2단계: 버그 재현 및 로그 수집

- **재현 시나리오**
  - `manage_editor`(action=`"stop"`) 후 `manage_editor`(action=`"play"`) 로 플레이 모드 진입. 필요 시 `manage_scene`(action=`"get_active"`) 으로 재현에 사용할 씬 확인.
- **콘솔 수집**
  - 재현 후 `read_console`(action=`"get"`, types=`["error","warning","log"]`, count=`"50"`, format=`"detailed"`, include_stacktrace=`true`).
  - 특정 키워드(예: NullReference, 스크립트명)로 좁히려면 `filter_text` 사용.
- **에러 발생 시점 고정**
  - 재현이 한 번에 되면: 재생 → 재현 → 즉시 `read_console`로 수집.
  - 간헐적이면: `read_console`를 여러 번 호출해 페이지네이션(cursor/page_size)으로 이전 로그까지 확인.

---

## 3단계: 원인 격리 (씬·오브젝트·코드)

- **씬/계층**
  - `manage_scene`(action=`"get_hierarchy"`, page_size=50, cursor 등) 으로 계층 구조 확인. 누락/비활성 오브젝트, 잘못된 부모 관계 파악.
  - `find_gameobjects`(search_term=이름 또는 스크립트명, search_method=`by_name` 또는 `by_component`) 로 의심 오브젝트 instance ID 조회.
- **오브젝트/컴포넌트**
  - ID 확보 후 `fetch_mcp_resource`(server=`user-unityMCP`, uri=`mcpforunity://scene/gameobject/{id}/components`) 로 컴포넌트 목록·프로퍼티 확인. 누락 컴포넌트, null 참조 가능성 검사.
  - 에디터에서 선택한 오브젝트가 있으면 `mcpforunity://editor/selection` 으로 선택 상태와 프로퍼티 확인.
- **코드 위치**
  - 콘솔 스택트레이스의 파일명·라인을 기준으로 해당 스크립트를 Cursor에서 `Read`로 열어 해당 라인 주변 분석.
  - `find_in_file`(uri=Assets 경로, pattern=예외 메시지 또는 메서드명) 로 동일 예외/호출 위치 검색.
  - 넓은 범위 검색은 Cursor `Grep`/`SemanticSearch` 활용(예: 특정 API 사용처, 이벤트 구독처).

---

## 4단계: 수정 및 검증

- **수정**
  - 코드/데이터 수정은 Cursor 편집 도구로 수행. 스크립트 변경 후 **반드시** `mcpforunity://editor/state` 로 `isCompiling` 확인하고, 필요 시 `read_console`(types=`["error"]`) 로 컴파일 에러 여부 재확인.
- **재현 테스트**
  - `manage_editor`(action=`"stop"`) → `manage_editor`(action=`"play"`) 로 다시 재현. 2단계와 동일하게 `read_console`로 로그 수집해 에러/경고 소멸 여부 확인.
- **자동화된 검증**
  - 프로젝트에 테스트가 있으면 `mcpforunity://tests` 로 목록 확인 후, `run_tests`(mode=`EditMode` 또는 `PlayMode`, include_details=`true`) 호출.
  - 반환된 `job_id`로 `get_test_job`(job_id, wait_timeout=30, include_failed_tests=`true`) 로 폴링해 실패한 테스트의 message/stackTrace 확인.

---

## 5단계: 성능/렌더링 이슈일 때

- **프레임/렌더 비용**
  - `mcpforunity://rendering/stats` 로 draw calls, batches, triangles, frame time 등 스냅샷 수집. 재현 전·후 또는 설정 변경 전·후 비교.
- **씬 뷰 시각 확인**
  - `manage_scene`(action=`"scene_view_frame"`, scene_view_target=오브젝트 id/이름) 으로 특정 오브젝트에 프레임 맞춤. 문서상 스크린샷은 `manage_camera`(screenshot 등) 사용.

---

## 권장 호출 순서 (에러 디버깅 예시)

1. `editor_state` → `read_console`(errors)
2. `manage_editor`(stop 후 play) → 재현 → `read_console`(get, detailed, include_stacktrace)
3. `manage_scene`(get_hierarchy) / `find_gameobjects` → `mcpforunity://scene/gameobject/{id}/components` → `find_in_file` 또는 Grep
4. 수정 후 `editor_state`(isCompiling) → `read_console`(errors) → 재현 → 필요 시 `run_tests` + `get_test_job`

---

## 페이로드/호출 주의사항

- **큰 응답 방지**: `manage_scene`(get_hierarchy)는 `page_size`(예: 50) + `cursor` 로 페이지네이션. `manage_gameobject`(get_components)는 `include_properties=false`로 메타만 먼저 조회하고, 필요 시 소량만 `include_properties=true`로 상세 조회.
- **스키마 선확인**: 새 MCP 도구 사용 전 `mcps/user-unityMCP/tools/*.json`, `resources/*.json` 에서 파라미터·URI 확인 후 호출.
- **커스텀 도구**: `mcpforunity://custom-tools` 로 프로젝트 전용 디버깅/진단 도구가 있는지 확인하고, 있으면 위 단계에 통합.

이 전략을 따르면 MCP로 상태·로그·씬·오브젝트·테스트·렌더링을 한 흐름에서 자동으로 확인하며, Cursor의 코드 검색/편집과 함께 체계적으로 디버깅할 수 있다.
