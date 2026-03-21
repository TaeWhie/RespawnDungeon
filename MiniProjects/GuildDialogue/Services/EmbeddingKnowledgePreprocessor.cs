using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// JSON 구조를 그대로 임베딩하지 않고, AI가 문맥 파악하기 쉬운 한국어 서술·Markdown 스타일로 변환합니다.
/// 메타데이터는 벡터 DB·필터 검색 확장 시 사용할 수 있도록 별도 보관합니다.
/// </summary>
public static class EmbeddingKnowledgePreprocessor
{
    /// <param name="embeddingText">임베딩 API에 넣을 전체 문자열(메타 한 줄 + 본문)</param>
    public sealed record ProcessedChunk(string Category, string Title, string EmbeddingText, IReadOnlyDictionary<string, string> Metadata);

    public static IReadOnlyList<ProcessedChunk> BuildAllChunks(
        WorldLore? lore,
        IReadOnlyList<MonsterData> monsters,
        IReadOnlyList<TrapTypeData> traps,
        IReadOnlyList<SkillData> skills,
        IReadOnlyList<ItemData> items,
        IReadOnlyList<Character>? characters)
    {
        var skillOwners = BuildSkillOwnerMap(characters);
        var list = new List<ProcessedChunk>();
        var worldName = lore?.WorldName ?? "이 세계";

        if (lore != null)
        {
            if (!string.IsNullOrWhiteSpace(lore.WorldSummary) || !string.IsNullOrWhiteSpace(lore.WorldName))
                list.Add(FromWorldOverview(lore));

            foreach (var loc in lore.Locations ?? Enumerable.Empty<LocationData>())
                list.Add(FromLocation(loc, worldName));

            foreach (var d in lore.Dungeons ?? Enumerable.Empty<DungeonData>())
                list.Add(FromDungeon(d, worldName));

            foreach (var o in lore.Organizations ?? Enumerable.Empty<OrganizationData>())
                list.Add(FromOrganization(o, worldName));

            foreach (var line in lore.Lore ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(line))
                    list.Add(FromLoreLine(line, worldName));
            }
        }

        foreach (var m in monsters)
            list.Add(FromMonster(m));

        foreach (var t in traps)
            list.Add(FromTrap(t));

        foreach (var sk in skills)
            list.Add(FromSkill(sk, skillOwners));

        foreach (var it in items)
            list.Add(FromItem(it));

        return list;
    }

    /// <summary>스킬명 → (Id, 이름, Role) 보유자 목록. JSON에 소유자가 없으므로 CharactersDatabase의 Skills 배열로 역주입.</summary>
    private static Dictionary<string, List<(string Id, string Name, string Role)>> BuildSkillOwnerMap(
        IReadOnlyList<Character>? characters)
    {
        var map = new Dictionary<string, List<(string, string, string)>>(StringComparer.Ordinal);
        if (characters == null) return map;

        foreach (var c in characters)
        {
            if (c.Skills == null) continue;
            foreach (var sk in c.Skills)
            {
                if (string.IsNullOrWhiteSpace(sk)) continue;
                if (!map.TryGetValue(sk.Trim(), out var lst))
                {
                    lst = new List<(string, string, string)>();
                    map[sk.Trim()] = lst;
                }
                lst.Add((c.Id, c.Name, c.Role));
            }
        }
        return map;
    }

    private static ProcessedChunk FromWorldOverview(WorldLore lore)
    {
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["kind"] = "world_overview",
            ["world_name"] = lore.WorldName ?? ""
        };
        var body = new StringBuilder();
        body.AppendLine($"## 세계관 개요");
        body.AppendLine();
        body.AppendLine($"{lore.WorldName} 세계는 다음과 같이 요약됩니다: {lore.WorldSummary}");
        if (!string.IsNullOrWhiteSpace(lore.GuildInfo))
            body.AppendLine($"길드·플레이어 진영: {lore.GuildInfo}");
        if (!string.IsNullOrWhiteSpace(lore.DungeonSystem))
            body.AppendLine($"던전·유적 규칙: {lore.DungeonSystem}");
        if (!string.IsNullOrWhiteSpace(lore.BaseCamp))
            body.AppendLine($"거점·캠프: {lore.BaseCamp}");
        if (!string.IsNullOrWhiteSpace(lore.CurrencyAndLoot))
            body.AppendLine($"화폐·전리품: {lore.CurrencyAndLoot}");
        return Finish("세계관", lore.WorldName ?? "개요", meta, body.ToString());
    }

    private static ProcessedChunk FromLocation(LocationData loc, string worldName)
    {
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["kind"] = "location",
            ["location_type"] = loc.Type ?? "",
            ["location_name"] = loc.Name ?? ""
        };
        var body = new StringBuilder();
        body.AppendLine($"## 장소");
        body.AppendLine();
        body.AppendLine($"{worldName} 세계의 장소 '{loc.Name}'은(는) 분류상 '{loc.Type}' 유형의 지명입니다. {loc.Description}");
        return Finish("장소", loc.Name ?? "", meta, body.ToString());
    }

    private static ProcessedChunk FromDungeon(DungeonData d, string worldName)
    {
        var monsterJoin = d.TypicalMonsters?.Count > 0 ? string.Join(", ", d.TypicalMonsters) : "정보 없음";
        var rewardJoin = d.KnownRewards?.Count > 0 ? string.Join(", ", d.KnownRewards) : "정보 없음";
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["kind"] = "dungeon",
            ["dungeon_name"] = d.Name ?? "",
            ["difficulty"] = d.Difficulty ?? ""
        };
        var body = new StringBuilder();
        body.AppendLine($"## 던전");
        body.AppendLine();
        body.AppendLine(
            $"{worldName} 세계의 '{d.Name}' 던전은 {d.Description} 난이도는 '{d.Difficulty}' 수준으로 분류됩니다. " +
            $"탐색 시 주로 {monsterJoin} 같은 적이 등장하는 편입니다. " +
            $"포획·전리로 자주 언급되는 보상에는 {rewardJoin} 등이 있습니다.");
        return Finish("던전", d.Name ?? "", meta, body.ToString());
    }

    private static ProcessedChunk FromOrganization(OrganizationData o, string worldName)
    {
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["kind"] = "organization",
            ["org_type"] = o.Type ?? "",
            ["organization_name"] = o.Name ?? ""
        };
        var body = new StringBuilder();
        body.AppendLine($"## 단체·세력");
        body.AppendLine();
        body.AppendLine($"{worldName} 세계의 단체 '{o.Name}'은(는) 성격상 '{o.Type}' 계열로 볼 수 있습니다. {o.Description}");
        return Finish("단체", o.Name ?? "", meta, body.ToString());
    }

    private static ProcessedChunk FromLoreLine(string line, string worldName)
    {
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["kind"] = "lore_snippet",
            ["world_name"] = worldName
        };
        var body = $"## 배경 설정 한 줄\n\n{worldName} 세계관에서 알려진 사실: {line}";
        return Finish("설정문장", line.Length > 32 ? line.Substring(0, 32) + "…" : line, meta, body);
    }

    private static ProcessedChunk FromMonster(MonsterData m)
    {
        var traits = m.Traits?.Count > 0 ? string.Join(", ", m.Traits) : "특성 미기재";
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["kind"] = "monster",
            ["monster_id"] = m.MonsterId,
            ["monster_type"] = m.Type,
            ["danger_level"] = m.DangerLevel
        };
        var body = new StringBuilder();
        body.AppendLine("## 몬스터 도감");
        body.AppendLine();
        body.AppendLine(
            $"이름이 '{m.MonsterName}'인 몬스터(내부 id: `{m.MonsterId}`)는 '{m.Type}' 타입으로 분류됩니다. " +
            $"위험도는 '{m.DangerLevel}'입니다. {m.Description} " +
            $"약점으로는 {m.Weakness}이(가) 효과적입니다. 전투·생태 특징으로는 {traits} 등을 들 수 있습니다.");
        return Finish("몬스터", m.MonsterName, meta, body.ToString());
    }

    private static ProcessedChunk FromTrap(TrapTypeData t)
    {
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["kind"] = "trap",
            ["trap_id"] = t.TrapId,
            ["trap_category"] = t.Category,
            ["danger_level"] = t.DangerLevel
        };
        var body = new StringBuilder();
        body.AppendLine("## 함정 유형");
        body.AppendLine();
        body.AppendLine(
            $"함정 '{t.TrapName}'(id: `{t.TrapId}`)은(는) {t.Category} 방식의 함정이며, 위험도는 '{t.DangerLevel}'입니다. " +
            $"발동 조건은 다음과 같다: {t.TriggerCondition} 발동 시 효과: {t.Effect} " +
            $"파티가 취할 수 있는 대응·무력화 방법: {t.CounterMeasure}");
        return Finish("함정", t.TrapName, meta, body.ToString());
    }

    private static ProcessedChunk FromSkill(
        SkillData sk,
        IReadOnlyDictionary<string, List<(string Id, string Name, string Role)>> skillOwners)
    {
        skillOwners.TryGetValue(sk.SkillName.Trim(), out var owners);
        owners ??= new List<(string, string, string)>();
        var tagJoin = sk.Tags?.Count > 0 ? string.Join(", ", sk.Tags) : "태그 없음";

        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["kind"] = "skill",
            ["skill_name"] = sk.SkillName,
            ["skill_type"] = sk.SkillType,
            ["owners"] = owners.Count > 0
                ? string.Join("; ", owners.Select(o => $"{o.Name}({o.Id})/{o.Role}"))
                : "_none_"
        };

        var body = new StringBuilder();
        body.AppendLine("## 스킬");
        body.AppendLine();
        body.AppendLine(
            $"스킬 '{sk.SkillName}'은(는) '{sk.SkillType}' 계열입니다. {sk.Description} " +
            $"전투 효과 요약: {sk.Effects} MP 소모는 {sk.MpCost}, 재사용 대기(쿨다운)는 약 {sk.CooldownTurns}턴입니다. 역할·전술 태그: {tagJoin}.");

        if (owners.Count > 0)
        {
            var who = string.Join(", ", owners.Select(o => $"{o.Name}(역할: {o.Role}, id:{o.Id})"));
            body.AppendLine();
            body.AppendLine($"**이 스킬을 캐릭터 시트에 보유한 인물:** {who}. " +
                           "질문이 특정 인물(예: 카일)에게 초점이 있으면 반드시 이 보유자 목록을 따른다.");
        }
        else
        {
            body.AppendLine();
            body.AppendLine("현재 등록된 캐릭터 데이터에서는 이 스킬의 **전용 보유자가 지정되지 않았다**. 일반 기술·NPC 기술로만 참고한다.");
        }

        return Finish("스킬", sk.SkillName, meta, body.ToString());
    }

    private static ProcessedChunk FromItem(ItemData it)
    {
        var meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["kind"] = "item",
            ["item_name"] = it.ItemName,
            ["item_type"] = it.ItemType,
            ["rarity"] = it.Rarity
        };
        var body = new StringBuilder();
        body.AppendLine("## 아이템");
        body.AppendLine();
        body.AppendLine(
            $"아이템 '{it.ItemName}'은(는) '{it.Rarity}' 등급이며, 인벤토리·장비 슬롯 분류는 '{it.ItemType}'입니다. {it.Description} " +
            $"사용·장착 시 효과: {it.Effects} 거래·시세 감으로는 대략 {it.Value}골드 수준으로 잡을 수 있다.");
        return Finish("아이템", it.ItemName, meta, body.ToString());
    }

    /// <summary>임베딩 입력: 짧은 메타 한 덩어리(문장 수준) + Markdown 본문. JSON 중괄호는 쓰지 않음.</summary>
    private static ProcessedChunk Finish(string category, string title, Dictionary<string, string> meta, string markdownBody)
    {
        var metaLine = string.Join(" ", meta.Select(kv => $"{kv.Key}={kv.Value}"));
        var embed = new StringBuilder();
        embed.AppendLine("[문서 유형과 검색용 꼬리표]");
        embed.AppendLine(metaLine);
        embed.AppendLine();
        embed.Append(markdownBody.Trim());
        var text = embed.ToString().Trim();
        return new ProcessedChunk(category, title, text, meta);
    }
}
