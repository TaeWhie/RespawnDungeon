using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// 길드장 집무실 발화별 실제 NPC 응답 생성. <c>dotnet run -- --explore-guild-office-llm [--buddy 이름] [--max N]</c>
/// </summary>
public static class GuildOfficeLlmExplorationRunner
{
    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        string? buddyArg = null;
        int maxCases = int.MaxValue;
        for (int i = 1; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--buddy", StringComparison.OrdinalIgnoreCase) &&
                i + 1 < args.Length)
            {
                buddyArg = args[++i];
                continue;
            }

            if (string.Equals(args[i], "--max", StringComparison.OrdinalIgnoreCase) &&
                i + 1 < args.Length &&
                int.TryParse(args[++i], out var m) &&
                m > 0)
            {
                maxCases = m;
            }
        }

        var loader = new DialogueConfigLoader();
        var path = Path.Combine(loader.ConfigDirectory, "GuildOfficeExploration.json");
        if (!File.Exists(path))
        {
            Console.WriteLine($"[llm-explore] 없음: {path}");
            return 2;
        }

        var root = JsonSerializer.Deserialize<GuildOfficeExplorationRoot>(await File.ReadAllTextAsync(path, ct), JsonOptions());
        if (root?.Cases == null || root.Cases.Count == 0)
        {
            Console.WriteLine("[llm-explore] Cases 비어 있음.");
            return 2;
        }

        var manager = new DialogueManager();
        await manager.InitializeAsync(ct).ConfigureAwait(false);

        var selectable = manager.Characters
            .Where(c => !string.Equals(c.Id, "master", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (selectable.Count == 0)
        {
            Console.WriteLine("[llm-explore] master 제외 동료 캐릭터 없음.");
            return 2;
        }

        Character respondent = selectable[0];
        if (!string.IsNullOrWhiteSpace(buddyArg))
        {
            var hit = selectable.FirstOrDefault(c =>
                c.Name.Equals(buddyArg.Trim(), StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(c.Id) &&
                 c.Id.Equals(buddyArg.Trim(), StringComparison.OrdinalIgnoreCase)));
            if (hit != null)
                respondent = hit;
            else
                Console.WriteLine($"[llm-explore] 동료 '{buddyArg}' 없음 — 첫 동료({respondent.Name}) 사용.");
        }

        const string baseWorld =
            "장소: 길드장 집무실\n상황: 던전 탐험을 마친 뒤 동료가 길드장에게 보고하거나 대화를 나누러 집무실을 찾아왔습니다.";

        var cases = root.Cases.Take(maxCases).ToList();
        Console.WriteLine($"[llm-explore] 상대: {respondent.Name} ({cases.Count}턴, 턴만 격리·세션 로그 미기록)\n");

        foreach (var c in cases)
        {
            Console.WriteLine("────────────────────────────────────────");
            Console.WriteLine($"[{c.Label}] 길드장: {c.Utterance}");
            var result = await manager.RunIsolatedGuildOfficeLlmTurnAsync(respondent, c.Utterance, baseWorld, ct)
                .ConfigureAwait(false);
            Console.WriteLine(
                $"[분류] atypical={result.AtypicalKind} deepExp={result.DeepExpedition} " +
                $"m={result.MetaSimilarity:F2} o={result.OffWorldSimilarity:F2} e={result.ExpeditionSimilarity:F2} | " +
                $"{respondent.Name}: {result.BuddyLine ?? "(없음)"}");
            if (!string.IsNullOrEmpty(result.ErrorMessage))
                Console.WriteLine($"[오류] {result.ErrorMessage}");
            if (!string.IsNullOrEmpty(result.RawResponsePreview))
                Console.WriteLine($"[원문 일부] {result.RawResponsePreview}");
            Console.WriteLine();
        }

        Console.WriteLine("[llm-explore] 끝.");
        return 0;
    }
}
