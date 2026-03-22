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
    /// <summary>스니펫 참조 + Params. 비어 있으면 EffectSnippetNames(레거시) 사용.</summary>
    public List<EffectSnippetRef> EffectSnippetRefs { get; set; } = new();
    /// <summary>EffectSnippetDatabase.json의 SnippetName 목록. 비어 있으면 Effects 문자열만 사용(레거시).</summary>
    public List<string> EffectSnippetNames { get; set; } = new();
    public int Value { get; set; } = 0;
    public string Rarity { get; set; } = "일반";
}
