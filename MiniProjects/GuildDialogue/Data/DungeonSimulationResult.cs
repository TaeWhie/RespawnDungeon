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
}
