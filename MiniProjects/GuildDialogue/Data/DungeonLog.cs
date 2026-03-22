namespace GuildDialogue.Data;

/// <summary>
/// 한 번의 던전 플레이 결과. 이벤트는 구조화된 데이터만 사용(줄글/Summary/Note 없음).
/// </summary>
public class DungeonLog
{
    public string DungeonName { get; set; } = "";
    public string FloorOrZone { get; set; } = "";
    public List<DungeonEvent> Events { get; set; } = new();
    public DateTime? PlayedAt { get; set; }
    /// <summary>런 결과: 클리어(다음 층 계단 발견·이동) | 탐색 후 복귀(자원/체력 부족으로 복귀) | 실패(파티 전멸)</summary>
    public string? Outcome { get; set; }
    public List<string>? PartyMembers { get; set; }
}

/// <summary>
/// 던전 내 발생 이벤트 한 건. 모든 내용은 필드로만 표현(줄글 금지).
/// </summary>
public class DungeonEvent
{
    /// <summary>Trap, Combat, Artifact, Heal, ConsumePotion, Loot, TrapAvoided, ItemTransfer, InventorySort, PartyCheck, DebuffClear</summary>
    public string EventType { get; set; } = "";

    public string? Location { get; set; }
    public string? TargetId { get; set; }
    public string? ActorId { get; set; }
    public int? Damage { get; set; }
    public int? HpBefore { get; set; }
    public int? HpAfter { get; set; }
    public int? MpBefore { get; set; }
    public int? MpAfter { get; set; }
    public string? ItemName { get; set; }
    public int? ItemCount { get; set; }

    /// <summary>전투 적. "스켈레톘 2마리" → [{ Name: "스켈레톤", Count: 2 }]</summary>
    public List<EnemyEntry>? Enemies { get; set; }

    public int? Turns { get; set; }

    /// <summary>방패 막기 등 횟수</summary>
    public int? BlockCount { get; set; }

    /// <summary>루팅 목록. "골드 80, 단검 1개" → [{ ItemName: "골드", Count: 80 }, { ItemName: "부러진 단검", Count: 1 }]</summary>
    public List<LootEntry>? LootItems { get; set; }

    /// <summary>TrapAvoided 시 회피한 함정 수</summary>
    public int? AvoidedCount { get; set; }

    /// <summary>ItemTransfer 시 건네준 쪽</summary>
    public string? FromId { get; set; }

    /// <summary>ItemTransfer 시 받는 쪽</summary>
    public string? ToId { get; set; }

    /// <summary>단일 아이템 이벤트(artifact 등)에서 최종 소지자 Id. LootItems에는 항목별 <see cref="LootEntry.AcquiredByCharacterId"/> 사용.</summary>
    public string? AcquiredByCharacterId { get; set; }

    /// <summary>PartyCheck 시 HpMp, PotionCount 등</summary>
    public string? CheckType { get; set; }

    /// <summary>DebuffClear 시 해제 횟수</summary>
    public int? ClearCount { get; set; }

    /// <summary>TrapTypeDatabase.TrapId와 매칭(있으면 Location보다 우선).</summary>
    public string? TrapId { get; set; }

    /// <summary>로그에 실은 고정 대사(에피소드 요약에 선택 반영).</summary>
    public List<string>? ScriptedDialogue { get; set; }

    /// <summary>이벤트 순서 (0,1,2… 또는 1,2,3…). 배열 인덱스와 같게 두거나, 외부 로그 ID와 동기화용으로 사용.</summary>
    public int Order { get; set; }

    /// <summary>플레이 시작 시점으로부터의 상대 시간(초). 필요 없으면 0.</summary>
    public double TimeOffsetSeconds { get; set; }
}

public class EnemyEntry
{
    public string Name { get; set; } = "";
    public int Count { get; set; } = 1;
}

public class LootEntry
{
    public string ItemName { get; set; } = "";
    public int Count { get; set; } = 1;

    /// <summary>해당 수량이 최종적으로 귀속된 캐릭터 Id (CharactersDatabase.Id). 루팅 분배 후 기록.</summary>
    public string? AcquiredByCharacterId { get; set; }
}
