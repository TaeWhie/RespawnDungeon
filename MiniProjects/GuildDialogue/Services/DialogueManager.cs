using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

public class DialogueManager
{
    private static readonly JsonSerializerOptions LooseJsonOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private readonly DialogueConfigLoader _loader;
    private readonly OllamaClient _ollama;
    private readonly MemoryManager _memory;
    private readonly DialogueSettings _settings;
    private WorldLore? _worldLore;
    private Dictionary<string, ItemData> _itemDb = new();
    private GameReferenceBundle _gameRefs = new();
    private List<ActionLogEntry> _actionLogEntries = new();
    private EmbeddingKnowledgeIndex? _embeddingIndex;
    private OllamaEmbeddingClient? _embeddingClient;
    private GuildOfficeSemanticGuardrail? _guildOfficeSemanticGuardrail;

    /// <summary>탐색·테스트용 로드된 캐릭터 목록.</summary>
    public IReadOnlyList<Character> Characters => _characters;

    private List<Character> _characters = new();
    private Dictionary<string, Character> _charMap = new();
    private Dictionary<string, string> _lastLineByChar = new();
    private Dictionary<string, string> _currentImpressions = new();
    private Dictionary<string, List<string>> _perspectiveLinesByCharacterId = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>메뉴 1·2 세션 전체 대화(ActionLog 저장용). 워킹 메모리와 별개로 누적.</summary>
    private readonly List<DialogueTurn> _sessionDialogueForLog = new();

    public DialogueManager()
    {
        _loader = new DialogueConfigLoader();
        _settings = _loader.LoadSettings();
        _ollama = new OllamaClient(_settings);
        
        var glossary = _loader.LoadLogGlossary();
        _memory = new MemoryManager(glossary);
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _characters = _loader.LoadCharactersWithJobSkillFilter();
        _charMap = new Dictionary<string, Character>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in _characters)
        {
            _charMap[c.Name] = c;
            if (!string.IsNullOrWhiteSpace(c.Id))
                _charMap[c.Id] = c;
        }

        _worldLore = _loader.LoadWorldLore();
        _itemDb = _loader.LoadItemDatabase()
            .ToDictionary(i => i.ItemName, StringComparer.OrdinalIgnoreCase);

        _gameRefs = new GameReferenceBundle
        {
            Bases = _loader.LoadBaseDatabase(),
            Monsters = _loader.LoadMonsterDatabase(),
            Skills = _loader.LoadSkillDatabase(),
            Traps = _loader.LoadTrapTypeDatabase(),
            EventTypes = _loader.LoadEventTypeDatabase(),
            Parties = _loader.LoadPartyDatabase(),
            Jobs = _loader.LoadJobDatabase()
        };

        var timeline = _loader.LoadTimelineData();
        _actionLogEntries = timeline?.ActionLog?.OrderBy(e => e.Order).ToList() ?? new List<ActionLogEntry>();

        if (timeline?.ActionLog != null)
        {
            var parsedLogs = ActionLogBuilder.Build(timeline.ActionLog);
            var allDungeonLogs = parsedLogs.DungeonLogsByCharacter
                .SelectMany(kvp => kvp.Value)
                .Distinct()
                .ToList();
            var monsterByName = _gameRefs.Monsters
                .GroupBy(m => m.MonsterName, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            var idToName = _characters
                .Where(c => !string.IsNullOrWhiteSpace(c.Id))
                .ToDictionary(c => c.Id, c => c.Name, StringComparer.OrdinalIgnoreCase);
            var episodicCtx = new EpisodicNarrativeContext
            {
                MonstersByLocalizedName = monsterByName,
                TrapTypes = _gameRefs.Traps,
                CharacterIdToDisplayName = idToName
            };
            _memory.BuildEpisodicBuffer(allDungeonLogs, parsedLogs.BaseLogs, episodicCtx);
        }

        RebuildPerspectiveMemoryCache();

        _embeddingClient?.Dispose();
        _embeddingClient = new OllamaEmbeddingClient(_settings);
        _embeddingIndex = new EmbeddingKnowledgeIndex();
        var items = _itemDb.Values.ToList();
        Console.WriteLine($"[시스템] 참조 지식 임베딩 인덱스 구축 중… (모델: {_embeddingClient.Model})");
        var embedConc = Math.Clamp(_settings.Ollama.EmbeddingMaxConcurrency, 1, 16);
        var ok = await _embeddingIndex.BuildAsync(
            _embeddingClient,
            _worldLore,
            _gameRefs.Monsters,
            _gameRefs.Traps,
            _gameRefs.Skills,
            items,
            _characters,
            embedConc,
            ct).ConfigureAwait(false);
        if (!ok)
            throw new InvalidOperationException(
                "참조 지식 임베딩 인덱스를 구축하지 못했습니다. Ollama를 실행하고 DialogueSettings.json의 EmbeddingModel(예: nomic-embed-text)을 확인하세요.");
        Console.WriteLine("[시스템] 임베딩 인덱스 준비 완료.");

        var retrieval = _settings.Retrieval ?? new RetrievalSettings();
        if (retrieval.UseGuildOfficeSemanticGuardrail)
        {
            try
            {
                _guildOfficeSemanticGuardrail = new GuildOfficeSemanticGuardrail();
                var anchorDb = _loader.LoadSemanticGuardrailAnchors();
                await _guildOfficeSemanticGuardrail.WarmupAsync(_embeddingClient, anchorDb, ct, embedConc)
                    .ConfigureAwait(false);
                Console.WriteLine("[시스템] 길드 집무실 세만틱 가드레일(임베딩 앵커) 준비됨.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[시스템] 세만틱 가드레일 생략: {ex.Message}");
                _guildOfficeSemanticGuardrail = null;
            }
        }
    }

    private async Task<string> ResolveReferenceKnowledgeRagAsync(
        string workingMemory,
        Character speaker,
        CancellationToken ct)
    {
        var retrieval = _settings.Retrieval ?? new RetrievalSettings();
        var query = GameKnowledgeRetriever.BuildRetrievalQuery(
            workingMemory,
            _memory.EpisodicBuffer,
            speaker,
            retrieval);

        if (_embeddingIndex == null || !_embeddingIndex.IsReady || _embeddingClient == null)
            throw new InvalidOperationException("임베딩 인덱스가 준비되지 않았습니다. InitializeAsync가 완료됐는지 확인하세요.");

        var emb = await _embeddingIndex.RetrieveFormattedAsync(
            _embeddingClient,
            query,
            retrieval.RagSearchPoolSize,
            ct).ConfigureAwait(false);
        return emb ?? "";
    }

    private string BuildLatestActionLogForPrompt()
    {
        var retrieval = _settings.Retrieval ?? new RetrievalSettings();
        if (retrieval.UseActionLogNarrativeProse)
        {
            var idToName = _characters
                .Where(c => !string.IsNullOrWhiteSpace(c.Id))
                .ToDictionary(c => c.Id, c => c.Name, StringComparer.OrdinalIgnoreCase);
            return ActionLogNarrativeFormatter.FormatForSystemPrompt(
                _actionLogEntries,
                _gameRefs.Bases,
                idToName,
                retrieval.ActionLogNarrativeMaxDungeonRuns,
                retrieval.ActionLogNarrativeMaxChars);
        }

        return LatestActionLogFormatter.FormatTailForSystemPrompt(
            _actionLogEntries,
            retrieval.LatestActionLogEntriesInPrompt);
    }

    private void RebuildPerspectiveMemoryCache()
    {
        var retrieval = _settings.Retrieval ?? new RetrievalSettings();
        _perspectiveLinesByCharacterId = retrieval.UsePerspectiveMemoryInPrompt
            ? CharacterPerspectiveMemoryBuilder.BuildByCharacterId(
                _actionLogEntries,
                _characters,
                retrieval.PerspectiveMemoryMaxLinesPerCharacter)
            : new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (!retrieval.UsePerspectiveMemoryInPrompt ||
            string.IsNullOrWhiteSpace(retrieval.PerspectiveMemoryExportPath))
            return;

        try
        {
            var exportRaw = retrieval.PerspectiveMemoryExportPath!;
            var full = Path.IsPathRooted(exportRaw)
                ? exportRaw
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, exportRaw));

            var actionLogCanonical = Path.GetFullPath(
                Path.Combine(DialogueConfigLoader.ResolveDefaultConfigDirectory(), "ActionLog.json"));
            if (string.Equals(Path.GetFullPath(full), actionLogCanonical, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    "[시스템] PerspectiveMemoryExportPath가 Config의 ActionLog.json과 같으면 행동 로그 형식이 깨집니다. Logs/ 등 다른 파일을 지정하세요. 덤프를 건너뜁니다.");
                return;
            }

            var dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_perspectiveLinesByCharacterId, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            File.WriteAllText(full, json);
            Console.WriteLine($"[시스템] 화자 관점 메모리 덤프: {full}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[시스템] 관점 메모리 덤프 실패: {ex.Message}");
        }
    }

    private string? BuildPerspectiveMemoryForSpeaker(Character speaker)
    {
        var retrieval = _settings.Retrieval ?? new RetrievalSettings();
        if (!retrieval.UsePerspectiveMemoryInPrompt)
            return null;

        var block = CharacterPerspectiveMemoryBuilder.FormatBlockForSpeaker(
            speaker.Id,
            _perspectiveLinesByCharacterId,
            retrieval.PerspectiveMemoryMaxLinesPerCharacter);

        return string.IsNullOrWhiteSpace(block) ? null : block;
    }

    private static string InferBaseLocationFromWorldState(string? worldState)
    {
        var ws = worldState ?? "";
        if (ws.Contains("집무실")) return "reception";
        if (ws.Contains("식당")) return "cafeteria";
        if (ws.Contains("훈련")) return "training_ground";
        if (ws.Contains("게시") || ws.Contains("의뢰")) return "quest_board";
        if (ws.Contains("접수")) return "reception";
        return "main_hall";
    }

    /// <summary>콘솔 대화(메뉴 1·2) 종료 직전, 워킹 메모리를 ActionLog에 Base/talk 한 건으로 추가합니다.</summary>
    private void PersistDialogueSessionToActionLogIfEnabled(string worldState, string sessionLabel)
    {
        var retrieval = _settings.Retrieval ?? new RetrievalSettings();
        if (!retrieval.PersistDialogueSessionsToActionLog)
            return;

        var turns = _sessionDialogueForLog.Count > 0
            ? (IReadOnlyList<DialogueTurn>)_sessionDialogueForLog.ToList()
            : _memory.GetWorkingMemoryTurns();
        if (turns.Count == 0)
            return;

        try
        {
            var existing = _loader.LoadTimelineData();
            var loc = InferBaseLocationFromWorldState(worldState);
            var merged = ActionLogPersistence.AppendGuildDialogueSession(existing, loc, turns, sessionLabel);
            _loader.SaveActionLog(merged);
            _actionLogEntries = merged.ActionLog.OrderBy(e => e.Order).ToList();
            RebuildPerspectiveMemoryCache();
            Console.WriteLine(
                $"[시스템] 대화가 ActionLog.json에 반영되었습니다. (Type=Base, EventType=talk, Location={loc}, 턴 {turns.Count})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[시스템] ActionLog 대화 저장 실패: {ex.Message}");
        }
        finally
        {
            _sessionDialogueForLog.Clear();
        }
    }

    private void LogSessionLineForActionLog(string speaker, string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        _sessionDialogueForLog.Add(new DialogueTurn { Speaker = speaker, Line = line.Trim() });
    }

    /// <summary>
    /// 아지트에서 선택된 두 동료만 짧은 대화(관전). 구 메뉴 1의 6인 고정 흐름 대체.
    /// <paramref name="utteranceCount"/>는 두 사람이 **번갈아** 말하는 **총 줄 수**(2~4). 홀수면 마지막 한 사람이 한 번 더 말함.
    /// </summary>
    public async Task RunBasePairDialogueAsync(
        Character a,
        Character b,
        int utteranceCount,
        string? worldStateOverride = null,
        CancellationToken ct = default,
        List<string>? transcriptSink = null,
        bool writeConsole = true,
        Func<string, Task>? onTranscriptLine = null)
    {
        async Task EmitAsync(string line)
        {
            if (writeConsole)
                Console.WriteLine(line);
            transcriptSink?.Add(line);
            if (onTranscriptLine != null)
                await onTranscriptLine(line).ConfigureAwait(false);
        }

        if (a == null || b == null || string.Equals(a.Id, b.Id, StringComparison.OrdinalIgnoreCase))
        {
            await EmitAsync("[오류] 대화 상대가 올바르지 않습니다.").ConfigureAwait(false);
            return;
        }

        var totalLines = Math.Clamp(utteranceCount, 2, 4);
        _sessionDialogueForLog.Clear();

        var locA = string.IsNullOrWhiteSpace(a.CurrentLocationNote)
            ? (a.CurrentLocationId ?? "아지트")
            : $"{a.CurrentLocationId ?? "아지트"}({a.CurrentLocationNote})";
        var locB = string.IsNullOrWhiteSpace(b.CurrentLocationNote)
            ? (b.CurrentLocationId ?? "아지트")
            : $"{b.CurrentLocationId ?? "아지트"}({b.CurrentLocationNote})";

        var worldState = worldStateOverride ?? (
            "장소: 길드 아지트\n" +
            $"시간: 업무·휴식 시간대\n" +
            $"상황: {a.Name}와(과) {b.Name}가(이) 같은 거점에서 잠시 마주쳐 짧게 이야기를 나눕니다.\n" +
            $"(참고 위치: {a.Name} {locA}, {b.Name} {locB})\n" +
            "\n" +
            "[이번 장면 — 출연(고정)]\n" +
            $"• 이 대화에 **실제로 등장하는 사람**은 **{a.Name}**와 **{b.Name}** 두 명뿐입니다. " +
            "둘이 번갈아 한 줄씩 말합니다.\n" +
            "• 파티에 다른 동료가 있어도, 그들이 **같은 자리에서 말을 걸거나 끼어드는** 것처럼 쓰지 마세요. " +
            "과거 원정·로그에 다른 이름이 나오는 것은 가능합니다.");

        await EmitAsync("[시스템] 최근 사건을 바탕으로 사회적 인상을 합성 중...").ConfigureAwait(false);
        _currentImpressions = await SynthesizeSocialImpressionsAsync(_memory.EpisodicBuffer, ct);

        var randomTopic = _memory.GetRandomPastEvent();
        var aSpeaksFirst = Random.Shared.Next(2) == 0;

        const int maxJsonRetriesPerTurn = 3;
        for (int i = 0; i < totalLines; i++)
        {
            if (ct.IsCancellationRequested) break;

            bool speakerIsA = (i % 2 == 0);
            if (!aSpeaksFirst) speakerIsA = !speakerIsA;
            var speaker = speakerIsA ? a : b;
            var listener = speakerIsA ? b : a;

            bool lineEmitted = false;
            var userPromptForTurn = "";
            for (int attempt = 0; attempt < maxJsonRetriesPerTurn && !lineEmitted; attempt++)
            {
                if (attempt > 0)
                {
                    await EmitAsync($"[시스템] {speaker.Name} 응답 형식 오류 — 재시도 {attempt + 1}/{maxJsonRetriesPerTurn}…")
                        .ConfigureAwait(false);
                }
                else
                {
                    await EmitAsync($"[시스템] {speaker.Name}의 응답을 생성하는 중...").ConfigureAwait(false);
                }

                string workingMem = _memory.GetWorkingMemoryContext();
                string archivalMem = _memory.RetrieveArchivalMemoryContext(workingMem);
                bool isFirstTurn = i == 0;
                bool isFinalTurn = i == totalLines - 1;
                string? lastLine = _lastLineByChar.GetValueOrDefault(speaker.Name);
                string? currentImpression = _currentImpressions.GetValueOrDefault($"{speaker.Id}->{listener.Id}");

                var latestLog = BuildLatestActionLogForPrompt();
                var codeDef = CodeDefinitionInstructions.Build(_gameRefs);
                // 동료 2인 잡담: RAG 블록은 아이템·설정 문장을 과잉 유도하므로 넣지 않음(길드장 1:1과 구분).
                var ragBlock = "";
                var partyRoster = PartyRosterResolver.ResolveForPrompt(_characters, speaker);

                string sysPrompt = PromptBuilder.BuildSystemPrompt(
                    speaker,
                    listener,
                    worldState,
                    _memory.EpisodicBuffer,
                    archivalMem,
                    _settings,
                    partyRoster,
                    latestLog,
                    codeDef,
                    BuildPerspectiveMemoryForSpeaker(speaker),
                    lastLine,
                    currentImpression,
                    false,
                    ragBlock,
                    _gameRefs,
                    allyPeerDialogueScene: true);
                userPromptForTurn = PromptBuilder.BuildUserPrompt(
                    workingMem,
                    isFirstTurn ? randomTopic : null,
                    isFinalTurn,
                    i,
                    allyPeerDialogue: true,
                    peerListenerName: listener.Name,
                    allyTotalUtterances: totalLines);

                string? responseText = await _ollama.GenerateResponseAsync(sysPrompt, userPromptForTurn, ct);
                if (string.IsNullOrWhiteSpace(responseText))
                    continue;

                if (TryParseAllyLineFromModelResponse(responseText, out var generatedLine) &&
                    !string.IsNullOrWhiteSpace(generatedLine))
                {
                    CharacterMoodUpdater.ApplyAfterSquadTurn(speaker, generatedLine);
                    await EmitAsync($"[{speaker.Name}] {generatedLine}\n").ConfigureAwait(false);

                    _memory.AddWorkingMemory(speaker.Name, generatedLine);
                    _lastLineByChar[speaker.Name] = generatedLine;
                    LogSessionLineForActionLog(speaker.Name, generatedLine);
                    lineEmitted = true;
                }
            }

            if (!lineEmitted)
            {
                await EmitAsync($"[시스템] {speaker.Name} — 형식 파싱 실패. **대사 한 줄만** 직출력으로 재시도…")
                    .ConfigureAwait(false);
                string plainSys =
                    $"당신은 {speaker.Name}입니다. {listener.Name}와 같은 자리에서 동료로 짧게 말합니다. " +
                    "**응답은 대사 한 줄만** — JSON·마크다운 코드펜스·앞뒤 메타 설명 금지.";
                string plainUser =
                    userPromptForTurn +
                    "\n\n[출력 규칙] 위 맥락에 맞춰 **한 문장**만 출력. 따옴표로 전체를 감싸지 말 것.";
                string? plain = await _ollama.GenerateResponseAsync(plainSys, plainUser, ct);
                var fallbackLine = ExtractAllyPlainFirstLine(plain);
                if (!string.IsNullOrWhiteSpace(fallbackLine))
                {
                    CharacterMoodUpdater.ApplyAfterSquadTurn(speaker, fallbackLine);
                    await EmitAsync($"[{speaker.Name}] {fallbackLine}\n").ConfigureAwait(false);
                    _memory.AddWorkingMemory(speaker.Name, fallbackLine);
                    _lastLineByChar[speaker.Name] = fallbackLine;
                    LogSessionLineForActionLog(speaker.Name, fallbackLine);
                    lineEmitted = true;
                }
            }

            if (!lineEmitted)
            {
                await EmitAsync($"[시스템] {speaker.Name} 턴을 건너뜁니다(모델 응답 실패). 대화를 종료합니다.")
                    .ConfigureAwait(false);
                break;
            }
        }

        await EmitAsync("========== 동료 2인 대화 종료 ==========").ConfigureAwait(false);
        PersistDialogueSessionToActionLogIfEnabled(worldState, "아지트 동료 대화(2인)");
        await UpdateSocialSettlementAsync(ct, null);
    }

    public async Task RunGuildMasterSessionAsync(CancellationToken ct = default)
    {
        Console.WriteLine("========== 길드장 집무실: 동료들과의 대화 ==========");
        Console.WriteLine("(팁: 먼저 대화할 동료를 고릅니다. 이후에는 그 동료와만 이어집니다.)");
        Console.WriteLine("(팁: 대화 중 다른 동료로 바꾸려면 'change' 또는 '바꿔'를 입력하세요.)");
        Console.WriteLine("(팁: 집무실을 나가려면 exit, 나가, 나가 그냥, 그만, 대화 끝 등)\n");

        if (_characters.Count == 0) return;

        var selectable = _characters
            .Where(c => !string.Equals(c.Id, "master", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (selectable.Count == 0)
        {
            Console.WriteLine("[오류] 대화 가능한 동료 캐릭터가 없습니다.");
            return;
        }

        _sessionDialogueForLog.Clear();
        var masterChar = new Character { Id = "master", Name = "길드장", Gender = "미정", Role = "Guild Master" };
        const string baseWorldState =
            "장소: 길드장 집무실\n상황: 던전 탐험을 마친 뒤 동료가 길드장에게 보고하거나 대화를 나누러 집무실을 찾아왔습니다.";

        while (!ct.IsCancellationRequested)
        {
            if (!TryPromptGuildOfficeBuddySelection(selectable, ct, out Character? respondent) || respondent is null)
            {
                Console.WriteLine("(집무실을 나갑니다.)");
                break;
            }

            Console.WriteLine($"\n──────── 현재 대화 상대: {respondent.Name} (1:1) ────────\n");

            // 다른 동료와 섞인 맥락·착각 방지: 상대를 바꿀 때마다 워킹 메모리는 새로 시작
            _memory.ClearWorkingMemory();

            var leaveOffice = false;
            while (!ct.IsCancellationRequested)
            {
                Console.Write("[길드장] ");
                string? userInput = ReadConsoleLine();

                if (string.IsNullOrWhiteSpace(userInput)) continue;
                userInput = NormalizeConsoleInputLine(userInput);

                // 파이프·리다이렉트 입력 시에는 화면에 한 줄이 안 보일 수 있어 동일 내용을 한 번 더 출력
                if (Console.IsInputRedirected)
                    Console.WriteLine(userInput);

                if (GuildOfficeTopicGate.IsGuildOfficeExitRoomCommand(userInput))
                {
                    Console.WriteLine("(집무실을 나갑니다.)");
                    leaveOffice = true;
                    break;
                }

                string normalized = userInput.Trim().ToLowerInvariant();
                if (normalized is "change" or "바꿔" or "바꾸기" or "상대")
                    break;

                string worldState =
                    $"{baseWorldState}\n현재: 길드장과 {respondent.Name}만 집무실에 있으며 1:1로 대화 중입니다.";

                _memory.AddWorkingMemory("길드장", userInput);
                LogSessionLineForActionLog("길드장", userInput);

                Console.WriteLine($"[시스템] {respondent.Name}이(가) 생각을 정리 중...");

                var turnResult = await GenerateGuildOfficeBuddyReplyAsync(
                    respondent,
                    masterChar,
                    worldState,
                    userInput,
                    persistBuddyLineToSession: true,
                    ct).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(turnResult.BuddyLine))
                    Console.WriteLine($"[{respondent.Name}] {turnResult.BuddyLine}\n");
                else if (!string.IsNullOrWhiteSpace(turnResult.ErrorMessage))
                    Console.WriteLine($"[오류] 응답·파싱 중 문제: {turnResult.ErrorMessage}");
            }

            if (leaveOffice) break;
        }

        Console.WriteLine("========== 상호작용 세션 종료 ==========");
        string settlementHistory = FormatDialogueTurnsForSettlement(_sessionDialogueForLog);
        PersistDialogueSessionToActionLogIfEnabled(baseWorldState, "길드장 상호작용");
        await UpdateSocialSettlementAsync(ct, settlementHistory);
    }

    /// <summary>
    /// 워킹 메모리에 길드장 발화가 이미 반영된 상태에서 NPC 한 줄 생성(집무실 1:1과 동일 파이프라인).
    /// </summary>
    public async Task<GuildOfficeLlmTurnResult> GenerateGuildOfficeBuddyReplyAsync(
        Character respondent,
        Character masterChar,
        string worldState,
        string userUtterance,
        bool persistBuddyLineToSession,
        CancellationToken ct = default)
    {
        string workingMem = _memory.GetWorkingMemoryContext();
        string archivalMem = _memory.RetrieveArchivalMemoryContext(workingMem);
        string? lastLine = _lastLineByChar.GetValueOrDefault(respondent.Name);
        string? currentImpression = _currentImpressions.GetValueOrDefault($"{respondent.Id}->master");

        var retrievalGm = _settings.Retrieval ?? new RetrievalSettings();
        var signals = await GuildOfficeTopicGate.ResolveGuildOfficeSignalsAsync(
            userUtterance,
            _guildOfficeSemanticGuardrail,
            _embeddingClient,
            _ollama,
            _settings,
            ct).ConfigureAwait(false);
        var atypicalKind = signals.AtypicalKind;
        bool guardrailReady = retrievalGm.UseGuildOfficeSemanticGuardrail &&
            _guildOfficeSemanticGuardrail?.IsReady == true &&
            _embeddingClient != null;
        bool deepExpedition = GuildOfficeTopicGate.ComputeDeepExpeditionForGuildOffice(
            userUtterance,
            signals,
            retrievalGm,
            guardrailReady);
        bool frustrationStop = GuildOfficeTopicGate.LooksLikeGuildMasterFrustrationOrStop(userUtterance);
        bool deepExpeditionEffective = deepExpedition && !frustrationStop;
        double sigMeta = signals.MetaSimilarity;
        double sigOff = signals.OffWorldSimilarity;
        double sigExp = signals.ExpeditionContextSimilarity;
        string episodicForPrompt = deepExpeditionEffective
            ? _memory.EpisodicBuffer
            : "[시스템 — 일상 대화 턴] 던전 서사(Episodic)는 응답을 방해하지 않도록 생략함. 던전·전투·로그 인용 금지.";
        string latestLogForPrompt = deepExpeditionEffective
            ? BuildLatestActionLogForPrompt()
            : "[시스템 — 일상 대화 턴] ActionLog 최신 블록 생략. 길드장이 작전·던전을 묻기 전까지 인용 금지.";
        string? perspectiveForPrompt =
            deepExpeditionEffective ? BuildPerspectiveMemoryForSpeaker(respondent) : null;
        string ragBlock = deepExpeditionEffective
            ? await ResolveReferenceKnowledgeRagAsync(workingMem, respondent, ct).ConfigureAwait(false)
            : string.Empty;

        var codeDef = CodeDefinitionInstructions.Build(_gameRefs);
        var partyRosterGm = PartyRosterResolver.ResolveForGuildMasterOneOnOne(respondent);
        string? mcpRuntimeFacts = AethelgardMcpRuntimeFacts.Build(
            userUtterance,
            partyRosterGm,
            respondent,
            _settings,
            _characters,
            _itemDb.Values.ToList(),
            atypicalKind);
        string sysPrompt = PromptBuilder.BuildSystemPrompt(
            respondent,
            masterChar,
            worldState,
            episodicForPrompt,
            archivalMem,
            _settings,
            partyRosterGm,
            latestLogForPrompt,
            codeDef,
            perspectiveForPrompt,
            lastLine,
            currentImpression,
            true,
            ragBlock,
            _gameRefs,
            guildMasterOneOnOneScene: true,
            guildMasterAtypicalKind: atypicalKind,
            guildOfficePersonaHijackCue: BuildShortWorldLoreCueForGuildOffice(_worldLore),
            guildMasterDeepExpeditionTurn: deepExpeditionEffective,
            guildMasterFrustrationStopTurn: frustrationStop,
            mcpRuntimeToolBlock: mcpRuntimeFacts);
        string userPrompt = PromptBuilder.BuildGuildMasterInteractiveUserPrompt(
            workingMem,
            userUtterance,
            respondent.Name,
            atypicalKind,
            deepExpeditionTurn: deepExpeditionEffective,
            frustrationStopTurn: frustrationStop);

        string? preview = null;
        try
        {
            string? responseText = await _ollama.GenerateResponseAsync(sysPrompt, userPrompt, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(responseText))
                return new GuildOfficeLlmTurnResult(null, atypicalKind, deepExpeditionEffective, "모델 빈 응답", null, sigMeta, sigOff, sigExp);

            preview = responseText.Length > 220 ? responseText[..220] + "…" : responseText;
            var cleanJson = CleanJsonResponse(responseText);

            using var doc = JsonDocument.Parse(cleanJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("line", out var lineProp))
                return new GuildOfficeLlmTurnResult(null, atypicalKind, deepExpeditionEffective, "JSON에 line 필드 없음", preview, sigMeta, sigOff, sigExp);

            string generatedLine = lineProp.GetString() ?? "";
            CharacterMoodUpdater.ApplyAfterGuildOfficeTurn(
                respondent,
                userUtterance,
                atypicalKind,
                frustrationStop,
                deepExpeditionEffective);
            if (persistBuddyLineToSession)
            {
                _memory.AddWorkingMemory(respondent.Name, generatedLine);
                _lastLineByChar[respondent.Name] = generatedLine;
                LogSessionLineForActionLog(respondent.Name, generatedLine);
            }

            return new GuildOfficeLlmTurnResult(generatedLine, atypicalKind, deepExpeditionEffective, null, null, sigMeta, sigOff, sigExp);
        }
        catch (Exception ex)
        {
            return new GuildOfficeLlmTurnResult(null, atypicalKind, deepExpeditionEffective, ex.Message, preview, sigMeta, sigOff, sigExp);
        }
    }

    /// <summary>웹 허브: 길드장 집무실 세션 시작 시 세션 로그를 비웁니다(콘솔 <see cref="RunGuildMasterSessionAsync"/>와 동일).</summary>
    public void BeginGuildMasterSession()
    {
        _sessionDialogueForLog.Clear();
    }

    /// <summary>웹 허브: 동료를 바꿀 때 워킹 메모리만 초기화합니다.</summary>
    public void ResetGuildOfficeBuddyContext()
    {
        _memory.ClearWorkingMemory();
    }

    /// <summary>웹 허브: 집무실 종료 시 ActionLog·관계 정산(콘솔 종료 직전과 동일).</summary>
    public async Task EndGuildMasterSessionAsync(CancellationToken ct = default)
    {
        const string baseWorldState =
            "장소: 길드장 집무실\n상황: 던전 탐험을 마친 뒤 동료가 길드장에게 보고하거나 대화를 나누러 집무실을 찾아왔습니다.";
        var settlementHistory = FormatDialogueTurnsForSettlement(_sessionDialogueForLog);
        PersistDialogueSessionToActionLogIfEnabled(baseWorldState, "길드장 상호작용");
        await UpdateSocialSettlementAsync(ct, settlementHistory).ConfigureAwait(false);
    }

    /// <summary>이름·Id로 캐릭터 조회.</summary>
    public Character? ResolveCharacter(string buddyIdOrName)
    {
        if (string.IsNullOrWhiteSpace(buddyIdOrName)) return null;
        return _charMap.GetValueOrDefault(buddyIdOrName.Trim());
    }

    /// <summary>웹 허브: 길드장 한 턴(콘솔 <see cref="RunGuildMasterSessionAsync"/> 루프와 동일 파이프라인).</summary>
    public async Task<GuildOfficeLlmTurnResult> GuildMasterUserTurnAsync(
        string buddyIdOrName,
        string userInput,
        CancellationToken ct = default)
    {
        var respondent = ResolveCharacter(buddyIdOrName);
        if (respondent == null)
        {
            return new GuildOfficeLlmTurnResult(
                null,
                GuildMasterAtypicalInputKind.None,
                false,
                "동료를 찾을 수 없습니다.",
                null,
                0,
                0,
                0);
        }

        if (string.Equals(respondent.Id, "master", StringComparison.OrdinalIgnoreCase))
        {
            return new GuildOfficeLlmTurnResult(
                null,
                GuildMasterAtypicalInputKind.None,
                false,
                "길드장은 대화 상대로 선택할 수 없습니다.",
                null,
                0,
                0,
                0);
        }

        var masterChar = new Character { Id = "master", Name = "길드장", Gender = "미정", Role = "Guild Master" };
        const string baseWorldState =
            "장소: 길드장 집무실\n상황: 던전 탐험을 마친 뒤 동료가 길드장에게 보고하거나 대화를 나누러 집무실을 찾아왔습니다.";
        var worldState =
            $"{baseWorldState}\n현재: 길드장과 {respondent.Name}만 집무실에 있으며 1:1로 대화 중입니다.";

        userInput = NormalizeConsoleInputLine(userInput);
        if (string.IsNullOrWhiteSpace(userInput))
        {
            return new GuildOfficeLlmTurnResult(
                null,
                GuildMasterAtypicalInputKind.None,
                false,
                "빈 입력입니다.",
                null,
                0,
                0,
                0);
        }

        _memory.AddWorkingMemory("길드장", userInput);
        LogSessionLineForActionLog("길드장", userInput);

        return await GenerateGuildOfficeBuddyReplyAsync(
            respondent,
            masterChar,
            worldState,
            userInput,
            persistBuddyLineToSession: true,
            ct).ConfigureAwait(false);
    }

    /// <summary>한 턴만 격리(워킹 메모리·상대 마지막 대사 초기화 후 LLM 호출). ActionLog 세션에는 남기지 않음.</summary>
    public async Task<GuildOfficeLlmTurnResult> RunIsolatedGuildOfficeLlmTurnAsync(
        Character respondent,
        string userUtterance,
        string baseWorldState,
        CancellationToken ct = default)
    {
        _memory.ClearWorkingMemory();
        if (!string.IsNullOrWhiteSpace(respondent.Name))
            _lastLineByChar.Remove(respondent.Name);
        _memory.AddWorkingMemory("길드장", userUtterance);

        var masterChar = new Character { Id = "master", Name = "길드장", Gender = "미정", Role = "Guild Master" };
        string worldState =
            $"{baseWorldState}\n현재: 길드장과 {respondent.Name}만 집무실에 있으며 1:1로 대화 중입니다.";

        return await GenerateGuildOfficeBuddyReplyAsync(
            respondent,
            masterChar,
            worldState,
            userUtterance,
            persistBuddyLineToSession: false,
            ct).ConfigureAwait(false);
    }

    /// <summary>WorldLore에서 골렘/마공 등 ‘메타를 세계관으로 흡수’할 힌트 한 덩어리.</summary>
    private static string? BuildShortWorldLoreCueForGuildOffice(WorldLore? w)
    {
        if (w == null) return null;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(w.WorldName)) parts.Add(w.WorldName);
        if (!string.IsNullOrWhiteSpace(w.GuildInfo)) parts.Add(w.GuildInfo);
        if (!string.IsNullOrWhiteSpace(w.BaseCamp)) parts.Add("거점 " + w.BaseCamp);
        var luminis = w.Locations?.FirstOrDefault(l =>
            l.Name.Contains("루미니스", StringComparison.OrdinalIgnoreCase));
        if (luminis != null)
            parts.Add($"{luminis.Name}: {luminis.Description}");

        var golemDungeon = w.Dungeons?.FirstOrDefault(d =>
            d.TypicalMonsters?.Any(m => m.Contains("골렘", StringComparison.OrdinalIgnoreCase)) == true);
        if (golemDungeon != null)
            parts.Add($"{golemDungeon.Name} 등에 골렘·마법 자동체 설정.");

        return parts.Count == 0 ? null : string.Join(' ', parts);
    }

    private static string FormatDialogueTurnsForSettlement(IReadOnlyList<DialogueTurn> turns)
    {
        if (turns == null || turns.Count == 0) return "(대화 없음)";
        var sb = new System.Text.StringBuilder();
        foreach (var t in turns)
            sb.AppendLine($"{t.Speaker}: \"{t.Line}\"");
        return sb.ToString().TrimEnd();
    }

    /// <summary>집무실: 번호·이름·Id로 동료를 고릅니다. exit 계열이면 false.</summary>
    private bool TryPromptGuildOfficeBuddySelection(
        IReadOnlyList<Character> selectable,
        CancellationToken ct,
        out Character? respondent)
    {
        respondent = null;
        while (!ct.IsCancellationRequested)
        {
            Console.WriteLine("대화할 동료를 선택하세요:");
            for (int i = 0; i < selectable.Count; i++)
            {
                var c = selectable[i];
                string idPart = string.IsNullOrWhiteSpace(c.Id) ? "" : $" [{c.Id}]";
                Console.WriteLine($"  {i + 1}. {c.Name}{idPart}");
            }

            Console.WriteLine("  (나가기: exit)\n선택> ");

            string? line = ReadConsoleLine();
            if (string.IsNullOrWhiteSpace(line))
            {
                Console.WriteLine("(번호 또는 이름/Id를 입력하세요.)\n");
                continue;
            }

            string trimmed = NormalizeConsoleInputLine(line);
            string low = trimmed.ToLowerInvariant();
            if (low is "exit" or "end" or "quit" or "종료")
                return false;

            if (int.TryParse(trimmed, System.Globalization.NumberStyles.Integer, null, out int n) &&
                n >= 1 && n <= selectable.Count)
            {
                respondent = selectable[n - 1];
                return true;
            }

            var digitsOnly = new string(trimmed.Where(c => c is >= '0' and <= '9').ToArray());
            if (digitsOnly.Length > 0 && digitsOnly.Length <= 3 &&
                int.TryParse(digitsOnly, System.Globalization.NumberStyles.Integer, null, out int n2) &&
                n2 >= 1 && n2 <= selectable.Count)
            {
                respondent = selectable[n2 - 1];
                return true;
            }

            Character? match = selectable.FirstOrDefault(c =>
                (!string.IsNullOrWhiteSpace(c.Id) &&
                 c.Id.Equals(trimmed, StringComparison.OrdinalIgnoreCase)) ||
                c.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                respondent = match;
                return true;
            }

            Console.WriteLine("[시스템] 목록에 없는 선택입니다. 번호 또는 이름/Id를 다시 입력하세요.\n");
        }

        return false;
    }

    /// <summary>ReadConsoleW·콘솔 입력 공통: BOM/제로폭 제거 후 Trim.</summary>
    private static string NormalizeConsoleInputLine(string? line)
    {
        if (string.IsNullOrEmpty(line)) return line ?? "";
        var t = line.Trim().Trim('\uFEFF');
        t = t.Replace("\u200B", "").Replace("\u200C", "").Replace("\u200D", "");
        return t.Trim();
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern bool ReadConsoleW(IntPtr hConsoleInput, [System.Runtime.InteropServices.Out] System.Text.StringBuilder lpBuffer, uint nNumberOfCharsToRead, out uint lpNumberOfCharsRead, IntPtr pReserved);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    private string? ReadConsoleLine()
    {
        try {
            // 1. Windows 환경에서만 ReadConsoleW 시도
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                const int STD_INPUT_HANDLE = -10;
                IntPtr hInput = GetStdHandle(STD_INPUT_HANDLE);
                
                if (hInput != IntPtr.Zero && hInput != new IntPtr(-1))
                {
                    // 버퍼 크기를 넉넉하게 확장 (2048 chars)
                    var sb = new System.Text.StringBuilder(2048);
                    if (ReadConsoleW(hInput, sb, (uint)sb.Capacity, out uint charsRead, IntPtr.Zero) &&
                        charsRead > 0)
                    {
                        // ReadConsoleW는 lpNumberOfCharsRead만큼만 유효. 전체 ToString()은 널 패딩·잔여 문자로
                        // "1" 파싱 실패·동료 선택 오류를 유발할 수 있음.
                        string raw = sb.ToString();
                        int z = raw.IndexOf('\0');
                        if (z >= 0) raw = raw.Substring(0, z);
                        if (raw.Length > (int)charsRead)
                            raw = raw.Substring(0, (int)charsRead);
                        return raw.TrimEnd('\r', '\n', ' ').Trim();
                    }
                }
            }
        } catch {
            // P/Invoke 실패 시 로그 없이 ReadLine으로 폴백
        }

        // 2. 비-Windows 환경이거나 P/Invoke 실패 시 기존 방식(ReadLine) 사용
        return Console.ReadLine();
    }

    private bool HasKorean(string input)
    {
        // 유니코드 한글 범위 (AC00-D7A3) 또는 자모 포함 여부 확인
        return input.Any(c => (c >= 0xAC00 && c <= 0xD7A3) || (c >= 0x3131 && c <= 0x318E));
    }

    private bool IsLikelyMangled(string input)
    {
        return input.Any(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t') || input.Contains('\ufffd');
    }

    private string CleanJsonResponse(string response)
    {
        var cleanJson = response.Trim();
        if (cleanJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) cleanJson = cleanJson.Substring(7);
        else if (cleanJson.StartsWith("```")) cleanJson = cleanJson.Substring(3);
        if (cleanJson.EndsWith("```")) cleanJson = cleanJson.Substring(0, cleanJson.Length - 3);
        
        int firstBrace = cleanJson.IndexOf('{');
        int lastBrace = cleanJson.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            cleanJson = cleanJson.Substring(firstBrace, lastBrace - firstBrace + 1);
        }
        return cleanJson.Trim();
    }

    private static string AllyJsonTryStripTrailingCommas(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return json;
        return Regex.Replace(json.Trim(), @",(\s*[}\]])", "$1");
    }

    /// <summary>동료 2인: 엄격 JSON 실패 시 느슨한 파싱·정규식으로 <c>line</c> 추출.</summary>
    private bool TryParseAllyLineFromModelResponse(string? responseText, out string line)
    {
        line = "";
        if (string.IsNullOrWhiteSpace(responseText)) return false;

        var clean = AllyJsonTryStripTrailingCommas(CleanJsonResponse(responseText));

        try
        {
            using var doc = JsonDocument.Parse(clean);
            if (doc.RootElement.TryGetProperty("line", out var lineProp) &&
                lineProp.ValueKind == JsonValueKind.String)
            {
                line = lineProp.GetString() ?? "";
                return !string.IsNullOrWhiteSpace(line);
            }
        }
        catch { }

        try
        {
            var el = JsonSerializer.Deserialize<JsonElement>(clean, LooseJsonOptions);
            if (el.ValueKind == JsonValueKind.Object &&
                el.TryGetProperty("line", out var lp) &&
                lp.ValueKind == JsonValueKind.String)
            {
                line = lp.GetString() ?? "";
                return !string.IsNullOrWhiteSpace(line);
            }
        }
        catch { }

        try
        {
            var m = Regex.Match(
                responseText,
                @"""line""\s*:\s*(""(?:\\.|[^""\\])*"")",
                RegexOptions.Singleline | RegexOptions.CultureInvariant);
            if (m.Success)
            {
                line = JsonSerializer.Deserialize<string>(m.Groups[1].Value) ?? "";
                return !string.IsNullOrWhiteSpace(line);
            }
        }
        catch { }

        return false;
    }

    private static string ExtractAllyPlainFirstLine(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var s = raw.Trim();
        if (s.StartsWith("```", StringComparison.Ordinal))
        {
            var fence = s.IndexOf('\n');
            if (fence > 0) s = s.Substring(fence + 1).Trim();
            var close = s.LastIndexOf("```", StringComparison.Ordinal);
            if (close >= 0) s = s.Substring(0, close).Trim();
        }

        foreach (var part in s.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = part.Trim();
            if (t.Length == 0) continue;
            if (t.StartsWith('{') && t.Contains("\"line\"", StringComparison.Ordinal))
                continue;
            if (t.Length > 400) t = t.Substring(0, 400).Trim();
            return t;
        }

        return s.Length > 400 ? s.Substring(0, 400).Trim() : s;
    }

    private async Task<Dictionary<string, string>> SynthesizeSocialImpressionsAsync(string episodicBuffer, CancellationToken ct)
    {
        var impressions = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(episodicBuffer)) return impressions;

        string systemPrompt = @"당신은 캐릭터 간의 사회적 관계를 분석하는 관찰자입니다. 결과는 반드시 JSON 형식으로만 출력하세요.";
        string userPrompt = $"[최근 던전 이벤트]\n{episodicBuffer}";

        string? response = await _ollama.GenerateResponseAsync(systemPrompt, userPrompt, ct);
        if (string.IsNullOrEmpty(response)) return impressions;

        var cleanJson = CleanJsonResponse(response);

        try {
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(cleanJson);
            if (data != null) impressions = data;
        } catch { }

        return impressions;
    }

    private async Task UpdateSocialSettlementAsync(CancellationToken ct, string? dialogueHistoryOverride)
    {
        Console.WriteLine("[시스템] 대화 내용을 바탕으로 관계 변화를 분석 중...");
        string history = dialogueHistoryOverride ?? _memory.GetWorkingMemoryContext();
        
        string systemPrompt =
            "당신은 캐릭터 간의 사회적 상호작용을 분석하는 심리 전문가입니다.\n" +
            "반드시 **객체 하나(JSON object)**만 출력하세요. 다른 설명 금지.\n" +
            "키 형식: `화자Id->대상Id` (영문 Id만, 화살표 -> 포함). 값은 객체 `{ \"affinity\": 정수, \"trust\": 정수 }`.\n" +
            "affinity/trust는 **이번 대화로 인한 변화량**(델타, -5~+5 권장). 변화 없으면 0.\n" +
            "예시: {\"kyle->rina\":{\"affinity\":0,\"trust\":1}}\n" +
            "대화에 등장한 관계만 넣으세요. 존재하지 않는 Id를 만들지 마세요.";

        string userPrompt = $"[최근 대화 내역]\n{history}";

        string? response = await _ollama.GenerateResponseAsync(systemPrompt, userPrompt, ct);
        if (string.IsNullOrEmpty(response)) return;

        try {
            string cleanJson = CleanJsonResponse(response);
            int applied = TryApplySocialSettlementDeltasFromJson(cleanJson);
            if (applied > 0)
            {
                _loader.SaveCharactersDatabase(_characters);
                Console.WriteLine("[시스템] 관계 수치가 CharactersDatabase.json(또는 Characters.json)에 저장되었습니다.\n");
            }
            else
                Console.WriteLine("[시스템] 관계 변화로 반영할 유효한 항목이 없었습니다. (모델 JSON 형식 확인)\n");
        } catch (Exception ex) {
            Console.WriteLine($"[시스템] 관계 분석 처리 중 오류: {ex.Message}");
        }
    }

    /// <summary>Ollama가 살짝 어긋난 JSON을 내도 델타를 최대한 반영합니다.</summary>
    private int TryApplySocialSettlementDeltasFromJson(string cleanJson)
    {
        using var doc = JsonDocument.Parse(cleanJson);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return 0;

        int appliedEdges = 0;
        foreach (var prop in root.EnumerateObject())
        {
            var key = prop.Name.Trim();
            var ids = key.Split("->", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (ids.Length != 2) continue;

            var fromId = ids[0];
            var toId = ids[1];
            var speaker = _characters.FirstOrDefault(c => c.Id.Equals(fromId, StringComparison.OrdinalIgnoreCase));
            var rel = speaker?.Relationships?.FirstOrDefault(r =>
                r.TargetId.Equals(toId, StringComparison.OrdinalIgnoreCase));
            if (rel == null) continue;

            if (!TryReadAffinityTrustDeltas(prop.Value, out int aDelta, out int tDelta)) continue;
            if (aDelta == 0 && tDelta == 0) continue;

            rel.Affinity = Math.Clamp(rel.Affinity + aDelta, 0, 100);
            rel.Trust = Math.Clamp(rel.Trust + tDelta, 0, 100);
            appliedEdges++;
            Console.WriteLine(
                $"  [정산] {speaker!.Name} -> {toId}: 친밀도({(aDelta >= 0 ? "+" : "")}{aDelta}), 신뢰도({(tDelta >= 0 ? "+" : "")}{tDelta})");
        }

        return appliedEdges;
    }

    private static bool TryReadAffinityTrustDeltas(JsonElement value, out int affinityDelta, out int trustDelta)
    {
        affinityDelta = 0;
        trustDelta = 0;
        if (value.ValueKind != JsonValueKind.Object) return false;

        foreach (var p in value.EnumerateObject())
        {
            var n = p.Name.Trim().ToLowerInvariant();
            if (n is "affinity" or "친밀" or "친밀도")
            {
                if (TryReadIntLoose(p.Value, out int x)) affinityDelta = x;
            }
            else if (n is "trust" or "신뢰" or "신뢰도")
            {
                if (TryReadIntLoose(p.Value, out int x)) trustDelta = x;
            }
        }

        return true;
    }

    private static bool TryReadIntLoose(JsonElement el, out int v)
    {
        v = 0;
        switch (el.ValueKind)
        {
            case JsonValueKind.Number:
                return el.TryGetInt32(out v);
            case JsonValueKind.String:
                return int.TryParse(el.GetString(), System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out v);
            default:
                return false;
        }
    }
}
