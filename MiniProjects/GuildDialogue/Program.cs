using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GuildDialogue.Services;

class Program
{
    static async Task Main(string[] args)
    {
        // 1. 인코딩 프로바이더 등록 (CP949 등 지원)
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        // 2. 터미널 출력만 UTF-8로 설정 (입력은 터미널 기본값 유지)
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (args.Length > 0 && string.Equals(args[0], "--explore-guild-office", StringComparison.OrdinalIgnoreCase))
        {
            var code = await GuildOfficeExplorationRunner.RunAsync(CancellationToken.None);
            Environment.Exit(code);
            return;
        }

        if (args.Length > 0 && string.Equals(args[0], "--explore-guild-office-llm", StringComparison.OrdinalIgnoreCase))
        {
            var code = await GuildOfficeLlmExplorationRunner.RunAsync(args, CancellationToken.None);
            Environment.Exit(code);
            return;
        }

        if (args.Length > 0 && string.Equals(args[0], "--gen-actionlog", StringComparison.OrdinalIgnoreCase))
        {
            var noSyncChars = args.Any(a => string.Equals(a, "--no-sync-chars", StringComparison.OrdinalIgnoreCase));
            var replaceActionLog = args.Any(a => string.Equals(a, "--replace-actionlog", StringComparison.OrdinalIgnoreCase));
            string? partyForSim = null;
            var argList = args.ToList();
            var pIdx = argList.FindIndex(a => string.Equals(a, "--party", StringComparison.OrdinalIgnoreCase));
            if (pIdx >= 0 && pIdx + 1 < argList.Count)
            {
                partyForSim = argList[pIdx + 1];
                argList.RemoveRange(pIdx, 2);
            }

            argList.RemoveAll(a => string.Equals(a, "--replace-actionlog", StringComparison.OrdinalIgnoreCase));

            var filtered = argList.Where(a => !string.Equals(a, "--no-sync-chars", StringComparison.OrdinalIgnoreCase)).ToArray();
            var outPath = Path.Combine(DialogueConfigLoader.ResolveDefaultConfigDirectory(), "ActionLog.json");
            int? seed = null;
            if (filtered.Length == 2 && int.TryParse(filtered[1], out var seedOnly))
                seed = seedOnly;
            else if (filtered.Length >= 2)
            {
                outPath = filtered[1];
                if (filtered.Length >= 3 && int.TryParse(filtered[2], out var seedAfterPath))
                    seed = seedAfterPath;
            }

            var configDir = Path.GetDirectoryName(Path.GetFullPath(outPath))!;
            var loader = new DialogueConfigLoader(configDir);
            var sim = DungeonRunSimulator.Generate(loader.LoadSimulationInputs(partyForSim), seed);

            CharacterMoodUpdater.ApplyAfterDungeonRun(
                sim.CharactersAfterRun,
                sim.ParticipatingCharacterIds,
                sim.RunOutcome,
                sim.PartyAvgHpRatio);

            var dir = Path.GetDirectoryName(Path.GetFullPath(outPath));
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            loader.SaveActionLogAfterSimulation(sim.Timeline, replaceExisting: replaceActionLog);
            var totalEntries = loader.LoadTimelineData()?.ActionLog?.Count ?? 0;

            if (!noSyncChars && sim.CharactersAfterRun.Count > 0)
            {
                loader.SaveCharactersDatabase(sim.CharactersAfterRun);
                Console.WriteLine("캐릭터 DB 갱신: CharactersDatabase.json(또는 Characters.json)에 스탯·인벤토리 반영됨.");
            }

            var seedNote = seed.HasValue ? $" (시드 {seed.Value})" : " (무작위 시드)";
            var syncNote = noSyncChars ? " [캐릭터 DB 미반영]" : "";
            var partyNote = string.IsNullOrWhiteSpace(partyForSim) ? "" : $" [파티 {partyForSim}]";
            var mergeNote = replaceActionLog ? " [ActionLog 덮어쓰기]" : $" [ActionLog 병합→전체 {totalEntries}건]";
            Console.WriteLine(
                $"ActionLog 시뮬레이션 저장: {outPath} (이번 시뮬 {sim.Timeline.ActionLog.Count}건){mergeNote}{seedNote}{partyNote}{syncNote}");
            return;
        }

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"[Config] {DialogueConfigLoader.ResolveDefaultConfigDirectory()}  ← 원정·파티·로그 저장 위치");
            Console.WriteLine("실행 모드를 선택하세요:");
            Console.WriteLine("1. 동료 간 관전 모드 (자동 대화)");
            Console.WriteLine("2. 길드장 직접 참여 모드 (사용자 입력)");
            Console.WriteLine("3. 파티 편성 (목록·생성·수정·삭제)");
            Console.WriteLine("4. 원정 보내기 (던전 시뮬 → ActionLog·캐릭터 반영)");
            Console.WriteLine("5. 캐릭터 생성 (JobDatabase·스킬; 서사 Ollama)");
            Console.WriteLine("0. 종료");
            Console.Write("> ");

            var choice = Console.ReadLine()?.Trim();
            if (choice == "0")
                return;

            var offlineLoader = new DialogueConfigLoader();
            if (choice == "3")
            {
                await PartyManagementConsole.RunPartyRosterMenuAsync(offlineLoader);
                continue;
            }

            if (choice == "4")
            {
                await PartyManagementConsole.RunExpeditionAsync(offlineLoader);
                continue;
            }

            if (choice == "5")
            {
                await CharacterCreationConsole.RunCreateCharacterAsync(offlineLoader);
                continue;
            }

            var manager = new DialogueManager();
            await manager.InitializeAsync();

            if (choice == "2")
                await manager.RunGuildMasterSessionAsync();
            else
                await manager.RunInteractiveSessionAsync();

            return;
        }
    }
}
