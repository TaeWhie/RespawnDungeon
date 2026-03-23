using System;
using System.Collections.Generic;
using System.Linq;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// 원정 UI: 던전별 최대 정복 층(ActionLog의 outcome=clear)을 읽어 다음에 선택 가능한 층 범위를 계산합니다.
/// </summary>
public static class ExpeditionDungeonProgress
{
    public static DungeonData? FindDungeon(WorldLore? lore, string? query)
    {
        if (lore?.Dungeons == null || lore.Dungeons.Count == 0 || string.IsNullOrWhiteSpace(query))
            return null;
        var q = query.Trim();
        foreach (var d in lore.Dungeons)
        {
            if (d.Name != null && string.Equals(d.Name.Trim(), q, StringComparison.OrdinalIgnoreCase))
                return d;
        }

        foreach (var d in lore.Dungeons)
        {
            if (d.Name != null && d.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
                return d;
        }

        return null;
    }

    /// <summary>난이도별 최대 층(심연은 B1/B2 두 단계).</summary>
    public static int MaxFloorCap(DungeonData d)
    {
        var diff = d.Difficulty?.Trim().ToLowerInvariant() ?? "";
        if (diff.Contains("abyss", StringComparison.OrdinalIgnoreCase) || diff.Contains("심연"))
            return 2;
        if (diff.Contains("high") || diff.Contains("상"))
            return 10;
        if (diff.Contains("mid") || diff.Contains("중"))
            return 6;
        return 4;
    }

    public static bool IsAbyssStyle(DungeonData d)
    {
        var diff = d.Difficulty?.Trim().ToLowerInvariant() ?? "";
        return diff.Contains("abyss", StringComparison.OrdinalIgnoreCase) || diff.Contains("심연");
    }

    /// <summary>시뮬 로그·UI에 쓰는 층 문자열.</summary>
    public static string FloorOrdinalToLabel(DungeonData d, int ordinal)
    {
        var o = Math.Max(1, ordinal);
        if (IsAbyssStyle(d))
            return o >= 2 ? "B2" : "B1";
        return o.ToString();
    }

    public static bool TryParseFloorOrdinal(string? floorLabel, DungeonData d, out int ordinal)
    {
        ordinal = 0;
        if (string.IsNullOrWhiteSpace(floorLabel)) return false;
        if (IsAbyssStyle(d))
        {
            var s = floorLabel.Trim().ToUpperInvariant();
            if (s.Length >= 2 && s[0] == 'B' && int.TryParse(s.AsSpan(1), out var b) && b >= 1)
            {
                ordinal = b;
                return true;
            }

            return false;
        }

        return int.TryParse(floorLabel.Trim(), out ordinal) && ordinal >= 1;
    }

    public static bool DungeonNameMatchesLog(string? logDungeonName, string dungeonCanonName)
    {
        if (string.IsNullOrWhiteSpace(logDungeonName) || string.IsNullOrWhiteSpace(dungeonCanonName))
            return false;
        var a = logDungeonName.Trim();
        var b = dungeonCanonName.Trim();
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;
        return a.Contains(b, StringComparison.OrdinalIgnoreCase) || b.Contains(a, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>해당 던전에서 클리어로 기록된 최대 층(서수).</summary>
    public static int GetMaxClearedOrdinal(IReadOnlyList<ActionLogEntry> log, DungeonData d)
    {
        var max = 0;
        foreach (var e in log)
        {
            if (!string.Equals(e.Type, "Dungeon", StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(e.EventType, "outcome", StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(e.Outcome?.Trim(), "clear", StringComparison.OrdinalIgnoreCase)) continue;
            if (!DungeonNameMatchesLog(e.DungeonName, d.Name)) continue;
            if (!TryParseFloorOrdinal(e.FloorOrZone, d, out var fo)) continue;
            if (fo > max) max = fo;
        }

        return max;
    }

    /// <summary>다음 원정에서 선택 가능한 최상층 서수 (최소 1, 최대 cap).</summary>
    public static int GetMaxSelectableOrdinal(IReadOnlyList<ActionLogEntry> log, DungeonData d)
    {
        var cleared = GetMaxClearedOrdinal(log, d);
        var cap = MaxFloorCap(d);
        return Math.Min(Math.Max(cleared + 1, 1), cap);
    }
}
