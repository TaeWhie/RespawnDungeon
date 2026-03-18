namespace GuildDialogue.Data;

public class DialogueRequest
{
    public string SpeakerId { get; set; } = "";
    public string OtherId { get; set; } = "";  // "gm" or another character id
    public bool IsCompanionDialogue => OtherId != "gm" && !string.IsNullOrEmpty(OtherId);
    public string LastUtterance { get; set; } = "";
    public string RecentEvent { get; set; } = "";
    public string CurrentSituation { get; set; } = "";
    public Character? Speaker { get; set; }
    public Character? OtherCharacter { get; set; }  // when companion dialogue
    public List<DungeonLog> DungeonLogs { get; set; } = new();
    public LongTermSummary LongTerm { get; set; } = new();
    public List<DialogueTurn> RecentTurns { get; set; } = new();
    public bool IsLastTurn { get; set; }
}
