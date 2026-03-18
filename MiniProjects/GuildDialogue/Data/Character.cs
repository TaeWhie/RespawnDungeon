namespace GuildDialogue.Data;

/// <summary>
/// 대화용 캐릭터 데이터. Unity의 CharacterData + 인벤토리 요약과 매핑 가능.
/// </summary>
public class Character
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string Background { get; set; } = "";
    public PersonalityValues Personality { get; set; } = new();
    public List<string> FallbackLines { get; set; } = new();

    /// <summary>Unity CharacterData 호환 스탯. 대화에서 "체력 낮다", "MP 부족" 등 맥락으로 사용.</summary>
    public CharacterStats? Stats { get; set; }

    /// <summary>소지품 요약. Unity InventoryData에서 itemName + itemType + 개수로 매핑.</summary>
    public List<InventoryEntry> Inventory { get; set; } = new();
}

/// <summary>Unity CharacterData 상태/스탯 필드와 1:1 대응.</summary>
public class CharacterStats
{
    public int CurrentHP { get; set; } = 100;
    public int MaxHP { get; set; } = 100;
    public int CurrentMP { get; set; } = 50;
    public int MaxMP { get; set; } = 50;
    public int Atk { get; set; } = 10;
    public int Def { get; set; } = 5;
    public int Str { get; set; } = 8;
    public int Dex { get; set; } = 12;
    public int Int { get; set; } = 6;
    public int StatPoints { get; set; } = 0;
}

/// <summary>인벤토리 한 슬롯(또는 소모품 N개). ItemData.itemName, itemType, 개수.</summary>
public class InventoryEntry
{
    public string ItemName { get; set; } = "";
    /// <summary>Unity ItemData.ItemType: Etc, Helmet, Armor, Weapon, Shield, Gloves, Boots, Accessory</summary>
    public string ItemType { get; set; } = "Etc";
    public int Count { get; set; } = 1;
}

public class PersonalityValues
{
    public int Courage { get; set; }
    public int Caution { get; set; }
    public int Greed { get; set; }
    public int Orderliness { get; set; }
    public int Impulsiveness { get; set; }
    public int Cooperation { get; set; }
    public int Aggression { get; set; }
    public int Focus { get; set; }
    public int Adaptability { get; set; }
    public int Frugality { get; set; }
}
