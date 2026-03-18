namespace GuildDialogue.Data;

public class DialogueTurn
{
    public string SpeakerId { get; set; } = "";
    public string Text { get; set; } = "";
}

public class LongTermSummary
{
    public int TrustOffset { get; set; }
    public int MoodOffset { get; set; }
    public int RelationOffset { get; set; }
    public string SummarySentence { get; set; } = "";
    public List<string> PendingItems { get; set; } = new();
}
