using System;
using System.Collections.Generic;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// 캐릭터 <see cref="Character.Mood"/>를 턴마다 갱신합니다.
/// 대화는 메모리 위주이며, 원정 시뮬 후 캐릭터 DB 동기화 시에는 갱신된 Mood가 JSON에 함께 저장될 수 있습니다.
/// </summary>
public static class CharacterMoodUpdater
{
    private static readonly string[] AmbientPool =
    {
        "중립", "차분", "맑음", "여유", "가벼움", "집중", "긴장", "기쁨", "뿌듯", "피곤", "짜증", "경계"
    };

    /// <summary>길드장 1:1 한 턴이 끝난 뒤, 동료(화자) 기분을 조정합니다.</summary>
    public static void ApplyAfterGuildOfficeTurn(
        Character speaker,
        string? guildUtterance,
        GuildMasterAtypicalInputKind atypical,
        bool frustrationStop,
        bool deepExpedition)
    {
        if (string.IsNullOrWhiteSpace(speaker.Mood))
            speaker.Mood = "중립";

        var g = (guildUtterance ?? "").Trim();
        if (frustrationStop)
        {
            speaker.Mood = Pick("짜증", "긴장", "경계");
            return;
        }

        if (atypical == GuildMasterAtypicalInputKind.MetaOrSystem ||
            atypical == GuildMasterAtypicalInputKind.OffWorldCasual)
        {
            speaker.Mood = Pick("혼란", "경계", "차분");
            return;
        }

        if (ContainsAny(g, "고마워", "고맙", "수고", "잘했", "훌륭", "대단"))
            speaker.Mood = Pick("기쁨", "뿌듯", "맑음");
        else if (ContainsAny(g, "바보", "쓰레기", "닥쳐", "죽어"))
            speaker.Mood = Pick("짜증", "경계", "상처");
        else if (ContainsAny(g, "미안", "실수", "잘못"))
            speaker.Mood = Pick("차분", "부끄", "여유");
        else if (deepExpedition)
            speaker.Mood = Pick("집중", "긴장", "차분");

        if (Random.Shared.NextDouble() < 0.1)
            speaker.Mood = AmbientDrift(speaker.Mood);
    }

    /// <summary>
    /// 던전 원정 시뮬 종료 직후, 참가자 기분을 결과·HP 여유에 맞춥니다.
    /// </summary>
    /// <param name="charactersAfterRun">전체 로스터(병합 스냅샷).</param>
    /// <param name="participatingIds">원정에 참가한 Id. 비어 있으면 전원에 적용합니다.</param>
    public static void ApplyAfterDungeonRun(
        IReadOnlyList<Character> charactersAfterRun,
        IReadOnlyList<string> participatingIds,
        string runOutcome,
        double partyAvgHpRatio)
    {
        if (charactersAfterRun == null || charactersAfterRun.Count == 0)
            return;

        var idSet = participatingIds != null && participatingIds.Count > 0
            ? new HashSet<string>(participatingIds, StringComparer.OrdinalIgnoreCase)
            : null;

        foreach (var c in charactersAfterRun)
        {
            if (idSet != null && (string.IsNullOrWhiteSpace(c.Id) || !idSet.Contains(c.Id)))
                continue;
            if (string.IsNullOrWhiteSpace(c.Mood))
                c.Mood = "중립";

            var o = (runOutcome ?? "clear").Trim().ToLowerInvariant();
            switch (o)
            {
                case "clear" when partyAvgHpRatio >= 0.45:
                    c.Mood = Pick("뿌듯", "맑음", "여유", "안도");
                    break;
                case "clear" when partyAvgHpRatio >= 0.25:
                    c.Mood = Pick("피곤", "차분", "안도", "집중");
                    break;
                case "clear":
                    c.Mood = Pick("기진", "피곤", "긴장", "안도");
                    break;
                case "retreat":
                    c.Mood = Pick("아쉬움", "집중", "경계", "차분");
                    break;
                case "fail":
                    c.Mood = Pick("좌절", "짜증", "무거움", "경계");
                    break;
                default:
                    c.Mood = Pick("중립", "차분");
                    break;
            }
        }
    }

    /// <summary>동료 자동 대화 한 턴 후 가벼운 기분 변화.</summary>
    public static void ApplyAfterSquadTurn(Character speaker, string? ownLine)
    {
        if (string.IsNullOrWhiteSpace(speaker.Mood))
            speaker.Mood = "중립";
        var line = ownLine ?? "";
        if (ContainsAny(line, "젠장", "짜증", "빌어먹"))
            speaker.Mood = Pick("짜증", "긴장");
        else if (ContainsAny(line, "다행", "좋아", "고마워"))
            speaker.Mood = Pick("기쁨", "맑음", "여유");
        if (Random.Shared.NextDouble() < 0.08)
            speaker.Mood = AmbientDrift(speaker.Mood);
    }

    private static string AmbientDrift(string current)
    {
        var idx = Array.IndexOf(AmbientPool, current);
        if (idx < 0)
            return Pick(AmbientPool);
        var delta = Random.Shared.Next(-1, 2);
        var n = (idx + delta + AmbientPool.Length) % AmbientPool.Length;
        return AmbientPool[n];
    }

    private static bool ContainsAny(string hay, params string[] needles)
    {
        foreach (var n in needles)
            if (hay.Contains(n, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static string Pick(params string[] options) => options[Random.Shared.Next(options.Length)];
}
