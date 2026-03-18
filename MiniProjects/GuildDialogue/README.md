# GuildDialogue — 길드 대화 미니프로젝트

설계 문서([MiniProjects/Docs/](../Docs/))에 따른 **데이터 기반(RAG 방식)** 대화 콘솔.

## 요구 사항

- .NET 6 SDK
- (선택) [Ollama](https://ollama.ai/) — 로컬 LLM 사용 시. 미실행/미설치 시 **폴백**으로 캐릭터/역할별 문장 출력

## 실행

```bash
cd MiniProjects/GuildDialogue
dotnet run
```

- 설정·캐릭터: `Config/DialogueSettings.json`, `Config/Characters.json` (하드 코딩 없이 여기서 로드)
- Ollama 사용 시: `DialogueSettings.json`의 `Ollama.Model`에 설치된 모델명(예: `llama3.2`) 지정

## 구조

- **Config/** — 설정 JSON(문서 2.6 스키마), 캐릭터 JSON(스탯·인벤토리 포함, Unity CharacterData/InventoryData와 매핑 가능)
- **Data/** — `DialogueSettings`, `Character`(Stats, Inventory), `DungeonLog`, `DialogueRequest`, `LlmResponse` 등
- **Services/** — `DialogueConfigLoader`, `PromptBuilder`, `OllamaClient`, `MemoryManager`, `DialogueManager`
- **Program.cs** — 콘솔 진입점, GM 한 마디 → 카일 응답 → 입력 대화 루프

## 설계 준수

- N값·키워드·허용 주제·리트라이·오프셋·폴백 문장 등 **전부 설정에서 로드** (하드 코딩 금지)
- RAG: Retrieve(캐릭터·던전 로그·메모리·최근 턴) → Augment(프롬프트 조합) → Generate(Ollama 또는 폴백)
