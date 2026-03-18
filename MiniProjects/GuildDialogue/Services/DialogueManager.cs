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
    
    private List<Character> _characters = new();
    private Dictionary<string, Character> _charMap = new();

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
        
        // 대화 순번 (턴제 진행) - 첫 턴부터 LLM이 동적 생성
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

            Console.WriteLine($"[시스템] {turn.Speaker.Name}의 응답을 생성하는 중... (Lorebook & 메모리 검색)");
            
            // RAG Context 병합
            string workingMem = _memory.GetWorkingMemoryContext();
            string archivalMem = _memory.RetrieveArchivalMemoryContext(workingMem);

            string sysPrompt = PromptBuilder.BuildSystemPrompt(turn.Speaker, turn.Listener, worldState, _memory.EpisodicBuffer, archivalMem, _settings);
            string userPrompt = PromptBuilder.BuildUserPrompt(workingMem, isFirstTurn ? randomTopic : null);
            isFirstTurn = false;

            string? responseText = await _ollama.GenerateResponseAsync(sysPrompt, userPrompt, ct);
            
            if (string.IsNullOrWhiteSpace(responseText))
            {
                Console.WriteLine("응답 생성 실패.");
                break;
            }

            // JSON 파싱 전 마크다운 코드블록 제거
            var cleanJson = responseText.Trim();
            if (cleanJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) 
                cleanJson = cleanJson.Substring(7);
            else if (cleanJson.StartsWith("```")) 
                cleanJson = cleanJson.Substring(3);
            if (cleanJson.EndsWith("```")) 
                cleanJson = cleanJson.Substring(0, cleanJson.Length - 3);
            cleanJson = cleanJson.Trim();

            try {
                using var doc = JsonDocument.Parse(cleanJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("line", out var lineProp))
                {
                    string generatedLine = lineProp.GetString() ?? "";
                    var intent = root.TryGetProperty("intent", out var intProp) ? intProp.GetString() : "";
                    
                    Console.WriteLine($"[{turn.Speaker.Name}] {generatedLine}");
                    Console.WriteLine($"  -> (Intent: {intent})\n");
                    
                    _memory.AddWorkingMemory(turn.Speaker.Name, generatedLine);
                }
                else {
                    Console.WriteLine($"파싱 에러: {responseText}\n");
                }
            }
            catch {
                Console.WriteLine($"JSON 규격 외 응답 발생:\n{responseText}\n");
            }
        }
        
        Console.WriteLine("========== 대화 세션 종료 ==========");
    }
}
