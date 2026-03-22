using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// [에델가드 MCP 런타임 팩트 엔진]
/// 사용자 질문 의도를 분석해 MCP 리소스(URI)와 도구(Tool) 실행 결과를 시스템 프롬프트에 동적으로 주입합니다.
/// 7B 모델의 토큰 다이어트와 팩트 정확도를 함께 맞추는 계층입니다(외부 MCP 서버가 아닌 본 앱 C# 실행).
/// </summary>
public static class AethelgardMcpRuntimeFacts
{
    private static readonly Regex s_subjectBeforeIga = new(
        @"(?:^|[\s,])(?<n>[\uAC00-\uD7A3]{2,8})[이가](?=[\s,\?!…]|$)",
        RegexOptions.Compiled);

    /// <summary>
    /// 길드장 발화 한 줄에 대해 MCP 스타일 팩트 블록을 생성합니다.
    /// <paramref name="allCharacters"/>·<paramref name="itemDatabase"/>가 null이면 파티 로스터·인벤만으로 제한합니다.
    /// </summary>
    public static string? Build(
        string userQuery,
        IReadOnlyList<Character> partyRoster,
        Character speaker,
        DialogueSettings settings,
        IReadOnlyList<Character>? allCharacters = null,
        IReadOnlyList<ItemData>? itemDatabase = null,
        GuildMasterAtypicalInputKind atypicalKind = GuildMasterAtypicalInputKind.None)
    {
        if (settings.Retrieval?.UseMcpRuntimeToolFacts != true)
            return null;
        if (string.IsNullOrWhiteSpace(userQuery))
            return null;
        if (atypicalKind == GuildMasterAtypicalInputKind.Gibberish)
            return null;

        var q = userQuery.Trim();
        var roster = partyRoster ?? Array.Empty<Character>();
        var allChars = (allCharacters ?? roster).Where(c => !string.IsNullOrWhiteSpace(c.Name)).ToList();

        var partyIds = new HashSet<string>(
            roster.Where(c => !string.IsNullOrWhiteSpace(c.Id)).Select(c => c.Id!),
            StringComparer.OrdinalIgnoreCase);
        var knownNames = new HashSet<string>(allChars.Select(c => c.Name), StringComparer.Ordinal);

        var itemNames = new HashSet<string>(StringComparer.Ordinal);
        if (itemDatabase != null)
        {
            foreach (var it in itemDatabase)
                if (!string.IsNullOrWhiteSpace(it.ItemName))
                    itemNames.Add(it.ItemName);
        }

        foreach (var ch in roster)
        {
            if (ch.Inventory == null) continue;
            foreach (var e in ch.Inventory)
                if (!string.IsNullOrWhiteSpace(e.ItemName))
                    itemNames.Add(e.ItemName);
        }

        var sb = new StringBuilder();
        bool hasContent = false;

        sb.AppendLine("=== [MCP RUNTIME: 리소스 조회 결과] ===");

        if (DetectNameQuery(q, roster, allChars, knownNames, itemNames))
        {
            AppendVerifyMemberResults(sb, q, roster, allChars, partyIds, knownNames, itemNames);
            hasContent = true;
        }

        if (DetectItemQuery(q, roster, knownNames, itemNames))
        {
            AppendResolveItemOwnershipResults(sb, q, roster, speaker, itemNames);
            hasContent = true;
        }

        if (DetectInventoryStatusQuery(q))
        {
            AppendFullInventorySnapshot(sb, roster, speaker);
            hasContent = true;
        }

        if (itemDatabase != null && itemDatabase.Count > 0 && DetectItemDefinitionIntent(q))
        {
            if (QueryMentionsItemFromDb(q, itemDatabase))
            {
                AppendItemDefinitionFacts(sb, q, itemDatabase);
                hasContent = true;
            }
            else if (TryAppendItemDefinitionFromInventoryFallback(sb, q, itemDatabase, speaker))
            {
                hasContent = true;
            }
        }

        if (!hasContent)
            return null;

        sb.AppendLine("=======================================");
        return sb.ToString().TrimEnd();
    }

    private static bool DetectNameQuery(
        string query,
        IReadOnlyList<Character> roster,
        IReadOnlyList<Character> allChars,
        HashSet<string> knownNames,
        HashSet<string> itemNames)
    {
        if (LooksLikePersonOrRosterQuestion(query))
            return true;

        foreach (var c in allChars)
            if (query.Contains(c.Name, StringComparison.Ordinal))
                return true;

        foreach (var raw in ExtractSubjectNamesBeforeIga(query))
        {
            if (IsLikelyItemFragment(raw, itemNames)) continue;
            if (!knownNames.Contains(raw))
                return true;
        }

        foreach (var ghost in GhostNameHints)
        {
            if (query.Contains(ghost, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>가짜 인물 함정으로 자주 쓰이는 이름(발화에 포함될 때만 검증).</summary>
    private static readonly string[] GhostNameHints =
    {
        "브라이언", "카일리어드", "리나우", "카일리"
    };

    private static bool DetectItemQuery(
        string query,
        IReadOnlyList<Character> roster,
        HashSet<string> knownNames,
        HashSet<string> itemNames)
    {
        if (LooksLikeOwnershipQuestion(query))
            return true;

        foreach (var name in itemNames.OrderByDescending(n => n.Length))
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (query.Contains(name, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool DetectInventoryStatusQuery(string query)
    {
        string[] cues =
        {
            "가방", "물자", "인벤", "소지품", "장비", "전부", "다 가지고", "뭐 가지고", "뭐 들고",
            "파티 인벤", "우리 인벤", "정리해", "챙겼", "비축"
        };
        foreach (var c in cues)
            if (query.Contains(c, StringComparison.Ordinal)) return true;

        // "뭐" 단독은 너무 넓음 — 물어보기 패턴과 함께
        if (query.Contains("뭐", StringComparison.Ordinal) &&
            (query.Contains("가지", StringComparison.Ordinal) || query.Contains("들고", StringComparison.Ordinal)))
            return true;

        return false;
    }

    private static bool LooksLikePersonOrRosterQuestion(string ut)
    {
        string[] cues =
        {
            "누구", "누가", "누군", "동료", "파티원", "소속", "길드원", "멤버",
            "실존", "유령", "이름", "맞는 사람", "누구냐", "누구야", "누구임", "누구신지", "누구신가",
            "봤어", "봤나", "봤어요", "알아?", "알아요?"
        };
        foreach (var c in cues)
            if (ut.Contains(c, StringComparison.Ordinal)) return true;
        return false;
    }

    private static bool LooksLikeOwnershipQuestion(string ut)
    {
        string[] cues =
        {
            "가지고", "갖고", "소지", "들고", "품에", "소유",
            "누구꺼", "누구거", "누구의", "어느", "나눠", "분배", "줬", "줬어", "받았", "넘겼",
            "루팅", "전리", "획득", "줄였", "나눴", "지급"
        };
        foreach (var c in cues)
            if (ut.Contains(c, StringComparison.Ordinal)) return true;
        return false;
    }

    private static IEnumerable<string> ExtractSubjectNamesBeforeIga(string ut)
    {
        foreach (Match m in s_subjectBeforeIga.Matches(ut))
        {
            var n = m.Groups["n"].Value;
            if (n.Length >= 2)
                yield return n;
        }
    }

    private static bool IsLikelyItemFragment(string token, HashSet<string> itemNames)
    {
        foreach (var it in itemNames)
            if (it.Contains(token, StringComparison.Ordinal) || token.Contains(it, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static void AppendVerifyMemberResults(
        StringBuilder sb,
        string query,
        IReadOnlyList<Character> roster,
        IReadOnlyList<Character> allChars,
        HashSet<string> partyIds,
        HashSet<string> knownNames,
        HashSet<string> itemNames)
    {
        sb.AppendLine("[Tool Result: Verify_Member]");
        var rosterNameSet = new HashSet<string>(
            roster.Where(x => !string.IsNullOrWhiteSpace(x.Name)).Select(x => x.Name),
            StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Line(string name, string status)
        {
            if (!seen.Add(name)) return;
            sb.AppendLine($"  - {name}: {status}");
        }

        foreach (var c in allChars.OrderByDescending(x => x.Name.Length))
        {
            if (!query.Contains(c.Name, StringComparison.Ordinal)) continue;
            if (partyIds.Contains(c.Id))
                Line(c.Name, $"STATUS_VALID (resource://guild/roster · ID: {c.Id})");
            else
                Line(c.Name, "IN_DATABASE_NOT_IN_PARTY (명단에 없음)");
        }

        foreach (var raw in ExtractSubjectNamesBeforeIga(query))
        {
            if (IsLikelyItemFragment(raw, itemNames)) continue;
            if (!seen.Contains(raw))
            {
                if (rosterNameSet.Contains(raw))
                    Line(raw, $"STATUS_VALID (resource://guild/roster · 이름 일치)");
                else if (knownNames.Contains(raw))
                    Line(raw, "STATUS_NOT_IN_ROSTER (DB에는 있으나 이번 파티 아님)");
                else
                    Line(raw, "STATUS_NOT_IN_ROSTER (⚠️ 명단·DB에 없음 — 가짜 인물 함정 차단)");
            }
        }

        foreach (var ghost in GhostNameHints)
        {
            if (!query.Contains(ghost, StringComparison.Ordinal)) continue;
            if (!seen.Contains(ghost))
            {
                var m = roster.FirstOrDefault(x => x.Name.Equals(ghost, StringComparison.Ordinal));
                if (m != null)
                    Line(ghost, $"STATUS_VALID (ID: {m.Id})");
                else
                    Line(ghost, "STATUS_NOT_IN_ROSTER (⚠️ 존재하지 않는 가짜 인물)");
            }
        }
    }

    private static void AppendResolveItemOwnershipResults(
        StringBuilder sb,
        string query,
        IReadOnlyList<Character> roster,
        Character speaker,
        HashSet<string> itemNames)
    {
        sb.AppendLine("[Tool Result: Resolve_Item_Ownership]");
        var ordered = itemNames.OrderByDescending(n => n.Length).ToList();
        var reported = new HashSet<string>(StringComparer.Ordinal);

        foreach (var itemName in ordered)
        {
            if (string.IsNullOrWhiteSpace(itemName)) continue;
            if (!query.Contains(itemName, StringComparison.Ordinal)) continue;
            if (!reported.Add(itemName)) continue;

            bool found = false;
            if (speaker.Inventory?.Any(i =>
                    !string.IsNullOrWhiteSpace(i.ItemName) &&
                    i.ItemName.Contains(itemName, StringComparison.Ordinal) &&
                    i.Count > 0) == true)
            {
                var id = string.IsNullOrWhiteSpace(speaker.Id) ? "self" : speaker.Id;
                sb.AppendLine($"  - {itemName}: resource://inventory/{id} (현재 화자 {speaker.Name} 소유)");
                found = true;
            }

            foreach (var member in roster.Where(m => m.Id != speaker.Id))
            {
                if (member.Inventory?.Any(i =>
                        !string.IsNullOrWhiteSpace(i.ItemName) &&
                        i.ItemName.Contains(itemName, StringComparison.Ordinal) &&
                        i.Count > 0) != true)
                    continue;

                var mid = string.IsNullOrWhiteSpace(member.Id) ? "(no-id)" : member.Id;
                sb.AppendLine($"  - {itemName}: resource://inventory/{mid} ({member.Name} 소유)");
                found = true;
            }

            if (!found)
                sb.AppendLine($"  - {itemName}: STATUS_NOT_FOUND (파티 인벤 스냅샷에 해당 전체 이름 없음 — 부분 일치·로그는 표 참고)");
        }
    }

    /// <summary>
    /// 아이템 이름·효과·설명 등을 Config/ItemDatabase.json 정의와 맞추기 위한 조회(환각 억제).
    /// </summary>
    private static bool DetectItemDefinitionIntent(string query)
    {
        string[] cues =
        {
            "효과", "설명", "뭐야", "뭔데", "무슨", "도감", "스펙", "용도", "정의", "능력",
            "어떻게 쓰", "쓰면", "먹으면", "사용하면", "먹었을", "마시면", "바르면"
        };
        foreach (var c in cues)
        {
            if (query.Contains(c, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool QueryMentionsItemFromDb(string query, IReadOnlyList<ItemData> db)
    {
        foreach (var it in db.OrderByDescending(x => x.ItemName.Length))
        {
            if (string.IsNullOrWhiteSpace(it.ItemName))
                continue;
            if (query.Contains(it.ItemName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 질문에 풀 네임이 없을 때: 화자(응답 NPC) 인벤 + 대명사/단일 후보/「포션」 힌트로 ItemDatabase 조회.
    /// </summary>
    private static bool TryAppendItemDefinitionFromInventoryFallback(
        StringBuilder sb,
        string query,
        IReadOnlyList<ItemData> db,
        Character speaker)
    {
        var resolved = ResolveSpeakerInventoryItemsInDb(speaker, db);
        if (resolved.Count == 0)
            return false;

        List<ItemData> pick;
        string? sourceNote;

        if (resolved.Count == 1)
        {
            pick = resolved;
            sourceNote = "(source: 화자 인벤에 ItemDatabase 정의가 있는 아이템이 1종류뿐 — 자동 매칭)";
        }
        else if (DetectPronounOrImplicitItemReference(query))
        {
            pick = resolved.OrderBy(x => x.ItemName, StringComparer.OrdinalIgnoreCase).Take(8).ToList();
            sourceNote = "(source: 화자 인벤 · 대명사/맥락 질문 — 후보 최대 8종, 풀 이름 없음)";
        }
        else if (QueryContainsPotionCategoryHint(query))
        {
            var pot = resolved.Where(x => x.ItemName.Contains("포션", StringComparison.Ordinal)).ToList();
            if (pot.Count == 0)
                return false;
            pick = pot.Count > 8 ? pot.Take(8).ToList() : pot;
            sourceNote = "(source: 화자 인벤 · 질문에 「포션」 포함 — 이름에 포션 포함 항목만)";
        }
        else
            return false;

        AppendItemDefinitionBlockForItems(sb, pick, sourceNote);
        return true;
    }

    /// <summary>화자 인벤 고유 아이템명 중 ItemDatabase에 있는 항목.</summary>
    private static List<ItemData> ResolveSpeakerInventoryItemsInDb(Character speaker, IReadOnlyList<ItemData> db)
    {
        var result = new List<ItemData>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (speaker.Inventory == null)
            return result;

        foreach (var inv in speaker.Inventory)
        {
            if (string.IsNullOrWhiteSpace(inv.ItemName) || inv.Count <= 0)
                continue;
            if (!seen.Add(inv.ItemName))
                continue;

            var data = db.FirstOrDefault(d =>
                !string.IsNullOrWhiteSpace(d.ItemName) &&
                d.ItemName.Equals(inv.ItemName, StringComparison.OrdinalIgnoreCase));
            if (data != null)
                result.Add(data);
        }

        return result;
    }

    private static bool DetectPronounOrImplicitItemReference(string query)
    {
        string[] cues =
        {
            "그 ", "그거", "그게", "그 포션", "그 아이템", "저거", "이거", "저건", "그건",
            "방금 ", "아까 ", "말한 ", "말했", "아까 말", "방금 말", "전에 말", "앞서 ", "조금 전"
        };

        foreach (var c in cues)
        {
            if (query.Contains(c, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool QueryContainsPotionCategoryHint(string query) =>
        query.Contains("포션", StringComparison.Ordinal);

    private static void AppendItemDefinitionFacts(StringBuilder sb, string query, IReadOnlyList<ItemData> db)
    {
        var items = new List<ItemData>();
        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var it in db.OrderByDescending(x => x.ItemName.Length))
        {
            if (string.IsNullOrWhiteSpace(it.ItemName))
                continue;
            if (!query.Contains(it.ItemName, StringComparison.Ordinal))
                continue;
            if (!reported.Add(it.ItemName))
                continue;
            items.Add(it);
        }

        if (items.Count == 0)
            return;

        AppendItemDefinitionBlockForItems(sb, items, null);
    }

    private static void AppendItemDefinitionBlockForItems(
        StringBuilder sb,
        IReadOnlyList<ItemData> items,
        string? sourceNote)
    {
        sb.AppendLine("[Tool Result: Lookup_Item_Definition]");
        sb.AppendLine("  (아래 Effects/Description은 ItemDatabase.json 확정값입니다. 수치·조건을 지어내지 마십시오.)");
        if (!string.IsNullOrWhiteSpace(sourceNote))
            sb.AppendLine($"  {sourceNote}");

        foreach (var it in items.OrderByDescending(x => x.ItemName.Length))
            AppendOneItemDefinition(sb, it);
    }

    private static void AppendOneItemDefinition(StringBuilder sb, ItemData it)
    {
        var uri = Uri.EscapeDataString(it.ItemName);
        sb.AppendLine($"  - {it.ItemName}: resource://itemdb/{uri}");
        sb.AppendLine($"      Type: {it.ItemType}, Rarity: {it.Rarity}, Value: {it.Value}");
        if (!string.IsNullOrWhiteSpace(it.Effects))
            sb.AppendLine($"      Effects (정의): {it.Effects.Trim()}");
        else
            sb.AppendLine("      Effects (정의): (없음)");
        if (!string.IsNullOrWhiteSpace(it.Description))
        {
            var desc = it.Description.Trim();
            if (desc.Length > 220)
                desc = desc[..220] + "…";
            sb.AppendLine($"      Description: {desc}");
        }
    }

    private static void AppendFullInventorySnapshot(StringBuilder sb, IReadOnlyList<Character> roster, Character speaker)
    {
        sb.AppendLine("[Resource: inventory/all_snapshot]");
        foreach (var c in roster)
        {
            var id = string.IsNullOrWhiteSpace(c.Id) ? "(no-id)" : c.Id.Trim();
            var tag = c.Id == speaker.Id ? " (화자)" : "";
            sb.AppendLine($"  · resource://inventory/{id} — {c.Name}{tag}");
            if (c.Inventory == null || c.Inventory.Count == 0)
            {
                sb.AppendLine("      (비어 있음)");
                continue;
            }

            foreach (var e in c.Inventory.OrderBy(x => x.ItemName))
            {
                if (string.IsNullOrWhiteSpace(e.ItemName) || e.Count <= 0) continue;
                sb.AppendLine($"      - {e.ItemName} ×{e.Count}");
            }
        }
    }
}
