# 11. 길드 집무실 · 가드레일 · CLI 탐색

구현: `GuildOfficeTopicGate.cs`, `GuildOfficeSemanticGuardrail.cs`, `GuildOfficeSemanticGuardrail` + `Config/SemanticGuardrailAnchors.json`, `GuildOfficeExplorationRunner`, `GuildOfficeLlmExplorationRunner`, `GuildOfficeLlmIntentRouter`.

---

## 11.1 변칙 입력 종류 (`GuildMasterAtypicalInputKind`)

| 값 | 의미 |
|----|------|
| `None` | 특별 분류 없음. |
| `Gibberish` | 반복 문자 등 난독·무의미. |
| `MetaOrSystem` | AI/프롬프트/올라마/JSON 등 **메타** 언급. |
| `OffWorldCasual` | 현실 브랜드·밈 등 **세계관 밖** 잡담. |

최종 분류는 **휴리스틱** → (옵션) **임베딩 코사인** → **원정 유사도와의 경쟁으로 None 하향** → (옵션) **키워드 타이브레이크** → (옵션) **LLM 라우터** 순으로 정제된다. 상세 로직은 `GuildOfficeTopicGate` 정적 메서드들.

---

## 11.2 세만틱 가드레일 (`GuildOfficeSemanticGuardrail`)

- **Warmup**: `SemanticGuardrailAnchors.json`(없거나 깨지면 내장 기본)의 앵커 문장들을 임베딩해 메모리에 보관.
- **ScoreUtteranceFullAsync**: 사용자 한 줄에 대해 메타/오프/원정 각 앵커 풀과의 최대 코사인을 반환.
- 초기화 실패 시 `DialogueManager`는 가드레일 객체를 **null**로 두고, 신호는 휴리스틱·키워드 위주로 동작.

---

## 11.3 “원정 딥 컨텍스트” (`ComputeDeepExpeditionForGuildOffice`)

- 길드장 발화가 **작전·로그·던전** 맥락이면 Episodic·ActionLog·RAG·관점기억을 **전부** 넣는 모드( [08](08-런타임-시퀀스-상세.md) 참고 ).
- 임베딩 임계값·상대 게이트(`GuildOfficeExpeditionUseRelativeEmbeddingGate`, `DeepContextLead`)·명시 키워드(`HasExplicitExpeditionKeywordCue`, `HasMinimalDungeonExpeditionKeywords`)가 조합된다.
- **짜증/중단** 룩어_like(`LooksLikeGuildMasterFrustrationOrStop`)이면 딥 원정을 **억제**해 장면을 가볍게 유지한다.

---

## 11.4 `AethelgardMcpRuntimeFacts` (이름만 MCP)

- 외부 MCP 서버가 아니라 **동일 프로세스**에서 문자열 블록을 만든다.
- 변칙 종류·유저 발화·파티 명단·인벤토리 DB를 받아 `inventory://` URI 설명, 멤버 검증 등을 프롬프트에 싣는다(`UseMcpRuntimeToolFacts`).

---

## 11.5 CLI: `--explore-guild-office`

- **목적**: 여러 **테스트 문장**에 대해 변칙 분류·원정 딥 플래그 등을 **출력만** (대화 UI 없음).
- **전제**: `UseGuildOfficeSemanticGuardrail == true`, `EmbeddingModel` 비어 있지 않음, `GuildOfficeExploration.json` 존재, `Cases` 비어 있지 않음.
- 러너는 `UseGuildOfficeLlmIntentRouter`를 **false로 강제**해 의도 라우터를 끈다.

---

## 11.6 CLI: `--explore-guild-office-llm`

- `GuildOfficeLlmExplorationRunner` — 발화별로 실제 **Ollama 호출**이 들어가는 탐색(인자는 러너 구현 참고). 부하·비용 주의.

---

## 11.7 `GuildOfficeLlmIntentRouter`

- `UseGuildOfficeLlmIntentRouter == true`일 때만 `ResolveGuildOfficeSignalsAsync` 후단에서 합류.
- 애매한 분류를 소형 프롬프트로 보완 — **토큰 증가**.

---

## 11.8 관련 설정 요약

- 가드레일·원정 게이트·3층·MCP 팩트는 모두 `Retrieval` 아래([09](09-설정-키-완전-참조.md)).
- 앵커 문구를 바꾸면 유사도 분포가 변하므로 **임계값 튜닝**이 함께 가는 경우가 많다.

다음: 처음 온 사람은 [07](07-문서-맵과-코드-읽기-가이드.md)로 돌아가 전체 맵을 본다.
