using System.Collections.Generic;

namespace GuildDialogue.Data;

/// <summary>
/// 던전 시뮬레이션 출력: 타임라인 + 원정 직후 캐릭터 스냅샷(실시간 DB 반영용).
/// </summary>
public class DungeonSimulationResult
{
    public TestDataRoot Timeline { get; set; } = new();

    /// <summary>원정 반영 후 캐릭터 전체 목록(LoadCharacters와 동일 스키마). 파티 외 인물도 포함.</summary>
    public List<Character> CharactersAfterRun { get; set; } = new();

    /// <summary>이번 원정의 결과 코드: clear | retreat | fail</summary>
    public string RunOutcome { get; set; } = "clear";

    /// <summary>복귀 시 파티 평균 HP 비율(0~1)</summary>
    public double PartyAvgHpRatio { get; set; } = 1.0;

    /// <summary>원정에 참가한 캐릭터 Id(기분·Mood 갱신 대상).</summary>
    public List<string> ParticipatingCharacterIds { get; set; } = new();
}
