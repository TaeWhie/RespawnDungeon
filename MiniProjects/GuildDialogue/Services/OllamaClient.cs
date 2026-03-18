using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

public class OllamaClient
{
    private readonly HttpClient _http;
    private readonly string _modelName;
    private readonly string _apiUrl;

    public OllamaClient(DialogueSettings settings)
    {
        _http = new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(settings.Ollama.TimeoutSeconds);
        _modelName = settings.Ollama.Model;
        _apiUrl = settings.Ollama.BaseUrl.TrimEnd('/') + "/api/generate";
    }

    public async Task<string?> GenerateResponseAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var finalPrompt = $"{systemPrompt}\n\n{userPrompt}";
        
        var requestBody = new
        {
            model = _modelName,
            prompt = finalPrompt,
            stream = false,
            options = new { temperature = 0.7, top_p = 0.9 }
        };

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var content = new StringContent(JsonSerializer.Serialize(requestBody, jsonOptions), Encoding.UTF8, "application/json");

        try
        {
            var response = await _http.PostAsync(_apiUrl, content, ct);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("response", out var respProp))
            {
                var text = respProp.GetString();
                return text?.Trim();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Ollama Error] {ex.Message}");
        }

        return null;
    }
}
