using System.Text.Json.Serialization;

namespace GuildDialogue.Data;

/// <summary>Config/LogGlossary.json의 로그 용어 정의(시간·이벤트 유형·필드 설명 등).</summary>
public class LogGlossaryRoot
{
    [JsonPropertyName("timeConcepts")]
    public List<GlossaryEntry>? TimeConcepts { get; set; }

    [JsonPropertyName("dungeonEventTypes")]
    public List<GlossaryEntry>? DungeonEventTypes { get; set; }

    [JsonPropertyName("outcomes")]
    public List<GlossaryEntry>? Outcomes { get; set; }

    [JsonPropertyName("baseEventTypes")]
    public List<GlossaryEntry>? BaseEventTypes { get; set; }

    [JsonPropertyName("fieldNotes")]
    public List<GlossaryEntry>? FieldNotes { get; set; }
}

/// <summary>용어 한 줄 정의. code, labelKo, meaning, note(선택).</summary>
public class GlossaryEntry
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";
    [JsonPropertyName("labelKo")]
    public string LabelKo { get; set; } = "";
    [JsonPropertyName("meaning")]
    public string Meaning { get; set; } = "";
    [JsonPropertyName("note")]
    public string? Note { get; set; }
}

/// <summary>이전 호환용. GlossaryEntry와 동일.</summary>
public class TimeConceptEntry : GlossaryEntry { }
