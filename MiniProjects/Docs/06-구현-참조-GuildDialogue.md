# 06. 구현 참조 — GuildDialogue

이 문서는 `MiniProjects/GuildDialogue` 소스 트리와 설정 키를 **빠르게 찾기 위한 색인**이다. 상세 설계는 [01](01-목표와-범위.md)–[05](05-품질-및-평가.md)를 본다.

---

## 6.1 프로젝트 개요

| 항목 | 내용 |
|------|------|
| 경로 | `MiniProjects/GuildDialogue/` |
| 런타임 | .NET 6 (`net6.0`), 콘솔 실행 파일 |
| 의존성 | `System.Text.Encoding.CodePages`(CP949 등) |
| 설정 복사 | `GuildDialogue.csproj`에서 `Config\**\*` → 출력 디렉터리 `PreserveNewest` |
| LLM·임베딩 | 로컬 **Ollama** (`/api/generate`, `/api/embeddings`) |

진입점은 루트의 `Program.cs` 하나다.

---

## 6.2 소스 디렉터리 구조

| 영역 | 주요 파일 | 역할 |
|------|-----------|------|
| 진입 | `Program.cs` | 대화형 메뉴(1–5), `--explore-guild-office`, `--explore-guild-office-llm`, `--gen-actionlog` |
| 데이터 모델 | `Data/*.cs` | `DialogueSettings`, `Character`, `WorldLore`, `TestData`(ActionLog 루트), `GameReferenceData`, DTO |
| 대화 코어 | `Services/DialogueManager.cs` | 초기화(임베딩 인덱스), 관전/길드장 세션, RAG·프롬프트·Ollama 호출 |
| 프롬프트 | `Services/PromptBuilder.cs` | 시스템/유저 문자열 조립, 3층 프롬프트, 집무실·원정 게이트 연동 |
| RAG | `Services/EmbeddingKnowledgeIndex.cs`, `EmbeddingKnowledgePreprocessor.cs`, `GameKnowledgeRetriever.cs` | 청크·임베딩·코사인 검색, 질의 문자열 빌드 |
| Ollama | `Services/OllamaClient.cs`, `Services/OllamaEmbeddingClient.cs` | 채팅·임베딩 HTTP |
| 메모리 | `Services/MemoryManager.cs`, `CharacterPerspectiveMemoryBuilder.cs`, `ActionLogNarrativeFormatter.cs`, `LatestActionLogFormatter.cs`, `ExpeditionRecentMemorableSummarizer.cs` | 에피소드 버퍼, 관점 기억, ActionLog 줄글/꼬리 |
| 길드 집무실 | `GuildOfficeTopicGate.cs`, `GuildOfficeSemanticGuardrail.cs`, `GuildOfficeLlmIntentRouter.cs`, `GuildOfficeExplorationRunner.cs`, `GuildOfficeLlmExplorationRunner.cs` | 토픽·임베딩 가드레일, 탐색 러너 |
| MCP 스타일 팩트 | `Services/AethelgardMcpRuntimeFacts.cs` | `inventory://`, `Verify_Member` 등 C# 계산 결과를 프롬프트에 주입 |
| 파티·원정 | `PartyManagementConsole.cs`, `DungeonRunSimulator.cs`, `CharacterMoodUpdater.cs` | 파티 CRUD, 던전 시뮬, 기분 반영 |
| 캐릭터 생성 | `CharacterCreationConsole.cs`, `CharacterCreationRng.cs`, `CharacterCreationLlmGenerator.cs`, `JobSkillRules.cs` | 메뉴 5, RNG, Ollama 서사 |
| 설정 로드 | `Services/DialogueConfigLoader.cs` | `Config` 경로 해석, JSON 로드/저장 |
| 지속성 | `Services/ActionLogPersistence.cs` | 세션 종료 시 ActionLog 반영 등 |

---

## 6.3 `Config` 폴더 데이터 파일

실행 시 기본적으로 **프로젝트 루트의 `GuildDialogue/Config`**가 사용된다(`DialogueConfigLoader.ResolveDefaultConfigDirectory` — `bin`에서 상위로 올라가 `.csproj` 옆 `Config` 탐색).

| 파일 | 용도 |
|------|------|
| `DialogueSettings.json` | Ollama URL/모델/온도, `Retrieval`·질적 분석·길드 집무실·3층 프롬프트·MCP 팩트 플래그 |
| `CharactersDatabase.json` | 캐릭터 로스터(없으면 `Characters.json` 폴백) |
| `PartyDatabase.json` | 파티 정의 |
| `ActionLog.json` | 원정·이벤트 타임라인(`TestDataRoot`) |
| `JobDatabase.json`, `SkillDatabase.json` | 직업·스킬, 생성 시 규칙(`JobSkillRules`) |
| `WorldLore.json`, `MonsterDatabase.json`, `TrapTypeDatabase.json`, `ItemDatabase.json`, `BaseDatabase.json`, `EventTypeDatabase.json` | 월드·도감·RAG 소스 |
| `SemanticGuardrailAnchors.json` | 집무실 임베딩 앵커(메타/오프/원정 등) |
| `GuildOfficeExploration.json` | 집무실 탐색 시나리오(러너용) |

---

## 6.4 `DialogueSettings.json` ↔ `DialogueSettings.cs`

### `Ollama` (`OllamaSettings`)

| 키 | 의미 |
|----|------|
| `BaseUrl` | 기본 `http://localhost:11434` |
| `Model` | `/api/generate` 모델 |
| `EmbeddingModel` | 비우면 `Model` 사용(임베딩 미지원 모델이면 오류). 예: `nomic-embed-text` |
| `TimeoutSeconds`, `Temperature`, `TopP`, `NumPredict` | 생성 옵션 |
| `AnalysisStrategyMaxTokens` | 분석/라우터 등 보조 호출 상한(0이면 미사용 경로) |

### `Retrieval` (`RetrievalSettings`)

임베딩 RAG, ActionLog 주입, 관점 기억, 길드 집무실 가드레일, 에델가드 3층, MCP 런타임 팩트가 **여기 한 객체**에 모인다. 코드 주석이 상세하므로 IDE에서 `DialogueSettings.cs`를 열어 그대로 따라가면 된다.

대표 키:

- **RAG**: `RagSearchPoolSize`, `RagIncludeSpeakerLoadout`, `MaxEpisodicCharsInQuery`, `ExcludeCurrencyLineFromLoreEmbedding`, `PrioritizeEpisodicMentionsInRag`
- **ActionLog**: `LatestActionLogEntriesInPrompt`, `UseActionLogNarrativeProse`, `ActionLogNarrativeMaxDungeonRuns`, `ActionLogNarrativeMaxChars`
- **관점 기억**: `UsePerspectiveMemoryInPrompt`, `PerspectiveMemoryMaxLinesPerCharacter`, `PerspectiveMemoryExportPath`
- **세션 저장**: `PersistDialogueSessionsToActionLog`
- **집무실**: `UseGuildOfficeSemanticGuardrail`, `GuildOfficeSemanticGuardrailThreshold`, `GuildOfficeExpeditionContextThreshold`, `GuildOfficeExpeditionUseRelativeEmbeddingGate`, …, `UseGuildOfficeLlmIntentRouter`
- **3층·MCP**: `UseAethelgardThreeLayerPrompt`, `UseMcpRuntimeToolFacts`

### `QualitativeAnalysis` (`QualitativeAnalysisSettings`)

`Enabled`, 콘솔/파일 로그, 프롬프트 미리보기 길이 등.

### `TraitToToneKeywords`

성향 → 톤 키워드 매핑(프롬프트 보조).

---

## 6.5 CLI 인자 (`Program.cs`)

| 인자 | 동작 |
|------|------|
| (없음) | 대화형 메뉴 루프 |
| `--explore-guild-office` | `GuildOfficeExplorationRunner.RunAsync` |
| `--explore-guild-office-llm` [추가 인자] | `GuildOfficeLlmExplorationRunner.RunAsync` |
| `--gen-actionlog` | 던전 시뮬 → ActionLog 저장. `[경로] [시드]`, `--party 이름`, `--replace-actionlog`, `--no-sync-chars` |

---

## 6.6 동작 의존성 메모

- 메뉴 **1·2**: `DialogueManager.InitializeAsync`에서 **임베딩 인덱스 구축**이 필요하다. Ollama 미기동·`EmbeddingModel` 부적합 시 실패할 수 있다.
- **참조 지식 RAG**는 임베딩 검색만 사용한다(키워드 전용 폴백 없음).
- 메뉴 **5**(캐릭터 생성): 서사 필드는 **Ollama**로만 생성; 실패 시 DB에 쓰지 않는다.

---

## 6.7 관련 문서

| 문서 | 내용 |
|------|------|
| [03-대화-플로우](03-대화-플로우.md) | 메뉴·세션 흐름 |
| [04-LLM-연동-설계](04-LLM-연동-설계.md) | Ollama·RAG·프롬프트 |
| [07-문서-맵과-코드-읽기-가이드](07-문서-맵과-코드-읽기-가이드.md) | 문서만으로 부족한 이유, 코드 읽기 순서 |
| [08-런타임-시퀀스-상세](08-런타임-시퀀스-상세.md) | `InitializeAsync`, 메뉴 1·2 상세 |
| [09-설정-키-완전-참조](09-설정-키-완전-참조.md) | 설정 키 전체 |
| [10-ActionLog-스키마-수명주기](10-ActionLog-스키마-수명주기.md) | ActionLog 병합·수명 주기 |
| [11-길드집무실-가드레일-CLI-탐색](11-길드집무실-가드레일-CLI-탐색.md) | 집무실 가드레일·탐색 CLI |
| [12-메뉴-345-파티-원정-캐릭터생성-상세](12-메뉴-345-파티-원정-캐릭터생성-상세.md) | 파티·원정·캐릭터 생성 |
| [13-구현-상세-관계-데이터-MCP-런타임](13-구현-상세-관계-데이터-MCP-런타임.md) | 관계 리스트·정산·`AethelgardMcpRuntimeFacts` |
| [게임 로그 기반 RP 챗봇 구조 설계](게임%20로그%20기반%20RP%20챗봇%20구조%20설계.md) | 일반론 + 본 레포 매핑 |

`Retrieval`의 필드별 긴 설명은 **09**가 단일 참조이며, 본 절(6.4)은 요약용이다.
