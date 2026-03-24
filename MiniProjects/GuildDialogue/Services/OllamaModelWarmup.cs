using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>Hub 첫 로드 시 Ollama가 모델을 메모리에 올리는 시간을 확보하기 위한 최소 호출.</summary>
public static class OllamaModelWarmup
{
    private const string KeepAlive = "30m";

    public sealed record WarmupPhase(string Id, string Model, bool Ok, long Ms, string? Detail, bool Skipped);

    public sealed record WarmupResult(bool Ok, string? Error, IReadOnlyList<WarmupPhase> Phases, long TotalMs);

    public static string ResolveChatModel(DialogueSettings settings) => (settings.Ollama.Model ?? "").Trim();

    public static string ResolveEmbedModel(DialogueSettings settings)
    {
        var chat = ResolveChatModel(settings);
        return string.IsNullOrWhiteSpace(settings.Ollama.EmbeddingModel)
            ? chat
            : settings.Ollama.EmbeddingModel!.Trim();
    }

    public static async Task<WarmupPhase> WarmupChatAsync(DialogueSettings settings, CancellationToken ct = default)
    {
        var baseUrl = settings.Ollama.BaseUrl.TrimEnd('/');
        var chatModel = ResolveChatModel(settings);
        if (string.IsNullOrEmpty(chatModel))
            return new WarmupPhase("chat", chatModel, false, 0, "DialogueSettings.json의 Ollama.Model이 비어 있습니다.", false);

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        var sw = Stopwatch.StartNew();
        try
        {
            await WarmupGenerateMinimalAsync(http, baseUrl, chatModel, ct).ConfigureAwait(false);
            return new WarmupPhase("chat", chatModel, true, sw.ElapsedMilliseconds, null, false);
        }
        catch (Exception ex)
        {
            return new WarmupPhase("chat", chatModel, false, sw.ElapsedMilliseconds, ex.Message, false);
        }
    }

    public static async Task<WarmupPhase> WarmupEmbedAsync(DialogueSettings settings, CancellationToken ct = default)
    {
        var chatModel = ResolveChatModel(settings);
        var embedModel = ResolveEmbedModel(settings);
        var embedDiffers = !string.Equals(chatModel, embedModel, StringComparison.OrdinalIgnoreCase);
        if (!embedDiffers)
            return new WarmupPhase("embed", embedModel, true, 0, "대화 모델과 동일 — 별도 로드 생략", true);

        var sw = Stopwatch.StartNew();
        try
        {
            using var embedClient = new OllamaEmbeddingClient(settings);
            var vec = await embedClient.EmbedAsync("warmup", ct).ConfigureAwait(false);
            var ok = vec is { Length: > 0 };
            return new WarmupPhase(
                "embed",
                embedModel,
                ok,
                sw.ElapsedMilliseconds,
                ok ? null : "빈 벡터",
                false);
        }
        catch (Exception ex)
        {
            return new WarmupPhase("embed", embedModel, false, sw.ElapsedMilliseconds, ex.Message, false);
        }
    }

    public static async Task<WarmupResult> RunAsync(DialogueSettings settings, CancellationToken ct = default)
    {
        var phases = new List<WarmupPhase>();
        var totalSw = Stopwatch.StartNew();
        var chat = await WarmupChatAsync(settings, ct).ConfigureAwait(false);
        phases.Add(chat);
        if (!chat.Ok)
            return new WarmupResult(false, chat.Detail, phases, totalSw.ElapsedMilliseconds);

        var embed = await WarmupEmbedAsync(settings, ct).ConfigureAwait(false);
        phases.Add(embed);
        if (!embed.Ok && !embed.Skipped)
            return new WarmupResult(false, embed.Detail ?? "임베딩 모델 워밍업에 실패했습니다.", phases, totalSw.ElapsedMilliseconds);

        return new WarmupResult(true, null, phases, totalSw.ElapsedMilliseconds);
    }

    private static async Task WarmupGenerateMinimalAsync(HttpClient http, string baseUrl, string model, CancellationToken ct)
    {
        var url = baseUrl + "/api/generate";
        var payload = new Dictionary<string, object>
        {
            ["model"] = model,
            ["prompt"] = ".",
            ["stream"] = false,
            ["keep_alive"] = KeepAlive,
            ["options"] = new Dictionary<string, object> { ["num_predict"] = 1 }
        };
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var resp = await http.PostAsync(url, content, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }
}
