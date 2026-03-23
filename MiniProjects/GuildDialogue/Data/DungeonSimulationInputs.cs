using System.Collections.Generic;

namespace GuildDialogue.Data;

/// <summary>
/// ActionLog 시뮬레이터가 참조하는 Config 데이터 묶음.
/// </summary>
public class DungeonSimulationInputs
{
    /// <summary>
    /// 설정 시 해당 PartyId를 가진 캐릭터만 원정(비어 있으면 master 제외 전원).
    /// </summary>
    public string? SimulationPartyId { get; init; }

    public IReadOnlyList<Character> Characters { get; init; } = new List<Character>();
    public IReadOnlyList<MonsterData> Monsters { get; init; } = new List<MonsterData>();
    public IReadOnlyList<TrapTypeData> Traps { get; init; } = new List<TrapTypeData>();
    public IReadOnlyList<ItemData> Items { get; init; } = new List<ItemData>();
    public IReadOnlyList<BaseFacilityData> Bases { get; init; } = new List<BaseFacilityData>();
    public WorldLore? Lore { get; init; }

    /// <summary>지정 시 <see cref="DungeonRunSimulator"/>가 해당 이름 던전만 사용합니다.</summary>
    public string? DungeonNameOverride { get; init; }

    /// <summary>지정 시 해당 층 문자열을 로그·시뮬 전체에 사용합니다 (예: 2, B1).</summary>
    public string? FloorLabelOverride { get; init; }
}
