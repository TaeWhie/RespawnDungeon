# 04. LLM·임베딩 연동 설계

구현 기준: `OllamaClient`, `OllamaEmbeddingClient`, `EmbeddingKnowledgeIndex`, `GameKnowledgeRetriever`, `PromptBuilder`, `DialogueManager`.

---

## 4.1 원칙 (현재 코드 기준)

| 원칙 | 내용 |
|------|------|
| **로컬 Ollama** | 채팅: `POST /api/generate`. 임베딩: `POST /api/embeddings`. |
| **참조 지식 RAG** | **임베딩 검색만** 사용. 키워드 전용 `BuildBlock` 폴백은 제거됨. |
| **초기화 실패** | 임베딩 인덱스 `BuildAsync`가 false면 `InitializeAsync`에서 예외 가능. |
| **설정 주도** | `DialogueSettings.json`의 `Ollama`, `Retrieval`이 온도·풀 크기·가드레일·3층 프롬프트·MCP 팩트를 제어. |

---

## 4.2 Ollama 채팅 (`OllamaClient`)

- 요청 본문: `model`, `prompt`(시스템+유저 결합 문자열), `stream: false`, `options`: `temperature`, `top_p`, 선택 `num_predict`.
- 응답 JSON에서 `response` 필드만 추출.

---

## 4.3 임베딩·RAG (`OllamaEmbeddingClient` + `EmbeddingKnowledgeIndex`)

1. **빌드**: 월드 로어, 몬스터, 함정, 스킬, 아이템, 캐릭터 등을 청크로 나누어 임베딩 후 메모리 인덱스에 적재.  
2. **질의 문자열**: `GameKnowledgeRetriever.BuildRetrievalQuery` — 워킹 메모리, 에피소드, (`RagIncludeSpeakerLoadout` 시) 스킬·인벤·장비명 혼합. 골드 라인 제거 옵션 등.  
3. **검색**: 코사인 유사도 상위 k(`RagSearchPoolSize`)를 포맷된 문자열로 반환.  
4. **프롬프트**: `PromptBuilder.AppendReferenceKnowledgeRag` — 결과가 비면 “검색 결과 없음” 안내 문구만 심는다.

---

## 4.4 프롬프트 골격 (`PromptBuilder`)

- **규모**: 수천 줄 분량의 주석·지시 누적 — 7B급 모델의 비서화·데이터 혼선·가짜 소유권 등을 줄이기 위한 **봉쇄 규칙**이 길게 정의되어 있다.  
- **에델가드 3층** (`UseAethelgardThreeLayerPrompt`): RAG = Library, MCP = Interface, Prompt = Brain 역할 분담 설명 블록.  
- **MCP 런타임 팩트** (`UseMcpRuntimeToolFacts`): **외부 MCP 서버가 아니라** 동일 프로세스의 `AethelgardMcpRuntimeFacts`가 `[Tool Result: …]`, `resource://inventory/{id}` 형태 문자열을 만들어 주입한다. 감지 규칙·URI·한계는 [13-구현-상세-관계-데이터-MCP-런타임.md](13-구현-상세-관계-데이터-MCP-런타임.md).  
- **길드 집무실**: `GuildOfficeTopicGate` + (옵션) `GuildOfficeSemanticGuardrail`로 메타/오프월드/원정 맥락 분기.

---

## 4.5 응답 형식

- 대화 턴은 통상 **JSON 한 객체** — 최소 `line`(대사). `tone`, `intent`, `innerThought` 등은 코드 경로에 따라 사용.  
- `DialogueManager.CleanJsonResponse` 유사 로직으로 코드펜스·앞뒤 잡담 제거 후 파싱.  
- 파싱 실패 시 리트라이·로그 메시지 경로가 있다.

---

## 4.6 캐릭터 생성 LLM (`CharacterCreationLlmGenerator`)

- 출력 키: `id`, `name`, `background`, `speechStyle`(camelCase).  
- 검증: Id 형식, `master` 금지, 이름 길이, 배경·말투 최소 길이, 기존 로스터와 중복 불가.  
- 말투 다양성: 시스템 프롬프트에서 직업=말투 고정 금지, 무작위 힌트 한 줄.

---

## 4.7 “폴백”에 대해

- 옛 문서의 “LLM 실패 시 규칙 기반 한 마디”는 **캐릭터 생성**에서는 제거되었고(실패 시 저장 안 함), **대화 턴**은 구현별로 리트라이·JSON 복구가 있다.  
- **RAG**는 키워드 폴백 없음 — 임베딩 실패는 초기화 단계에서 드러난다.

다음: [05-품질-및-평가.md](05-품질-및-평가.md)
