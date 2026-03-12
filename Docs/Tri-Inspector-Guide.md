# Tri-Inspector 활용 가이드

이 프로젝트는 **Tri Inspector** 패키지(`com.codewriter.triinspector`)를 사용합니다.  
인스펙터 속성(Attributes)으로 UI를 정리하고, 검증·데코레이터를 적용할 때 이 가이드와 [패키지 README](Packages/com.codewriter.triinspector/README.md)를 참고하세요.

---

## 1. 패키지 개요

- **역할**: Unity 인스펙터용 고급 속성(Attributes) 라이브러리
- **네임스페이스**: `using TriInspector;`
- **적용 대상**: `MonoBehaviour`, `ScriptableObject`, `Serializable` 클래스 등

---

## 2. 기능 분류 및 사용 상황

### 2.1 스타일·구조 (Styling & Groups)

| 속성 | 용도 | 사용 상황 |
|------|------|-----------|
| `[Title("제목")]` | 구역 제목 + 구분선 | 인스펙터를 섹션별로 나눌 때 |
| `[LabelText("라벨")]` | 필드 라벨 변경 | 기본 변수명 대신 읽기 쉬운 이름을 보여줄 때 |
| `[HideLabel]` | 라벨 숨김 | 넓은 필드(예: Vector3)만 보여줄 때 |
| `[Group("경로")]` | 같은 그룹으로 묶기 | 관련 필드를 한 덩어리로 표시할 때 |
| `[DeclareFoldoutGroup("id", Title = "제목")]` | 접을 수 있는 그룹 | 필드가 많을 때 폴더처럼 접기 |
| `[DeclareBoxGroup("id", Title = "제목")]` | 박스 그룹 | 시각적으로 박스로 구분할 때 |
| `[DeclareTabGroup("id")]` + `[Tab("탭이름")]` | 탭 그룹 | 설정이 많을 때 탭으로 나눌 때 |
| `[PropertyOrder(n)]` | 표시 순서 변경 | 특정 필드를 위/아래로 보내고 싶을 때 |
| `[PropertySpace]` / `[Indent]` | 간격·들여쓰기 | 가독성 조정 |

**동적 문자열**: `"$" + nameof(프로퍼티)` 로 런타임 문자열을 제목/라벨에 사용 가능.

---

### 2.2 데코레이터 (Decorators)

| 속성 | 용도 | 사용 상황 |
|------|------|-----------|
| `[Slider(min, max)]` / `[Slider(nameof(_min), nameof(_max))]` | 슬라이더 | 수치를 범위로 제한해 입력할 때 |
| `[MinMaxSlider(min, max)]` | 최소~최대 슬라이더 | Vector2 등 min-max 쌍을 다룰 때 |
| `[Dropdown(nameof(값목록))]` | 드롭다운 | 미리 정한 값 목록에서만 선택할 때 |
| `[AssetDropdown("t:ScriptableObject")]` | 에셋 선택 드롭다운 | 특정 타입 에셋만 골라야 할 때 |
| `[Scene]` | 씬 선택 | 씬 이름 필드에 사용 |
| `[Layer]` | 레이어 선택 | 레이어 인덱스 필드에 사용 |
| `[AnimatorParameter(nameof(animator))]` | 애니메이터 파라미터 목록 | Animator 파라미터 이름 선택 시 |
| `[ReadOnly]` | 읽기 전용 | 런타임/설정값 표시만 할 때 |
| `[PropertyTextArea]` | 여러 줄 텍스트 | string 필드를 큰 입력창으로 |
| `[InlineEditor]` | 인라인 에디터 | Material 등 오브젝트를 인스펙터 안에서 편집 |
| `[PreviewObject]` / `[PreviewMesh]` | 미리보기 | 텍스처·메시 미리보기 |

---

### 2.3 조건부 표시·비활성화 (Conditionals)

| 속성 | 용도 | 사용 상황 |
|------|------|-----------|
| `[ShowIf(nameof(조건), 값)]` | 조건 만족 시 표시 | 다른 필드 값에 따라 보이기 |
| `[HideIf(nameof(조건))]` | 조건 만족 시 숨김 | 불필요할 때 필드 숨기기 |
| `[EnableIf(nameof(조건))]` | 조건 만족 시 편집 가능 | 상황에 따라 수정 가능/불가 |
| `[DisableIf(nameof(조건))]` | 조건 만족 시 비활성화 | 표시만 하고 수정은 막을 때 |
| `[HideInPlayMode]` / `[ShowInPlayMode]` | 플레이 모드에서만 표시/숨김 | 에디터 전용 vs 런타임 전용 필드 |
| `[DisableInPlayMode]` / `[EnableInPlayMode]` | 플레이 모드에서만 비활성/활성 | 런타임에 수정하면 안 되는 필드 |

조건은 `nameof(멤버)` 또는 `nameof(멤버), 기대값` 형태로 지정.

---

### 2.4 검증 (Validators)

| 속성 | 용도 | 사용 상황 |
|------|------|-----------|
| `[Required]` / `[Required("메시지")]` | null 불가 | 참조가 꼭 있어야 할 때 |
| `[Required(FixAction = nameof(메서드), FixActionName = "버튼이름")]` | 수정 액션 버튼 | 인스펙터에서 한 번에 할당할 때 |
| `[ValidateInput(nameof(검증메서드))]` | 커스텀 검증 | bool 또는 `TriValidationResult` 반환 메서드로 검증 |
| `[InfoBox("문구", TriMessageType.Info/Warning/Error)]` | 안내/경고/에러 박스 | 사용자에게 설명·경고 표시 |
| `[InfoBox("$" + nameof(동적문자열), visibleIf: nameof(조건))]` | 동적·조건부 안내 | 상황에 따라 다른 안내 |
| `[AssetsOnly]` | 에셋만 할당 | 씬 오브젝트 드래그 방지 |
| `[SceneObjectsOnly]` | 씬 오브젝트만 할당 | 에셋 참조 방지 |

`ValidateInput` 메서드 시그니처 예:

- `bool ValidateXxx()`  
- `TriValidationResult ValidateXxx()`  
  - `TriValidationResult.Valid`  
  - `TriValidationResult.Error("메시지")`  
  - `TriValidationResult.Warning("메시지")`  

---

### 2.5 버튼·기타 (Buttons & Misc)

| 속성 | 용도 | 사용 상황 |
|------|------|-----------|
| `[Button]` / `[Button("이름")]` | 인스펙터에서 메서드 실행 | 테스트·초기화·툴 버튼 |
| `[EnumToggleButtons]` | enum을 토글 버튼으로 표시 | enum 선택을 버튼 UI로 |
| `[OnValueChanged(nameof(메서드))]` | 값 변경 시 콜백 | 참조/값 변경 시 로직 실행 |
| `[ShowInInspector]` | 비직렬화 멤버 표시 | 프로퍼티·private 필드를 인스펙터에 표시 |
| `[HideMonoScript]` | Script 필드 숨김 | 클래스 단위로 스크립트 필드 제거 |

---

## 3. 사용 시 유의사항

- **동적 값**: `[Slider]`, `[Title]` 등에서 `nameof(필드/메서드)` 또는 `"$" + nameof(프로퍼티)` 로 동적 값/문자열 사용 가능.
- **그룹 선언**: Box/Foldout/Tab 그룹은 **클래스에** `[DeclareXxxGroup("id")]` 로 선언하고, 필드에는 `[Group("id")]` 로 붙인다.
- **검증 메서드**: `ValidateInput` 은 인자 없거나 해당 필드 타입 인자 하나만 사용. 반환은 `bool` 또는 `TriValidationResult`.

---

## 4. 참고

- 상세 예제: Unity 메뉴 **Tools → Tri Inspector → Samples** 에서 확인.
- 공식 문서: `Packages/com.codewriter.triinspector/README.md`
- **규칙**: 인스펙터를 작성·수정할 때 해당 상황에 맞는 Tri-Inspector 속성이 있으면 **반드시 활용**하도록 [.cursor/rules/tri-inspector-usage.mdc](.cursor/rules/tri-inspector-usage.mdc) 규칙을 따릅니다.
