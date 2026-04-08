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
    private readonly double _temperature;
    private readonly double _topP;
    private readonly int _numPredict;

    public OllamaClient(DialogueSettings settings)
    {
        _http = new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(settings.Ollama.TimeoutSeconds);
        _modelName = settings.Ollama.Model;
        _apiUrl = settings.Ollama.BaseUrl.TrimEnd('/') + "/api/generate";
        _temperature = settings.Ollama.Temperature;
        _topP = settings.Ollama.TopP;
        _numPredict = settings.Ollama.NumPredict;
    }

    public async Task<string?> GenerateResponseAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var finalPrompt = $"{systemPrompt}\n\n{userPrompt}";

        var options = new Dictionary<string, object>
        {
            ["temperature"] = _temperature,
            ["top_p"] = _topP
        };
        if (_numPredict > 0)
            options["num_predict"] = _numPredict;

        var requestBody = new Dictionary<string, object>
        {
            ["model"] = _modelName,
            ["prompt"] = finalPrompt,
            ["stream"] = false,
            ["keep_alive"] = "30m",
            ["options"] = options
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
