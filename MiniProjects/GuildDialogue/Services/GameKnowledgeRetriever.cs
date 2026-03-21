using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// 참조 지식(WorldLore·몬스터·함정·스킬·아이템) 키워드 RAG. Base·EventType은 포함하지 않습니다.
/// 벡터 DB 없이 동작하며, 필요 시 Ollama 임베딩으로 동일 인터페이스를 대체할 수 있습니다.
/// </summary>
public static class GameKnowledgeRetriever
{
    private static readonly Regex s_tokenSplit = new(@"[\s\p{P}·・/\\]+", RegexOptions.Compiled);

    /// <summary>RAG용 도움말 텍스트. 빈 문자열이면 호출 측에서 Fallback 전체 DB를 쓰면 됩니다.</summary>
    public static string BuildBlock(
        string workingMemory,
        string episodicBuffer,
        Character? speaker,
        GameReferenceBundle? bundle,
        WorldLore? lore,
        IEnumerable<ItemData>? items,
        RetrievalSettings? retrieval)
    {
        if (bundle == null && lore == null && items == null)
            return "";

        retrieval ??= new RetrievalSettings();
        if (retrieval.UseKeywordRagForGameDb != true)
            return "";

        var query = BuildRetrievalQuery(workingMemory, episodicBuffer, speaker, retrieval);
        if (query.Length == 0)
            return "";

        var episodic = MaybeStripCurrencyLines(episodicBuffer, retrieval);
        var scored = new List<(string Line, int Score)>();

        void Add(string category, string title, string body, int baseWeight = 1)
        {
            var text = $"{title} {body}";
            var s = ScoreDocument(query, text, title, episodic, retrieval) * baseWeight;
            if (s <= 0) return;
            scored.Add(($"  • [{category}] {title}: {body}", s));
        }

        foreach (var m in bundle?.Monsters ?? Enumerable.Empty<MonsterData>())
        {
            var body =
                $"{m.Type}, {m.DangerLevel}. {m.Description} (약점: {m.Weakness}) 특성: {string.Join(", ", m.Traits)}";
            Add("몬스터", m.MonsterName, body.Trim());
        }

        foreach (var t in bundle?.Traps ?? Enumerable.Empty<TrapTypeData>())
            Add("함정", t.TrapName,
                $"{t.Category}, {t.DangerLevel}. {t.Effect} 대응: {t.CounterMeasure}");

        foreach (var sk in bundle?.Skills ?? Enumerable.Empty<SkillData>())
            Add("스킬", sk.SkillName,
                $"{sk.SkillType}. {sk.Effects} — {sk.Description} (MP{sk.MpCost}, 쿨{sk.CooldownTurns})");

        // Base·EventType은 시스템 프롬프트의 '코드 정의' Instruction으로만 주입 (RAG 대상 아님)

        foreach (var it in items ?? Enumerable.Empty<ItemData>())
            Add("아이템", it.ItemName,
                $"{it.Rarity}/{it.ItemType}. {it.Description} 효과: {it.Effects} ({it.Value}G)");

        if (lore != null)
        {
            foreach (var d in lore.Dungeons ?? Enumerable.Empty<DungeonData>())
            {
                var body =
                    $"{d.Difficulty}. {d.Description} (전형 몬스터: {string.Join(", ", d.TypicalMonsters)}, 보상: {string.Join(", ", d.KnownRewards)})";
                Add("던전(로어)", d.Name, body);
            }
            foreach (var loc in lore.Locations ?? Enumerable.Empty<LocationData>())
                Add("지명", loc.Name, $"({loc.Type}) {loc.Description}");
            foreach (var o in lore.Organizations ?? Enumerable.Empty<OrganizationData>())
                Add("단체", o.Name, $"({o.Type}) {o.Description}");
        }

        if (scored.Count == 0)
            return "";

        var top = scored
            .OrderByDescending(x => x.Score)
            .Take(Math.Max(1, retrieval.RagSearchPoolSize))
            .Select(x => x.Line);

        var sb = new StringBuilder();
        foreach (var line in top)
            sb.AppendLine(line);
        return sb.ToString().TrimEnd();
    }

    /// <summary>임베딩·키워드 RAG 공통 질의 문자열.</summary>
    public static string BuildRetrievalQuery(
        string workingMemory,
        string episodicBuffer,
        Character? speaker,
        RetrievalSettings retrieval)
    {
        var sb = new StringBuilder();
        if (retrieval.PrioritizeEpisodicMentionsInRag && !string.IsNullOrWhiteSpace(episodicBuffer))
            sb.AppendLine(episodicBuffer);
        sb.AppendLine(workingMemory);

        if (retrieval.RagIncludeSpeakerLoadout && speaker != null)
        {
            foreach (var x in speaker.Skills ?? Enumerable.Empty<string>())
                sb.Append(' ').Append(x);
            foreach (var i in speaker.Inventory ?? Enumerable.Empty<InventoryEntry>())
                sb.Append(' ').Append(i.ItemName);
            var eq = speaker.Equipment;
            if (eq != null)
            {
                void Slot(string? v)
                {
                    if (!string.IsNullOrWhiteSpace(v)) sb.Append(' ').Append(v);
                }
                Slot(eq.Weapon); Slot(eq.Armor); Slot(eq.Helmet); Slot(eq.Gloves); Slot(eq.Boots); Slot(eq.Accessory);
            }
        }

        var raw = sb.ToString();
        raw = MaybeStripCurrencyLines(raw, retrieval);
        if (raw.Length > retrieval.MaxEpisodicCharsInQuery)
            raw = raw.Substring(raw.Length - retrieval.MaxEpisodicCharsInQuery);
        return raw;
    }

    private static string MaybeStripCurrencyLines(string text, RetrievalSettings retrieval)
    {
        if (!retrieval.ExcludeCurrencyLineFromLoreEmbedding || string.IsNullOrEmpty(text))
            return text;
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var kept = lines.Where(l =>
            !Regex.IsMatch(l, @"골드\s*[:：]?\s*\d", RegexOptions.IgnoreCase) &&
            !Regex.IsMatch(l, @"\d+\s*G\b"));
        return string.Join("\n", kept);
    }

    private static int ScoreDocument(
        string queryCorpus,
        string docBody,
        string title,
        string episodic,
        RetrievalSettings retrieval)
    {
        int s = 0;
        s += ScoreOverlap(queryCorpus, title) * 5;
        s += ScoreOverlap(queryCorpus, docBody) * 2;
        var em = retrieval.PrioritizeEpisodicMentionsInRag ? 2 : 1;
        if (!string.IsNullOrEmpty(episodic))
        {
            s += ScoreOverlap(episodic, title) * 6 * em;
            s += ScoreOverlap(episodic, docBody) * em;
        }
        return s;
    }

    private static int ScoreOverlap(string haystack, string searchable)
    {
        if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(searchable)) return 0;
        int s = 0;
        foreach (var term in Tokenize(haystack))
        {
            if (term.Length < 2) continue;
            if (searchable.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                s += Math.Min(term.Length, 12);
        }
        return s;
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        foreach (var part in s_tokenSplit.Split(text))
        {
            var t = part.Trim();
            if (t.Length >= 2) yield return t;
        }
    }
}
