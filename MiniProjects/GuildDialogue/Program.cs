using System.Text;
using GuildDialogue.Data;
using GuildDialogue.Services;

Console.OutputEncoding = Encoding.UTF8;

var configPath = Path.Combine(AppContext.BaseDirectory, "Config");
var loader = new DialogueConfigLoader(configPath);
var settings = loader.LoadSettings();
var characters = loader.LoadCharacters();
var testData = loader.LoadTestData();

// ---- 테스트 데이터 적용 ----
var kyle = characters.FirstOrDefault(c => c.Id == "kyle") ?? throw new InvalidOperationException("캐릭터 'kyle' 없음.");
var rina = characters.FirstOrDefault(c => c.Id == "rina");
var bram = characters.FirstOrDefault(c => c.Id == "bram");

var manager = new DialogueManager(settings, useOllama: true);

if (testData?.DungeonLogs != null)
{
    foreach (var (id, logs) in testData.DungeonLogs)
        manager.SetDungeonLogs(id, logs);
}
else
{
    manager.SetDungeonLogs("kyle", new List<DungeonLog>
    {
        new() { DungeonName = "잊힌 묘지", FloorOrZone = "2층", Events = new List<DungeonEvent> { new() { EventType = "Trap", Location = "동쪽 복도", TargetId = "카일", Damage = 15, HpBefore = 100, HpAfter = 85 }, new() { EventType = "Artifact", ItemName = "잊혀진 룬", Location = "보스방 직전" }, new() { EventType = "Combat", Enemies = new List<EnemyEntry> { new() { Name = "스켈레톤", Count = 2 } }, Turns = 2 } }, Outcome = "2층 클리어" },
        new() { DungeonName = "습지 동굴", FloorOrZone = "1층", Events = new List<DungeonEvent> { new() { EventType = "Combat", Enemies = new List<EnemyEntry> { new() { Name = "슬라임", Count = 3 } } }, new() { EventType = "ConsumePotion", ActorId = "리나", ItemCount = 1 }, new() { EventType = "ItemTransfer", FromId = "브람", ToId = "카일", ItemName = "회복 포션", ItemCount = 2 } }, Outcome = "1층 탐색 완료" }
    });
    manager.SetDungeonLogs("rina", new List<DungeonLog>
    {
        new() { DungeonName = "잊힌 묘지", FloorOrZone = "2층", Events = new List<DungeonEvent> { new() { EventType = "Heal", ActorId = "리나", TargetId = "카일", HpBefore = 85, HpAfter = 100 }, new() { EventType = "ConsumePotion", ActorId = "리나", MpBefore = 20, MpAfter = 45 }, new() { EventType = "PartyCheck", ActorId = "리나", CheckType = "HpMp" } }, Outcome = "2층 클리어" }
    });
}

// 공통 시나리오
var scenario = testData?.CompanionDialogue ?? new CompanionDialogueScenario
{
    CurrentSituation = "아지트 식당에서 식사 후 카일과 리나가 마주 앉아 있음.",
    MaxTurnsPerCharacter = 2
};

async Task RunCompanionDialogue(Character? a, Character? b, string label)
{
    if (a == null || b == null) return;

    var situation = $"아지트 식당에서 식사 후 {a.Name}와 {b.Name}가 마주 앉아 있음.";

    manager.ClearSession();
    Console.WriteLine($"=== 동료 대화: {label} ===\n");
    Console.WriteLine($"상황: {situation}\n");

    var lastUtterance = string.Empty;

    for (var i = 0; i < scenario.MaxTurnsPerCharacter; i++)
    {
        var isLastExchange = i == scenario.MaxTurnsPerCharacter - 1;

        // a 턴
        var aResponse = await manager.GenerateTurnAsync(
            speaker: a,
            other: b,
            otherId: b.Id,
            lastUtterance: lastUtterance,
            recentEvent: string.Empty,
            currentSituation: situation,
            allCharacters: characters,
            isLastTurn: isLastExchange);
        Console.WriteLine($"{a.Name}: {aResponse.Line}");
        lastUtterance = aResponse.Line;

        // b 턴
        var bResponse = await manager.GenerateTurnAsync(
            speaker: b,
            other: a,
            otherId: a.Id,
            lastUtterance: lastUtterance,
            recentEvent: string.Empty,
            currentSituation: situation,
            allCharacters: characters,
            isLastTurn: isLastExchange);
        Console.WriteLine($"{b.Name}: {bResponse.Line}\n");
        lastUtterance = bResponse.Line;
    }

    Console.WriteLine("--- 대화 종료 ---\n");
}

// ========== 실험 1: 다양한 동료 대화 샘플 ==========
await RunCompanionDialogue(kyle, rina!, "카일 ↔ 리나");
await RunCompanionDialogue(rina!, kyle, "리나 ↔ 카일");
await RunCompanionDialogue(bram!, kyle, "브람 ↔ 카일");

// ========== 실험 2: GM 대화 (선택) ==========
Console.WriteLine("=== GM 대화 (엔터로 한 번만 체험) ===\n");
Console.WriteLine("GM: 오늘 던전에서 유물 나왔다며? 어떻게 됐어?\n");

manager.ClearSession();
var gmResponse = await manager.GenerateTurnAsync(
    speaker: kyle,
    other: null,
    otherId: "gm",
    lastUtterance: "오늘 던전에서 유물 나왔다며? 어떻게 됐어?",
    recentEvent: string.Empty,
    currentSituation: "아지트 식당에서 식사 중",
    allCharacters: characters);

Console.WriteLine($"{kyle.Name}: {gmResponse.Line}");
Console.WriteLine($"  [tone: {gmResponse.Tone}, intent: {gmResponse.Intent}]");
Console.WriteLine("\n대화 종료.");
