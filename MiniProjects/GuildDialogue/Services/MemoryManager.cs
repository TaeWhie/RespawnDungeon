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

    // 입력된 런들을 "서사화된 기억"으로 롤링 요약.
    public void BuildEpisodicBuffer(List<DungeonLog> logs)
    {
        var sb = new StringBuilder();
        foreach(var log in logs)
        {
            sb.AppendLine($"[던전 기록] {log.DungeonName} {log.FloorOrZone} / 결과: {log.Outcome ?? "불명"}");
            var events = log.Events.Where(e => e.EventType != "income").ToList();
            if (events.Count == 0) continue;
            
            foreach(var e in events) 
            {
                // 각 이벤트를 서사형 한 문장으로 변환
                string narrative = e.EventType switch {
                    "combat" => 
                        e.Enemies?.Any() == true
                        ? $"  - {e.ActorId ?? "파티"}(이)가 {string.Join(", ", e.Enemies.Select(en => $"{en.Name} {en.Count}마리"))}과 전투했다. (HP {e.HpBefore} -> {e.HpAfter})"
                        : $"  - 전투 발생. (HP {e.HpBefore} -> {e.HpAfter})",
                    "trap_triggered" =>
                        $"  - {e.ActorId ?? "누군가"}(이)가 {e.Location ?? "해당 구역"}에서 함정을 밝았다. {(e.Damage.HasValue ? $"({e.Damage}데미지)" : "")}",
                    "trap_avoided" =>
                        $"  - 함정 {e.AvoidedCount ?? 1}개를 미리 회피했다.",
                    "loot" =>
                        $"  - 루팅: {e.ItemName ?? "아이템"} x{e.ItemCount ?? 1} 획득.",
                    "item_transfer" =>
                        $"  - {e.FromId}(이)가 {e.ToId}에게 {e.ItemName} {e.ItemCount}개를 건네줌.",
                    "skill_use" =>
                        $"  - {e.ActorId ?? "???"}이(가) 스킬을 사용했다. (MP {e.MpBefore} -> {e.MpAfter})",
                    "heal" =>
                        $"  - 회복: HP {e.HpBefore} -> {e.HpAfter}.",
                    "debuff_clear" =>
                        $"  - 디버프 {e.ClearCount ?? 1}개 해제되었다.",
                    "rest" => "  - 파티가 잠시 휴식했다.",
                    "level_up" => $"  - {e.ActorId ?? "???"}이(가) 레벨업되었다.",
                    _ => $"  - [{e.EventType}] 이벤트 발생."
                };
                sb.AppendLine(narrative);
                _allPastEvents.Add($"지역: {log.DungeonName} {log.FloorOrZone}, 사건: {narrative.Trim()}");
            }
        }
        EpisodicBuffer = sb.ToString();
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
