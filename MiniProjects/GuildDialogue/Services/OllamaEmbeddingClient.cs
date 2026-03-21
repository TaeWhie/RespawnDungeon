using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>Ollama /api/embeddings 호출.</summary>
public sealed class OllamaEmbeddingClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _model;

    public OllamaEmbeddingClient(DialogueSettings settings)
    {
        _baseUrl = settings.Ollama.BaseUrl.TrimEnd('/');
        _model = string.IsNullOrWhiteSpace(settings.Ollama.EmbeddingModel)
            ? settings.Ollama.Model
            : settings.Ollama.EmbeddingModel!;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Max(30, settings.Ollama.TimeoutSeconds)) };
    }

    public string Model => _model;

    public async Task<float[]?> EmbedAsync(string prompt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return null;

        var url = _baseUrl + "/api/embeddings";
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var body = JsonSerializer.Serialize(new { model = _model, prompt = prompt.Trim() });
                using var content = new StringContent(body, Encoding.UTF8, "application/json");
                using var response = await _http.PostAsync(url, content, ct).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(json);
                    if (!doc.RootElement.TryGetProperty("embedding", out var emb))
                        return null;
                    return emb.EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
                }

                var code = (int)response.StatusCode;
                if (attempt == 0 && code is >= 500 and < 600)
                {
                    await Task.Delay(350, ct).ConfigureAwait(false);
                    continue;
                }

                Console.WriteLine($"[Embedding Error] HTTP {(int)response.StatusCode} — 임베딩 가드레일·RAG가 약해질 수 있습니다.");
                return null;
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Embedding Error] {ex.Message}");
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}
