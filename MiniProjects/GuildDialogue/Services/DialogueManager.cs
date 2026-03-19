using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

public class DialogueManager
{
    private readonly DialogueConfigLoader _loader;
    private readonly OllamaClient _ollama;
    private readonly MemoryManager _memory;
    private readonly DialogueSettings _settings;
    private WorldLore? _worldLore;
    private Dictionary<string, ItemData> _itemDb = new();
    
    private List<Character> _characters = new();
    private Dictionary<string, Character> _charMap = new();
    private Dictionary<string, string> _lastLineByChar = new();
    private Dictionary<string, string> _currentImpressions = new();

    public DialogueManager()
    {
        _loader = new DialogueConfigLoader();
        _settings = _loader.LoadSettings();
        _ollama = new OllamaClient(_settings);
        
        var glossary = _loader.LoadLogGlossary();
        _memory = new MemoryManager(glossary);
    }

    public async Task InitializeAsync()
    {
        _characters = _loader.LoadCharacters();
        _charMap = _characters.ToDictionary(c => c.Name);
        _worldLore = _loader.LoadWorldLore();
        _itemDb = _loader.LoadItemDatabase()
            .ToDictionary(i => i.ItemName, StringComparer.OrdinalIgnoreCase);
        
        var testData = _loader.LoadTestData();
        if (testData?.ActionLog != null)
        {
            var parsedLogs = ActionLogBuilder.Build(testData.ActionLog);
            var allDungeonLogs = parsedLogs.DungeonLogsByCharacter.SelectMany(kvp => kvp.Value).Distinct().ToList();
            _memory.BuildEpisodicBuffer(allDungeonLogs);
        }
    }

    public async Task RunInteractiveSessionAsync(CancellationToken ct = default)
    {
        Console.WriteLine("========== RAG 기반 지능형 길드 대화 세션 ==========\n");
        if (_characters.Count < 2)
        {
            Console.WriteLine("캐릭터 데이터가 충분하지 않습니다.");
            return;
        }

        var kyle = _charMap.GetValueOrDefault("카일");
        var rena = _charMap.GetValueOrDefault("리나");
        var bram = _charMap.GetValueOrDefault("브람");

        if (kyle == null || rena == null || bram == null)
        {
            Console.WriteLine("필수 캐릭터(카일, 리나, 브람)를 찾을 수 없습니다.");
            return;
        }

        string worldState = "장소: 길드 아지트 식당\n시간: 저녁\n함께 있는 동료: 카일, 리나, 브람\n상황: 던전 탐험을 무사히 마치고 식사하며 휴식 중입니다.";
        
        Console.WriteLine("[시스템] 최근 사건을 바탕으로 사회적 인상을 합성 중...");
        _currentImpressions = await SynthesizeSocialImpressionsAsync(_memory.EpisodicBuffer, ct);
        
        var conversationFlow = new[]
        {
            (Speaker: kyle, Listener: rena),
            (Speaker: rena, Listener: kyle),
            (Speaker: bram, Listener: rena),
            (Speaker: kyle, Listener: bram),
            (Speaker: rena, Listener: bram),
            (Speaker: bram, Listener: kyle)
        };

        bool isFirstTurn = true;
        string? randomTopic = _memory.GetRandomPastEvent();

        foreach (var turn in conversationFlow)
        {
            if (ct.IsCancellationRequested) break;

            Console.WriteLine($"[시스템] {turn.Speaker.Name}의 응답을 생성하는 중...");
            
            string workingMem = _memory.GetWorkingMemoryContext();
            string archivalMem = _memory.RetrieveArchivalMemoryContext(workingMem);

            int turnIndex = Array.IndexOf(conversationFlow, turn);
            bool isFinalTurn = (turnIndex == conversationFlow.Length - 1);
            string? lastLine = _lastLineByChar.GetValueOrDefault(turn.Speaker.Name);
            string? currentImpression = _currentImpressions.GetValueOrDefault($"{turn.Speaker.Id}->{turn.Listener.Id}");

            string sysPrompt = PromptBuilder.BuildSystemPrompt(turn.Speaker, turn.Listener, worldState, _memory.EpisodicBuffer, archivalMem, _settings, _worldLore, _itemDb, lastLine, currentImpression, false);
            string userPrompt = PromptBuilder.BuildUserPrompt(workingMem, isFirstTurn ? randomTopic : null, isFinalTurn, turnIndex);
            isFirstTurn = false;

            string? responseText = await _ollama.GenerateResponseAsync(sysPrompt, userPrompt, ct);
            
            if (string.IsNullOrWhiteSpace(responseText)) break;

            var cleanJson = CleanJsonResponse(responseText);

            try {
                using var doc = JsonDocument.Parse(cleanJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("line", out var lineProp))
                {
                    string generatedLine = lineProp.GetString() ?? "";
                    Console.WriteLine($"[{turn.Speaker.Name}] {generatedLine}\n");
                    
                    _memory.AddWorkingMemory(turn.Speaker.Name, generatedLine);
                    _lastLineByChar[turn.Speaker.Name] = generatedLine;
                }
            }
            catch { }
        }
        
        Console.WriteLine("========== 대화 세션 종료 ==========");
        await UpdateSocialSettlementAsync(ct);
    }

    public async Task RunGuildMasterSessionAsync(CancellationToken ct = default)
    {
        Console.WriteLine("========== 길드장 집무실: 동료들과의 대화 ==========");
        Console.WriteLine("(팁: @kyle @rina @bram 태그로 대화 상대를 지정할 수 있습니다.)");
        Console.WriteLine("(팁: 대화를 마치려면 'exit' 또는 'end'를 입력하세요.)\n");

        if (_characters.Count == 0) return;

        var masterChar = new Character { Id = "master", Name = "길드장", Role = "Guild Master" };
        string worldState = "장소: 길드장 집무실\n상황: 던전 탐험을 마친 동료들이 길드장에게 보고하거나 대화를 나누러 왔습니다.";

        while (!ct.IsCancellationRequested)
        {
            Console.Write("[길드장] ");
            string? userInput = ReadConsoleLine();

            if (string.IsNullOrWhiteSpace(userInput)) continue;
            
            string normalized = userInput.Trim().ToLower();
            if (normalized == "exit" || normalized == "end" || normalized == "quit" || normalized == "종료") break;

            _memory.AddWorkingMemory("길드장", userInput);

            Character respondent = PickRespondent(userInput);
            
            Console.WriteLine($"[시스템] {respondent.Name}이(가) 생각을 정리 중...");

            string workingMem = _memory.GetWorkingMemoryContext();
            string archivalMem = _memory.RetrieveArchivalMemoryContext(workingMem);
            string? lastLine = _lastLineByChar.GetValueOrDefault(respondent.Name);
            string? currentImpression = _currentImpressions.GetValueOrDefault($"{respondent.Id}->master");

            string sysPrompt = PromptBuilder.BuildSystemPrompt(respondent, masterChar, worldState, _memory.EpisodicBuffer, archivalMem, _settings, _worldLore, _itemDb, lastLine, currentImpression, true);
            string userPrompt = PromptBuilder.BuildUserPrompt(workingMem, null, false, 0);

            try {
                string? responseText = await _ollama.GenerateResponseAsync(sysPrompt, userPrompt, ct);
                if (string.IsNullOrWhiteSpace(responseText)) continue;

                var cleanJson = CleanJsonResponse(responseText);

                using var doc = JsonDocument.Parse(cleanJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("line", out var lineProp))
                {
                    string generatedLine = lineProp.GetString() ?? "";
                    Console.WriteLine($"[{respondent.Name}] {generatedLine}\n");
                    
                    _memory.AddWorkingMemory(respondent.Name, generatedLine);
                    _lastLineByChar[respondent.Name] = generatedLine;
                }
            } catch (Exception ex) {
                Console.WriteLine($"[오류] 응답 생성 또는 처리 중 문제가 발생했습니다: {ex.Message}");
            }
        }

        Console.WriteLine("========== 상호작용 세션 종료 ==========");
        await UpdateSocialSettlementAsync(ct);
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
                    if (ReadConsoleW(hInput, sb, (uint)sb.Capacity, out uint charsRead, IntPtr.Zero))
                    {
                        string result = sb.ToString();
                        // 개행 문자 처리 (\r\n 제거)
                        return result.Replace("\r", "").Replace("\n", "");
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

    private Character PickRespondent(string userInput)
    {
        string normalized = userInput.ToLower();
        
        if (normalized.Contains("@kyle")) return _characters.FirstOrDefault(c => c.Id == "kyle") ?? _characters[0];
        if (normalized.Contains("@rina")) return _characters.FirstOrDefault(c => c.Id == "rina") ?? _characters[0];
        if (normalized.Contains("@bram")) return _characters.FirstOrDefault(c => c.Id == "bram") ?? _characters[0];

        if (normalized.StartsWith("1")) return _characters[0];
        if (normalized.StartsWith("2") && _characters.Count > 1) return _characters[1];
        if (normalized.StartsWith("3") && _characters.Count > 2) return _characters[2];

        foreach (var c in _characters)
        {
            if (userInput.IndexOf(c.Name, StringComparison.OrdinalIgnoreCase) >= 0) return c;
        }

        var random = new Random();
        return _characters[random.Next(_characters.Count)];
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

    private async Task UpdateSocialSettlementAsync(CancellationToken ct)
    {
        Console.WriteLine("[시스템] 대화 내용을 바탕으로 관계 변화를 분석 중...");
        string history = _memory.GetWorkingMemoryContext();
        
        string systemPrompt = @"당신은 캐릭터 간의 사회적 상호작용을 분석하는 심리 전문가입니다. {A->B: {affinity: X, trust: Y}} 형식의 JSON으로만 응답하세요.";
        string userPrompt = $"[최근 대화 내역]\n{history}";

        string? response = await _ollama.GenerateResponseAsync(systemPrompt, userPrompt, ct);
        if (string.IsNullOrEmpty(response)) return;

        try {
            string cleanJson = CleanJsonResponse(response);
            
            // Try flexible deserialization
            var options = new JsonSerializerOptions 
            { 
                AllowTrailingCommas = true, 
                PropertyNameCaseInsensitive = true,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
            };
            var deltas = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(cleanJson, options);
            
            if (deltas != null)
            {
                foreach (var kvp in deltas)
                {
                    var ids = kvp.Key.Split("->", StringSplitOptions.TrimEntries);
                    if (ids.Length != 2) continue;
                    
                    var speaker = _characters.FirstOrDefault(c => c.Id.Equals(ids[0], StringComparison.OrdinalIgnoreCase));
                    var rel = speaker?.Relationships?.FirstOrDefault(r => r.TargetId.Equals(ids[1], StringComparison.OrdinalIgnoreCase));
                    
                    if (rel != null)
                    {
                        if (kvp.Value.TryGetValue("affinity", out int aDelta)) rel.Affinity = Math.Clamp(rel.Affinity + aDelta, 0, 100);
                        if (kvp.Value.TryGetValue("trust", out int tDelta)) rel.Trust = Math.Clamp(rel.Trust + tDelta, 0, 100);
                        
                        if (aDelta != 0 || tDelta != 0)
                            Console.WriteLine($"  [정산] {speaker!.Name} -> {ids[1]}: 친밀도({(aDelta >= 0 ? "+" : "")}{aDelta}), 신뢰도({(tDelta >= 0 ? "+" : "")}{tDelta})");
                    }
                }
                _loader.SaveCharacters(_characters);
                Console.WriteLine("[시스템] 관계 수치가 업데이트되어 저장되었습니다.\n");
            }
        } catch (Exception ex) {
            Console.WriteLine($"[시스템] 관계 분석 처리 중 오류: {ex.Message}");
        }
    }
}
