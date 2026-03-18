namespace GuildDialogue.Data;

public class LlmResponse
{
    public string Tone { get; set; } = "";
    public string Intent { get; set; } = "";
    public string Line { get; set; } = "";
    public string InnerThought { get; set; } = "";
}
