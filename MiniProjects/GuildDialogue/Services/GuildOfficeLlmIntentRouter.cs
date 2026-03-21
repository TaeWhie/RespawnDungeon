using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// 소형 LLM 호출로 in-world / gibberish / meta / offworld 를 한 번 더 나눕니다.
/// 키워드·임베딩만으로 애매한 경우 보완용(토큰 추가).
/// </summary>
public static class GuildOfficeLlmIntentRouter
{
    private const string RouterSystem =
        "당신은 분류기입니다. 유저의 **한 문장**만 보고 아래 네 값 중 하나만 골라 JSON 한 줄만 출력하세요. 설명·따옴표·markdown 금지.\n" +
        "{\"kind\":\"normal\"} — 길드·던전·동료·임무 등 판타지 세계 안 대화\n" +
        "{\"kind\":\"gibberish\"} — 뜻 없는 자모 나열, 기호만, 키보드 난타\n" +
        "{\"kind\":\"meta\"} — AI·봇·프롬프트·게임 시스템·모델·프로그램 정체성\n" +
        "{\"kind\":\"offworld\"} — 현실 브랜드·정치·주식·유튜브 등 세계관 밖 잡담";

    /// <summary>파싱 실패 시 null.</summary>
    public static async Task<GuildMasterAtypicalInputKind?> TryClassifyAsync(
        OllamaClient ollama,
        string? utterance,
        CancellationToken ct = default)
    {
        if (ollama == null || string.IsNullOrWhiteSpace(utterance)) return null;

        try
        {
            string? raw = await ollama.GenerateResponseAsync(RouterSystem, utterance.Trim(), ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var json = ExtractJsonObject(raw);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("kind", out var k)) return null;
            string? v = k.GetString()?.Trim().ToLowerInvariant();
            return v switch
            {
                "gibberish" => GuildMasterAtypicalInputKind.Gibberish,
                "meta" => GuildMasterAtypicalInputKind.MetaOrSystem,
                "offworld" => GuildMasterAtypicalInputKind.OffWorldCasual,
                "normal" => GuildMasterAtypicalInputKind.None,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractJsonObject(string response)
    {
        var s = response.Trim();
        if (s.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) s = s[7..];
        else if (s.StartsWith("```")) s = s[3..];
        if (s.EndsWith("```")) s = s[..^3];
        s = s.Trim();
        int a = s.IndexOf('{');
        int b = s.LastIndexOf('}');
        if (a >= 0 && b > a) s = s.Substring(a, b - a + 1);
        return s;
    }
}
