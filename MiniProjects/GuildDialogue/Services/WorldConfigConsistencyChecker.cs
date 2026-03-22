using System.Text.Json;
using System.Text.RegularExpressions;
using GuildDialogue.Data;
using System.Linq;

namespace GuildDialogue.Services;

/// <summary>세계관 JSON 간 교차 참조 일관성(스킬→직업, 아이템/몬스터→WorldLore 던전 등).</summary>
public sealed record WorldConfigIssue(string Severity, string Code, string Message, string? StepId);

public static class WorldConfigConsistencyChecker
{
    private static JsonSerializerOptions Opts() => new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static IReadOnlyList<WorldConfigIssue> Validate(
        DialogueConfigLoader loader,
        IReadOnlyDictionary<string, string>? overrides = null)
    {
        var issues = new List<WorldConfigIssue>();
        string Text(string name) =>
            overrides != null && overrides.TryGetValue(name, out var o) ? o : loader.ReadWorldConfigText(name);

        List<TagDatabaseEntry>? tagDb = null;
        try
        {
            tagDb = JsonSerializer.Deserialize<List<TagDatabaseEntry>>(Text("TagDatabase.json"), Opts());
        }
        catch (Exception ex)
        {
            issues.Add(new WorldConfigIssue("error", "PARSE", $"TagDatabase.json 파싱 실패: {ex.Message}", "tags"));
        }

        List<SkillData>? skills = null;
        try
        {
            skills = JsonSerializer.Deserialize<List<SkillData>>(Text("SkillDatabase.json"), Opts());
        }
        catch (Exception ex)
        {
            issues.Add(new WorldConfigIssue("error", "PARSE", $"SkillDatabase.json 파싱 실패: {ex.Message}", "skill"));
        }

        var tagNames = new HashSet<string>(
            (tagDb ?? new List<TagDatabaseEntry>()).Where(t => !string.IsNullOrWhiteSpace(t.TagName)).Select(t => t.TagName),
            StringComparer.OrdinalIgnoreCase);

        List<SkillTypeEntry>? skillTypeDb = null;
        try
        {
            skillTypeDb = JsonSerializer.Deserialize<List<SkillTypeEntry>>(Text("SkillTypeDatabase.json"), Opts());
        }
        catch (Exception ex)
        {
            issues.Add(new WorldConfigIssue("error", "PARSE", $"SkillTypeDatabase.json 파싱 실패: {ex.Message}", "lexicon"));
        }

        List<EffectSnippetEntry>? effectSnippetDb = null;
        try
        {
            effectSnippetDb = JsonSerializer.Deserialize<List<EffectSnippetEntry>>(Text("EffectSnippetDatabase.json"), Opts());
        }
        catch (Exception ex)
        {
            issues.Add(new WorldConfigIssue("error", "PARSE", $"EffectSnippetDatabase.json 파싱 실패: {ex.Message}", "lexicon"));
        }

        var skillTypeNames = new HashSet<string>(
            (skillTypeDb ?? new List<SkillTypeEntry>()).Where(x => !string.IsNullOrWhiteSpace(x.TypeName)).Select(x => x.TypeName),
            StringComparer.OrdinalIgnoreCase);
        var effectSnippetByName = (effectSnippetDb ?? new List<EffectSnippetEntry>())
            .Where(e => !string.IsNullOrWhiteSpace(e.SnippetName))
            .ToDictionary(e => e.SnippetName!, e => e, StringComparer.OrdinalIgnoreCase);

        foreach (var se in effectSnippetDb ?? new List<EffectSnippetEntry>())
        {
            if (string.IsNullOrWhiteSpace(se.SnippetName))
                continue;
            var msg = ValidateEffectSnippetStructure(se);
            if (msg != null)
                issues.Add(new WorldConfigIssue("error", "EFFECT_SNIPPET_INVALID", msg, "lexicon"));
        }

        if (skills != null)
        {
            foreach (var s in skills)
            {
                var sn = string.IsNullOrWhiteSpace(s.SkillName) ? "(SkillName 없음)" : s.SkillName;
                foreach (var tg in s.Tags ?? new List<string>())
                {
                    if (string.IsNullOrWhiteSpace(tg))
                        continue;
                    if (!tagNames.Contains(tg))
                        issues.Add(new WorldConfigIssue(
                            "error",
                            "TAG_UNKNOWN",
                            $"스킬 «{sn}» 의 Tags에 «{tg}» 가 TagDatabase.json에 없습니다. 태그 마스터에 추가하거나 선택을 수정하세요.",
                            "skill"));
                }

                if (skillTypeNames.Count > 0 && string.IsNullOrWhiteSpace(s.SkillType))
                    issues.Add(new WorldConfigIssue(
                        "error",
                        "SKILL_TYPE_EMPTY",
                        $"스킬 «{sn}» 에 SkillType이 비어 있습니다. SkillTypeDatabase.json 값을 선택하세요.",
                        "skill"));
                else if (!string.IsNullOrWhiteSpace(s.SkillType) && skillTypeNames.Count > 0 && !skillTypeNames.Contains(s.SkillType))
                    issues.Add(new WorldConfigIssue(
                        "error",
                        "SKILL_TYPE_UNKNOWN",
                        $"스킬 «{sn}» 의 SkillType «{s.SkillType}» 가 SkillTypeDatabase.json에 없습니다.",
                        "skill"));

                foreach (var (en, pvals) in EnumerateSkillSnippetBindings(s))
                {
                    if (string.IsNullOrWhiteSpace(en))
                        continue;
                    if (!effectSnippetByName.TryGetValue(en, out var ent))
                        issues.Add(new WorldConfigIssue(
                            "error",
                            "EFFECT_SNIPPET_UNKNOWN",
                            $"스킬 «{sn}» 의 스니펫 참조 «{en}» 가 EffectSnippetDatabase.json에 없습니다.",
                            "skill"));
                    else if (string.Equals(ent.Scope, "item", StringComparison.OrdinalIgnoreCase))
                        issues.Add(new WorldConfigIssue(
                            "error",
                            "EFFECT_SNIPPET_SCOPE",
                            $"스킬 «{sn}» 에는 item 전용 스니펫 «{en}» 을(를) 쓸 수 없습니다.",
                            "skill"));
                    else
                        ValidateSnippetParamsForTemplate($"스킬 «{sn}»", en, ent, pvals, issues, "skill");
                }
            }
        }

        List<JobRoleData>? jobs = null;
        try
        {
            jobs = JsonSerializer.Deserialize<List<JobRoleData>>(Text("JobDatabase.json"), Opts());
        }
        catch (Exception ex)
        {
            issues.Add(new WorldConfigIssue("error", "PARSE", $"JobDatabase.json 파싱 실패: {ex.Message}", "job"));
        }

        List<ItemData>? items = null;
        try
        {
            items = JsonSerializer.Deserialize<List<ItemData>>(Text("ItemDatabase.json"), Opts());
        }
        catch (Exception ex)
        {
            issues.Add(new WorldConfigIssue("error", "PARSE", $"ItemDatabase.json 파싱 실패: {ex.Message}", "item"));
        }

        List<MonsterTraitDatabaseEntry>? traitDb = null;
        try
        {
            traitDb = JsonSerializer.Deserialize<List<MonsterTraitDatabaseEntry>>(Text("MonsterTraitDatabase.json"), Opts());
        }
        catch (Exception ex)
        {
            issues.Add(new WorldConfigIssue("error", "PARSE", $"MonsterTraitDatabase.json 파싱 실패: {ex.Message}", "monsterTraits"));
        }

        List<MonsterData>? monsters = null;
        try
        {
            monsters = JsonSerializer.Deserialize<List<MonsterData>>(Text("MonsterDatabase.json"), Opts());
        }
        catch (Exception ex)
        {
            issues.Add(new WorldConfigIssue("error", "PARSE", $"MonsterDatabase.json 파싱 실패: {ex.Message}", "monster"));
        }

        List<ItemTypeEntry>? itemTypeDb = null;
        try
        {
            itemTypeDb = JsonSerializer.Deserialize<List<ItemTypeEntry>>(Text("ItemTypeDatabase.json"), Opts());
        }
        catch (Exception ex)
        {
            issues.Add(new WorldConfigIssue("error", "PARSE", $"ItemTypeDatabase.json 파싱 실패: {ex.Message}", "lexicon"));
        }

        List<RarityEntry>? rarityDb = null;
        try
        {
            rarityDb = JsonSerializer.Deserialize<List<RarityEntry>>(Text("RarityDatabase.json"), Opts());
        }
        catch (Exception ex)
        {
            issues.Add(new WorldConfigIssue("error", "PARSE", $"RarityDatabase.json 파싱 실패: {ex.Message}", "lexicon"));
        }

        List<MonsterTypeEntry>? monsterTypeDb = null;
        try
        {
            monsterTypeDb = JsonSerializer.Deserialize<List<MonsterTypeEntry>>(Text("MonsterTypeDatabase.json"), Opts());
        }
        catch (Exception ex)
        {
            issues.Add(new WorldConfigIssue("error", "PARSE", $"MonsterTypeDatabase.json 파싱 실패: {ex.Message}", "lexicon"));
        }

        List<DangerLevelEntry>? dangerLevelDb = null;
        try
        {
            dangerLevelDb = JsonSerializer.Deserialize<List<DangerLevelEntry>>(Text("DangerLevelDatabase.json"), Opts());
        }
        catch (Exception ex)
        {
            issues.Add(new WorldConfigIssue("error", "PARSE", $"DangerLevelDatabase.json 파싱 실패: {ex.Message}", "lexicon"));
        }

        List<TrapCategoryEntry>? trapCategoryDb = null;
        try
        {
            trapCategoryDb = JsonSerializer.Deserialize<List<TrapCategoryEntry>>(Text("TrapCategoryDatabase.json"), Opts());
        }
        catch (Exception ex)
        {
            issues.Add(new WorldConfigIssue("error", "PARSE", $"TrapCategoryDatabase.json 파싱 실패: {ex.Message}", "lexicon"));
        }

        var itemTypeNames = new HashSet<string>(
            (itemTypeDb ?? new List<ItemTypeEntry>()).Where(x => !string.IsNullOrWhiteSpace(x.TypeName)).Select(x => x.TypeName),
            StringComparer.OrdinalIgnoreCase);
        var rarityNames = new HashSet<string>(
            (rarityDb ?? new List<RarityEntry>()).Where(x => !string.IsNullOrWhiteSpace(x.RarityName)).Select(x => x.RarityName),
            StringComparer.OrdinalIgnoreCase);
        var monsterTypeNames = new HashSet<string>(
            (monsterTypeDb ?? new List<MonsterTypeEntry>()).Where(x => !string.IsNullOrWhiteSpace(x.TypeName)).Select(x => x.TypeName),
            StringComparer.OrdinalIgnoreCase);
        var dangerLevelNames = new HashSet<string>(
            (dangerLevelDb ?? new List<DangerLevelEntry>()).Where(x => !string.IsNullOrWhiteSpace(x.LevelName)).Select(x => x.LevelName),
            StringComparer.OrdinalIgnoreCase);
        var trapCategoryNames = new HashSet<string>(
            (trapCategoryDb ?? new List<TrapCategoryEntry>()).Where(x => !string.IsNullOrWhiteSpace(x.CategoryName)).Select(x => x.CategoryName),
            StringComparer.OrdinalIgnoreCase);

        if (items != null)
        {
            foreach (var it in items)
            {
                var nm = string.IsNullOrWhiteSpace(it.ItemName) ? "(ItemName 없음)" : it.ItemName;
                if (itemTypeNames.Count > 0 && string.IsNullOrWhiteSpace(it.ItemType))
                    issues.Add(new WorldConfigIssue(
                        "error",
                        "ITEM_TYPE_EMPTY",
                        $"아이템 «{nm}» 에 ItemType이 비어 있습니다.",
                        "item"));
                else if (!string.IsNullOrWhiteSpace(it.ItemType) && itemTypeNames.Count > 0 && !itemTypeNames.Contains(it.ItemType))
                    issues.Add(new WorldConfigIssue(
                        "error",
                        "ITEM_TYPE_UNKNOWN",
                        $"아이템 «{nm}» 의 ItemType «{it.ItemType}» 가 ItemTypeDatabase.json에 없습니다.",
                        "item"));
                if (rarityNames.Count > 0 && string.IsNullOrWhiteSpace(it.Rarity))
                    issues.Add(new WorldConfigIssue(
                        "error",
                        "RARITY_EMPTY",
                        $"아이템 «{nm}» 에 Rarity가 비어 있습니다.",
                        "item"));
                else if (!string.IsNullOrWhiteSpace(it.Rarity) && rarityNames.Count > 0 && !rarityNames.Contains(it.Rarity))
                    issues.Add(new WorldConfigIssue(
                        "error",
                        "RARITY_UNKNOWN",
                        $"아이템 «{nm}» 의 Rarity «{it.Rarity}» 가 RarityDatabase.json에 없습니다.",
                        "item"));
                foreach (var (en, pvals) in EnumerateItemSnippetBindings(it))
                {
                    if (string.IsNullOrWhiteSpace(en))
                        continue;
                    if (!effectSnippetByName.TryGetValue(en, out var ent))
                        issues.Add(new WorldConfigIssue(
                            "error",
                            "EFFECT_SNIPPET_UNKNOWN",
                            $"아이템 «{nm}» 의 스니펫 참조 «{en}» 가 EffectSnippetDatabase.json에 없습니다.",
                            "item"));
                    else if (string.Equals(ent.Scope, "skill", StringComparison.OrdinalIgnoreCase))
                        issues.Add(new WorldConfigIssue(
                            "error",
                            "EFFECT_SNIPPET_SCOPE",
                            $"아이템 «{nm}» 에는 skill 전용 스니펫 «{en}» 을(를) 쓸 수 없습니다.",
                            "item"));
                    else
                        ValidateSnippetParamsForTemplate($"아이템 «{nm}»", en, ent, pvals, issues, "item");
                }
            }
        }

        var traitNames = new HashSet<string>(
            (traitDb ?? new List<MonsterTraitDatabaseEntry>()).Where(t => !string.IsNullOrWhiteSpace(t.TraitName)).Select(t => t.TraitName),
            StringComparer.OrdinalIgnoreCase);

        if (monsters != null)
        {
            foreach (var m in monsters)
            {
                var mn = string.IsNullOrWhiteSpace(m.MonsterName) ? "(MonsterName 없음)" : m.MonsterName;
                foreach (var tr in m.Traits ?? new List<string>())
                {
                    if (string.IsNullOrWhiteSpace(tr))
                        continue;
                    if (!traitNames.Contains(tr))
                        issues.Add(new WorldConfigIssue(
                            "error",
                            "TRAIT_UNKNOWN",
                            $"몬스터 «{mn}» 의 Traits에 «{tr}» 가 MonsterTraitDatabase.json에 없습니다. 특성 마스터에 추가하거나 선택을 수정하세요.",
                            "monster"));
                }

                if (monsterTypeNames.Count > 0 && string.IsNullOrWhiteSpace(m.Type))
                    issues.Add(new WorldConfigIssue(
                        "error",
                        "MONSTER_TYPE_EMPTY",
                        $"몬스터 «{mn}» 에 Type이 비어 있습니다.",
                        "monster"));
                else if (!string.IsNullOrWhiteSpace(m.Type) && monsterTypeNames.Count > 0 && !monsterTypeNames.Contains(m.Type))
                    issues.Add(new WorldConfigIssue(
                        "error",
                        "MONSTER_TYPE_UNKNOWN",
                        $"몬스터 «{mn}» 의 Type «{m.Type}» 가 MonsterTypeDatabase.json에 없습니다.",
                        "monster"));
                if (dangerLevelNames.Count > 0 && string.IsNullOrWhiteSpace(m.DangerLevel))
                    issues.Add(new WorldConfigIssue(
                        "error",
                        "DANGER_LEVEL_EMPTY",
                        $"몬스터 «{mn}» 에 DangerLevel이 비어 있습니다.",
                        "monster"));
                else if (!string.IsNullOrWhiteSpace(m.DangerLevel) && dangerLevelNames.Count > 0 && !dangerLevelNames.Contains(m.DangerLevel))
                    issues.Add(new WorldConfigIssue(
                        "error",
                        "DANGER_LEVEL_UNKNOWN",
                        $"몬스터 «{mn}» 의 DangerLevel «{m.DangerLevel}» 가 DangerLevelDatabase.json에 없습니다.",
                        "monster"));
            }
        }

        List<TrapTypeData>? traps = null;
        try
        {
            traps = JsonSerializer.Deserialize<List<TrapTypeData>>(Text("TrapTypeDatabase.json"), Opts());
        }
        catch (Exception ex)
        {
            issues.Add(new WorldConfigIssue("error", "PARSE", $"TrapTypeDatabase.json 파싱 실패: {ex.Message}", "trap"));
        }

        if (traps != null)
        {
            foreach (var t in traps)
            {
                var tn = string.IsNullOrWhiteSpace(t.TrapName) ? "(TrapName 없음)" : t.TrapName;
                if (trapCategoryNames.Count > 0 && string.IsNullOrWhiteSpace(t.Category))
                    issues.Add(new WorldConfigIssue(
                        "error",
                        "TRAP_CATEGORY_EMPTY",
                        $"함정 «{tn}» 에 Category가 비어 있습니다.",
                        "trap"));
                else if (!string.IsNullOrWhiteSpace(t.Category) && trapCategoryNames.Count > 0 && !trapCategoryNames.Contains(t.Category))
                    issues.Add(new WorldConfigIssue(
                        "error",
                        "TRAP_CATEGORY_UNKNOWN",
                        $"함정 «{tn}» 의 Category «{t.Category}» 가 TrapCategoryDatabase.json에 없습니다.",
                        "trap"));
                if (dangerLevelNames.Count > 0 && string.IsNullOrWhiteSpace(t.DangerLevel))
                    issues.Add(new WorldConfigIssue(
                        "error",
                        "TRAP_DANGER_EMPTY",
                        $"함정 «{tn}» 에 DangerLevel이 비어 있습니다.",
                        "trap"));
                else if (!string.IsNullOrWhiteSpace(t.DangerLevel) && dangerLevelNames.Count > 0 && !dangerLevelNames.Contains(t.DangerLevel))
                    issues.Add(new WorldConfigIssue(
                        "error",
                        "DANGER_LEVEL_UNKNOWN",
                        $"함정 «{tn}» 의 DangerLevel «{t.DangerLevel}» 가 DangerLevelDatabase.json에 없습니다.",
                        "trap"));
            }
        }

        WorldLore? lore = null;
        try
        {
            lore = JsonSerializer.Deserialize<WorldLore>(Text("WorldLore.json"), Opts());
        }
        catch (Exception ex)
        {
            issues.Add(new WorldConfigIssue("error", "PARSE", $"WorldLore.json 파싱 실패: {ex.Message}", "worldLore"));
        }

        var skillNames = new HashSet<string>(
            (skills ?? new List<SkillData>()).Where(s => !string.IsNullOrWhiteSpace(s.SkillName)).Select(s => s.SkillName),
            StringComparer.OrdinalIgnoreCase);

        if (skillNames.Count == 0 && (jobs?.Count ?? 0) > 0)
            issues.Add(new WorldConfigIssue("warn", "SKILL_EMPTY", "SkillDatabase가 비어 있으면 직업 허용 스킬 검증이 의미 없습니다.", "skill"));

        if (jobs != null)
        {
            foreach (var job in jobs)
            {
                var role = string.IsNullOrWhiteSpace(job.RoleId) ? "(RoleId 없음)" : job.RoleId;
                foreach (var sn in job.AllowedSkillNames ?? new List<string>())
                {
                    if (string.IsNullOrWhiteSpace(sn))
                        continue;
                    if (!skillNames.Contains(sn))
                        issues.Add(new WorldConfigIssue(
                            "error",
                            "JOB_SKILL_UNKNOWN",
                            $"직업 «{role}» 의 AllowedSkillNames에 «{sn}» 가 있으나 SkillDatabase.json에 동일 SkillName이 없습니다.",
                            "job"));
                }
            }
        }

        var itemNames = new HashSet<string>(
            (items ?? new List<ItemData>()).Where(i => !string.IsNullOrWhiteSpace(i.ItemName)).Select(i => i.ItemName),
            StringComparer.OrdinalIgnoreCase);

        var monsterNames = new HashSet<string>(
            (monsters ?? new List<MonsterData>()).Where(m => !string.IsNullOrWhiteSpace(m.MonsterName)).Select(m => m.MonsterName),
            StringComparer.OrdinalIgnoreCase);

        if (lore?.Dungeons != null)
        {
            foreach (var d in lore.Dungeons)
            {
                var dn = string.IsNullOrWhiteSpace(d.Name) ? "(이름 없는 던전)" : d.Name;
                foreach (var reward in d.KnownRewards ?? new List<string>())
                {
                    if (string.IsNullOrWhiteSpace(reward))
                        continue;
                    if (!itemNames.Contains(reward))
                        issues.Add(new WorldConfigIssue(
                            "error",
                            "DUNGEON_REWARD_NOT_IN_ITEM_DB",
                            $"던전 «{dn}» 의 KnownRewards «{reward}» 가 ItemDatabase.json에 없습니다. 원정 시뮬·대사 검증에서 아이템 정의가 필요합니다.",
                            "worldLore"));
                }

                foreach (var mon in d.TypicalMonsters ?? new List<string>())
                {
                    if (string.IsNullOrWhiteSpace(mon))
                        continue;
                    if (!monsterNames.Contains(mon))
                        issues.Add(new WorldConfigIssue(
                            "warn",
                            "DUNGEON_MONSTER_NOT_IN_MONSTER_DB",
                            $"던전 «{dn}» 의 TypicalMonsters «{mon}» 가 MonsterDatabase.json에 없습니다. 시뮬 풀·대사 일치에 불리할 수 있습니다.",
                            "worldLore"));
                }
            }
        }

        static void TryJson(string file, string stepId, Func<string> getText, List<WorldConfigIssue> list)
        {
            try
            {
                JsonDocument.Parse(
                    getText(),
                    new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            }
            catch (Exception ex)
            {
                list.Add(new WorldConfigIssue("error", "PARSE", $"{file} 파싱 실패: {ex.Message}", stepId));
            }
        }

        TryJson("TagDatabase.json", "tags", () => Text("TagDatabase.json"), issues);
        TryJson("SkillTypeDatabase.json", "lexicon", () => Text("SkillTypeDatabase.json"), issues);
        TryJson("ItemTypeDatabase.json", "lexicon", () => Text("ItemTypeDatabase.json"), issues);
        TryJson("RarityDatabase.json", "lexicon", () => Text("RarityDatabase.json"), issues);
        TryJson("MonsterTypeDatabase.json", "lexicon", () => Text("MonsterTypeDatabase.json"), issues);
        TryJson("DangerLevelDatabase.json", "lexicon", () => Text("DangerLevelDatabase.json"), issues);
        TryJson("TrapCategoryDatabase.json", "lexicon", () => Text("TrapCategoryDatabase.json"), issues);
        TryJson("EffectSnippetDatabase.json", "lexicon", () => Text("EffectSnippetDatabase.json"), issues);
        TryJson("MonsterTraitDatabase.json", "monsterTraits", () => Text("MonsterTraitDatabase.json"), issues);
        TryJson("TrapTypeDatabase.json", "trap", () => Text("TrapTypeDatabase.json"), issues);
        TryJson("BaseDatabase.json", "base", () => Text("BaseDatabase.json"), issues);
        TryJson("EventTypeDatabase.json", "event", () => Text("EventTypeDatabase.json"), issues);
        TryJson("DialogueSettings.json", "dialogue", () => Text("DialogueSettings.json"), issues);
        TryJson("SemanticGuardrailAnchors.json", "llmAux", () => Text("SemanticGuardrailAnchors.json"), issues);
        TryJson("GuildOfficeExploration.json", "llmAux", () => Text("GuildOfficeExploration.json"), issues);

        return issues;
    }

    static string? ValidateEffectSnippetStructure(EffectSnippetEntry e)
    {
        var n = (e.SnippetName ?? "").Trim();
        var kind = (e.EffectKind ?? "").Trim();
        if (string.IsNullOrEmpty(kind))
        {
            if (string.IsNullOrWhiteSpace(e.Body))
                return $"스니펫 «{n}»: EffectKind를 지정하거나 레거시 Body를 채우세요.";
            return null;
        }
        switch (kind)
        {
            case "Custom":
                var tpl = (e.SummaryTemplate ?? "").Trim();
                var sum = (e.Summary ?? "").Trim();
                if (string.IsNullOrEmpty(tpl) && string.IsNullOrEmpty(sum))
                    return $"스니펫 «{n}»: Custom은 Summary 또는 SummaryTemplate 중 하나를 채우세요.";
                if (!string.IsNullOrEmpty(tpl))
                {
                    var keys = ExtractTemplateKeysFromSnippet(tpl);
                    var declared = new HashSet<string>(
                        (e.Parameters ?? new List<EffectSnippetParameterDef>())
                            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                            .Select(x => x.Key.Trim()),
                        StringComparer.Ordinal);
                    foreach (var k in keys)
                    {
                        if (!declared.Contains(k))
                            return $"스니펫 «{n}»: 템플릿의 {{{k}}}에 대응하는 Parameters 항목(Key)이 필요합니다.";
                    }
                }
                return null;
            case "None":
                return null;
            default:
                return $"스니펫 «{n}»: EffectKind는 Custom 또는 None만 사용합니다.";
        }
    }

    static IEnumerable<string> ExtractTemplateKeysFromSnippet(string template)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(template, @"\{([a-zA-Z_][a-zA-Z0-9_]*)\}"))
        {
            var k = m.Groups[1].Value;
            if (seen.Add(k))
                yield return k;
        }
    }

    static IEnumerable<(string Name, Dictionary<string, JsonElement>? Params)> EnumerateSkillSnippetBindings(SkillData s)
    {
        if (s.EffectSnippetRefs != null && s.EffectSnippetRefs.Count > 0)
        {
            foreach (var r in s.EffectSnippetRefs)
            {
                if (string.IsNullOrWhiteSpace(r.SnippetName))
                    continue;
                yield return (r.SnippetName.Trim(), r.Params);
            }
            yield break;
        }
        foreach (var n in s.EffectSnippetNames ?? new List<string>())
        {
            if (!string.IsNullOrWhiteSpace(n))
                yield return (n.Trim(), null);
        }
    }

    static IEnumerable<(string Name, Dictionary<string, JsonElement>? Params)> EnumerateItemSnippetBindings(ItemData it)
    {
        if (it.EffectSnippetRefs != null && it.EffectSnippetRefs.Count > 0)
        {
            foreach (var r in it.EffectSnippetRefs)
            {
                if (string.IsNullOrWhiteSpace(r.SnippetName))
                    continue;
                yield return (r.SnippetName.Trim(), r.Params);
            }
            yield break;
        }
        foreach (var n in it.EffectSnippetNames ?? new List<string>())
        {
            if (!string.IsNullOrWhiteSpace(n))
                yield return (n.Trim(), null);
        }
    }

    static void ValidateSnippetParamsForTemplate(
        string ownerLabel,
        string snippetName,
        EffectSnippetEntry ent,
        Dictionary<string, JsonElement>? pvals,
        List<WorldConfigIssue> issues,
        string stepId)
    {
        var tpl = (ent.SummaryTemplate ?? "").Trim();
        if (string.IsNullOrEmpty(tpl))
            return;
        foreach (var k in ExtractTemplateKeysFromSnippet(tpl))
        {
            if (pvals == null || !pvals.ContainsKey(k))
                issues.Add(new WorldConfigIssue(
                    "error",
                    "EFFECT_SNIPPET_PARAM_MISSING",
                    $"{ownerLabel}: 스니펫 «{snippetName}» 템플릿에 {{{k}}} 값이 필요합니다. EffectSnippetRefs[].Params에 넣으세요.",
                    stepId));
        }
    }
}
