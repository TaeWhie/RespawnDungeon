namespace GuildDialogue.Data;

/// <summary>
/// 아이템 정의. ItemDatabase.json 한 엔트리.
/// </summary>
public class ItemData
{
    public string ItemName { get; set; } = "";
    public string ItemType { get; set; } = "Etc";
    public string Description { get; set; } = "";
    public string Effects { get; set; } = "";
    public int Value { get; set; } = 0;
    public string Rarity { get; set; } = "일반";
}
