namespace GuildDialogue.Data;

public class TestDataRoot
{
    /// <summary>시간순 단일 행동 로그. 던전·아지트 이벤트가 Order 순으로 나열된다.</summary>
    public List<ActionLogEntry> ActionLog { get; set; } = new();
}

/// <summary>행동 로그 한 건. Type에 따라 Dungeon 또는 Base 필드를 사용.</summary>
public class ActionLogEntry
{
    /// <summary>전체 타임라인 순서. 작을수록 먼저 발생.</summary>
    public int Order { get; set; }

    /// <summary>"Dungeon" | "Base"</summary>
    public string Type { get; set; } = "";

    // ---- Dungeon / Base 공통 ----
    /// <summary>
    /// 이 이벤트에 참여한 캐릭터들.
    /// Dungeon: 해당 파티 전체,
    /// Base: 이 행동에 실제로 관여한 캐릭터들(복귀 파티, 같이 식사한 멤버 등).
    /// </summary>
    public List<string>? PartyMembers { get; set; }

    // ---- Dungeon 전용 (Type == "Dungeon") ----
    public string? DungeonName { get; set; }
    public string? FloorOrZone { get; set; }
        /// <summary>런 결과: clear | retreat | fail</summary>
    public string? Outcome { get; set; }

    public string? EventType { get; set; }
    public double TimeOffsetSeconds { get; set; }
    public string? Location { get; set; }
    public string? TargetId { get; set; }
    public string? ActorId { get; set; }
    public int? Damage { get; set; }
    public int? HpBefore { get; set; }
    public int? HpAfter { get; set; }
    public int? MpBefore { get; set; }
    public int? MpAfter { get; set; }
    public string? ItemName { get; set; }
    public int? ItemCount { get; set; }
    public List<EnemyEntry>? Enemies { get; set; }
    public int? Turns { get; set; }
    public int? BlockCount { get; set; }
    public List<LootEntry>? LootItems { get; set; }
    public int? AvoidedCount { get; set; }
    public string? FromId { get; set; }
    public string? ToId { get; set; }
    public string? CheckType { get; set; }
    public int? ClearCount { get; set; }

    // ---- Base (Type == "Base"). EventType+Location만 사용. Summary는 LLM 등 동적 생성용이라 로그에는 넣지 않음. ----
}

/// <summary>아지트 로그 한 건. EventType(Income=복귀, 퀘스트 수주, 식사, 대화 등)+Location만.</summary>
public class BaseLogEntry
{
    public string Location { get; set; } = "";
    public string EventType { get; set; } = "";
    /// <summary>이 아지트 이벤트에 참여한 캐릭터들.</summary>
    public List<string> PartyMembers { get; set; } = new();
}

/// <summary>ActionLog(시간순 단일 로그)를 DungeonLogs·BaseLogs로 변환한다.</summary>
public static class ActionLogBuilder
{
    /// <summary>ActionLog에서 던전별·캐릭터별 DungeonLog와 BaseLog 목록을 만든다.</summary>
    public static (Dictionary<string, List<DungeonLog>> DungeonLogsByCharacter, List<BaseLogEntry> BaseLogs) Build(
        IReadOnlyList<ActionLogEntry> actionLog)
    {
        var dungeonLogsByCharacter = new Dictionary<string, List<DungeonLog>>(StringComparer.OrdinalIgnoreCase);
        var baseLogs = new List<BaseLogEntry>();

        if (actionLog == null || actionLog.Count == 0)
            return (dungeonLogsByCharacter, baseLogs);

        List<ActionLogEntry>? currentRun = null;
        string? currentDungeon = null;
        string? currentFloor = null;
        List<string>? currentParty = null;
        string? currentOutcome = null;

        foreach (var entry in actionLog.OrderBy(e => e.Order))
        {
            if (entry.Type == "Base")
            {
                FlushDungeonRun(currentRun, currentDungeon, currentFloor, currentParty, currentOutcome, dungeonLogsByCharacter);
                currentRun = null;
                baseLogs.Add(new BaseLogEntry
                {
                    Location = entry.Location ?? "",
                    EventType = entry.EventType ?? "",
                    PartyMembers = entry.PartyMembers != null ? new List<string>(entry.PartyMembers) : new List<string>()
                });
                continue;
            }

            if (entry.Type != "Dungeon" || string.IsNullOrEmpty(entry.DungeonName))
                continue;

            // outcome 전용 로그: 이번 런의 최종 결과만 기록. Events에는 넣지 않고 DungeonLog.Outcome으로만 사용.
            if (entry.EventType == "outcome")
            {
                currentOutcome = entry.Outcome;
                continue;
            }

            var sameRun = currentDungeon == entry.DungeonName && currentFloor == entry.FloorOrZone;
            if (!sameRun)
            {
                FlushDungeonRun(currentRun, currentDungeon, currentFloor, currentParty, currentOutcome, dungeonLogsByCharacter);
                currentRun = new List<ActionLogEntry>();
                currentDungeon = entry.DungeonName;
                currentFloor = entry.FloorOrZone;
                currentParty = entry.PartyMembers;
                currentOutcome = null;
            }
            else
                currentRun ??= new List<ActionLogEntry>();

            currentRun.Add(entry);
        }

        FlushDungeonRun(currentRun, currentDungeon, currentFloor, currentParty, currentOutcome, dungeonLogsByCharacter);
        return (dungeonLogsByCharacter, baseLogs);
    }

    private static void FlushDungeonRun(
        List<ActionLogEntry>? run,
        string? dungeonName,
        string? floorOrZone,
        List<string>? partyMembers,
        string? outcome,
        Dictionary<string, List<DungeonLog>> dungeonLogsByCharacter)
    {
        if (run == null || run.Count == 0 || string.IsNullOrEmpty(dungeonName) || partyMembers == null || partyMembers.Count == 0)
            return;

        var events = run
            .Where(e => e.EventType != "outcome")
            .Select(e => new DungeonEvent
        {
            EventType = e.EventType ?? "",
            TimeOffsetSeconds = e.TimeOffsetSeconds,
            Location = e.Location,
            TargetId = e.TargetId,
            ActorId = e.ActorId,
            Damage = e.Damage,
            HpBefore = e.HpBefore,
            HpAfter = e.HpAfter,
            MpBefore = e.MpBefore,
            MpAfter = e.MpAfter,
            ItemName = e.ItemName,
            ItemCount = e.ItemCount,
            Enemies = e.Enemies,
            Turns = e.Turns,
            BlockCount = e.BlockCount,
            LootItems = e.LootItems,
            AvoidedCount = e.AvoidedCount,
            FromId = e.FromId,
            ToId = e.ToId,
            CheckType = e.CheckType,
            ClearCount = e.ClearCount
        }).OrderBy(ev => ev.TimeOffsetSeconds).ToList();

        for (var i = 0; i < events.Count; i++)
            events[i].Order = i + 1;

        var log = new DungeonLog
        {
            DungeonName = dungeonName,
            FloorOrZone = floorOrZone ?? "",
            Events = events,
            Outcome = outcome,
            PartyMembers = partyMembers
        };

        foreach (var member in partyMembers)
        {
            var id = member switch
            {
                "카일" => "kyle",
                "리나" => "rina",
                "브람" => "bram",
                _ => member.ToLowerInvariant()
            };
            if (!dungeonLogsByCharacter.TryGetValue(id, out var list))
            {
                list = new List<DungeonLog>();
                dungeonLogsByCharacter[id] = list;
            }
            list.Add(log);
        }
    }
}
