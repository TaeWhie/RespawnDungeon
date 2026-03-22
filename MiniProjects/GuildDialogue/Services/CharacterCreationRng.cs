using System;
using System.Collections.Generic;
using System.Linq;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>캐릭터 생성 시 나이·경력·접수대 위치 등 Ollama 프롬프트에 넣을 수치·배치만 담당합니다.</summary>
public static class CharacterCreationRng
{
    private static readonly string[] ReceptionNotes = {
        "접수대에서 등록 서류를 제출한 직후.",
        "신규 모험가 등록 절차를 마치고 대기 중.",
        "의뢰 접수 창구 앞에서 번호표를 기다리는 중."
    };

    public static int RollAge(Random rng) => rng.Next(16, 51);

    /// <summary>경력 0~20. 나이에 따른 상한(14세 이전 활동 없음 가정)과 낮은 값 쪽 가중치.</summary>
    public static int RollCareerYears(int age, Random rng)
    {
        var maxByAge = Math.Min(20, Math.Max(0, age - 14));
        if (maxByAge <= 0)
            return 0;

        double sum = 0;
        var w = new double[maxByAge + 1];
        for (var k = 0; k <= maxByAge; k++)
        {
            w[k] = 1.0 / Math.Pow(k + 1, 1.35);
            sum += w[k];
        }

        var pick = rng.NextDouble() * sum;
        double acc = 0;
        for (var k = 0; k <= maxByAge; k++)
        {
            acc += w[k];
            if (pick <= acc)
                return k;
        }

        return 0;
    }

    public static string ResolveReceptionLocationId(IReadOnlyList<BaseFacilityData> bases)
    {
        if (bases == null || bases.Count == 0)
            return "reception";

        var exact = bases.FirstOrDefault(b =>
            string.Equals(b.BaseId?.Trim(), "reception", StringComparison.OrdinalIgnoreCase));
        if (exact != null)
            return exact.BaseId.Trim();

        var byName = bases.FirstOrDefault(b =>
            b.Name != null && (b.Name.Contains("접수") || b.Name.Contains("등록")));
        if (byName != null)
            return byName.BaseId.Trim();

        return bases[0].BaseId.Trim();
    }

    public static string? RollReceptionLocationNote(Random rng)
    {
        if (rng.Next(4) == 0)
            return null;
        return ReceptionNotes[rng.Next(ReceptionNotes.Length)];
    }
}
