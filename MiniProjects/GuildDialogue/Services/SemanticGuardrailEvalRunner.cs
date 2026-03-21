using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// 시멘틱 가드레일 회귀 점검. <c>dotnet run -- --eval-guardrail</c> — Ollama 임베딩 필요.
/// </summary>
public static class SemanticGuardrailEvalRunner
{
    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        var loader = new DialogueConfigLoader();
        var settings = loader.LoadSettings();
        // 재현성을 위해 LLM 라우터는 끔(임베딩·휴리스틱만).
        if (settings.Retrieval != null)
            settings.Retrieval.UseGuildOfficeLlmIntentRouter = false;

        var retrieval = settings.Retrieval ?? new RetrievalSettings();
        if (!retrieval.UseGuildOfficeSemanticGuardrail)
        {
            Console.WriteLine("[eval] DialogueSettings.Retrieval.UseGuildOfficeSemanticGuardrail 가 false입니다. true로 켜고 다시 실행하세요.");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(settings.Ollama.EmbeddingModel))
        {
            Console.WriteLine("[eval] Ollama.EmbeddingModel(예: nomic-embed-text)이 비어 있습니다.");
            return 2;
        }

        var evalPath = Path.Combine(loader.ConfigDirectory, "SemanticGuardrailEval.json");
        if (!File.Exists(evalPath))
        {
            Console.WriteLine($"[eval] 파일 없음: {evalPath}");
            return 2;
        }

        var eval = JsonSerializer.Deserialize<SemanticGuardrailEvalRoot>(await File.ReadAllTextAsync(evalPath, ct), JsonOptions());
        if (eval?.Cases == null || eval.Cases.Count == 0)
        {
            Console.WriteLine("[eval] SemanticGuardrailEval.json에 Cases가 없습니다.");
            return 2;
        }

        Console.WriteLine($"[eval] 앵커 로드 및 Warmup… ({settings.Ollama.EmbeddingModel})");
        using var embed = new OllamaEmbeddingClient(settings);
        var guardrail = new GuildOfficeSemanticGuardrail();
        await guardrail.WarmupAsync(embed, loader.LoadSemanticGuardrailAnchors(), ct).ConfigureAwait(false);

            double th = retrieval.GuildOfficeSemanticGuardrailThreshold;

            int fail = 0;
            Console.WriteLine(
                $"[eval] threshold={th:F3}, tieEps={retrieval.GuildOfficeSemanticMetaOffEmbeddingTieEpsilon:F3}, " +
                $"strongFloor={retrieval.GuildOfficeSemanticStrongAtypicalFloor:F2}, expDisambigMin={retrieval.GuildOfficeSemanticExpeditionDisambigMin:F2}, " +
                $"margin={retrieval.GuildOfficeSemanticDisambiguationMargin:F3}");
        Console.WriteLine();
        Console.WriteLine(
            $"{"Utterance",-40} {"Expect",-15} {"Got",-15} {"meta",-7}{"off",-7}{"exp",-7} OK");
        Console.WriteLine(new string('-', 110));

        foreach (var c in eval.Cases)
        {
            if (!TryParseExpect(c.Expect, out var expect))
            {
                Console.WriteLine($"[eval] 잘못된 Expect 값: {c.Expect}");
                fail++;
                continue;
            }

                var h0 = GuildOfficeTopicGate.ClassifyAtypicalPlayerInput(c.Utterance);
                var (meta, off, exp) = await guardrail.ScoreUtteranceFullAsync(c.Utterance, embed, ct).ConfigureAwait(false);
                var merged = GuildOfficeTopicGate.ClassifyWithEmbeddingSignals(h0, c.Utterance, meta, off, exp, retrieval);

            bool ok = merged == expect;
            if (!ok) fail++;

            var u = c.Utterance.Length > 37 ? c.Utterance[..37] + "…" : c.Utterance;
            Console.WriteLine(
                $"{u,-40} {expect,-15} {merged,-15} {meta:F3} {off:F3} {exp:F3} {(ok ? "OK" : "FAIL")}");
            if (!ok && !string.IsNullOrWhiteSpace(c.Note))
                Console.WriteLine($"         … note: {c.Note}");
        }

        Console.WriteLine();
        if (fail == 0)
        {
            Console.WriteLine($"[eval] 전부 통과 ({eval.Cases.Count}건).");
            return 0;
        }

        Console.WriteLine($"[eval] 실패 {fail} / {eval.Cases.Count}. threshold·margin 조정 또는 앵커·케이스를 검토하세요.");
        return 1;
    }

    private static bool TryParseExpect(string s, out GuildMasterAtypicalInputKind kind)
    {
        kind = GuildMasterAtypicalInputKind.None;
        if (string.IsNullOrWhiteSpace(s)) return false;
        return Enum.TryParse(s.Trim(), ignoreCase: true, out kind);
    }
}
