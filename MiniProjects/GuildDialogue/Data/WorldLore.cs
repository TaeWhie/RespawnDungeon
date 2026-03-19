using System.Collections.Generic;

namespace GuildDialogue.Data;

public class WorldLore
{
    public string WorldName { get; set; } = "";
    public string WorldSummary { get; set; } = "";
    public string GuildInfo { get; set; } = "";
    public string DungeonSystem { get; set; } = "";
    public string BaseCamp { get; set; } = "";
    public string CurrencyAndLoot { get; set; } = "";
    
    // New structures
    public List<LocationData> Locations { get; set; } = new();
    public List<DungeonData> Dungeons { get; set; } = new();
    
    public List<string> Lore { get; set; } = new();
}

public class LocationData
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = ""; // e.g., City, Forest, Coast
}

public class DungeonData
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Difficulty { get; set; } = ""; // e.g., Low, Mid, High, Abyss
    public List<string> TypicalMonsters { get; set; } = new();
    public List<string> KnownRewards { get; set; } = new();
}
