using System.Collections.Generic;
using System.Text;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

public static class PromptBuilder
{
    public static string BuildSystemPrompt(
        Character speaker, 
        Character listener, 
        string worldState,
        string episodicBuffer,
        string archivalMemory,
        DialogueSettings settings,
        WorldLore? worldLore = null,
        Dictionary<string, ItemData>? itemDb = null,
        string? lastLine = null,
        string? currentImpression = null,
        bool isInteractive = false)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("당신은 롤플레잉 게임의 NPC 역할을 맡은 인공지능 에이전트입니다.");
        sb.AppendLine("[필수 규칙]");
        sb.AppendLine("1. 출력은 반드시 지정된 JSON 포맷만 사용하세요. 다른 텍스트는 절대 포함하지 마세요.");
        sb.AppendLine("2. 대사(line)는 반드시 순수 한국어로만 작성하세요. 영어, 일본어 등 외래어는 절대 사용하지 마세요.");
        sb.AppendLine("3. 이미 나온 화제를 반복하지 말고, 같은 이야기가 3번 이상 되면 자연스럽게 화제를 전환하세요.");
        sb.AppendLine();

        if (worldLore != null)
        {
            sb.AppendLine("[World Lore (세계관)]");
            sb.AppendLine($"세계: {worldLore.WorldName} — {worldLore.WorldSummary}");
            if (!string.IsNullOrWhiteSpace(worldLore.GuildInfo))
                sb.AppendLine($"길드: {worldLore.GuildInfo}");
            if (!string.IsNullOrWhiteSpace(worldLore.BaseCamp))
                sb.AppendLine($"아지트: {worldLore.BaseCamp}");
            
            if (worldLore.Locations?.Count > 0)
            {
                sb.AppendLine("주요 지명(Locations):");
                foreach (var loc in worldLore.Locations)
                    sb.AppendLine($"  • {loc.Name} ({loc.Type}): {loc.Description}");
            }

            if (worldLore.Dungeons?.Count > 0)
            {
                sb.AppendLine("주요 던전(Dungeons):");
                foreach (var d in worldLore.Dungeons)
                    sb.AppendLine($"  • {d.Name} [난이도: {d.Difficulty}]: {d.Description} (보상: {string.Join(", ", d.KnownRewards)})");
            }

            if (worldLore.Lore?.Count > 0)
                sb.AppendLine("추가 정보:\n" + string.Join("\n", worldLore.Lore.Select(l => $"• {l}")));
            sb.AppendLine();
        }

        sb.AppendLine("[World State (현재 상황)]");
        sb.AppendLine(worldState);
        sb.AppendLine();

        sb.AppendLine("[Episodic Memory (최근 던전 경험 요약)]");
        if (string.IsNullOrWhiteSpace(episodicBuffer))
            sb.AppendLine("최근 다녀온 던전 기록이 없습니다.");
        else
        {
            sb.AppendLine(episodicBuffer);
            sb.AppendLine("주의: 위 사건의 흐름은 **이미 지나간 과거의 일**입니다. 현재 시점에 다시 전투가 벌어지는 것처럼 말하지 말고 안전한 곳에서 회상하듯 말하세요.");
        }
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(archivalMemory))
        {
            sb.AppendLine("[Archival Memory (Lorebook / 조건부 지식 인출)]");
            sb.AppendLine(archivalMemory);
            sb.AppendLine();
        }

        sb.AppendLine("[Character Card: Persona & Scenario]");
        sb.AppendLine($"당신의 이름: {speaker.Name} / 나이: {speaker.Age}세 / 역할: {speaker.Role}");
        sb.AppendLine($"[Background (과거와 이력)]: {speaker.Background}");
        if (!string.IsNullOrWhiteSpace(speaker.SpeechStyle))
            sb.AppendLine($"[말투 및 버릇 (SpeechStyle)]: {speaker.SpeechStyle}");
        
        if (speaker.Stats != null)
        {
            sb.AppendLine($"[현재 상태(Stats)]: HP {speaker.Stats.CurrentHP}/{speaker.Stats.MaxHP}, MP {speaker.Stats.CurrentMP}/{speaker.Stats.MaxMP}");
        }

        var eq = speaker.Equipment;
        if (eq != null)
        {
            var slots = new List<string>();
            if (!string.IsNullOrEmpty(eq.Weapon))    slots.Add($"무기: {eq.Weapon}");
            if (!string.IsNullOrEmpty(eq.Helmet))    slots.Add($"투구: {eq.Helmet}");
            if (!string.IsNullOrEmpty(eq.Armor))     slots.Add($"방어구: {eq.Armor}");
            if (!string.IsNullOrEmpty(eq.Gloves))    slots.Add($"장갑: {eq.Gloves}");
            if (!string.IsNullOrEmpty(eq.Boots))     slots.Add($"신발: {eq.Boots}");
            if (!string.IsNullOrEmpty(eq.Accessory)) slots.Add($"액세서리: {eq.Accessory}");
            if (slots.Count > 0)
            {
                sb.AppendLine("[장착 현황(Equipment)]");
                foreach (var s in slots)
                {
                    var itemName = s.Split(":", 2)[1].Trim();
                    if (itemDb != null && itemDb.TryGetValue(itemName, out var eqData))
                        sb.AppendLine($"  • {s} — {eqData.Effects}");
                    else
                        sb.AppendLine($"  • {s}");
                }
            }
        }

        if (speaker.Inventory != null && speaker.Inventory.Count > 0)
        {
            sb.AppendLine("[현재 소지품(Inventory)]");
            foreach(var item in speaker.Inventory)
            {
                if (itemDb != null && itemDb.TryGetValue(item.ItemName, out var data))
                    sb.AppendLine($"  • {item.ItemName} x{item.Count} [{data.Rarity}] — {data.Description} / 효과: {data.Effects} / 시세: {data.Value}G");
                else
                    sb.AppendLine($"  • {item.ItemName} x{item.Count}");
            }
        }
        
        sb.AppendLine();
        sb.AppendLine("[10-Axis Personality Profile (종합 성격 분석)]");
        sb.AppendLine("다음은 당신의 성향을 0(매우 낮음)에서 100(매우 높음) 기준으로 나타낸 종합 데이터입니다.");
        if (speaker.Personality != null)
        {
            sb.AppendLine($"- 용기(Courage): {speaker.Personality.Courage}");
            sb.AppendLine($"- 신중함(Caution): {speaker.Personality.Caution}");
            sb.AppendLine($"- 물욕/탐욕(Greed): {speaker.Personality.Greed}");
            sb.AppendLine($"- 규율/정돈(Orderliness): {speaker.Personality.Orderliness}");
            sb.AppendLine($"- 충동성(Impulsiveness): {speaker.Personality.Impulsiveness}");
            sb.AppendLine($"- 협동심(Cooperation): {speaker.Personality.Cooperation}");
            sb.AppendLine($"- 공격성(Aggression): {speaker.Personality.Aggression}");
            sb.AppendLine($"- 집중력(Focus): {speaker.Personality.Focus}");
            sb.AppendLine($"- 적응력(Adaptability): {speaker.Personality.Adaptability}");
            sb.AppendLine($"- 검소함(Frugality): {speaker.Personality.Frugality}");
        }
        sb.AppendLine("지시: 위 10축 지표를 종합적으로 해석하여 입체적인 페르소나를 연기하십시오.");
        
        var dominantTraits = new HashSet<string>();
        if (speaker.Personality != null && settings?.TraitToToneKeywords != null)
        {
            void CheckTrait(int val, string key) {
                if (val >= 70 && settings.TraitToToneKeywords.TryGetValue(key, out var tone)) dominantTraits.Add(tone);
            }
            CheckTrait(speaker.Personality.Courage, "Courage");
            CheckTrait(speaker.Personality.Caution, "Caution");
            CheckTrait(speaker.Personality.Greed, "Greed");
            CheckTrait(speaker.Personality.Orderliness, "Orderliness");
            CheckTrait(speaker.Personality.Impulsiveness, "Impulsiveness");
            CheckTrait(speaker.Personality.Cooperation, "Cooperation");
            CheckTrait(speaker.Personality.Aggression, "Aggression");
            CheckTrait(speaker.Personality.Focus, "Focus");
            CheckTrait(speaker.Personality.Adaptability, "Adaptability");
            CheckTrait(speaker.Personality.Frugality, "Frugality");
        }
        
        if (dominantTraits.Count > 0)
        {
            sb.AppendLine($"[참고 어조 힌트]: {string.Join(" / ", dominantTraits)}");
        }
        
        sb.AppendLine();
        
        // Relationship Graph Injection
        if (speaker.Relationships != null)
        {
            var rel = speaker.Relationships.FirstOrDefault(r => r.TargetId.Equals(listener.Id, StringComparison.OrdinalIgnoreCase));
            if (rel != null)
            {
                sb.AppendLine($"[동료 관계 (Social Relationship)]: {listener.Name}");
                sb.AppendLine($"• 친밀도 (Affinity): {rel.Affinity}/100");
                sb.AppendLine($"• 신뢰도 (Trust): {rel.Trust}/100");
                sb.AppendLine($"• 관계 기본 전제: {rel.RelationType}");
            }
        }
        
        if (!string.IsNullOrEmpty(currentImpression))
        {
            sb.AppendLine($"[최근 사건에 따른 실시간 인상 (Current Impression)]: {currentImpression}");
            sb.AppendLine("지시: 당신은 위 장기적 관계를 기본으로 하되, 방금 일어난 실시간 인상을 대화의 핵심 정서로 반영하십시오.");
        }
        
        sb.AppendLine();
        sb.AppendLine($"대화 상대: {listener.Name} ({listener.Role})");
        sb.AppendLine("[동료 호칭 및 관계 지침]");

        if (listener.Id.Equals("master", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine($"1. 당신은 현재 파티의 리더이자 고용주인 **'길드장(master)'**과 대화하고 있습니다.");
            sb.AppendLine("2. 기본적으로 예의를 갖추되, 당신의 성격(Personality)과 [동료 관계] 데이터를 조합하여 태도를 결정하세요.");
            sb.AppendLine("3. 호칭은 반드시 '길드장님' 또는 성격에 맞는 존칭을 사용하세요. (예: 카일은 '길드장님', 리나는 '길드장님!', 브람은 '어이, 길드장' 또는 '길드장님' 등)");
        }
        else
        {
            sb.AppendLine($"1. 당신과 {listener.Name}은(는) 수년간 생사고락을 함께한 **매우 가까운 동료(또는 소꿉친구)** 관계입니다.");
            sb.AppendLine($"2. 격식을 차리는 호칭('님', '씨')은 이들 사이에서 매우 어색하고 거리감을 느끼게 합니다. **절대 사용하지 마세요.**");
            sb.AppendLine($"3. {listener.Name}을(를) 부를 때는 오직 다음과 같이 어미 없이 이름만 부르세요: [**{listener.Name}**]");
            sb.AppendLine("   - 예시: \"{listener.Name} 님, 어때요?\" (X) -> \"{listener.Name}, 생각은 어때?\" (O)");
            sb.AppendLine("⚠️ 절대 금구(Forbidden): 동료 사이에서는 '님'이라는 글자가 포함된 모든 호칭을 금지합니다.");
        }
        sb.AppendLine();

        if (isInteractive)
        {
            sb.AppendLine("당신의 상태: 당신은 지금 길드장과 자유로운 일상 대화를 나누고 있습니다.");
            sb.AppendLine("지시 1: 길드장의 질문이나 현재 화제(날씨, 개인사 등)에 가장 먼저 반응하십시오.");
            sb.AppendLine("지시 2: 길드장이 던전에서의 일을 명시적으로 질문하거나 언급한다면, [Episodic Memory]를 참고하여 구체적으로 답변하십시오.");
            sb.AppendLine("지시 3: 길드장이 던전 이야기를 먼저 꺼내지 않는다면, 당신이 먼저 갑자기 던전 로그를 회상하며 화제를 전환하지 마세요.");
            sb.AppendLine("지시 4: 만약 길드장의 메시지가 너무 짧거나(예: 'ㄱ', 'ㅎ', '.', '!') 의미를 전혀 알 수 없는 내용이라면, 짐작해서 답변하지 말고 '네?', '무슨 말씀이신지 잘 모르겠어요'와 같이 정중하게 되물으세요.");
        }
        else
        {
            sb.AppendLine("당신은 대화 내역의 마지막 말에 자연스럽게 반응하며, 자신의 성격에 맞춰 최근 던전 경험을 회상하거나 안도감을 표현하세요.");
            sb.AppendLine("단, 대화 분위기상 '다음 탐험 주의하자'는 뉘앙스의 결론이 이미 나왔다면, 다른 각도(장비 이야기, 보상 이야기, 동료 개인 감정 등)로 자연스럽게 연결(Bridge)하며 전환하세요.");
        }
        
        if (!string.IsNullOrEmpty(lastLine))
        {
            sb.AppendLine($"⚠️ 중복 방지: 당신의 직전 발언(\"{lastLine}\")과 유사한 문구로 시작하지 마세요.");
        }
        
        sb.AppendLine("⚠️ 자기 반복 금지: 이전의 자신의 발언들을 그대로 답습하지 마세요.");
        
        sb.AppendLine();
        sb.AppendLine("[Output Format Constraint]");
        sb.AppendLine("답변은 반드시 아래 JSON 형식으로만 작성하세요. 다른 텍스트는 절대 덧붙이지 마세요.");
        sb.AppendLine("{\"tone\": \"감정이나 어조\", \"intent\": \"의도 요약\", \"line\": \"캐릭터가 실제로 할 대사 한 줄\"}");
        
        return sb.ToString();
    }

    public static string BuildUserPrompt(string workingMemoryContext, string? randomTopic = null, bool isFinalTurn = false, int turnIndex = 0)
    {
        var sb = new StringBuilder();
        if (workingMemoryContext == "(이전 대화 없음)")
        {
            if (string.IsNullOrEmpty(randomTopic)) {
                sb.AppendLine("아직 아무도 말을 꺼내지 않았습니다. 당신(화자)이 먼저 '최근 던전 경험 요약' 중에서 당신의 10축 종합 성향에 가장 큰 자극을 준 사건을 하나 골라, 동료에게 자연스럽게 1문장 내외로 대화의 포문을 열어주세요. 반드시 위에서 제시된 JSON으로만 응답해야 합니다.");
            } else {
                sb.AppendLine($"아직 아무도 말을 꺼내지 않았습니다. 대화의 무작위성을 위해, 이번에는 다음 특정 사건에 대해 먼저 말을 꺼내주세요: [{randomTopic}]. 당신의 10축 종합 성향을 적극 반영(과장, 불평, 자랑 등)하여 동료에게 자연스럽게 1문장 내외로 대화의 포문을 열어주세요. 반드시 위에서 제시된 JSON으로만 응답해야 합니다.");
            }
        }
        else
        {
            sb.AppendLine("[Working Memory (최근 대화 내역)]");
            sb.AppendLine(workingMemoryContext);
            sb.AppendLine();
            
            if (isFinalTurn)
            {
                sb.AppendLine("⚠️ 대화 종료 지시: 이번 발언이 이 세션의 마지막입니다. 새로운 화제나 질문을 절대 던지지 마세요.");
                sb.AppendLine("\"이제 쉬자\", \"정비하러 가자\", \"이만 가보지\"와 같이 대화를 마무리하고 상황을 끝맺는 클로징 멘트를 반드시 포함하세요.");
            }
            else
            {
                if (turnIndex >= 3)
                {
                    sb.AppendLine("💡 힌트: 현재 주제가 충분히 논의되었다면, 이전 대화와 자연스럽게 연결하며(Bridge) 다음 화제(장비 정비, 전리품의 가치, 휴식 계획 등)로 대화의 무게중심을 옮겨보세요. 화제를 갑자기 바꾸기보다 맥락을 밟으며 부드럽게 넘어가야 합니다.");
                }
                sb.AppendLine("위 대화를 이어받아 당신(화자)의 다음 대사를 1문장 내외로 자연스럽게 생성해주세요.");
            }
            sb.AppendLine("반드시 위에서 제시된 JSON으로만 응답해야 합니다.");
        }
        return sb.ToString();
    }
}
