using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>임베딩 RAG용 질의 문자열을 만듭니다. (Ollama 임베딩 전제)</summary>
public static class GameKnowledgeRetriever
{
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
}
