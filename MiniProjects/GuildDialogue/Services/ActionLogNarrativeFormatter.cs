using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// ActionLog 타임라인을 원정(던전 런) 단위 **한국어 줄글**로 변환해 시스템 프롬프트에 넣습니다.
/// </summary>
public static class ActionLogNarrativeFormatter
{
    public static string FormatForSystemPrompt(
        IReadOnlyList<ActionLogEntry>? log,
        IReadOnlyList<BaseFacilityData>? bases,
        IReadOnlyDictionary<string, string>? idToDisplayName,
        int maxDungeonRuns = 8,
        int maxTotalChars = 14000)
    {
        if (log == null || log.Count == 0)
            return "(ActionLog 없음)";

        var locNames = bases != null
            ? bases.GroupBy(b => b.BaseId, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string R(string? id)
        {
            if (string.IsNullOrEmpty(id)) return "불명";
            return idToDisplayName != null && idToDisplayName.TryGetValue(id, out var n) ? n : id;
        }

        string LocLabel(string? code)
        {
            if (string.IsNullOrEmpty(code)) return "장소 불명";
            return locNames.TryGetValue(code, out var n) ? $"{n} ({code})" : code;
        }

        var segments = BuildSegments(log);
        segments = KeepLastDungeonRuns(segments, maxDungeonRuns);

        var sb = new StringBuilder();
        sb.AppendLine("※ 아래는 ActionLog를 원정(챕터) 단위로 풀어 쓴 요약입니다. **Order**는 전역 타임라인 순번(클수록 최근). 던전 줄의 **원정 경과 초**는 그 원정 진입 후 상대 시간(TimeOffsetSeconds)입니다. 수치·인명은 아래 줄글을 사실로 따르세요.");
        sb.AppendLine();

        var expeditionIdx = 0;
        foreach (var seg in segments)
        {
            switch (seg)
            {
                case BaseSegment bs:
                    AppendBaseNarrative(sb, bs.Entries, R, LocLabel);
                    break;
                case DungeonSegment ds:
                    expeditionIdx++;
                    AppendDungeonNarrative(sb, expeditionIdx, ds, R, LocLabel);
                    break;
            }
        }

        if (expeditionIdx > 0)
        {
            sb.AppendLine();
            sb.AppendLine("📊 기록 요약: 위 원정은 시간 순서대로 정리되었습니다. 세부 수치가 대화 생성에 필요하면 이 줄글과 [던전 서사 요약]을 함께 참고하세요.");
        }

        var text = sb.ToString().TrimEnd();
        if (text.Length > maxTotalChars)
        {
            var cut = text.Length - maxTotalChars;
            text = "…(앞부분 생략: 설정 ActionLogNarrativeMaxChars 초과)\n\n" + text.Substring(cut);
        }

        return text;
    }

    private abstract record Segment;
    private sealed record BaseSegment(List<ActionLogEntry> Entries) : Segment;
    private sealed record DungeonSegment(
        string DungeonName,
        string? FloorOrZone,
        List<string>? PartyMembers,
        string? Outcome,
        List<ActionLogEntry> Events) : Segment;

    private static List<Segment> BuildSegments(IReadOnlyList<ActionLogEntry> log)
    {
        var ordered = log.OrderBy(e => e.Order).ToList();
        var segments = new List<Segment>();
        var baseBuffer = new List<ActionLogEntry>();

        List<ActionLogEntry>? run = null;
        string? dungeon = null;
        string? floor = null;
        List<string>? party = null;
        string? runOutcome = null;

        void FlushDungeon()
        {
            if (run == null || run.Count == 0 || string.IsNullOrEmpty(dungeon) || party == null || party.Count == 0)
            {
                run = null;
                return;
            }
            segments.Add(new DungeonSegment(dungeon, floor, new List<string>(party), runOutcome, run.ToList()));
            run = null;
            runOutcome = null;
        }

        foreach (var entry in ordered)
        {
            if (string.Equals(entry.Type, "Base", StringComparison.OrdinalIgnoreCase))
            {
                FlushDungeon();
                baseBuffer.Add(entry);
                continue;
            }

            if (!string.Equals(entry.Type, "Dungeon", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(entry.DungeonName))
                continue;

            if (string.Equals(entry.EventType, "outcome", StringComparison.OrdinalIgnoreCase))
            {
                runOutcome = entry.Outcome;
                continue;
            }

            var sameRun = dungeon == entry.DungeonName && floor == entry.FloorOrZone;
            if (!sameRun)
            {
                FlushDungeon();
                if (baseBuffer.Count > 0)
                {
                    segments.Add(new BaseSegment(baseBuffer.ToList()));
                    baseBuffer.Clear();
                }
                dungeon = entry.DungeonName;
                floor = entry.FloorOrZone;
                party = entry.PartyMembers != null ? new List<string>(entry.PartyMembers.Where(s => !string.IsNullOrWhiteSpace(s))) : new List<string>();
                run = new List<ActionLogEntry>();
            }
            else
                run ??= new List<ActionLogEntry>();

            run!.Add(entry);
        }

        FlushDungeon();
        if (baseBuffer.Count > 0)
            segments.Add(new BaseSegment(baseBuffer.ToList()));

        return segments;
    }

    private static List<Segment> KeepLastDungeonRuns(List<Segment> segments, int maxRuns)
    {
        if (maxRuns <= 0 || segments.Count == 0) return segments;
        var idx = segments
            .Select((s, i) => (s, i))
            .Where(x => x.s is DungeonSegment)
            .Select(x => x.i)
            .ToList();
        if (idx.Count <= maxRuns) return segments;
        var firstKeep = idx[idx.Count - maxRuns];
        return segments.Skip(firstKeep).ToList();
    }

    private static void AppendBaseNarrative(
        StringBuilder sb,
        List<ActionLogEntry> entries,
        Func<string?, string> r,
        Func<string?, string> locLabel)
    {
        foreach (var e in entries)
        {
            var anchor = BaseAnchor(e);
            var who = e.PartyMembers is { Count: > 0 }
                ? string.Join(", ", e.PartyMembers.Select(x => r(x)))
                : "불명";
            var loc = locLabel(e.Location);
            var et = Norm(e.EventType);

            var line = et switch
            {
                "income" => $"{anchor}[아지트] {loc}에 {who}(이)가 도착·복귀했습니다.",
                "partyform" => $"[준비] {loc}에서 {who}(이)가 파티를 편성했습니다.",
                "adventurerregister" => $"[등록] {loc}에서 신규 모험가 접수가 있었습니다. ({who})",
                "questaccept" => $"[의뢰] {loc}에서 퀘스트를 수주했습니다. ({who})",
                "training" => $"[훈련] {loc}에서 훈련을 실시했습니다. ({who})",
                "meal" => $"[식사] {loc}에서 식사했습니다. ({who})",
                "talk" when e.Dialogue is { Count: > 0 } =>
                    $"[복귀 후 대화] {loc}: " + string.Join(" ", e.Dialogue.Select(d => $"「{d}」")),
                "talk" => $"[대화] {loc}에서 대화했습니다. ({who})",
                _ => $"[아지트] {loc} — 이벤트 `{e.EventType}` ({who})"
            };
            sb.AppendLine(line);
        }
        sb.AppendLine();
    }

    private static void AppendDungeonNarrative(
        StringBuilder sb,
        int expeditionNumber,
        DungeonSegment ds,
        Func<string?, string> r,
        Func<string?, string> locLabel)
    {
        var party = ds.PartyMembers is { Count: > 0 }
            ? string.Join(", ", ds.PartyMembers)
            : "불명";
        var result = ds.Outcome?.ToLowerInvariant() switch
        {
            "clear" => "클리어 (Clear)",
            "retreat" => "퇴각 (Retreat)",
            "fail" => "실패 (Fail)",
            _ => ds.Outcome ?? "불명"
        };
        sb.AppendLine($"📅 {expeditionNumber}차 원정: {ds.DungeonName}");
        sb.AppendLine($"참여자: {party} | 결과: {result}");
        sb.AppendLine("(이 아래 던전 이벤트의 「원정 경과」는 이 원정 진입 후 경과 초입니다. 전역 순서는 각 줄의 Order를 따르세요.)");
        sb.AppendLine();

        foreach (var e in ds.Events.OrderBy(x => x.TimeOffsetSeconds).ThenBy(x => x.Order))
        {
            var line = FormatDungeonEventLine(e, r, locLabel);
            if (!string.IsNullOrWhiteSpace(line))
                sb.AppendLine(line);
        }
        sb.AppendLine();
    }

    private static string? FormatDungeonEventLine(
        ActionLogEntry e,
        Func<string?, string> r,
        Func<string?, string> locLabel)
    {
        var locRaw = string.IsNullOrEmpty(e.Location) ? "" : locLabel(e.Location);
        var locSeg = string.IsNullOrEmpty(locRaw) ? "알 수 없는 위치" : locRaw;
        var et = Norm(e.EventType);
        var floor = string.IsNullOrWhiteSpace(e.FloorOrZone) ? "?" : $"{e.FloorOrZone}";
        var a = DungeonAnchor(e);

        switch (et)
        {
            case "income":
                return $"{a}[시작] {locSeg}에서 파티가 {e.DungeonName} {floor}층 구역으로 진입했습니다.";

            case "combat":
                var enemies = e.Enemies is { Count: > 0 }
                    ? string.Join(", ", e.Enemies.Select(en => $"{en.Name} {en.Count}마리"))
                    : "적";
                var tail = new List<string>();
                if (e.Turns.HasValue) tail.Add($"{e.Turns.Value}턴 교전");
                if (e.BlockCount.HasValue) tail.Add($"막기 {e.BlockCount.Value}회");
                var extra = tail.Count > 0 ? string.Join(", ", tail) : "";
                return $"{a}[전투] {locSeg}에서 {enemies}과(와) 조우했습니다. {r(e.ActorId)}이(가) 전투를 주도했습니다{(string.IsNullOrEmpty(extra) ? "." : $". ({extra})")}";

            case "trap":
                var trapDetail = e.HpBefore.HasValue && e.HpAfter.HasValue
                    ? $" {r(e.TargetId)}의 HP가 {e.HpBefore}→{e.HpAfter}{(e.Damage.HasValue ? $" (피해 {e.Damage})" : "")}."
                    : $" {r(e.TargetId)}이(가) 피격되었습니다.";
                return $"{a}[사건: 함정] {locSeg}에서 함정 발동.{trapDetail}";

            case "trapavoided":
                return $"{a}[회피] {locSeg}에서 함정 {e.AvoidedCount ?? 1}건을 회피했습니다.";

            case "heal":
                return $"{a}[치유] {r(e.ActorId)}이(가) {r(e.TargetId)}을(를) 치유하여 HP {e.HpBefore}→{e.HpAfter}였습니다.";

            case "loot":
                if (e.LootItems is { Count: > 0 })
                    return $"{a}[획득] {locSeg}에서 " + string.Join(", ", e.LootItems.Select(l => $"{l.ItemName}×{l.Count}")) + "을(를) 획득했습니다.";
                return $"{a}[획득] {locSeg}에서 {e.ItemName ?? "전리품"} ×{e.ItemCount ?? 1}.";

            case "artifact":
                return $"{a}[유물] {locSeg}에서 **'{e.ItemName}'**을(를) 발견·회수했습니다.";

            case "consumepotion":
                var stat = new List<string>();
                if (e.HpBefore.HasValue || e.HpAfter.HasValue) stat.Add($"HP {e.HpBefore}→{e.HpAfter}");
                if (e.MpBefore.HasValue || e.MpAfter.HasValue) stat.Add($"MP {e.MpBefore}→{e.MpAfter}");
                var st = stat.Count > 0 ? $" ({string.Join(", ", stat)})" : "";
                return $"{a}[소모품] {r(e.ActorId)}이(가) {e.ItemName ?? "포션"} ×{e.ItemCount ?? 1} 사용{st}.";

            case "itemtransfer":
                return $"{a}[분배] {r(e.FromId)}이(가) {r(e.ToId)}에게 {e.ItemName} ×{e.ItemCount ?? 1}을(를) 전달했습니다.";

            case "debuffclear":
                return $"{a}[정화] {r(e.ActorId)}이(가) {r(e.TargetId)}의 상태이상 {e.ClearCount ?? 1}건을 해제했습니다.";

            case "partycheck":
                return $"{a}[점검] {locSeg}에서 {r(e.ActorId)}이(가) 파티 상태를 확인했습니다{(string.IsNullOrEmpty(e.CheckType) ? "" : $" ({e.CheckType})")}.";

            case "inventorysort":
                return $"{a}[정비] {locSeg}에서 {r(e.ActorId)}이(가) 인벤토리를 정리했습니다.";

            case "skilluse":
                return $"{a}[스킬] {r(e.ActorId)}이(가) 스킬을 사용했습니다 (MP {e.MpBefore}→{e.MpAfter}).";

            default:
                return $"{a}[기타:{e.EventType}] {locSeg} {(string.IsNullOrEmpty(e.ItemName) ? "" : e.ItemName)}";
        }
    }

    private static string BaseAnchor(ActionLogEntry e) => $"[Order {e.Order}] ";

    private static string DungeonAnchor(ActionLogEntry e) =>
        $"[Order {e.Order}, 원정 경과 {FormatRunElapsedSeconds(e.TimeOffsetSeconds)}] ";

    private static string FormatRunElapsedSeconds(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds)) return "?초";
        if (seconds < 0) seconds = 0;
        if (Math.Abs(seconds - Math.Round(seconds)) < 0.0001)
            return $"{(int)Math.Round(seconds)}초";
        return $"{seconds:0.#}초";
    }

    private static string Norm(string? eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType)) return "";
        return eventType.Trim().Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();
    }
}
