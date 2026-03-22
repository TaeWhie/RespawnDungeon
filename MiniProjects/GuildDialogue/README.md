# GuildDialogue — 길드 대화 미니프로젝트

설계 문서([MiniProjects/Docs/](../Docs/))에 따른 **데이터 기반(RAG 방식)** 대화 콘솔.

## 요구 사항

- .NET 6 SDK
- [Ollama](https://ollama.ai/) — 채팅·임베딩 모델 (`DialogueSettings.json`의 `Model`, `EmbeddingModel`)

## 실행

```bash
cd MiniProjects/GuildDialogue
dotnet run
```

### 길드 집무실 탐색(키워드·임베딩만, LLM 호출 없음)

```bash
dotnet run -- --explore-guild-office
```

### 길드 집무실: 발화별 실제 NPC 응답(LLM)

`GuildOfficeExploration.json`의 각 `Utterance`에 대해 **집무실 1:1과 동일 프롬프트**로 Ollama 한 번씩 호출합니다(턴 격리, ActionLog 세션 미기록).

```bash
dotnet run -- --explore-guild-office-llm
dotnet run -- --explore-guild-office-llm --buddy 리나 --max 8
```

- 설정·캐릭터: `Config/DialogueSettings.json`, `Config/CharactersDatabase.json`
- `DialogueSettings.json`의 `Ollama.Model`·`EmbeddingModel`에 설치된 모델명 지정

## 구조

- **Config/** — 설정 JSON, 캐릭터·던전·아이템 등
- **Data/** — `DialogueSettings`, `Character`, `ActionLog` DTO 등
- **Services/** — `DialogueManager`, RAG 임베딩, 프롬프트 빌더 등

빌드 시 `Config/**`가 출력 폴더로 복사됩니다.
