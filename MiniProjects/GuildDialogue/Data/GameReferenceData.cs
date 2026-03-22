using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GuildDialogue.Data;

/// <summary>스니펫 파라미터 자리 정의 (Key = 템플릿 {Key} 와 일치).</summary>
public class EffectSnippetParameterDef
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Unit { get; set; } = "";
}

/// <summary>스킬/아이템이 참조하는 스니펫 + 수치.</summary>
public class EffectSnippetRef
{
    public string SnippetName { get; set; } = "";
    public Dictionary<string, JsonElement>? Params { get; set; }
}

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
    /// <summary><see cref="MonsterTraitDatabaseEntry.TraitName"/> 목록에서 선택해 채웁니다.</summary>
    public List<string> Traits { get; set; } = new();
}

/// <summary>Config/TagDatabase.json — 스킬 <see cref="SkillData.Tags"/> 마스터.</summary>
public class TagDatabaseEntry
{
    public string TagName { get; set; } = "";
    public string Description { get; set; } = "";
}

/// <summary>Config/MonsterTraitDatabase.json — <see cref="MonsterData.Traits"/> 마스터.</summary>
public class MonsterTraitDatabaseEntry
{
    public string TraitName { get; set; } = "";
    public string Description { get; set; } = "";
}

/// <summary>Config/SkillTypeDatabase.json — 스킬 SkillType 마스터.</summary>
public class SkillTypeEntry
{
    public string TypeName { get; set; } = "";
    public string Description { get; set; } = "";
}

/// <summary>Config/ItemTypeDatabase.json — 아이템 ItemType 마스터.</summary>
public class ItemTypeEntry
{
    public string TypeName { get; set; } = "";
    public string Description { get; set; } = "";
}

/// <summary>Config/RarityDatabase.json — 아이템 Rarity 마스터.</summary>
public class RarityEntry
{
    public string RarityName { get; set; } = "";
    public string Description { get; set; } = "";
}

/// <summary>Config/MonsterTypeDatabase.json — 몬스터 Type 마스터.</summary>
public class MonsterTypeEntry
{
    public string TypeName { get; set; } = "";
    public string Description { get; set; } = "";
}

/// <summary>Config/DangerLevelDatabase.json — 몬스터·함정 DangerLevel 마스터.</summary>
public class DangerLevelEntry
{
    public string LevelName { get; set; } = "";
    public string Description { get; set; } = "";
}

/// <summary>Config/TrapCategoryDatabase.json — 함정 Category 마스터.</summary>
public class TrapCategoryEntry
{
    public string CategoryName { get; set; } = "";
    public string Description { get; set; } = "";
}

/// <summary>Config/EffectSnippetDatabase.json — 효과 문장 틀·파라미터 정의. 수치는 스킬/아이템 EffectSnippetRefs.Params.</summary>
public class EffectSnippetEntry
{
    public string SnippetName { get; set; } = "";
    /// <summary>레거시: EffectKind 미지정 시 표시문.</summary>
    public string Body { get; set; } = "";
    /// <summary>skill, item, both</summary>
    public string Scope { get; set; } = "both";
    /// <summary>Custom(요약/템플릿), None(비고). 비어 있으면 Body 사용.</summary>
    public string? EffectKind { get; set; }
    /// <summary>Custom — 고정 문장(템플릿 없을 때).</summary>
    public string? Summary { get; set; }
    /// <summary>Custom — {amount} 등 자리 표시자. Parameters와 함께 사용.</summary>
    public string? SummaryTemplate { get; set; }
    /// <summary>템플릿 자리 정의.</summary>
    public List<EffectSnippetParameterDef>? Parameters { get; set; }
    /// <summary>None.</summary>
    public string? Note { get; set; }

    /// <summary>스킬/아이템 Effects 합성용 한 줄 표시.</summary>
    public string ToDisplayString(Dictionary<string, JsonElement>? paramValues = null)
    {
        var kind = (EffectKind ?? "").Trim();
        if (string.IsNullOrEmpty(kind))
            return Body;

        return kind switch
        {
            "None" => string.IsNullOrWhiteSpace(Note) ? "없음" : Note!.Trim(),
            "Custom" => ResolveCustom(paramValues),
            _ => string.IsNullOrWhiteSpace(Summary) ? Body : Summary!
        };
    }

    string ResolveCustom(Dictionary<string, JsonElement>? paramValues)
    {
        var tpl = (SummaryTemplate ?? "").Trim();
        if (!string.IsNullOrEmpty(tpl))
            return InterpolateTemplate(tpl, paramValues);
        return (Summary ?? "").Trim();
    }

    static string InterpolateTemplate(string template, Dictionary<string, JsonElement>? p)
    {
        p ??= new Dictionary<string, JsonElement>();
        return Regex.Replace(template, @"\{([a-zA-Z_][a-zA-Z0-9_]*)\}", m =>
        {
            var key = m.Groups[1].Value;
            if (!p.TryGetValue(key, out var el))
                return m.Value;
            return FormatJsonElement(el);
        });
    }

    static string FormatJsonElement(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Number => FormatNumber(el),
        JsonValueKind.String => el.GetString() ?? "",
        _ => el.ToString()
    };

    static string FormatNumber(JsonElement el)
    {
        if (el.TryGetInt64(out var l))
            return l.ToString(CultureInfo.InvariantCulture);
        if (el.TryGetDouble(out var d))
        {
            if (double.IsFinite(d) && Math.Abs(d - Math.Round(d)) < 1e-9)
                return ((long)Math.Round(d)).ToString(CultureInfo.InvariantCulture);
            return d.ToString(CultureInfo.InvariantCulture);
        }
        return el.ToString();
    }
}

/// <summary>Config/SkillDatabase.json</summary>
public class SkillData
{
    public string SkillName { get; set; } = "";
    public string SkillType { get; set; } = "";
    /// <summary><see cref="TagDatabaseEntry.TagName"/> 목록에서 선택해 채웁니다.</summary>
    public List<string> Tags { get; set; } = new();
    public string Description { get; set; } = "";
    public string Effects { get; set; } = "";
    /// <summary>스니펫 참조 + Params. 비어 있으면 EffectSnippetNames(레거시) 사용.</summary>
    public List<EffectSnippetRef> EffectSnippetRefs { get; set; } = new();
    /// <summary>EffectSnippetDatabase.json의 SnippetName 목록. 비어 있으면 Effects 문자열만 사용(레거시).</summary>
    public List<string> EffectSnippetNames { get; set; } = new();
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
