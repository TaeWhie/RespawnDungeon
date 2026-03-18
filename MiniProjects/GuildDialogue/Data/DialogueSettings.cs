namespace GuildDialogue.Data;

public class DialogueSettings
{
    public int MaxRecentTurns { get; set; }
    public int MaxDungeonLogs { get; set; }
    public int MaxRecentTurnsFallback { get; set; }
    public int MaxDungeonLogsFallback { get; set; }
    public int MaxRetryCount { get; set; }
    public int OffsetMin { get; set; }
    public int OffsetMax { get; set; }
    public int OffsetStep { get; set; }
    public int KeywordExtractMinLength { get; set; }
    public Dictionary<string, string> TraitToToneKeywords { get; set; } = new();
    public KeywordCategoriesConfig KeywordCategories { get; set; } = new();
    public List<string> AllowedTopicKeywords { get; set; } = new();
    public Dictionary<string, List<string>> FallbackToneByRole { get; set; } = new();
    public OllamaSettings Ollama { get; set; } = new();
}

public class KeywordCategoriesConfig
{
    public KeywordDirection Relation { get; set; } = new();
    public KeywordDirection Trust { get; set; } = new();
    public KeywordDirection Mood { get; set; } = new();
}

public class KeywordDirection
{
    public List<string> Positive { get; set; } = new();
    public List<string> Negative { get; set; } = new();
}

public class OllamaSettings
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.2";
    public int TimeoutSeconds { get; set; } = 60;
}
