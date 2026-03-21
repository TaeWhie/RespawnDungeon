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
    private static bool IsValidPartyId(string s) =>
        !string.IsNullOrWhiteSpace(s) &&
        s.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c is '_' or '-');

    private static string SuggestPartyId() => $"party_{Guid.NewGuid().ToString("N")[..8]}";

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
        var chars = loader.LoadCharacters()
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

        var chars = loader.LoadCharacters();
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
        var chars = loader.LoadCharacters();
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

        var chars = loader.LoadCharacters();
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
    private static void ApplyPartyMembership(
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

        return Task.CompletedTask;
    }
}
