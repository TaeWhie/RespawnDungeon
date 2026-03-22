using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>Ollama로 캐릭터 Id·이름·배경·말투를 JSON으로 생성합니다.</summary>
public static class CharacterCreationLlmGenerator
{
    public sealed record Profile(string Id, string Name, string Background, string SpeechStyle);

    private sealed class DraftDto
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("background")] public string? Background { get; set; }
        [JsonPropertyName("speechStyle")] public string? SpeechStyle { get; set; }
    }

    public static async Task<Profile?> TryGenerateAsync(
        DialogueConfigLoader loader,
        OllamaClient ollama,
        JobRoleData job,
        int age,
        int career,
        IReadOnlyList<string> skills,
        string currentLocationId,
        BaseFacilityData? receptionFacility,
        IReadOnlyList<Character> roster,
        CancellationToken ct)
    {
        var rng = Random.Shared;
        var lore = loader.LoadWorldLore();
        var loreSb = new StringBuilder();
        if (lore != null)
        {
            if (!string.IsNullOrWhiteSpace(lore.WorldName))
                loreSb.AppendLine($"세계명: {lore.WorldName.Trim()}");
            AppendTrimmed(loreSb, "세계 요약", lore.WorldSummary);
            AppendTrimmed(loreSb, "길드", lore.GuildInfo);
            AppendTrimmed(loreSb, "아지트/베이스 캠프", lore.BaseCamp);
        }

        var loreBlock = loreSb.ToString();
        if (loreBlock.Length > 2400)
            loreBlock = loreBlock[..2400] + "\n…(이하 생략)";

        var facilityBlock = receptionFacility != null
            ? $"시설명: {receptionFacility.Name}\nBaseId: {receptionFacility.BaseId}\n설명: {receptionFacility.Description}\n서비스: {receptionFacility.AvailableServices}"
            : $"현재 위치 BaseId: {currentLocationId}";

        var usedIds = string.Join(", ",
            roster.Select(c => c.Id).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase));
        var usedNames = string.Join(", ",
            roster.Select(c => c.Name).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());

        var skillLine = skills.Count > 0 ? string.Join(", ", skills) : "(없음)";

        var systemPrompt =
            "당신은 판타지 길드 RPG의 **모험가 등록 카드**를 채우는 작가입니다.\n" +
            "출력은 **JSON 객체 하나만** 하세요. 코드펜스·주석·설명 문장 금지.\n" +
            "키(영문, camelCase): id, name, background, speechStyle\n" +
            "규칙:\n" +
            "- id: 소문자 영문·숫자·밑줄(_)·하이픈(-)만. 길이 3~28. `master` 금지. 기존 Id와 겹치면 안 됨.\n" +
            "- name: 한국어 표기 이름. 2~8자 내외. 기존 이름과 겹치면 안 됨.\n" +
            "- background: 한국어 2~4문장. 직업·나이·경력·보유 스킬·접수 시설 맥락과 모순 없게. 에델가드 길드 세계관에 맞출 것.\n" +
            "- speechStyle: 한국어 1~2문장. **말투·어조·말버릇 규칙**만 서술(대사 예시는 넣지 말 것).\n" +
            "  **말투 다양성(중요)**: 직업과 말투를 억지로 맞추지 마세요. " +
            "예: 지원·치유 담당이라고 해서 ‘조곤조곤·다정·위로 잔치’로 고정하지 말 것. " +
            "같은 직업이라도 무뚝뚝·건조·냉소·수다·보고체·짧은 반말·질문 많음 등 **서로 다른 어조**를 허용합니다. " +
            "스테레오타입(‘치유사니까 부드럽게’)을 피하고, 인물 성격에 맞는 말투를 고르세요.\n" +
            "서로 다른 캐릭터끼리 이름·배경·말투 결이 겹치지 않게 변주하세요.";

        var userPrompt = new StringBuilder();
        userPrompt.AppendLine("[등록 컨텍스트]");
        userPrompt.AppendLine($"직업 RoleId: {job.RoleId}");
        userPrompt.AppendLine($"직업 설명: {job.Description}");
        userPrompt.AppendLine($"나이: {age}세");
        userPrompt.AppendLine($"모험/관련 경력(년): {career}");
        userPrompt.AppendLine($"보유 스킬(이름): {skillLine}");
        userPrompt.AppendLine();
        userPrompt.AppendLine("[접수 시설]");
        userPrompt.AppendLine(facilityBlock);
        userPrompt.AppendLine();
        userPrompt.AppendLine("[세계관 참고]");
        userPrompt.AppendLine(string.IsNullOrWhiteSpace(loreBlock) ? "(WorldLore.json 없음)" : loreBlock);
        userPrompt.AppendLine();
        userPrompt.AppendLine("[중복 금지 목록]");
        userPrompt.AppendLine($"이미 사용 중인 Id: {usedIds}");
        userPrompt.AppendLine($"이미 사용 중인 이름: {usedNames}");
        userPrompt.AppendLine();
        userPrompt.AppendLine("[말투 무작위 힌트 — 그대로 복붙 말고, 이런 계열 중 하나를 참고해 겹치지 않게]");
        userPrompt.AppendLine(SpeechStyleHintForPrompt(rng));

        var response = await ollama.GenerateResponseAsync(systemPrompt, userPrompt.ToString(), ct);
        if (string.IsNullOrWhiteSpace(response))
        {
            Console.WriteLine("[캐릭터 LLM] 응답이 비었습니다.");
            return null;
        }

        var clean = CleanJsonObject(response);
        DraftDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<DraftDto>(clean, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[캐릭터 LLM] JSON 파싱 실패: {ex.Message}");
            return null;
        }

        if (dto == null)
        {
            Console.WriteLine("[캐릭터 LLM] JSON이 비었습니다.");
            return null;
        }

        var id = (dto.Id ?? "").Trim();
        var name = (dto.Name ?? "").Trim();
        var background = (dto.Background ?? "").Trim();
        var speech = (dto.SpeechStyle ?? "").Trim();

        if (id.Length is < 3 or > 28 ||
            !id.All(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c is '_' or '-'))
        {
            Console.WriteLine("[캐릭터 LLM] id 형식이 올바르지 않습니다.");
            return null;
        }

        if (id.Equals("master", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[캐릭터 LLM] id가 예약어입니다.");
            return null;
        }

        if (name.Length is < 2 or > 12)
        {
            Console.WriteLine("[캐릭터 LLM] 이름 길이가 범위를 벗어났습니다.");
            return null;
        }

        if (background.Length < 20 || speech.Length < 10)
        {
            Console.WriteLine("[캐릭터 LLM] 배경/말투가 너무 짧습니다.");
            return null;
        }

        if (roster.Any(c => c.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine("[캐릭터 LLM] id가 기존 캐릭터와 중복됩니다.");
            return null;
        }

        if (roster.Any(c => string.Equals(c.Name?.Trim(), name, StringComparison.Ordinal)))
        {
            Console.WriteLine("[캐릭터 LLM] 이름이 기존 캐릭터와 중복됩니다.");
            return null;
        }

        return new Profile(id.ToLowerInvariant(), name, background, speech);
    }

    /// <summary>LLM이 말투를 직업 스테레오타입에 고정하지 않도록 매 요청 다른 힌트를 붙입니다.</summary>
    private static string SpeechStyleHintForPrompt(Random rng)
    {
        var hints = new[]
        {
            "차갑고 짧은 지시형; 감정·위로 멘트는 최소.",
            "수다·가벼운 농담이 많고, 핵심 정보는 끝에 한 번 정리.",
            "딱딱한 보고체(상태·권고·다음 조치); 감정 표현 거의 없음.",
            "질문이 잦고 같은 것을 재확인하는 불안·불신형.",
            "무뚝뚝·건조하지만 책임은 지는 타입; 칭찬은 아낌.",
            "응급일수록 말이 빨라지고 단문·반복 지시.",
            "겉으론 장난·비꼼 섞이지만 거절·후퇴 명령은 단호.",
            "존댓말·반말이 상황에 따라 섞이는 하이브리드.",
            "냉소는 있으나 동료를 버리진 않음; 말버릇으로 티를 냄.",
            "말이 길어지다가 스스로 끊으며 ‘요지만’으로 마무리."
        };
        return hints[rng.Next(hints.Length)];
    }

    private static void AppendTrimmed(StringBuilder sb, string label, string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        sb.AppendLine($"{label}: {text.Trim()}");
    }

    /// <summary>코드펜스·앞뒤 잡담를 제거하고 첫 `{`~마지막 `}` 구간만 남깁니다.</summary>
    private static string CleanJsonObject(string response)
    {
        var cleanJson = response.Trim();
        if (cleanJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) cleanJson = cleanJson[7..];
        else if (cleanJson.StartsWith("```")) cleanJson = cleanJson[3..];
        if (cleanJson.EndsWith("```")) cleanJson = cleanJson[..^3];

        cleanJson = cleanJson.Trim();
        var first = cleanJson.IndexOf('{');
        var last = cleanJson.LastIndexOf('}');
        if (first >= 0 && last > first)
            cleanJson = cleanJson.Substring(first, last - first + 1);

        return cleanJson.Trim();
    }
}
