using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// 설정의 KeywordCategories를 로드해 line/intent에서 키워드 매칭 후 오프셋 누적. 상하한 적용.
/// </summary>
public class MemoryManager
{
    private readonly DialogueSettings _settings;

    public MemoryManager(DialogueSettings settings)
    {
        _settings = settings;
    }

    public void ApplyOffsetFromLine(string line, string intent, LongTermSummary current)
    {
        var text = (line + " " + intent).ToLowerInvariant();
        var step = _settings.OffsetStep;
        var min = _settings.OffsetMin;
        var max = _settings.OffsetMax;

        foreach (var kw in _settings.KeywordCategories.Trust.Positive)
            if (text.Contains(kw, StringComparison.OrdinalIgnoreCase)) current.TrustOffset = Clamp(current.TrustOffset + step, min, max);
        foreach (var kw in _settings.KeywordCategories.Trust.Negative)
            if (text.Contains(kw, StringComparison.OrdinalIgnoreCase)) current.TrustOffset = Clamp(current.TrustOffset - step, min, max);
        foreach (var kw in _settings.KeywordCategories.Mood.Positive)
            if (text.Contains(kw, StringComparison.OrdinalIgnoreCase)) current.MoodOffset = Clamp(current.MoodOffset + step, min, max);
        foreach (var kw in _settings.KeywordCategories.Mood.Negative)
            if (text.Contains(kw, StringComparison.OrdinalIgnoreCase)) current.MoodOffset = Clamp(current.MoodOffset - step, min, max);
        foreach (var kw in _settings.KeywordCategories.Relation.Positive)
            if (text.Contains(kw, StringComparison.OrdinalIgnoreCase)) current.RelationOffset = Clamp(current.RelationOffset + step, min, max);
        foreach (var kw in _settings.KeywordCategories.Relation.Negative)
            if (text.Contains(kw, StringComparison.OrdinalIgnoreCase)) current.RelationOffset = Clamp(current.RelationOffset - step, min, max);
    }

    public void UpdateSummarySentence(LongTermSummary current)
    {
        var parts = new List<string>();
        if (current.TrustOffset != 0) parts.Add($"GM에 대한 신뢰 {current.TrustOffset:+#;-#;0}");
        if (current.MoodOffset != 0) parts.Add($"기분 {current.MoodOffset:+#;-#;0}");
        if (current.RelationOffset != 0) parts.Add($"관계 {current.RelationOffset:+#;-#;0}");
        current.SummarySentence = parts.Count > 0 ? string.Join(", ", parts) : "특이사항 없음.";
    }

    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
}
