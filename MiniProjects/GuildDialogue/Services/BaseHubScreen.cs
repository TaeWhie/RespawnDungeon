using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// 메인 아지트 화면: 베이스 시설 기준 활동 풀에서 캐릭터마다 랜덤 배정, 그중 정확히 한 쌍은「대화」.
/// 이후 2인이 총 2~4발화(번갈아 한 줄씩)만 생성한다.
/// </summary>
public static class BaseHubScreen
{
    /// <summary>콘솔 메뉴 1번 — 진행 상황 출력 + 대화 실행.</summary>
    public static async Task RunAsync(DialogueManager manager, CancellationToken ct = default)
    {
        var lines = new List<string>();
        await RunCoreAsync(manager, ct, lines, printToConsole: true, onEmitLine: null, generatePairDialogue: true)
            .ConfigureAwait(false);
    }

    /// <summary>HTTP 허브 — 서버 콘솔 스팸 방지용으로 대화만 트랜스크립트 수집.</summary>
    /// <param name="generatePairDialogue">false면 활동 배정만 하고 Ollama 동료 2인 대화는 생략.</param>
    /// <returns>동료 2인 대화를 실제로 시도했으면 true(베이스 동료 2명 이상일 때).</returns>
    public static async Task<(List<string> Transcript, bool CompanionDialogueAttempted)> RunSpectatorForApiAsync(
        DialogueManager manager,
        CancellationToken ct = default,
        bool generatePairDialogue = true)
    {
        var lines = new List<string>();
        var attempted = await RunCoreAsync(manager, ct, lines, printToConsole: false, onEmitLine: null, generatePairDialogue)
            .ConfigureAwait(false);
        return (lines, attempted);
    }

    /// <summary>허브 NDJSON 스트림 — 한 줄씩 생성되는 즉시 콜백 (클라이언트가 기다리는 시간을 줄임).</summary>
    public static async Task<bool> RunSpectatorStreamForApiAsync(
        DialogueManager manager,
        Func<string, Task> onEmitLine,
        CancellationToken ct = default,
        bool generatePairDialogue = true)
    {
        var lines = new List<string>();
        return await RunCoreAsync(manager, ct, lines, printToConsole: false, onEmitLine: onEmitLine, generatePairDialogue)
            .ConfigureAwait(false);
    }

    private static async Task<bool> RunCoreAsync(
        DialogueManager manager,
        CancellationToken ct,
        List<string> lines,
        bool printToConsole,
        Func<string, Task>? onEmitLine,
        bool generatePairDialogue)
    {
        async Task EmitAsync(string s)
        {
            if (printToConsole)
                Console.WriteLine(s);
            lines.Add(s);
            if (onEmitLine != null)
                await onEmitLine(s).ConfigureAwait(false);
        }

        var loader = new DialogueConfigLoader();
        var bases = loader.LoadBaseDatabase();
        var baseIds = new HashSet<string>(
            bases.Select(b => b.BaseId).Where(s => !string.IsNullOrWhiteSpace(s)),
            StringComparer.OrdinalIgnoreCase);

        var roster = manager.Characters
            .Where(c => !c.Id.Equals("master", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var atBase = FilterAtBase(roster, baseIds);
        if (atBase.Count < 2)
        {
            await EmitAsync("[오류] 아지트(베이스)에 있는 동료가 2명 이상 필요합니다. CurrentLocationId를 베이스 시설이거나 비워 두세요.")
                .ConfigureAwait(false);
            return false;
        }

        var pool = BuildActivityPool(bases);
        if (pool.Count == 0)
            pool.Add("길드 시설에서 잡무");

        var rng = Random.Shared;
        var shuffled = atBase.OrderBy(_ => rng.Next()).ToList();

        var placeName = bases.FirstOrDefault(b =>
                string.Equals(b.BaseId, shuffled[0].CurrentLocationId, StringComparison.OrdinalIgnoreCase))?.Name
            ?? bases.FirstOrDefault()?.Name ?? "아지트";

        var pairA = shuffled[0];
        var pairB = shuffled[1];
        var assignments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (generatePairDialogue)
        {
            foreach (var c in shuffled)
            {
                if (c.Id.Equals(pairA.Id, StringComparison.OrdinalIgnoreCase) ||
                    c.Id.Equals(pairB.Id, StringComparison.OrdinalIgnoreCase))
                    continue;
                assignments[c.Id] = pool[rng.Next(pool.Count)];
            }

            assignments[pairA.Id] = $"{placeName}에서 동료와 대화 중 (↔ {pairB.Name})";
            assignments[pairB.Id] = $"{placeName}에서 동료와 대화 중 (↔ {pairA.Name})";
        }
        else
        {
            foreach (var c in shuffled)
                assignments[c.Id] = pool[rng.Next(pool.Count)];
        }

        await EmitAsync("").ConfigureAwait(false);
        await EmitAsync("──────── 길드 아지트 · 지금 누가 무엇을 ────────").ConfigureAwait(false);
        foreach (var c in shuffled.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var act = assignments.GetValueOrDefault(c.Id, "대기");
            await EmitAsync($"• {c.Name,-8} — {act}").ConfigureAwait(false);
        }

        if (!generatePairDialogue)
            return false;

        // 총 발화 수(두 사람 합산). 왕복 회수가 아님 — 예: 3이면 A→B→A 식으로 번갈아 최대 3줄.
        var utteranceCount = rng.Next(2, 5);
        await manager.RunBasePairDialogueAsync(
                pairA,
                pairB,
                utteranceCount,
                null,
                ct,
                lines,
                writeConsole: printToConsole,
                onTranscriptLine: onEmitLine)
            .ConfigureAwait(false);
        return true;
    }

    /// <summary>CurrentLocationId가 비어 있으면 아지트로 간주. 던전 등 베이스 밖은 제외.</summary>
    internal static List<Character> FilterAtBase(IReadOnlyList<Character> roster, HashSet<string> baseIds)
    {
        return roster.Where(c =>
        {
            if (string.IsNullOrWhiteSpace(c.CurrentLocationId))
                return true;
            return baseIds.Contains(c.CurrentLocationId.Trim());
        }).ToList();
    }

    internal static List<string> BuildActivityPool(IReadOnlyList<BaseFacilityData> bases)
    {
        var list = new List<string>();
        foreach (var b in bases)
        {
            if (!string.IsNullOrWhiteSpace(b.AvailableServices))
            {
                foreach (var part in b.AvailableServices.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var t = part.Trim();
                    if (t.Length > 0)
                        list.Add($"{b.Name} — {t}");
                }
            }

            if (!string.IsNullOrWhiteSpace(b.Name))
                list.Add($"{b.Name} — 시설 점검");
        }

        list.AddRange(new[]
        {
            "개인 장비 정비",
            "길드 문서 작성",
            "창가에서 잠시 휴식",
            "동료에게 연락 메모 남기기"
        });

        return list.Distinct(StringComparer.Ordinal).ToList();
    }
}
