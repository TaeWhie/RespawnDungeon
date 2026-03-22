using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// 참조 지식을 **자연어 전처리된 청크**로 임베딩합니다. (JSON 그대로 넣지 않음)
/// </summary>
public sealed class EmbeddingKnowledgeIndex
{
    private readonly List<KnowledgeChunk> _chunks = new();
    private readonly List<float[]> _vectors = new();

    public bool IsReady => _chunks.Count > 0 && _vectors.Count == _chunks.Count;

    public async Task<bool> BuildAsync(
        OllamaEmbeddingClient embedder,
        WorldLore? lore,
        IReadOnlyList<MonsterData> monsters,
        IReadOnlyList<TrapTypeData> traps,
        IReadOnlyList<SkillData> skills,
        IReadOnlyList<ItemData> items,
        IReadOnlyList<Character>? characters = null,
        int embeddingMaxConcurrency = 4,
        CancellationToken ct = default)
    {
        _chunks.Clear();
        _vectors.Clear();

        var processed = EmbeddingKnowledgePreprocessor.BuildAllChunks(
            lore, monsters, traps, skills, items, characters);

        foreach (var p in processed)
        {
            if (string.IsNullOrWhiteSpace(p.EmbeddingText) || p.EmbeddingText.Length < 12)
                continue;
            _chunks.Add(new KnowledgeChunk(p.Category, p.Title, p.EmbeddingText, p.Metadata));
        }

        if (_chunks.Count == 0)
            return false;

        var maxConc = Math.Clamp(embeddingMaxConcurrency, 1, 16);
        var buf = new float[_chunks.Count][];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, _chunks.Count),
            new ParallelOptions { MaxDegreeOfParallelism = maxConc, CancellationToken = ct },
            async (i, token) =>
            {
                token.ThrowIfCancellationRequested();
                var v = await embedder.EmbedAsync(_chunks[i].EmbeddingText, token).ConfigureAwait(false);
                buf[i] = v ?? Array.Empty<float>();
            }).ConfigureAwait(false);

        foreach (var v in buf)
        {
            if (v.Length == 0)
                return false;
            _vectors.Add(v);
        }

        return IsReady;
    }

    public async Task<string> RetrieveFormattedAsync(
        OllamaEmbeddingClient embedder,
        string query,
        int topK,
        CancellationToken ct = default)
    {
        if (!IsReady || string.IsNullOrWhiteSpace(query)) return "";
        var qv = await embedder.EmbedAsync(query, ct).ConfigureAwait(false);
        if (qv == null || qv.Length == 0) return "";

        var scored = new List<(KnowledgeChunk Chunk, float Score)>();
        for (var i = 0; i < _chunks.Count; i++)
            scored.Add((_chunks[i], CosineSimilarity(qv, _vectors[i])));

        var sb = new StringBuilder();
        foreach (var (chunk, score) in scored.OrderByDescending(x => x.Score).Take(Math.Max(1, topK)))
        {
            var metaShort = string.Join(", ",
                chunk.Metadata.Select(kv => $"{kv.Key}={kv.Value}"));
            sb.AppendLine($"  • [{chunk.Category}] {chunk.Title} (유사도 {score:F3})");
            sb.AppendLine($"    꼬리표: {metaShort}");
            sb.AppendLine(chunk.EmbeddingText);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0;
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        var d = Math.Sqrt(na) * Math.Sqrt(nb);
        return d < 1e-8 ? 0 : (float)(dot / d);
    }

    private sealed record KnowledgeChunk(
        string Category,
        string Title,
        string EmbeddingText,
        IReadOnlyDictionary<string, string> Metadata);
}
