using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

public class OllamaClient
{
    private readonly DialogueSettings _settings;
    private readonly HttpClient _http;

    public OllamaClient(DialogueSettings settings)
    {
        _settings = settings;
        _http = new HttpClient
        {
            BaseAddress = new Uri(_settings.Ollama.BaseUrl.TrimEnd('/')),
            Timeout = TimeSpan.FromSeconds(_settings.Ollama.TimeoutSeconds)
        };
    }

    public async Task<LlmResponse?> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        // gemma3:4b는 chat 중심 모델이라 /api/chat 엔드포인트를 사용한다.
        var payload = new
        {
            model = _settings.Ollama.Model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            stream = false
        };

        var res = await _http.PostAsJsonAsync("/api/chat", payload, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
            return null; // 404(모델 없음), 500 등 시 폴백으로 넘김

        var jsonText = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(jsonText)) return null;

        try
        {
            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;

            // /api/chat 기본 응답: { "message": { "role": "...", "content": "..." }, ... }
            if (root.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var contentProp))
            {
                var content = contentProp.GetString() ?? "";
                if (!string.IsNullOrWhiteSpace(content))
                {
                    // content 안에 ```json 코드 블록 + JSON 객체가 들어오는 현재 패턴을 처리한다.
                    var parsed = ParseJsonResponse(content);
                    if (parsed != null && !string.IsNullOrWhiteSpace(parsed.Line))
                        return parsed;

                    // JSON 형태가 아니면 전체를 한 줄로 사용
                    return new LlmResponse
                    {
                        Tone = "raw",
                        Intent = "raw",
                        Line = content.Trim(),
                        InnerThought = ""
                    };
                }
            }

            // 혹시 response 필드를 쓰는 모델일 경우를 대비한 보조 처리
            if (root.TryGetProperty("response", out var responseProp))
            {
                var content = responseProp.GetString() ?? "";
                if (!string.IsNullOrWhiteSpace(content))
                {
                    var parsed = ParseJsonResponse(content);
                    if (parsed != null && !string.IsNullOrWhiteSpace(parsed.Line))
                        return parsed;

                    return new LlmResponse
                    {
                        Tone = "raw",
                        Intent = "raw",
                        Line = content.Trim(),
                        InnerThought = ""
                    };
                }
            }
        }
        catch
        {
            // JSON 파싱 실패 시 폴백 처리로 넘긴다.
            return null;
        }

        return null;
    }

    public static LlmResponse? ParseJsonResponse(string text)
    {
        // ```json ... ``` 코드 블록이 있으면 내부만 추출
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewLine = trimmed.IndexOf('\n');
            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewLine >= 0 && lastFence > firstNewLine)
            {
                trimmed = trimmed.Substring(firstNewLine + 1, lastFence - firstNewLine - 1).Trim();
            }
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;
            var line = root.TryGetProperty("line", out var l) ? l.GetString() ?? "" : "";
            var tone = root.TryGetProperty("tone", out var t) ? t.GetString() ?? "" : "";
            var intent = root.TryGetProperty("intent", out var i) ? i.GetString() ?? "" : "";
            var inner = root.TryGetProperty("innerThought", out var th) ? th.GetString() ?? "" : "";

            if (string.IsNullOrWhiteSpace(line))
                return null;

            return new LlmResponse
            {
                Tone = tone,
                Intent = intent,
                Line = line,
                InnerThought = inner
            };
        }
        catch
        {
            return null;
        }
    }
}
