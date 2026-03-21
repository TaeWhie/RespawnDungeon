using System;
using System.Collections.Generic;
using System.Linq;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// 원정 시뮬 한 번의 <see cref="ActionLogEntry"/>만으로 <see cref="Character.RecentMemorableEvent"/>에 넣을
/// 한국어 한 줄 요약을 규칙 기반으로 생성합니다. (LLM 없음)
/// </summary>
public static class ExpeditionRecentMemorableSummarizer
{
    private const int MaxLineLength = 240;

    /// <summary>
    /// 이번 원정 타임라인만 넘깁니다. Type=Base 행은 요약에 쓰지 않고 Dungeon 행만 사용합니다.
    /// </summary>
    public static string SummarizeLineForCharacter(string characterId, string displayName, IReadOnlyList<ActionLogEntry>? runLog)
    {
        if (string.IsNullOrWhiteSpace(characterId) || runLog == null || runLog.Count == 0)
            return "";

        var name = (displayName ?? "").Trim();
        var dungeon = runLog
            .Where(e => string.Equals(e.Type, "Dungeon", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Order)
            .ToList();

        if (dungeon.Count == 0)
            return "";

        var dName = dungeon.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.DungeonName))?.DungeonName?.Trim() ?? "던전";
        var floor = dungeon.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.FloorOrZone))?.FloorOrZone?.Trim() ?? "";
        var floorBit = string.IsNullOrEmpty(floor) ? "" : $"({floor})";

        var outcomeEntry = dungeon.LastOrDefault(e =>
            string.Equals(e.EventType, "outcome", StringComparison.OrdinalIgnoreCase));
        var oc = outcomeEntry?.Outcome?.Trim().ToLowerInvariant() ?? "clear";
        var ocKo = oc switch
        {
            "clear" => "클리어",
            "retreat" => "철수",
            "fail" => "실패",
            _ => oc
        };

        var head = $"{dName}{floorBit} 원정, 결과 {ocKo}.";

        var tail = PickBestTail(dungeon, characterId, name);
        if (string.IsNullOrEmpty(tail))
            tail = "파티와 함께 원정을 마쳤다.";

        var line = $"{head} {tail}";
        return Truncate(line, MaxLineLength);
    }

    private static string PickBestTail(IReadOnlyList<ActionLogEntry> dungeon, string id, string name)
    {
        string? best = null;
        var bestOrder = int.MinValue;

        foreach (var e in dungeon)
        {
            if (string.Equals(e.EventType, "outcome", StringComparison.OrdinalIgnoreCase))
                continue;

            var t = TailFromEntry(e, id, name);
            if (string.IsNullOrEmpty(t)) continue;
            if (e.Order >= bestOrder)
            {
                bestOrder = e.Order;
                best = t;
            }
        }

        return best ?? "";
    }

    private static bool InParty(ActionLogEntry e, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || e.PartyMembers == null) return false;
        return e.PartyMembers.Any(p => p.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static string? TailFromEntry(ActionLogEntry e, string id, string name)
    {
        var et = e.EventType?.Trim().ToLowerInvariant() ?? "";
        var party = InParty(e, name);

        switch (et)
        {
            case "combat":
                if (string.Equals(e.ActorId, id, StringComparison.OrdinalIgnoreCase))
                {
                    var enemy = e.Enemies?.FirstOrDefault()?.Name ?? "적";
                    var loc = string.IsNullOrWhiteSpace(e.Location) ? "전장" : e.Location!;
                    return $"{loc}에서 {enemy}와 맞섰다.";
                }
                if (party)
                {
                    var loc = string.IsNullOrWhiteSpace(e.Location) ? "전장" : e.Location!;
                    return $"{loc}에서 교전을 지원했다.";
                }
                return null;

            case "trap":
                if (string.Equals(e.TargetId, id, StringComparison.OrdinalIgnoreCase))
                {
                    var loc = string.IsNullOrWhiteSpace(e.Location) ? "함정" : e.Location!;
                    return $"{loc}에서 부상을 입었다.";
                }
                return null;

            case "trapavoided":
                if (string.Equals(e.ActorId, id, StringComparison.OrdinalIgnoreCase))
                {
                    var loc = string.IsNullOrWhiteSpace(e.Location) ? "함정" : e.Location!;
                    return $"{loc}에서 함정을 피했다.";
                }
                return null;

            case "heal":
                if (string.Equals(e.TargetId, id, StringComparison.OrdinalIgnoreCase))
                    return "동료에게 치유를 받았다.";
                if (string.Equals(e.ActorId, id, StringComparison.OrdinalIgnoreCase))
                    return "동료에게 치유를 베풀었다.";
                return null;

            case "consumepotion":
                if (string.Equals(e.ActorId, id, StringComparison.OrdinalIgnoreCase))
                {
                    var item = string.IsNullOrWhiteSpace(e.ItemName) ? "포션" : e.ItemName!;
                    return $"{item}을(를) 사용했다.";
                }
                return null;

            case "loot":
                return party ? "전리품을 정리했다." : null;

            case "artifact":
                if (!party) return null;
                if (!string.IsNullOrWhiteSpace(e.ItemName))
                    return $"유물 {e.ItemName}을(를) 손에 넣었다.";
                return "유물을 손에 넣었다.";

            case "skilluse":
                if (string.Equals(e.ActorId, id, StringComparison.OrdinalIgnoreCase))
                    return "스킬을 써서 전투를 도왔다.";
                return null;

            case "itemtransfer":
                if (string.Equals(e.FromId, id, StringComparison.OrdinalIgnoreCase))
                {
                    var item = string.IsNullOrWhiteSpace(e.ItemName) ? "보급" : e.ItemName!;
                    return $"{item}을(를) 동료에게 넘겼다.";
                }
                if (string.Equals(e.ToId, id, StringComparison.OrdinalIgnoreCase))
                {
                    var item = string.IsNullOrWhiteSpace(e.ItemName) ? "보급" : e.ItemName!;
                    return $"동료에게 {item}을(를) 받았다.";
                }
                return null;

            case "debuffclear":
                if (string.Equals(e.ActorId, id, StringComparison.OrdinalIgnoreCase))
                    return "동료의 상태를 정화했다.";
                if (string.Equals(e.TargetId, id, StringComparison.OrdinalIgnoreCase))
                    return "동료에게 정화를 받았다.";
                return null;

            case "inventorysort":
                if (string.Equals(e.ActorId, id, StringComparison.OrdinalIgnoreCase))
                    return "짐을 정리했다.";
                return null;

            case "partycheck":
                if (string.Equals(e.ActorId, id, StringComparison.OrdinalIgnoreCase))
                    return "파티 상태를 점검했다.";
                return null;

            case "income":
                return party ? "던전에 들어섰다." : null;

            default:
                return null;
        }
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        return s.Substring(0, max - 1) + "…";
    }
}
