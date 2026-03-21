using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>ActionLog 최신 N건을 시스템 프롬프트용으로 압축 직렬화.</summary>
public static class LatestActionLogFormatter
{
    public static string FormatTailForSystemPrompt(IReadOnlyList<ActionLogEntry>? log, int maxEntries = 28)
    {
        if (log == null || log.Count == 0)
            return "(ActionLog 없음)";

        var ordered = log.OrderBy(e => e.Order).ToList();
        var tail = ordered.Count <= maxEntries ? ordered : ordered.Skip(Math.Max(0, ordered.Count - maxEntries)).ToList();

        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var sb = new StringBuilder();
        sb.AppendLine($"총 {ordered.Count}건 중 최신 {tail.Count}건(JSON 줄, Order 오름차순). 해석: Order↑=시간상 나중; 던전 행의 TimeOffsetSeconds=해당 원정 진입 후 경과(초).");
        foreach (var e in tail)
        {
            var slim = new
            {
                e.Order,
                e.TimeOffsetSeconds,
                e.Type,
                e.EventType,
                e.Outcome,
                e.Location,
                e.DungeonName,
                e.FloorOrZone,
                e.PartyMembers,
                e.ActorId,
                e.TargetId,
                e.HpBefore,
                e.HpAfter,
                e.MpBefore,
                e.MpAfter,
                e.Enemies,
                e.ItemName,
                e.ItemCount,
                e.LootItems,
                e.Dialogue
            };
            sb.AppendLine(JsonSerializer.Serialize(slim, options));
        }

        return sb.ToString().TrimEnd();
    }
}
