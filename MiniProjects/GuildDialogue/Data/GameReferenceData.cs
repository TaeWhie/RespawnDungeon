namespace GuildDialogue.Data;

/// <summary>Config/BaseDatabase.json — 아지트 시설.</summary>
public class BaseFacilityData
{
    public string BaseId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public string AvailableServices { get; set; } = "";
    public string RiskOrLimit { get; set; } = "";
}

/// <summary>Config/MonsterDatabase.json</summary>
public class MonsterData
{
    public string MonsterId { get; set; } = "";
    public string MonsterName { get; set; } = "";
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public string Weakness { get; set; } = "";
    public string DangerLevel { get; set; } = "";
    public List<string> Traits { get; set; } = new();
}

/// <summary>Config/SkillDatabase.json</summary>
public class SkillData
{
    public string SkillName { get; set; } = "";
    public string SkillType { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public string Description { get; set; } = "";
    public string Effects { get; set; } = "";
    public int MpCost { get; set; }
    public int CooldownTurns { get; set; }
}

/// <summary>Config/TrapTypeDatabase.json</summary>
public class TrapTypeData
{
    public string TrapId { get; set; } = "";
    public string TrapName { get; set; } = "";
    public string Category { get; set; } = "";
    public string TriggerCondition { get; set; } = "";
    public string Effect { get; set; } = "";
    public string CounterMeasure { get; set; } = "";
    public string DangerLevel { get; set; } = "";
}

/// <summary>Config/EventTypeDatabase.json 한 줄.</summary>
public class EventTypeDbEntry
{
    public string Code { get; set; } = "";
    public string LabelKo { get; set; } = "";
    public string Meaning { get; set; } = "";
}

public class EventTypeDatabaseRoot
{
    public List<EventTypeDbEntry> DungeonEventTypes { get; set; } = new();
    public List<EventTypeDbEntry> BaseEventTypes { get; set; } = new();
}

/// <summary>WorldLore.json Organizations 항목.</summary>
public class OrganizationData
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = "";
}

/// <summary>Config/JobDatabase.json — <see cref="Character.Role"/>과 매칭되는 직업·허용 스킬.</summary>
public class JobRoleData
{
    /// <summary><see cref="Character.Role"/>과 동일한 식별자(예: Vanguard, Support).</summary>
    public string RoleId { get; set; } = "";

    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary><see cref="SkillData.SkillName"/>과 일치하는 스킬만 이 직업이 사용 가능.</summary>
    public List<string> AllowedSkillNames { get; set; } = new();
}

/// <summary>Config/PartyDatabase.json — 파티 편성·호칭.</summary>
public class PartyData
{
    public string PartyId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Callsign { get; set; }
    public string Description { get; set; } = "";
    /// <summary>선택. 비어 있어도 Character.PartyId로 멤버를 역추적 가능.</summary>
    public List<string> MemberIds { get; set; } = new();
    public string? Notes { get; set; }
}

/// <summary>프롬프트·에피소드 서사에 넘기는 참조 데이터 묶음.</summary>
public class GameReferenceBundle
{
    public List<BaseFacilityData> Bases { get; set; } = new();
    public List<MonsterData> Monsters { get; set; } = new();
    public List<SkillData> Skills { get; set; } = new();
    public List<TrapTypeData> Traps { get; set; } = new();
    public EventTypeDatabaseRoot? EventTypes { get; set; }
    public List<PartyData> Parties { get; set; } = new();
    public List<JobRoleData> Jobs { get; set; } = new();
}

/// <summary>행동 로그 → 자연어 서사 시 몬스터·함정·이름 해석 보강.</summary>
public class EpisodicNarrativeContext
{
    public Dictionary<string, MonsterData>? MonstersByLocalizedName { get; init; }
    public IReadOnlyList<TrapTypeData>? TrapTypes { get; init; }
    public Dictionary<string, string>? CharacterIdToDisplayName { get; init; }
}
