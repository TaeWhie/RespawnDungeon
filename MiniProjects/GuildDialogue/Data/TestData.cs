namespace GuildDialogue.Data;

public class TestDataRoot
{
    public CompanionDialogueScenario CompanionDialogue { get; set; } = new();
    public Dictionary<string, List<DungeonLog>> DungeonLogs { get; set; } = new();
}

public class CompanionDialogueScenario
{
    public string CurrentSituation { get; set; } = "";
    // 테스트 대화는 너무 길게 반복되지 않도록 기본 2턴으로 제한한다.
    public int MaxTurnsPerCharacter { get; set; } = 2;
}
