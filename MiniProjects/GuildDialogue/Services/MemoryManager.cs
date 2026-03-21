using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

public class DialogueTurn
{
    public string Speaker { get; set; } = string.Empty;
    public string Line { get; set; } = string.Empty;
}

public class MemoryManager
{
    // 1. Working Memory: 최근 N턴 대화 유지
    private readonly List<DialogueTurn> _workingMemory = new();
    private readonly int _maxWorkingMemoryTurns = 8;
    
    // 2. Episodic Buffer: 단기 기억 (최근 던전/아지트 롤링 요약)
    public string EpisodicBuffer { get; private set; } = string.Empty;
    
    // 3. Archival Memory (Lorebook 데이터)
    private readonly LogGlossaryRoot? _glossary;
    
    // 4. Random Seeding Source
    private readonly List<string> _allPastEvents = new();
    
    public MemoryManager(LogGlossaryRoot? glossary)
    {
        _glossary = glossary;
    }

    public void AddWorkingMemory(string speaker, string line)
    {
        _workingMemory.Add(new DialogueTurn { Speaker = speaker, Line = line });
        if (_workingMemory.Count > _maxWorkingMemoryTurns)
            _workingMemory.RemoveAt(0);
    }

    public string GetWorkingMemoryContext()
    {
        if (_workingMemory.Count == 0) return "(이전 대화 없음)";
        var sb = new StringBuilder();
        foreach (var t in _workingMemory)
            sb.AppendLine($"{t.Speaker}: \"{t.Line}\"");
        return sb.ToString().TrimEnd();
    }

    public void ClearWorkingMemory() => _workingMemory.Clear();

    /// <summary>ActionLog 저장용: 현재까지의 워킹 메모리 대화 턴(순서 유지).</summary>
    public IReadOnlyList<DialogueTurn> GetWorkingMemoryTurns() => _workingMemory.ToList();

    /// <summary>던전 런 + 아지트 타임라인을 자연어 에피소드 버퍼로 변환. ActionLog.json 이벤트 코드와 정렬됨.</summary>
    public void BuildEpisodicBuffer(
        List<DungeonLog> logs,
        IReadOnlyList<BaseLogEntry>? baseLogs = null,
        EpisodicNarrativeContext? ctx = null)
    {
        _allPastEvents.Clear();
        var sb = new StringBuilder();

        if (baseLogs != null && baseLogs.Count > 0)
        {
            sb.AppendLine("[아지트 기록 (복귀·편성·식사 등)]");
            foreach (var b in baseLogs)
            {
                var who = b.PartyMembers.Count > 0 ? string.Join(", ", b.PartyMembers) : "(참가자 불명)";
                var line = $"  • [{b.EventType}] {b.Location}: 참여 {who}";
                sb.AppendLine(line);
                if (b.ScriptedDialogue?.Count > 0)
                {
                    foreach (var d in b.ScriptedDialogue)
                        sb.AppendLine($"      「{d}」");
                }
                _allPastEvents.Add($"아지트 {b.Location}, 사건: {line.Trim()}");
            }
            sb.AppendLine();
        }

        foreach (var log in logs)
        {
            sb.AppendLine($"[던전 기록] {log.DungeonName} 층/구역: {log.FloorOrZone} / 런 결과: {log.Outcome ?? "불명"}");
            if (log.PartyMembers?.Count > 0)
                sb.AppendLine($"  파티: {string.Join(", ", log.PartyMembers)}");

            var events = log.Events.Where(e => !IsTimelineNoiseEvent(e.EventType)).ToList();
            if (events.Count == 0) continue;

            foreach (var e in events)
            {
                string narrative = AppendScriptedLines(BuildDungeonEventNarrative(e, log, ctx), e.ScriptedDialogue);
                sb.AppendLine(narrative);
                _allPastEvents.Add($"지역: {log.DungeonName} {log.FloorOrZone}, 사건: {narrative.Trim()}");
            }
        }

        EpisodicBuffer = sb.ToString();
    }

    private static bool IsTimelineNoiseEvent(string? eventType)
    {
        var n = NormalizeEventCode(eventType);
        return n is "income" or "outcome";
    }

    private static string NormalizeEventCode(string? eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType)) return "";
        var s = eventType.Trim();
        return s.Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();
    }

    private string BuildDungeonEventNarrative(DungeonEvent e, DungeonLog log, EpisodicNarrativeContext? ctx)
    {
        string R(string? id) => ResolveDisplayName(id, ctx);
        var norm = NormalizeEventCode(e.EventType);

        string MonsterHint(EnemyEntry en)
        {
            if (ctx?.MonstersByLocalizedName == null) return $"{en.Name} {en.Count}마리";
            if (!ctx.MonstersByLocalizedName.TryGetValue(en.Name, out var m)) return $"{en.Name} {en.Count}마리";
            var traits = m.Traits.Count > 0 ? string.Join("/", m.Traits.Take(2)) : m.Type;
            return $"{en.Name} {en.Count}마리 [{m.DangerLevel}, {traits}]";
        }

        TrapTypeData? MatchTrap()
        {
            if (ctx?.TrapTypes == null) return null;
            if (!string.IsNullOrEmpty(e.TrapId))
            {
                foreach (var t in ctx.TrapTypes)
                {
                    if (string.Equals(t.TrapId, e.TrapId, StringComparison.OrdinalIgnoreCase))
                        return t;
                }
            }
            if (string.IsNullOrEmpty(e.Location)) return null;
            var compactLoc = e.Location.Replace(" ", "", StringComparison.Ordinal);
            foreach (var t in ctx.TrapTypes)
            {
                if (e.Location.Contains(t.TrapName, StringComparison.Ordinal)) return t;
                var tn = t.TrapName.Replace(" ", "", StringComparison.Ordinal).Replace("함정", "", StringComparison.Ordinal);
                if (tn.Length >= 2 && compactLoc.Contains(tn, StringComparison.Ordinal)) return t;
            }
            return null;
        }

        string narrative = norm switch
        {
            "combat" =>
                e.Enemies?.Any() == true
                    ? $"  - 전투: {R(e.ActorId)}이(가) {string.Join(", ", e.Enemies.Select(MonsterHint))}과 교전"
                      + CombatSuffix(e)
                    : $"  - 전투 발생.{CombatSuffix(e)}",
            "trap" or "traptriggered" =>
                $"  - 함정 피해: {R(e.TargetId)} @ {e.Location ?? "장소 불명"}{(e.Damage.HasValue ? $", 피해 {e.Damage}" : "")}"
                + TrapSuffix(MatchTrap()),
            "trapavoided" =>
                $"  - 함정 회피: {e.AvoidedCount ?? 1}건 ({e.Location ?? log.DungeonName})",
            "loot" =>
                e.LootItems?.Count > 0
                    ? $"  - 루팅: {string.Join(", ", e.LootItems.Select(l => $"{l.ItemName}×{l.Count}"))}"
                    : $"  - 루팅: {e.ItemName ?? "아이템"} ×{e.ItemCount ?? 1}",
            "itemtransfer" =>
                $"  - 아이템 전달: {R(e.FromId)} → {R(e.ToId)}, {e.ItemName ?? "?"} ×{e.ItemCount ?? 1}",
            "heal" =>
                $"  - 치유: {R(e.ActorId)}가 {R(e.TargetId)} 회복 (HP {e.HpBefore}→{e.HpAfter})",
            "consumepotion" => ConsumePotionLine(e, R),
            "artifact" =>
                $"  - 유물/특수 획득: {e.ItemName ?? "아이템"} @ {e.Location ?? "?"}",
            "partycheck" =>
                $"  - 파티 점검: {R(e.ActorId)}{(string.IsNullOrEmpty(e.CheckType) ? "" : $" ({e.CheckType})")} @ {e.Location ?? ""}",
            "debuffclear" =>
                $"  - 디버프 해제: {R(e.ActorId)} → {R(e.TargetId)}, {e.ClearCount ?? 1}개",
            "inventorysort" =>
                $"  - 인벤토리 정리: {R(e.ActorId)} @ {e.Location ?? ""}",
            "skilluse" =>
                $"  - 스킬 사용: {R(e.ActorId)} (MP {e.MpBefore}→{e.MpAfter})",
            "rest" => "  - 휴식",
            "levelup" => $"  - 레벨 업: {R(e.ActorId)}",
            _ => $"  - [{e.EventType}] @ {e.Location ?? log.DungeonName}"
        };

        return narrative;
    }

    private static string ConsumePotionLine(DungeonEvent e, Func<string?, string> R)
    {
        var core = $"  - 소모품 사용: {R(e.ActorId)}, {e.ItemName ?? "포션"} ×{e.ItemCount ?? 1}";
        var stat = new List<string>();
        if (e.HpBefore.HasValue || e.HpAfter.HasValue)
            stat.Add($"HP {e.HpBefore}→{e.HpAfter}");
        if (e.MpBefore.HasValue || e.MpAfter.HasValue)
            stat.Add($"MP {e.MpBefore}→{e.MpAfter}");
        return stat.Count > 0 ? $"{core} ({string.Join(", ", stat)})" : core;
    }

    private string ResolveDisplayName(string? id, EpisodicNarrativeContext? ctx)
    {
        if (string.IsNullOrEmpty(id)) return "불명";
        if (ctx?.CharacterIdToDisplayName != null &&
            ctx.CharacterIdToDisplayName.TryGetValue(id, out var name))
            return name;
        return id;
    }

    private static string CombatSuffix(DungeonEvent e)
    {
        var parts = new List<string>();
        if (e.Turns.HasValue) parts.Add($"{e.Turns.Value}턴");
        if (e.BlockCount.HasValue) parts.Add($"막기 {e.BlockCount.Value}회");
        if (e.HpBefore.HasValue && e.HpAfter.HasValue)
            parts.Add($"HP {e.HpBefore}→{e.HpAfter}");
        return parts.Count == 0 ? "." : ". " + string.Join(", ", parts) + ".";
    }

    private static string TrapSuffix(TrapTypeData? t)
    {
        if (t == null) return "";
        return $" — ({t.TrapName}: {t.Effect})";
    }

    private static string AppendScriptedLines(string narrative, IReadOnlyList<string>? lines)
    {
        if (lines == null || lines.Count == 0) return narrative;
        var sb = new StringBuilder(narrative);
        foreach (var d in lines)
            sb.AppendLine($"    대사: {d}");
        return sb.ToString().TrimEnd();
    }

    public string? GetRandomPastEvent()
    {
        if (_allPastEvents.Count == 0) return null;
        var rnd = new Random();
        return _allPastEvents[rnd.Next(_allPastEvents.Count)];
    }

    // 대화나 에피소드 버퍼 내의 키워드를 스캔하여 Lorebook 조건부 인출 (RAG)
    public string RetrieveArchivalMemoryContext(string currentContextStr)
    {
        if (_glossary == null) return string.Empty;
        
        var sb = new StringBuilder();
        var allTextToScan = currentContextStr + " " + EpisodicBuffer;
        
        void CheckAndAppend(string category, List<GlossaryEntry>? entries) {
            if (entries == null) return;
            foreach (var e in entries) {
                if (allTextToScan.Contains(e.Code, StringComparison.OrdinalIgnoreCase)) {
                    sb.AppendLine($"• [{category}] {e.Code}: {e.Meaning}{(string.IsNullOrEmpty(e.Note) ? "" : $" ({e.Note})")}");
                }
            }
        }
        
        CheckAndAppend("DungeonEvent", _glossary.DungeonEventTypes);
        CheckAndAppend("FieldNote", _glossary.FieldNotes);
        CheckAndAppend("TimeConcept", _glossary.TimeConcepts);
        
        return sb.ToString().TrimEnd();
    }
}
