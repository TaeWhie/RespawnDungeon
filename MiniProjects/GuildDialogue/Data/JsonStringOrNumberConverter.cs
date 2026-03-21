using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GuildDialogue.Data;

/// <summary>JSON에서 문자열 또는 숫자 모두 읽어 문자열로 저장(ActionLog FloorOrZone 등).</summary>
public sealed class JsonStringOrNumberConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var l)
                ? l.ToString(CultureInfo.InvariantCulture)
                : reader.GetDouble().ToString(CultureInfo.InvariantCulture),
            _ => throw new JsonException($"Cannot convert {reader.TokenType} to string.")
        };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value == null) writer.WriteNullValue();
        else writer.WriteStringValue(value);
    }
}
