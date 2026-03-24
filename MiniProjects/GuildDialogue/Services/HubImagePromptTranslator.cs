using System.Text;
using System.Text.RegularExpressions;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// Translates image prompt source text to English using Ollama.
/// </summary>
public sealed class HubImagePromptTranslator
{
    private readonly OllamaClient _ollama;
    private readonly Dictionary<string, string> _cache = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    public HubImagePromptTranslator(DialogueSettings settings)
    {
        _ollama = new OllamaClient(settings);
    }

    public async Task<string> TranslateToEnglishAsync(string source, CancellationToken ct)
    {
        var input = (source ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Fast path: already English-ish.
        if (!Regex.IsMatch(input, "[가-힣]"))
            return ToAsciiSingleLine(input);

        lock (_lock)
        {
            if (_cache.TryGetValue(input, out var cached))
                return cached;
        }

        var systemPrompt =
            "당신은 게임 아트 프롬프트 번역 전문가입니다.\n" +
            "한국어 원문을 이미지 생성용 간결한 영어 프롬프트로 번역하세요.\n" +
            "규칙:\n" +
            "- 출력은 영어만 사용하세요.\n" +
            "- 핵심 개체, 역할, 분위기, 장면 정보를 유지하세요.\n" +
            "- 설명, 라벨, 마크다운, 따옴표를 추가하지 마세요.\n" +
            "- 한 줄의 일반 텍스트만 출력하세요.";

        var userPrompt = $"원문:\n{input}\n\n영어 프롬프트 텍스트만 출력하세요.";

        var translated = await _ollama.GenerateResponseAsync(systemPrompt, userPrompt, ct).ConfigureAwait(false);
        var final = ToAsciiSingleLine(string.IsNullOrWhiteSpace(translated) ? input : translated);

        if (string.IsNullOrWhiteSpace(final))
            final = "fantasy guild scene";

        lock (_lock)
            _cache[input] = final;
        return final;
    }

    private static string ToAsciiSingleLine(string text)
    {
        var line = (text ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        line = Regex.Replace(line, @"\s+", " ");
        line = line.Normalize(NormalizationForm.FormKD);
        line = Regex.Replace(line, @"[^\x20-\x7E]", " ");
        line = Regex.Replace(line, @"\s+", " ").Trim();
        return line;
    }
}
