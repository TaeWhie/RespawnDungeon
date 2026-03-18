using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// 한 턴 흐름: RAG 컨텍스트 구성 → LLM 호출(또는 폴백) → 파싱 실패/반복/주제 이탈 시 리트라이 → 메모리 갱신.
/// 모든 수치·목록은 설정에서 로드(하드 코딩 없음).
/// </summary>
public class DialogueManager
{
    private readonly DialogueSettings _settings;
    private readonly PromptBuilder _promptBuilder;
    private readonly OllamaClient? _ollama;
    private readonly MemoryManager _memoryManager;
    private readonly List<DialogueTurn> _sessionTurns = new();
    private LongTermSummary _longTerm = new();

    public DialogueManager(DialogueSettings settings, bool useOllama = true)
    {
        _settings = settings;
        _promptBuilder = new PromptBuilder(settings);
        _ollama = useOllama ? new OllamaClient(settings) : null;
        _memoryManager = new MemoryManager(settings);
    }

    public IReadOnlyList<DialogueTurn> SessionTurns => _sessionTurns;
    public LongTermSummary LongTerm => _longTerm;

    public void SetLongTerm(LongTermSummary summary) => _longTerm = summary;
    public void SetDungeonLogs(string speakerId, List<DungeonLog> logs) => _dungeonLogsBySpeaker[speakerId] = logs;
    private readonly Dictionary<string, List<DungeonLog>> _dungeonLogsBySpeaker = new();

    public async Task<LlmResponse> GenerateTurnAsync(
        Character speaker,
        Character? other,
        string otherId,
        string lastUtterance,
        string recentEvent,
        string currentSituation,
        List<Character> allCharacters,
        bool isLastTurn = false,
        CancellationToken ct = default)
    {
        var request = new DialogueRequest
        {
            SpeakerId = speaker.Id,
            OtherId = otherId,
            LastUtterance = lastUtterance,
            RecentEvent = recentEvent,
            CurrentSituation = currentSituation,
            Speaker = speaker,
            OtherCharacter = other,
            DungeonLogs = _dungeonLogsBySpeaker.GetValueOrDefault(speaker.Id, new List<DungeonLog>()),
            LongTerm = _longTerm,
            RecentTurns = _sessionTurns.Take(_settings.MaxRecentTurns).ToList(),
            IsLastTurn = isLastTurn
        };

        var prompt = _promptBuilder.BuildPrompt(request);
        LlmResponse? response = null;

        // 간단한 필터만 둔 상태에서 LLM 응답을 사용한다.
        if (_ollama != null)
        {
            try
            {
                response = await _ollama.GenerateAsync(prompt, ct).ConfigureAwait(false);
            }
            catch
            {
                response = null; // 연결 실패 등 시 폴백
            }
        }

        if (response == null || string.IsNullOrWhiteSpace(response.Line))
        {
            response = Fallback(speaker, lastUtterance, recentEvent);
        }
        else if (IsRepeat(speaker.Id, response.Line))
        {
            // 같은 주제가 반복될 때는 대화를 마무리하는 한 마디로 정리해 준다.
            response = new LlmResponse
            {
                Tone = response.Tone,
                Intent = "wrap_up",
                Line = "오늘 이야기는 여기까지 하자. 다음에 다른 얘기도 해보자.",
                InnerThought = response.InnerThought
            };
        }

        _sessionTurns.Add(new DialogueTurn { SpeakerId = speaker.Id, Text = response.Line });
        _memoryManager.ApplyOffsetFromLine(response.Line, response.Intent, _longTerm);
        _memoryManager.UpdateSummarySentence(_longTerm);

        return response;
    }

    private bool IsRepeat(string speakerId, string line)
    {
        if (_sessionTurns.Count == 0) return false;

        // 직전 전체 발화와 동일하면 반복으로 간주
        var last = _sessionTurns[^1].Text;
        if (string.Equals(line.Trim(), last.Trim(), StringComparison.Ordinal))
            return true;

        // 같은 화자가 직전에 했던 말과도 비교
        var lastBySpeaker = _sessionTurns.LastOrDefault(t => t.SpeakerId == speakerId)?.Text;
        if (!string.IsNullOrEmpty(lastBySpeaker) &&
            string.Equals(line.Trim(), lastBySpeaker.Trim(), StringComparison.Ordinal))
            return true;

        var minLen = _settings.KeywordExtractMinLength;
        var words = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= minLen).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var lastWords = last.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= minLen).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 같은 화자의 마지막 발화 단어들도 포함해서 비교
        if (!string.IsNullOrEmpty(lastBySpeaker))
        {
            var speakerWords = lastBySpeaker.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= minLen).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var w in speakerWords) lastWords.Add(w);
        }

        return words.Count > 0 && words.Overlaps(lastWords) && words.All(w => lastWords.Contains(w));
    }

    private bool IsTopicDrift(string line, DialogueRequest request)
    {
        var allowed = _settings.AllowedTopicKeywords.Select(x => x.Trim().ToLowerInvariant()).ToHashSet();
        var context = (request.LastUtterance + " " + request.RecentEvent + " " + request.CurrentSituation).ToLowerInvariant();
        var minLen = _settings.KeywordExtractMinLength;
        var lineWords = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= minLen).Select(w => w.Trim().ToLowerInvariant()).ToList();
        if (lineWords.Count == 0) return false;
        var topWords = lineWords.Take(3).ToHashSet();
        if (topWords.Any(w => allowed.Contains(w))) return false;
        if (topWords.Any(w => context.Contains(w))) return false;
        return true;
    }

    private LlmResponse Fallback(Character speaker, string lastUtterance, string recentEvent)
    {
        // 나중에 실제 폴백 대사를 구현하기 전까지는,
        // 그냥 "fallback" 이라는 고정 텍스트만 내려준다.
        return new LlmResponse
        {
            Tone = "fallback",
            Intent = "fallback",
            Line = "fallback",
            InnerThought = ""
        };
    }

    public void ClearSession() => _sessionTurns.Clear();
}
