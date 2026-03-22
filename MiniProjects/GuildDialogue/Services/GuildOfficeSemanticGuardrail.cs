using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// 메타·세계관 밖 질문을 키워드 없이 잡기 위한 임베딩 코사인 유사도 가드레일.
/// 앵커 문장만 소량 두고, Ollama nomic-embed-text 등으로 벡터 비교합니다.
/// </summary>
public sealed class GuildOfficeSemanticGuardrail
{
    private float[][]? _metaEmbeddings;
    private float[][]? _offWorldEmbeddings;
    private float[][]? _expeditionEmbeddings;

    public bool IsReady =>
        _metaEmbeddings is { Length: > 0 } &&
        _offWorldEmbeddings is { Length: > 0 } &&
        _expeditionEmbeddings is { Length: > 0 };

    /// <param name="anchorsFromFile">
    /// <see cref="DialogueConfigLoader.LoadSemanticGuardrailAnchors"/> 결과.
    /// 카테고리별 목록이 비어 있으면 <see cref="SemanticGuardrailAnchorsRoot.CreateBuiltInDefaults"/>로 해당 축만 보강.
    /// </param>
    public async Task WarmupAsync(
        OllamaEmbeddingClient client,
        SemanticGuardrailAnchorsRoot? anchorsFromFile = null,
        CancellationToken ct = default,
        int embeddingMaxConcurrency = 4)
    {
        if (client == null) throw new ArgumentNullException(nameof(client));

        var maxConc = Math.Clamp(embeddingMaxConcurrency, 1, 16);
        var builtIn = SemanticGuardrailAnchorsRoot.CreateBuiltInDefaults();
        var metaPhrases = PickAnchorPhrases(anchorsFromFile?.MetaAnchors, builtIn.MetaAnchors);
        var offPhrases = PickAnchorPhrases(anchorsFromFile?.OffWorldAnchors, builtIn.OffWorldAnchors);
        var expPhrases = PickAnchorPhrases(anchorsFromFile?.ExpeditionAnchors, builtIn.ExpeditionAnchors);

        var metaTask = EmbedAllAsync(client, metaPhrases, maxConc, ct);
        var offTask = EmbedAllAsync(client, offPhrases, maxConc, ct);
        var expTask = EmbedAllAsync(client, expPhrases, maxConc, ct);
        await Task.WhenAll(metaTask, offTask, expTask).ConfigureAwait(false);
        _metaEmbeddings = await metaTask.ConfigureAwait(false);
        _offWorldEmbeddings = await offTask.ConfigureAwait(false);
        _expeditionEmbeddings = await expTask.ConfigureAwait(false);

        if (_metaEmbeddings.Length == 0 || _offWorldEmbeddings.Length == 0 || _expeditionEmbeddings.Length == 0)
            throw new InvalidOperationException("의미 가드레일: 앵커 임베딩이 비었습니다.");
    }

    private static IReadOnlyList<string> PickAnchorPhrases(List<string>? fromDb, List<string> builtInFallback)
    {
        var list = new List<string>();
        if (fromDb != null)
        {
            foreach (var s in fromDb)
            {
                var t = s?.Trim();
                if (!string.IsNullOrEmpty(t))
                    list.Add(t!);
            }
        }

        if (list.Count > 0) return list;
        return builtInFallback;
    }

    private static async Task<float[][]> EmbedAllAsync(
        OllamaEmbeddingClient client,
        IReadOnlyList<string> phrases,
        int maxConcurrency,
        CancellationToken ct)
    {
        if (phrases.Count == 0)
            return Array.Empty<float[]>();

        var maxConc = Math.Clamp(maxConcurrency, 1, 16);
        var buf = new float[phrases.Count][];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, phrases.Count),
            new ParallelOptions { MaxDegreeOfParallelism = maxConc, CancellationToken = ct },
            async (i, token) =>
            {
                token.ThrowIfCancellationRequested();
                var v = await client.EmbedAsync(phrases[i], token).ConfigureAwait(false);
                buf[i] = v is { Length: > 0 } ? v : Array.Empty<float>();
            }).ConfigureAwait(false);

        var rows = new List<float[]>();
        foreach (var v in buf)
        {
            if (v.Length > 0)
                rows.Add(v);
        }

        return rows.ToArray();
    }

    /// <summary>유저 발화와 앵커 세트 간 최대 코사인 유사도.</summary>
    public async Task<(double MetaMax, double OffWorldMax)> ScoreUtteranceAsync(
        string? utterance,
        OllamaEmbeddingClient client,
        CancellationToken ct = default)
    {
        if (!IsReady || string.IsNullOrWhiteSpace(utterance) || client == null)
            return (0, 0);

        var u = await client.EmbedAsync(utterance.Trim(), ct).ConfigureAwait(false);
        if (u == null || u.Length == 0)
            return (0, 0);

        double meta = MaxCosine(u, _metaEmbeddings!);
        double off = MaxCosine(u, _offWorldEmbeddings!);
        return (meta, off);
    }

    /// <summary>유저 발화 1회 임베딩으로 메타·오프월드·원정 의도 유사도를 한 번에 계산.</summary>
    public async Task<(double MetaMax, double OffWorldMax, double ExpeditionMax)> ScoreUtteranceFullAsync(
        string? utterance,
        OllamaEmbeddingClient client,
        CancellationToken ct = default)
    {
        if (!IsReady || string.IsNullOrWhiteSpace(utterance) || client == null)
            return (0, 0, 0);

        var u = await client.EmbedAsync(utterance.Trim(), ct).ConfigureAwait(false);
        if (u == null || u.Length == 0)
            return (0, 0, 0);

        double meta = MaxCosine(u, _metaEmbeddings!);
        double off = MaxCosine(u, _offWorldEmbeddings!);
        double exp = MaxCosine(u, _expeditionEmbeddings!);
        return (meta, off, exp);
    }

    private static double MaxCosine(float[] u, float[][] set)
    {
        double best = 0;
        foreach (var row in set)
        {
            if (row.Length != u.Length) continue;
            var c = CosineSimilarity(u, row);
            if (c > best) best = c;
        }

        return best;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }

        if (na <= 0 || nb <= 0) return 0;
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }
}
