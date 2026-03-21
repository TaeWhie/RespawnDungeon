using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// 원정 시뮬 등으로 생성된 타임라인을 기존 ActionLog.json에 안전하게 이어 붙이거나 교체할 때 사용.
/// </summary>
public static class ActionLogPersistence
{
    private static JsonSerializerOptions CloneOptions() => new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    /// <summary>
    /// 기존 로그 뒤에 새 원정 항목을 붙이고 <see cref="ActionLogEntry.Order"/>를 전역으로 다시 매깁니다.
    /// </summary>
    public static TestDataRoot AppendToExisting(TestDataRoot? existing, TestDataRoot newRun)
    {
        var opts = CloneOptions();
        if (newRun?.ActionLog == null || newRun.ActionLog.Count == 0)
        {
            if (existing?.ActionLog == null || existing.ActionLog.Count == 0)
                return new TestDataRoot();
            return JsonSerializer.Deserialize<TestDataRoot>(
                JsonSerializer.Serialize(existing, opts), opts) ?? new TestDataRoot();
        }

        if (existing?.ActionLog == null || existing.ActionLog.Count == 0)
        {
            return JsonSerializer.Deserialize<TestDataRoot>(
                JsonSerializer.Serialize(newRun, opts), opts) ?? new TestDataRoot();
        }

        var mergedJson = JsonSerializer.Serialize(existing.ActionLog, opts);
        var merged = JsonSerializer.Deserialize<List<ActionLogEntry>>(mergedJson, opts) ?? new List<ActionLogEntry>();

        var maxOrder = merged.Count > 0 ? merged.Max(e => e.Order) : 0;
        var next = maxOrder;

        foreach (var e in newRun.ActionLog.OrderBy(x => x.Order))
        {
            var copyJson = JsonSerializer.Serialize(e, opts);
            var copy = JsonSerializer.Deserialize<ActionLogEntry>(copyJson, opts);
            if (copy == null) continue;
            copy.Order = ++next;
            merged.Add(copy);
        }

        return new TestDataRoot { ActionLog = merged };
    }

    /// <summary>
    /// 길드 대화 세션을 <see cref="ActionLogEntry.Type"/> = Base, <see cref="ActionLogEntry.EventType"/> = talk 로 한 건 추가합니다.
    /// </summary>
    public static TestDataRoot AppendGuildDialogueSession(
        TestDataRoot? existing,
        string baseLocationId,
        IReadOnlyList<DialogueTurn> turns,
        string? sessionLabel = null)
    {
        var opts = CloneOptions();

        var contentTurns = (turns ?? Array.Empty<DialogueTurn>()).Where(t => !string.IsNullOrWhiteSpace(t.Line)).ToList();
        if (contentTurns.Count == 0)
        {
            if (existing?.ActionLog == null || existing.ActionLog.Count == 0)
                return new TestDataRoot();
            return JsonSerializer.Deserialize<TestDataRoot>(
                JsonSerializer.Serialize(existing, opts), opts) ?? new TestDataRoot();
        }

        List<ActionLogEntry> merged;
        var nextOrder = 1;

        if (existing?.ActionLog != null && existing.ActionLog.Count > 0)
        {
            var mergedJson = JsonSerializer.Serialize(existing.ActionLog, opts);
            merged = JsonSerializer.Deserialize<List<ActionLogEntry>>(mergedJson, opts) ?? new List<ActionLogEntry>();
            nextOrder = merged.Max(e => e.Order) + 1;
        }
        else
            merged = new List<ActionLogEntry>();

        var dialogueLines = new List<string>();
        if (!string.IsNullOrWhiteSpace(sessionLabel))
            dialogueLines.Add($"[{sessionLabel}]");

        foreach (var t in contentTurns)
            dialogueLines.Add($"{t.Speaker}: {t.Line}");

        var party = contentTurns
            .Select(t => t.Speaker)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var entry = new ActionLogEntry
        {
            Order = nextOrder,
            Type = "Base",
            EventType = "talk",
            Location = string.IsNullOrWhiteSpace(baseLocationId) ? "main_hall" : baseLocationId.Trim(),
            TimeOffsetSeconds = 0,
            PartyMembers = party,
            Dialogue = dialogueLines
        };

        merged.Add(entry);
        return new TestDataRoot { ActionLog = merged };
    }
}
