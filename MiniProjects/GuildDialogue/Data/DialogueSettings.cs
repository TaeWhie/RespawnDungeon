namespace GuildDialogue.Data;

public class DialogueSettings
{
    public Dictionary<string, string> TraitToToneKeywords { get; set; } = new();
    public OllamaSettings Ollama { get; set; } = new();
    public RetrievalSettings? Retrieval { get; set; }
    public QualitativeAnalysisSettings? QualitativeAnalysis { get; set; }
}

public class OllamaSettings
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.2";
    /// <summary>Ollama /api/embeddings 전용(비우면 Model 사용 — 임베딩 미지원 모델이면 오류). 예: nomic-embed-text</summary>
    public string? EmbeddingModel { get; set; }
    public int TimeoutSeconds { get; set; } = 60;
    public double Temperature { get; set; } = 0.7;
    public double TopP { get; set; } = 0.9;
    public int NumPredict { get; set; } = 0;
    public int AnalysisStrategyMaxTokens { get; set; } = 0;
}

public class RetrievalSettings
{
    public int MaxEpisodicCharsInQuery { get; set; } = 2800;
    public bool ExcludeCurrencyLineFromLoreEmbedding { get; set; }
    public bool PrioritizeEpisodicMentionsInRag { get; set; }
    public int RagSearchPoolSize { get; set; } = 12;

    /// <summary>true면 게임 DB(몬스터·아이템 등)를 전부 넣지 않고, 질의·에피소드와 겹치는 항목만 발췌해 넣습니다.</summary>
    public bool UseKeywordRagForGameDb { get; set; } = true;

    /// <summary>RAG 결과가 비었을 때만 전체 DB 블록으로 대체.</summary>
    public bool RagFallbackToFullDb { get; set; } = true;

    /// <summary>RAG 질의문에 화자 인벤·장비·스킬 이름을 섞어 장비 언급과 연동.</summary>
    public bool RagIncludeSpeakerLoadout { get; set; } = true;

    /// <summary>WorldLore·몬스터·함정·스킬·아이템 참조 지식은 Ollama 임베딩 RAG로 검색.</summary>
    public bool UseEmbeddingRag { get; set; } = true;

    /// <summary>시스템 프롬프트에 넣을 ActionLog 최신 N건(Order 기준 꼬리).</summary>
    public int LatestActionLogEntriesInPrompt { get; set; } = 28;

    /// <summary>true면 ActionLog를 JSON 꼬리 대신 원정 단위 한국어 줄글로 넣습니다.</summary>
    public bool UseActionLogNarrativeProse { get; set; } = true;

    /// <summary>줄글 모드에서 최근 던전 런(원정) 최대 개수. 0이면 제한 없음.</summary>
    public int ActionLogNarrativeMaxDungeonRuns { get; set; } = 8;

    /// <summary>줄글 모드 시스템 프롬프트 블록 최대 문자 수(초과 시 앞부분 잘림).</summary>
    public int ActionLogNarrativeMaxChars { get; set; } = 14000;

    /// <summary>화자별 ActionLog 기반 주관 기억(성향 스텁)을 시스템 프롬프트에 넣습니다.</summary>
    public bool UsePerspectiveMemoryInPrompt { get; set; } = true;

    /// <summary>화자당 최근 N줄만 유지(0이면 전체).</summary>
    public int PerspectiveMemoryMaxLinesPerCharacter { get; set; } = 24;

    /// <summary>비어 있지 않으면 초기화 시 캐릭터별 관점 줄을 JSON으로 덤프합니다(상대 경로는 앱 기준 디렉터리).</summary>
    public string? PerspectiveMemoryExportPath { get; set; }

    /// <summary>
    /// 콘솔 1·2번 대화 세션 종료 시, 워킹 메모리 대화를 ActionLog.json에 Base/talk 한 건으로 추가합니다.
    /// </summary>
    public bool PersistDialogueSessionsToActionLog { get; set; } = true;

    /// <summary>
    /// 길드장 집무실: 메타·오프월드 발화를 임베딩 앵커와 코사인 유사도로 보강(키워드 누락 완화).
    /// EmbeddingModel(예: nomic-embed-text) 필요.
    /// </summary>
    public bool UseGuildOfficeSemanticGuardrail { get; set; } = true;

    /// <summary>유사도가 이 값 이상이면 메타 또는 오프월드로 분류해 휴리스틱을 덮어씁니다(0~1).</summary>
    public double GuildOfficeSemanticGuardrailThreshold { get; set; } = 0.78;

    /// <summary>
    /// 메타/오프월드 승격 시 원정 앵커 유사도와의 간격(0~1). &gt;0이면 expedition이 충분히 “작전 말투”면 오탐 억제.
    /// </summary>
    public double GuildOfficeSemanticDisambiguationMargin { get; set; } = 0.06;

    /// <summary>
    /// <see cref="GuildOfficeSemanticDisambiguationMargin"/> 적용 시, 원정 유사도가 이 값 이상일 때만 ‘작전 맥락과 경쟁’으로 본다(낮은 exp에서 오탐 억제 방지).
    /// </summary>
    public double GuildOfficeSemanticExpeditionDisambigMin { get; set; } = 0.58;

    /// <summary>
    /// 메타/오프 임베딩이 이 값 이상이면(강한 확신) 원정 경쟁 억제로 None을 내리지 않음.
    /// </summary>
    public double GuildOfficeSemanticStrongAtypicalFloor { get; set; } = 0.88;

    /// <summary>메타 vs 오프월드 코사인 차이가 이 값 미만이면 임베딩만으로는 구분 안 함 → None 후 키워드 타이브레이크.</summary>
    public double GuildOfficeSemanticMetaOffEmbeddingTieEpsilon { get; set; } = 0.015;

    /// <summary>임베딩이 None인데 명백한 메타/오프 단서가 있으면 승격(코사인이 임계값 근처일 때 보조).</summary>
    public bool UseGuildOfficeObviousKeywordTieBreak { get; set; } = true;

    /// <summary>길드장 발화가 작전·로그·던전 맥락(두꺼운 프롬프트)에 가까운지 임베딩 앵커와 비교할 때 임계값(0~1).</summary>
    public double GuildOfficeExpeditionContextThreshold { get; set; } = 0.72;

    /// <summary>
    /// true면 두꺼운 원정 프롬프트는 <c>exp >= 임계값</c>이면서 <c>exp &gt;= max(meta,off) + Lead</c>일 때만(키워드 예외 제외).
    /// false면 기존: 임계값만 충족하면 deep.
    /// </summary>
    public bool GuildOfficeExpeditionUseRelativeEmbeddingGate { get; set; } = true;

    /// <summary>상대 게이트: 원정 코사인이 메타/오프 최대보다 이 값만큼 커야 deep.</summary>
    public double GuildOfficeExpeditionDeepContextLead { get; set; } = 0.03;

    /// <summary>
    /// true면 소형 분류 LLM 한 번 추가(토큰 증가). 키워드·임베딩이 애매할 때 보완.
    /// </summary>
    public bool UseGuildOfficeLlmIntentRouter { get; set; } = false;
}

public class QualitativeAnalysisSettings
{
    public bool Enabled { get; set; }
    public bool LogToConsole { get; set; }
    public bool LogToFile { get; set; }
    public string LogFilePath { get; set; } = "";
    public bool IncludeRawLlmResponse { get; set; }
    public int ConsoleMaxPromptChars { get; set; }
    public int MaxRetrievalQueryPreviewChars { get; set; }
}
