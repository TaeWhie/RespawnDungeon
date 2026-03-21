using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// 길드장 발화 다양성 탐색: 변칙 분류 + 원정(두꺼운 로그) 플래그. <c>dotnet run -- --explore-guild-office</c>
/// </summary>
public static class GuildOfficeExplorationRunner
{
    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static async Task<int> RunAsync(CancellationToken ct)
    {
        var loader = new DialogueConfigLoader();
        var settings = loader.LoadSettings();
        if (settings.Retrieval != null)
            settings.Retrieval.UseGuildOfficeLlmIntentRouter = false;

        var retrieval = settings.Retrieval ?? new RetrievalSettings();
        if (!retrieval.UseGuildOfficeSemanticGuardrail)
        {
            Console.WriteLine("[explore] UseGuildOfficeSemanticGuardrail=false — 켠 뒤 다시 실행하세요.");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(settings.Ollama.EmbeddingModel))
        {
            Console.WriteLine("[explore] EmbeddingModel 비어 있음.");
            return 2;
        }

        var path = Path.Combine(loader.ConfigDirectory, "GuildOfficeExploration.json");
        if (!File.Exists(path))
        {
            Console.WriteLine($"[explore] 파일 없음: {path}");
            return 2;
        }

        var root = JsonSerializer.Deserialize<GuildOfficeExplorationRoot>(await File.ReadAllTextAsync(path, ct), JsonOptions());
        if (root?.Cases == null || root.Cases.Count == 0)
        {
            Console.WriteLine("[explore] Cases 비어 있음.");
            return 2;
        }

        Console.WriteLine($"[explore] Warmup… {settings.Ollama.EmbeddingModel}");
        using var embed = new OllamaEmbeddingClient(settings);
        var guardrail = new GuildOfficeSemanticGuardrail();
        await guardrail.WarmupAsync(embed, loader.LoadSemanticGuardrailAnchors(), ct).ConfigureAwait(false);

        Console.WriteLine(
            $"[explore] deepExp = 키워드 또는 (exp≥{retrieval.GuildOfficeExpeditionContextThreshold:F2} " +
            $"∧ exp≥max(m,o)+{retrieval.GuildOfficeExpeditionDeepContextLead:F2}) " +
            $"[relativeGate={retrieval.GuildOfficeExpeditionUseRelativeEmbeddingGate}]");
        Console.WriteLine();

        Console.WriteLine(
            $"{nameof(GuildOfficeExplorationCase.Label),-20} {"Utterance",-42} {"heur",-10} {"atypical",-14} {"deepEx",-6} m o e");
        Console.WriteLine(new string('-', 120));

        foreach (var c in root.Cases)
        {
            var h0 = GuildOfficeTopicGate.ClassifyAtypicalPlayerInput(c.Utterance);
            var (meta, off, exp) = await guardrail.ScoreUtteranceFullAsync(c.Utterance, embed, ct).ConfigureAwait(false);
            var atypical = GuildOfficeTopicGate.ClassifyWithEmbeddingSignals(h0, c.Utterance, meta, off, exp, retrieval);
            var signals = new GuildOfficeUtteranceSignals(atypical, exp, meta, off);
            bool deepEx = GuildOfficeTopicGate.ComputeDeepExpeditionForGuildOffice(c.Utterance, signals, retrieval, true);

            var u = c.Utterance.Length > 40 ? c.Utterance[..40] + "…" : c.Utterance;
            var lab = (c.Label ?? "").Length > 18 ? (c.Label ?? "")[..18] + "…" : (c.Label ?? "");
            Console.WriteLine(
                $"{lab,-20} {u,-42} {h0,-10} {atypical,-14} {(deepEx ? "Y" : "N"),-6} {meta:F2} {off:F2} {exp:F2}");
        }

        Console.WriteLine();
        Console.WriteLine("[explore] heur=휴리스틱만(자모/난독). Gibberish면 atypical도 항상 Gibberish(임베딩이 덮지 않음).");
        return 0;
    }
}
