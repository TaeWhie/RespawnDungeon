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
}
