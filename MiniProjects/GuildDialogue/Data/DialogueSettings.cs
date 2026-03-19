namespace GuildDialogue.Data;

public class DialogueSettings
{
    public Dictionary<string, string> TraitToToneKeywords { get; set; } = new();
    public OllamaSettings Ollama { get; set; } = new();
}

public class OllamaSettings
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.2";
    public int TimeoutSeconds { get; set; } = 60;
}
