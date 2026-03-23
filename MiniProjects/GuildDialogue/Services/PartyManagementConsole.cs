using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// 콘솔: 파티 편성(<see cref="RunPartyRosterMenuAsync"/>)과 원정(<see cref="RunExpeditionAsync"/>)을 각각 분리.
/// </summary>
public static class PartyManagementConsole
{
    public static bool IsValidPartyId(string s) =>
        !string.IsNullOrWhiteSpace(s) &&
        s.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c is '_' or '-');

    public static string SuggestPartyId() => $"party_{Guid.NewGuid().ToString("N")[..8]}";

    /// <summary>파티 목록·생성·수정·삭제만 (원정은 별도 메뉴).</summary>
    public static Task RunPartyRosterMenuAsync(DialogueConfigLoader loader)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("========== 파티 편성 ==========");
            Console.WriteLine($"Config: {loader.ConfigDirectory}");
            Console.WriteLine("1. 파티 목록 보기");
            Console.WriteLine("2. 새 파티 만들기 (표시 이름·호칭·멤버 지정 → DB 저장)");
            Console.WriteLine("3. 파티 수정 (이름·호칭·설명·멤버)");
            Console.WriteLine("4. 파티 삭제");
            Console.WriteLine("0. 상위 메뉴로");
            Console.Write("> ");

            var choice = Console.ReadLine()?.Trim();
            switch (choice)
            {
                case "1":
                    PrintPartyAndRoster(loader);
                    break;
                case "2":
                    CreatePartyInteractive(loader);
                    break;
                case "3":
                    EditPartyInteractive(loader);
                    break;
                case "4":
                    DeletePartyInteractive(loader);
                    break;
                case "0":
                case "":
                    return Task.CompletedTask;
                default:
                    Console.WriteLine("잘못된 선택입니다.");
                    break;
            }
        }
    }

    private static void PrintPartyAndRoster(DialogueConfigLoader loader)
    {
        var parties = loader.LoadPartyDatabase();
        var chars = loader.LoadCharactersWithJobSkillFilter()
            .Where(c => !c.Id.Equals("master", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Console.WriteLine("--- 파티 ---");
        if (parties.Count == 0)
            Console.WriteLine("(등록된 파티 없음)");
        foreach (var p in parties)
        {
            var members = string.Join(", ", p.MemberIds.Select(id =>
            {
                var c = chars.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                return c != null ? $"{c.Name}({id})" : id;
            }));
            Console.WriteLine($"• [{p.PartyId}] {p.Name} | 호칭: {p.Callsign ?? "-"} | 멤버: {members}");
            if (!string.IsNullOrWhiteSpace(p.Description))
                Console.WriteLine($"  └ {p.Description}");
        }

        Console.WriteLine("--- 캐릭터(소속 PartyId) ---");
        foreach (var c in chars.OrderBy(c => c.Id, StringComparer.OrdinalIgnoreCase))
            Console.WriteLine($"  {c.Name} ({c.Id}) → PartyId: {c.PartyId ?? "(없음)"}");
    }

    private static void CreatePartyInteractive(DialogueConfigLoader loader)
    {
        Console.WriteLine("표시 이름(예: 붉은 창병대): ");
        var name = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            Console.WriteLine("이름은 필수입니다.");
            return;
        }

        Console.WriteLine($"PartyId (영문·숫자·_-, 엔터 시 자동: {SuggestPartyId()}): ");
        var pidInput = Console.ReadLine()?.Trim();
        var partyId = string.IsNullOrWhiteSpace(pidInput) ? SuggestPartyId() : pidInput;
        if (!IsValidPartyId(partyId))
        {
            Console.WriteLine("PartyId는 영문·숫자·언더스코어·하이픈만 사용하세요.");
            return;
        }

        var parties = loader.LoadPartyDatabase();
        if (parties.Any(p => p.PartyId.Equals(partyId, StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine("이미 같은 PartyId가 있습니다.");
            return;
        }

        Console.WriteLine("호칭/무전명 (엔터 생략): ");
        var callsign = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(callsign)) callsign = null;

        Console.WriteLine("설명 한 줄 (엔터 생략): ");
        var desc = Console.ReadLine()?.Trim() ?? "";

        var chars = loader.LoadCharactersWithJobSkillFilter();
        var memberIds = PickMemberIds(chars);
        if (memberIds.Count == 0)
        {
            Console.WriteLine("멤버가 없어 파티를 만들지 않습니다.");
            return;
        }

        var party = new PartyData
        {
            PartyId = partyId,
            Name = name,
            Callsign = callsign,
            Description = desc,
            MemberIds = memberIds,
            Notes = "콘솔에서 편성됨."
        };

        parties.Add(party);
        ApplyPartyMembership(parties, chars, party, memberIds);
        loader.SavePartyDatabase(parties);
        loader.SaveCharactersDatabase(chars);
        Console.WriteLine($"저장 완료: [{partyId}] {name}, 멤버 {memberIds.Count}명.");
    }

    private static void EditPartyInteractive(DialogueConfigLoader loader)
    {
        var parties = loader.LoadPartyDatabase();
        var party = SelectParty(parties);
        if (party == null) return;

        Console.WriteLine($"표시 이름 (현재: {party.Name}, 엔터 유지): ");
        var name = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(name)) party.Name = name;

        Console.WriteLine($"호칭 (현재: {party.Callsign ?? "-"}, 엔터 유지): ");
        var cs = Console.ReadLine()?.Trim();
        if (cs != null)
            party.Callsign = string.IsNullOrWhiteSpace(cs) ? null : cs;

        Console.WriteLine($"설명 (현재: {party.Description}, 엔터 유지): ");
        var desc = Console.ReadLine()?.Trim();
        if (desc != null && desc.Length > 0) party.Description = desc;

        Console.WriteLine("멤버를 다시 고릅니다.");
        var chars = loader.LoadCharactersWithJobSkillFilter();
        var memberIds = PickMemberIds(chars);
        if (memberIds.Count == 0)
        {
            Console.WriteLine("멤버가 비어 있으면 저장하지 않습니다.");
            return;
        }

        ApplyPartyMembership(parties, chars, party, memberIds);
        loader.SavePartyDatabase(parties);
        loader.SaveCharactersDatabase(chars);
        Console.WriteLine("수정 저장 완료.");
    }

    private static void DeletePartyInteractive(DialogueConfigLoader loader)
    {
        var parties = loader.LoadPartyDatabase();
        var party = SelectParty(parties);
        if (party == null) return;

        Console.WriteLine($"정말 삭제? '{party.Name}' ({party.PartyId}) — y/N: ");
        if (!string.Equals(Console.ReadLine()?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
            return;

        var chars = loader.LoadCharactersWithJobSkillFilter();
        foreach (var c in chars.Where(c =>
                     !string.IsNullOrWhiteSpace(c.PartyId) &&
                     c.PartyId.Equals(party.PartyId, StringComparison.OrdinalIgnoreCase)))
            c.PartyId = null;

        parties.RemoveAll(p => p.PartyId.Equals(party.PartyId, StringComparison.OrdinalIgnoreCase));
        loader.SavePartyDatabase(parties);
        loader.SaveCharactersDatabase(chars);
        Console.WriteLine("삭제 및 캐릭터 소속 해제 완료.");
    }

    private static PartyData? SelectParty(List<PartyData> parties)
    {
        if (parties.Count == 0)
        {
            Console.WriteLine("파티가 없습니다.");
            return null;
        }

        for (var i = 0; i < parties.Count; i++)
            Console.WriteLine($"{i + 1}. [{parties[i].PartyId}] {parties[i].Name}");

        Console.Write("번호: ");
        if (!int.TryParse(Console.ReadLine()?.Trim(), out var idx) || idx < 1 || idx > parties.Count)
        {
            Console.WriteLine("잘못된 번호.");
            return null;
        }

        return parties[idx - 1];
    }

    private static List<string> PickMemberIds(List<Character> allChars)
    {
        var roster = allChars
            .Where(c => !string.IsNullOrWhiteSpace(c.Id) &&
                        !c.Id.Equals("master", StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Console.WriteLine("--- 모험가 목록 (번호 선택) ---");
        for (var i = 0; i < roster.Count; i++)
            Console.WriteLine($"  {i + 1}. {roster[i].Name} ({roster[i].Id})  [현재 파티: {roster[i].PartyId ?? "-"}]");

        Console.WriteLine("포함할 번호를 공백으로 구분 (예: 1 2 3), 전원은 all: ");
        var line = Console.ReadLine()?.Trim() ?? "";

        List<Character> pick;
        if (line.Equals("all", StringComparison.OrdinalIgnoreCase))
            pick = roster.ToList();
        else
        {
            var nums = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var set = new HashSet<int>();
            foreach (var t in nums)
            {
                if (int.TryParse(t, out var n) && n >= 1 && n <= roster.Count)
                    set.Add(n);
            }

            pick = set.OrderBy(x => x).Select(x => roster[x - 1]).ToList();
        }

        return pick.Select(c => c.Id).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// 파티 멤버십을 PartyData·Character.PartyId 양쪽에 반영. 다른 파티에 있던 멤버는 이전 소속에서 제거.
    /// </summary>
    public static void ApplyPartyMembership(
        List<PartyData> parties,
        List<Character> chars,
        PartyData targetParty,
        List<string> newMemberIds)
    {
        var pid = targetParty.PartyId;
        var idSet = new HashSet<string>(newMemberIds, StringComparer.OrdinalIgnoreCase);

        foreach (var c in chars)
        {
            if (string.IsNullOrWhiteSpace(c.Id)) continue;
            if (c.Id.Equals("master", StringComparison.OrdinalIgnoreCase)) continue;

            var wasThis = !string.IsNullOrWhiteSpace(c.PartyId) &&
                          c.PartyId.Equals(pid, StringComparison.OrdinalIgnoreCase);
            var inNew = idSet.Contains(c.Id);

            if (wasThis && !inNew)
                c.PartyId = null;
        }

        foreach (var mid in newMemberIds)
        {
            var c = chars.FirstOrDefault(x => x.Id.Equals(mid, StringComparison.OrdinalIgnoreCase));
            if (c == null) continue;

            if (!string.IsNullOrWhiteSpace(c.PartyId) &&
                !c.PartyId.Equals(pid, StringComparison.OrdinalIgnoreCase))
            {
                var old = parties.FirstOrDefault(p =>
                    p.PartyId.Equals(c.PartyId, StringComparison.OrdinalIgnoreCase));
                old?.MemberIds.RemoveAll(m => m.Equals(mid, StringComparison.OrdinalIgnoreCase));
            }

            c.PartyId = pid;
        }

        targetParty.MemberIds = newMemberIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var q in parties)
        {
            if (q.PartyId.Equals(pid, StringComparison.OrdinalIgnoreCase)) continue;
            q.MemberIds.RemoveAll(m => idSet.Contains(m));
        }
    }

    /// <summary>등록된 파티 중 하나를 골라 던전 시뮬 → ActionLog·(선택) 캐릭터 DB 반영.</summary>
    public static Task RunExpeditionAsync(DialogueConfigLoader loader)
    {
        Console.WriteLine();
        Console.WriteLine("========== 원정 보내기 ==========");
        var parties = loader.LoadPartyDatabase();
        var party = SelectParty(parties);
        if (party == null) return Task.CompletedTask;

        if (party.MemberIds.Count == 0)
        {
            Console.WriteLine("멤버가 비어 있습니다. 먼저 파티를 편성하세요.");
            return Task.CompletedTask;
        }

        Console.WriteLine("시드 (정수, 엔터=무작위): ");
        int? seed = null;
        var seedLine = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(seedLine) && int.TryParse(seedLine, out var s))
            seed = s;

        Console.WriteLine("원정 후 CharactersDatabase.json에 스탯·인벤 반영? (Y/n): ");
        var syncLine = Console.ReadLine()?.Trim();
        var syncChars = string.IsNullOrWhiteSpace(syncLine) ||
                        !syncLine.Equals("n", StringComparison.OrdinalIgnoreCase);

        Console.WriteLine(
            "ActionLog.json: 기존 로그 뒤에 이 원정을 붙일까요? (엔터=Y 권장, n=파일 전체를 이번 원정만으로 덮어쓰기)");
        var replaceLog = string.Equals(Console.ReadLine()?.Trim(), "n", StringComparison.OrdinalIgnoreCase);

        var inputs = loader.LoadSimulationInputs(party.PartyId);
        var sim = DungeonRunSimulator.Generate(inputs, seed);

        CharacterMoodUpdater.ApplyAfterDungeonRun(
            sim.CharactersAfterRun,
            sim.ParticipatingCharacterIds,
            sim.RunOutcome,
            sim.PartyAvgHpRatio);

        var actionPath = Path.Combine(loader.ConfigDirectory, "ActionLog.json");
        var prevCount = loader.LoadTimelineData()?.ActionLog?.Count ?? 0;
        loader.SaveActionLogAfterSimulation(sim.Timeline, replaceExisting: replaceLog);
        var totalCount = loader.LoadTimelineData()?.ActionLog?.Count ?? 0;

        if (syncChars && sim.CharactersAfterRun.Count > 0)
        {
            loader.SaveCharactersDatabase(sim.CharactersAfterRun);
            Console.WriteLine("캐릭터 DB 갱신 완료.");
        }
        else if (!syncChars)
            Console.WriteLine("[안내] 캐릭터 DB는 건드리지 않았습니다.");

        var logNote = replaceLog
            ? $"덮어쓰기 후 {totalCount}건"
            : $"이번 원정 {sim.Timeline.ActionLog.Count}건 추가 (저장 후 전체 {totalCount}건, 이전 {prevCount}건)";
        Console.WriteLine(
            $"원정 완료: [{party.Name}] → {actionPath} | {logNote}{((seed.HasValue) ? $" | 시드 {seed.Value}" : "")}");

        CompanionDialoguePendingFile.SetPostDungeonPending(loader.ConfigDirectory);

        return Task.CompletedTask;
    }

    /// <summary>웹 허브: 파티 ID로 원정 실행(콘솔 메뉴 4와 동일 로직).</summary>
    /// <param name="dungeonName">월드 던전 이름(부분 일치). null이면 무작위 던전·층.</param>
    /// <param name="floorOrdinal">1부터 시작하는 층 서수. null이면 1층으로 간주.</param>
    public static ExpeditionResult RunExpeditionForParty(
        DialogueConfigLoader loader,
        string partyId,
        int? seed,
        bool syncChars,
        bool replaceActionLog,
        string? dungeonName = null,
        int? floorOrdinal = null)
    {
        var parties = loader.LoadPartyDatabase();
        var party = parties.FirstOrDefault(p =>
            p.PartyId.Equals(partyId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (party == null)
            return new ExpeditionResult(false, "파티를 찾을 수 없습니다.", null, null, 0, 0, 0, null, null);

        if (party.MemberIds.Count == 0)
            return new ExpeditionResult(false, "멤버가 비어 있습니다. 먼저 파티를 편성하세요.", null, null, 0, 0, 0, null, null);

        string? dungeonOverride = null;
        string? floorLabelOverride = null;
        if (!string.IsNullOrWhiteSpace(dungeonName))
        {
            var lore = loader.LoadWorldLore();
            var d = ExpeditionDungeonProgress.FindDungeon(lore, dungeonName.Trim());
            if (d == null)
                return new ExpeditionResult(false, "던전을 찾을 수 없습니다. WorldLore.json 던전 목록을 확인하세요.", null, null, 0, 0, 0, null, null);

            var log = loader.LoadTimelineData()?.ActionLog ?? new List<ActionLogEntry>();
            var maxSel = ExpeditionDungeonProgress.GetMaxSelectableOrdinal(log, d);
            var ord = floorOrdinal ?? 1;
            if (ord < 1 || ord > maxSel)
            {
                return new ExpeditionResult(
                    false,
                    $"선택한 층은 아직 해금되지 않았습니다. (이 던전은 현재 {maxSel}층까지 선택 가능)",
                    null,
                    null,
                    0,
                    0,
                    0,
                    null,
                    null);
            }

            dungeonOverride = d.Name;
            floorLabelOverride = ExpeditionDungeonProgress.FloorOrdinalToLabel(d, ord);
        }

        var inputs = loader.LoadSimulationInputs(party.PartyId, dungeonOverride, floorLabelOverride);
        var sim = DungeonRunSimulator.Generate(inputs, seed);

        CharacterMoodUpdater.ApplyAfterDungeonRun(
            sim.CharactersAfterRun,
            sim.ParticipatingCharacterIds,
            sim.RunOutcome,
            sim.PartyAvgHpRatio);

        var actionPath = Path.Combine(loader.ConfigDirectory, "ActionLog.json");
        var prevCount = loader.LoadTimelineData()?.ActionLog?.Count ?? 0;
        loader.SaveActionLogAfterSimulation(sim.Timeline, replaceExisting: replaceActionLog);
        var totalCount = loader.LoadTimelineData()?.ActionLog?.Count ?? 0;

        if (syncChars && sim.CharactersAfterRun.Count > 0)
            loader.SaveCharactersDatabase(sim.CharactersAfterRun);

        var logNote = replaceActionLog
            ? $"덮어쓰기 후 {totalCount}건"
            : $"이번 원정 {sim.Timeline.ActionLog.Count}건 추가 (저장 후 전체 {totalCount}건, 이전 {prevCount}건)";

        CompanionDialoguePendingFile.SetPostDungeonPending(loader.ConfigDirectory);

        var simLogLines = FormatExpeditionSimLogLines(sim.Timeline.ActionLog);
        return new ExpeditionResult(
            true,
            null,
            actionPath,
            party.Name,
            sim.Timeline.ActionLog.Count,
            prevCount,
            totalCount,
            logNote,
            simLogLines);
    }

    /// <summary>허브 UI: 이번 원정 로그를 「장소」「행동」 중심 한 줄로 표시.</summary>
    private static IReadOnlyList<string> FormatExpeditionSimLogLines(IReadOnlyList<ActionLogEntry> entries)
    {
        var list = new List<string>();
        foreach (var e in entries.OrderBy(x => x.Order))
            list.Add($"[{e.Order}] 장소: {FormatSimLogPlace(e)} · 행동: {FormatSimLogAction(e)}");
        return list;
    }

    private static string FormatSimLogPlace(ActionLogEntry e)
    {
        if (string.Equals(e.Type, "Base", StringComparison.OrdinalIgnoreCase))
            return BaseLocationLabel(e.Location);

        if (string.Equals(e.Type, "Dungeon", StringComparison.OrdinalIgnoreCase))
            return DungeonPlaceLabel(e);

        return e.Location ?? e.Type ?? "—";
    }

    private static string FormatSimLogAction(ActionLogEntry e)
    {
        if (string.Equals(e.Type, "Base", StringComparison.OrdinalIgnoreCase))
            return BaseEventLabel(e.EventType);

        if (string.Equals(e.Type, "Dungeon", StringComparison.OrdinalIgnoreCase))
            return DungeonActionLabel(e);

        return e.EventType ?? "—";
    }

    private static string BaseLocationLabel(string? loc)
    {
        if (string.IsNullOrWhiteSpace(loc)) return "아지트";
        return loc.Trim().ToLowerInvariant() switch
        {
            "main_hall" => "아지트 · 메인 홀",
            "quest_board" => "아지트 · 의뢰 게시판",
            "training_ground" or "training" => "아지트 · 훈련장",
            "cafeteria" => "아지트 · 식당",
            "reception" => "아지트 · 접수대",
            _ => $"아지트 · {loc.Trim()}"
        };
    }

    private static string BaseEventLabel(string? eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType)) return "—";
        return eventType.Trim().ToLowerInvariant() switch
        {
            "income" => "길드 출입",
            "partyform" => "파티 편성",
            "questaccept" => "의뢰 수락",
            "training" => "훈련",
            "meal" => "식사",
            "adventurerregister" => "모험가 등록",
            "talk" => "대화",
            _ => eventType.Trim()
        };
    }

    private static string DungeonPlaceLabel(ActionLogEntry e)
    {
        var name = string.IsNullOrWhiteSpace(e.DungeonName) ? "던전" : e.DungeonName.Trim();
        var floor = e.FloorOrZone?.ToString()?.Trim();
        if (string.Equals(e.EventType, "outcome", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(floor)) return $"{name} ({floor}) · 마무리";
            return $"{name} · 마무리";
        }

        var zone = string.IsNullOrWhiteSpace(e.Location) ? "" : e.Location.Trim();
        if (string.IsNullOrEmpty(zone))
        {
            return string.IsNullOrEmpty(floor) ? name : $"{name} ({floor})";
        }

        if (string.IsNullOrEmpty(floor)) return $"{name} · {zone}";
        return $"{name} ({floor}) · {zone}";
    }

    private static string DungeonActionLabel(ActionLogEntry e)
    {
        var et = e.EventType ?? "";
        if (string.Equals(et, "outcome", StringComparison.OrdinalIgnoreCase))
        {
            return e.Outcome?.ToLowerInvariant() switch
            {
                "clear" => "클리어",
                "retreat" => "철수",
                "fail" => "전멸 직전",
                _ => $"결과 ({e.Outcome ?? "—"})"
            };
        }

        if (string.Equals(et, "income", StringComparison.OrdinalIgnoreCase)) return "던전 입구 진입";
        if (string.Equals(et, "combat", StringComparison.OrdinalIgnoreCase)) return SummarizeCombatAction(e);
        if (string.Equals(et, "trap", StringComparison.OrdinalIgnoreCase)) return "함정 발동";
        if (string.Equals(et, "trapavoided", StringComparison.OrdinalIgnoreCase)) return "함정 회피";
        if (string.Equals(et, "skilluse", StringComparison.OrdinalIgnoreCase)) return "스킬 사용";
        if (string.Equals(et, "loot", StringComparison.OrdinalIgnoreCase)) return "전리품 수집";
        if (string.Equals(et, "artifact", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(e.ItemName) ? "유물 획득" : $"유물 획득 ({e.ItemName})";
        if (string.Equals(et, "itemtransfer", StringComparison.OrdinalIgnoreCase)) return "아이템 전달";
        if (string.Equals(et, "partycheck", StringComparison.OrdinalIgnoreCase)) return "파티 상태 점검";
        if (string.Equals(et, "inventorysort", StringComparison.OrdinalIgnoreCase)) return "인벤토리 정리";
        if (string.Equals(et, "debuffclear", StringComparison.OrdinalIgnoreCase)) return "디버프 해제";
        if (string.Equals(et, "heal", StringComparison.OrdinalIgnoreCase)) return "치유";
        if (string.Equals(et, "consumepotion", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(e.ItemName) ? "포션 사용" : $"포션 사용 ({e.ItemName})";

        return string.IsNullOrWhiteSpace(et) ? "—" : et;
    }

    private static string SummarizeCombatAction(ActionLogEntry e)
    {
        if (e.Enemies == null || e.Enemies.Count == 0) return "전투";
        var parts = e.Enemies
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .Select(x => x.Count > 1 ? $"{x.Name}×{x.Count}" : x.Name)
            .Take(4)
            .ToList();
        return parts.Count == 0 ? "전투" : $"전투 ({string.Join(", ", parts)})";
    }
}

/// <summary>웹 허브용 원정 실행 결과.</summary>
public sealed record ExpeditionResult(
    bool Ok,
    string? Error,
    string? ActionLogPath,
    string? PartyName,
    int ThisRunEntries,
    int PrevTotalEntries,
    int NewTotalEntries,
    string? LogNote,
    IReadOnlyList<string>? SimLogLines);
