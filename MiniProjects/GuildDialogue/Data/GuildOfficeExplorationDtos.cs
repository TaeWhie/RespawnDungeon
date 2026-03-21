using System.Collections.Generic;

namespace GuildDialogue.Data;

/// <summary><see cref="Services.GuildOfficeExplorationRunner"/> — 탐색용 발화 묶음.</summary>
public class GuildOfficeExplorationRoot
{
    public string? Description { get; set; }
    public List<GuildOfficeExplorationCase> Cases { get; set; } = new();
}

public class GuildOfficeExplorationCase
{
    /// <summary>그룹 라벨(분석용, 로직에 미사용).</summary>
    public string Label { get; set; } = "";

    public string Utterance { get; set; } = "";
}
