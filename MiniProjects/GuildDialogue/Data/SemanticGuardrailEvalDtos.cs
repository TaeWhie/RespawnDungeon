using System.Collections.Generic;

namespace GuildDialogue.Data;

/// <summary>
/// <see cref="Services.SemanticGuardrailEvalRunner"/> — Config/SemanticGuardrailEval.json
/// </summary>
public class SemanticGuardrailEvalRoot
{
    public string? Description { get; set; }
    public List<SemanticGuardrailEvalCase> Cases { get; set; } = new();
}

public class SemanticGuardrailEvalCase
{
    /// <summary>플레이어 발화.</summary>
    public string Utterance { get; set; } = "";

    /// <summary>기대 변칙 종류: None | Gibberish | MetaOrSystem | OffWorldCasual</summary>
    public string Expect { get; set; } = "None";

    /// <summary>테스트 설명(선택).</summary>
    public string? Note { get; set; }
}
