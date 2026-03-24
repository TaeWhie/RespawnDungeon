namespace GuildDialogue.Data;

/// <summary>
/// 대화용 캐릭터 데이터. Unity의 CharacterData + 인벤토리 요약과 매핑 가능.
/// </summary>
public class Character
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>성별. 예: 남성, 여성, 논바이너리, 미정</summary>
    public string Gender { get; set; } = "미정";
    public int Age { get; set; } = 0;
    public string Role { get; set; } = "";
    /// <summary>
    /// PartyDatabase.json의 PartyId와 매칭. 비어 있으면 프롬프트/시뮬에서 "파티 미지정(전체 동료)" 규칙을 씁니다.
    /// </summary>
    public string? PartyId { get; set; }

    /// <summary>
    /// 캐릭터가 **지금** 머무르는 장소. 보통 BaseDatabase.json의 BaseId(main_hall, cafeteria 등).
    /// 아지트 밖이면 던전명·맵 코드 등 자유 문자열 가능.
    /// </summary>
    public string? CurrentLocationId { get; set; }

    /// <summary>위치 보조 설명(선택). 예: "식당 창가", "2층 복도".</summary>
    public string? CurrentLocationNote { get; set; }

    /// <summary>모험/군 경력 등 연차. CharactersDatabase.json.</summary>
    public int? Career { get; set; }
    public string Background { get; set; } = "";
    public string SpeechStyle { get; set; } = "";

    /// <summary>
    /// 현재 기분·감정 톤(한국어 짧은 라벨). JSON 시드값이며, 런타임에 대화 턴마다 갱신됩니다(메모리만).
    /// </summary>
    public string Mood { get; set; } = "중립";

    /// <summary>성향·취향에 맞게 좋아하는 것(한 줄씩). 대화 톤·화제 훅.</summary>
    public List<string>? Likes { get; set; }

    /// <summary>싫어하거나 피하는 것(한 줄씩).</summary>
    public List<string>? Dislikes { get; set; }

    /// <summary>최근 인상 깊었던 일(수동 기록). 원정 시뮬 종료 시 이번 런 로그 한 줄로 덮어씀(저장 시 반영). ActionLog와 충돌하면 로그가 우선.</summary>
    public string? RecentMemorableEvent { get; set; }

    public PersonalityValues Personality { get; set; } = new();

    /// <summary>Unity CharacterData 호환 스탯. 대화에서 "체력 낮다", "MP 부족" 등 맥락으로 사용.</summary>
    public CharacterStats? Stats { get; set; }

    /// <summary>소지품 요약. Unity InventoryData에서 itemName + itemType + 개수로 매핑.</summary>
    public List<InventoryEntry> Inventory { get; set; } = new();

    /// <summary>현재 장착 현황. 각 슬롯에 장착된 아이템명. null이면 미장착.</summary>
    public EquipmentSlots Equipment { get; set; } = new();

    /// <summary>다른 캐릭터와의 관계 데이터.</summary>
    public List<Relationship>? Relationships { get; set; } = new();

    /// <summary>보유 스킬 이름 목록(SkillDatabase.json와 매칭).</summary>
    public List<string> Skills { get; set; } = new();
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

/// <summary>장착 슬롯. 각 슬롯에 아이템명이 들어오며 null = 비어있음.</summary>
public class EquipmentSlots
{
    public string? Weapon { get; set; }
    public string? Helmet { get; set; }
    public string? Armor { get; set; }
    public string? Gloves { get; set; }
    public string? Boots { get; set; }
    public string? Accessory { get; set; }
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

public class Relationship
{
    public string TargetId { get; set; } = string.Empty;
    public int Affinity { get; set; } // 친밀도 (0~100)
    public int Trust { get; set; }    // 신뢰도 (0~100)
    public string RelationType { get; set; } = string.Empty; // 관계 유형 (예: 소꿉친구, 비즈니스)
}
