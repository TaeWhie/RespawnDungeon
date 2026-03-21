namespace GuildDialogue.Services;

/// <summary>길드장 집무실 1:1 한 턴 LLM 결과 (--explore-guild-office-llm).</summary>
public readonly record struct GuildOfficeLlmTurnResult(
    string? BuddyLine,
    GuildMasterAtypicalInputKind AtypicalKind,
    bool DeepExpedition,
    string? ErrorMessage,
    string? RawResponsePreview,
    double MetaSimilarity = 0,
    double OffWorldSimilarity = 0,
    double ExpeditionSimilarity = 0);
