using System;
using System.Collections.Generic;
using System.Linq;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// ActionLog(팩트)를 순회해 캐릭터별 **주관적 기억 한 줄**을 누적합니다.
/// 객관 사실은 ActionLog/Episodic을 따르고, 여기 줄은 성향 필터 스텁(규칙 기반)입니다.
/// </summary>
public static class CharacterPerspectiveMemoryBuilder
{
    public static Dictionary<string, List<string>> BuildByCharacterId(
        IReadOnlyList<ActionLogEntry>? log,
        IReadOnlyList<Character> characters,
        int maxLinesPerCharacter)
    {
        var result = characters
            .Where(c => !string.IsNullOrWhiteSpace(c.Id))
            .ToDictionary(c => c.Id, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);

        if (log == null || log.Count == 0 || result.Count == 0)
            return result;

        var byToken = BuildCharacterLookup(characters);

        foreach (var entry in log.OrderBy(e => e.Order))
        {
            if (string.Equals(entry.EventType, "outcome", StringComparison.OrdinalIgnoreCase))
            {
                AppendOutcomePerspectives(entry, byToken, result);
                continue;
            }

            var involved = CollectInvolvedCharacterIds(entry, byToken);
            if (involved.Count == 0) continue;

            foreach (var charId in involved)
            {
                if (!result.ContainsKey(charId)) continue;
                if (!byToken.TryGetValue(charId, out var ch))
                    ch = characters.FirstOrDefault(c => c.Id.Equals(charId, StringComparison.OrdinalIgnoreCase));
                if (ch == null) continue;

                var note = BuildPerspectiveNote(entry, charId, byToken, ch.Personality);
                if (string.IsNullOrEmpty(note)) continue;

                var fact = FactOneLiner(entry);
                result[charId].Add($"• (Order {entry.Order}) {fact} — {note}");
            }
        }

        if (maxLinesPerCharacter > 0)
        {
            foreach (var id in result.Keys.ToList())
            {
                var list = result[id];
                if (list.Count > maxLinesPerCharacter)
                    result[id] = list.Skip(list.Count - maxLinesPerCharacter).ToList();
            }
        }

        return result;
    }

    public static string FormatBlockForSpeaker(
        string? speakerId,
        IReadOnlyDictionary<string, List<string>> linesByCharacterId,
        int maxLines)
    {
        if (string.IsNullOrWhiteSpace(speakerId) ||
            !linesByCharacterId.TryGetValue(speakerId, out var lines) ||
            lines == null || lines.Count == 0)
            return "";

        var take = maxLines > 0 ? lines.TakeLast(maxLines) : lines;
        return string.Join(Environment.NewLine, take);
    }

    private static Dictionary<string, Character> BuildCharacterLookup(IReadOnlyList<Character> characters
    )
    {
        var map = new Dictionary<string, Character>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in characters)
        {
            if (!string.IsNullOrWhiteSpace(c.Id)) map[c.Id] = c;
            if (!string.IsNullOrWhiteSpace(c.Name)) map[c.Name] = c;
        }
        return map;
    }

    private static string? ResolveId(string? token, Dictionary<string, Character> byToken)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var t = token.Trim();
        if (byToken.TryGetValue(t, out var c)) return c.Id;
        return t;
    }

    private static HashSet<string> CollectInvolvedCharacterIds(
        ActionLogEntry entry,
        Dictionary<string, Character> byToken)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (entry.PartyMembers != null)
        {
            foreach (var p in entry.PartyMembers)
            {
                var id = ResolveId(p, byToken);
                if (!string.IsNullOrEmpty(id)) set.Add(id);
            }
        }

        foreach (var raw in new[] { entry.ActorId, entry.TargetId, entry.FromId, entry.ToId })
        {
            var id = ResolveId(raw, byToken);
            if (!string.IsNullOrEmpty(id)) set.Add(id);
        }

        return set;
    }

    private static void AppendOutcomePerspectives(
        ActionLogEntry entry,
        Dictionary<string, Character> byToken,
        Dictionary<string, List<string>> result)
    {
        var partyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (entry.PartyMembers != null)
        {
            foreach (var p in entry.PartyMembers)
            {
                var id = ResolveId(p, byToken);
                if (!string.IsNullOrEmpty(id)) partyIds.Add(id);
            }
        }

        if (partyIds.Count == 0) return;

        var oc = entry.Outcome?.Trim().ToLowerInvariant() ?? "";

        foreach (var charId in partyIds)
        {
            if (!result.ContainsKey(charId)) continue;
            if (!byToken.TryGetValue(charId, out var ch))
                continue;
            var p = ch.Personality;

            string note = oc switch
            {
                "clear" when Hi(p.Courage) => "목표를 끝까지 밀어붙인 보람이 남는다.",
                "clear" when Hi(p.Caution) => "무사히 마무리되어 안도한다.",
                "retreat" when Hi(p.Caution) => "무리하지 않고 물러낸 선택이 맞았다고 본다.",
                "retreat" when Hi(p.Courage) => "아쉽지만 다음을 노리면 된다고 정리한다.",
                "fail" when Hi(p.Focus) => "다음엔 준비를 더 해야 한다고 머릿속에 박제된다.",
                "fail" => "결과가 마음에 걸린다.",
                _ => "이번 원정의 결말이 기억에 남는다."
            };

            var fact = $"{entry.DungeonName ?? "던전"} 런 결과({oc})";
            result[charId].Add($"• (Order {entry.Order}) {fact} — {note}");
        }
    }

    private static string FactOneLiner(ActionLogEntry e)
    {
        if (string.Equals(e.Type, "Base", StringComparison.OrdinalIgnoreCase))
        {
            var loc = e.Location ?? "?";
            return $"아지트 {loc}, {e.EventType ?? "?"}";
        }

        var d = e.DungeonName ?? "";
        var loc2 = e.Location ?? "?";
        return $"{d} / {loc2} / {e.EventType ?? "?"}";
    }

    private static string? BuildPerspectiveNote(
        ActionLogEntry e,
        string selfId,
        Dictionary<string, Character> byToken,
        PersonalityValues p)
    {
        var et = Norm(e.EventType);
        var isDungeon = string.Equals(e.Type, "Dungeon", StringComparison.OrdinalIgnoreCase);

        if (!isDungeon)
            return BasePerspective(e, selfId, byToken, p, et);

        var actor = ResolveId(e.ActorId, byToken);
        var target = ResolveId(e.TargetId, byToken);
        var from = ResolveId(e.FromId, byToken);
        var to = ResolveId(e.ToId, byToken);

        return et switch
        {
            "income" => Hi(p.Orderliness)
                ? "전열과 역할을 다시 점검하며 구역에 들어섰다."
                : "움츠린 긴장 속에서 발을 들였다.",

            "combat" => CombatPerspective(selfId, actor, p),

            "trap" => TrapPerspective(selfId, target, p),

            "trapavoided" => Hi(p.Caution)
                ? "함정 징후를 미리 읽어낸 듯한 안도가 있다."
                : "위기를 피한 순간이 가볍게 남는다.",

            "heal" => HealPerspective(selfId, actor, target, byToken, p),

            "loot" => Hi(p.Greed)
                ? "수확이 짜릿하게 남는다."
                : Hi(p.Frugality)
                    ? "꼭 필요한 것만 챙겼다고 정리한다."
                    : "전리 정리가 마음에 든다.",

            "artifact" => Hi(p.Focus)
                ? "단서가 손에 잡힌 느낌이 강하게 남는다."
                : "예상 밖의 발견에 잠시 생각이 멈췄다.",

            "consumepotion" => selfId.Equals(actor, StringComparison.OrdinalIgnoreCase)
                ? Hi(p.Frugality)
                    ? "소모품을 아꼈는지 잠깐 신경 쓰인다."
                    : "컨디션 회복이 우선이라 납득한다."
                : "동료가 자원을 쓰는 걸 옆에서 봤다.",

            "itemtransfer" => ItemTransferPerspective(selfId, from, to, p),

            "debuffclear" => selfId.Equals(actor, StringComparison.OrdinalIgnoreCase)
                ? Hi(p.Cooperation) ? "팀 상태를 바로잡았다는 안도." : "역할을 했다."
                : "상태가 풀려 숨이 트인다.",

            "partycheck" => Hi(p.Orderliness)
                ? "체크리스트대로 점검했다는 만족."
                : "잠시 호흡을 맞췄다.",

            "inventorysort" => Hi(p.Orderliness) || Hi(p.Focus)
                ? "짐정리 후 머리가 맑아진다."
                : "짐을 정돈했다.",

            "skilluse" => selfId.Equals(actor, StringComparison.OrdinalIgnoreCase)
                ? Hi(p.Aggression) ? "기술을 꽂아 넣은 감각이 남는다." : "MP 관리가 신경 쓰인다."
                : "연계가 눈에 들어왔다.",

            _ => null
        };
    }

    private static string? BasePerspective(
        ActionLogEntry e,
        string selfId,
        Dictionary<string, Character> byToken,
        PersonalityValues p,
        string et)
    {
        return et switch
        {
            "income" => Hi(p.Caution) ? "거점에 서자 마자 숨이 고른다." : "복귀한 실감이 난다.",
            "partyform" => Hi(p.Cooperation) ? "역할을 맞추는 데 집중했다." : "파티 짜임이 눈에 들어왔다.",
            "meal" => Hi(p.Frugality) ? "배부름과 지출을 동시에 헤아린다." : "식사로 리듬이 돌아왔다.",
            "talk" when e.Dialogue is { Count: > 0 } => Hi(p.Cooperation)
                ? "나눈 말이 짧게라도 마음에 남는다."
                : "그 자리의 공기가 잔상으로 남는다.",
            "talk" => "동료와 말을 주고받았다.",
            "training" => Hi(p.Focus) ? "몸이 규칙적으로 각인된 느낌." : "땀으로 압을 풀었다.",
            "questaccept" => Hi(p.Courage) ? "새 승부에 마음이 움직인다." : "리스크를 한 번 더 곱씹는다.",
            "adventurerregister" => "길드가 바빠지는 기색을 본다.",
            _ => $"아지트에서 {e.EventType}를 겪었다."
        };
    }

    private static string CombatPerspective(string selfId, string? actor, PersonalityValues p)
    {
        var isActor = !string.IsNullOrEmpty(actor) &&
                      selfId.Equals(actor, StringComparison.OrdinalIgnoreCase);
        if (isActor)
        {
            if (Hi(p.Aggression)) return "전선을 맡아 피가 뜨거워졌다.";
            if (Hi(p.Caution)) return "소모를 최소화하려 애썼다.";
            return "전투의 리듬이 손에 잡혔다.";
        }

        if (Hi(p.Cooperation)) return "앞선 동료에게 시선이 갔다.";
        if (Hi(p.Caution)) return "거리와 호흡을 의식했다.";
        return "전투가 지나가는 동안 숨을 잘 쉬었다.";
    }

    private static string TrapPerspective(string selfId, string? target, PersonalityValues p)
    {
        var hit = !string.IsNullOrEmpty(target) &&
                  selfId.Equals(target, StringComparison.OrdinalIgnoreCase);
        if (hit)
        {
            if (Hi(p.Caution) && Lo(p.Courage, 50)) return "자신의 실수로 팀이 흔들렸다고 느낀다.";
            if (Hi(p.Courage)) return "다음엔 밟지 않으면 된다고 가볍게 넘긴다.";
            if (Hi(p.Cooperation)) return "동료에게 폐를 끼친 것 같아 찜찜하다.";
            return "함정에 몸이 먼저 반응했다.";
        }

        if (Hi(p.Cooperation)) return "누군가 다친 순간 가슴이 쿵 내려앉았다.";
        return "함정 발동을 곁에서 보았다.";
    }

    private static string HealPerspective(
        string selfId,
        string? actor,
        string? target,
        Dictionary<string, Character> byToken,
        PersonalityValues p)
    {
        var isActor = !string.IsNullOrEmpty(actor) &&
                      selfId.Equals(actor, StringComparison.OrdinalIgnoreCase);
        var isTarget = !string.IsNullOrEmpty(target) &&
                       selfId.Equals(target, StringComparison.OrdinalIgnoreCase);

        if (isActor)
            return Hi(p.Cooperation)
                ? "동료의 상태를 바로 잡았다는 안도가 크다."
                : "치유는 할 일의 하나였다.";

        if (isTarget)
            return Hi(p.Cooperation)
                ? "손을 내민 동료에게 고마움이 남는다."
                : "숨이 돌아왔다.";

        return "누군가 회복되는 장면을 보았다.";
    }

    private static string ItemTransferPerspective(string selfId, string? from, string? to, PersonalityValues p)
    {
        var isFrom = !string.IsNullOrEmpty(from) &&
                     selfId.Equals(from, StringComparison.OrdinalIgnoreCase);
        var isTo = !string.IsNullOrEmpty(to) &&
                   selfId.Equals(to, StringComparison.OrdinalIgnoreCase);
        if (isFrom) return Hi(p.Cooperation) ? "부족한 쪽에 맞춰 나눴다." : "분배했다.";
        if (isTo) return Hi(p.Greed) ? "손에 들어온 게 고맙다." : "받아들였다.";
        return "파티 내에서 물건이 움직였다.";
    }

    private static string Norm(string? eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType)) return "";
        return eventType.Trim().Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();
    }

    private static bool Hi(int v, int th = 58) => v >= th;

    private static bool Lo(int v, int th = 42) => v <= th;
}
