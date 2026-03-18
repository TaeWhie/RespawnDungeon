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
        DialogueSettings settings)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("당신은 롤플레잉 게임의 NPC 역할을 맡은 인공지능 에이전트입니다.");
        sb.AppendLine("[필수 규칙]");
        sb.AppendLine("1. 출력은 반드시 지정된 JSON 포맷만 사용하세요. 다른 텍스트는 절대 포함하지 마세요.");
        sb.AppendLine("2. 대사(line)는 반드시 순수 한국어로만 작성하세요. 영어, 일본어 등 외래어는 절대 사용하지 마세요.");
        sb.AppendLine("3. 이미 나온 화제를 반복하지 말고, 같은 이야기가 3번 이상 됩니다면 자연스럽게 화제를 전환하세요.");
        sb.AppendLine();
        
        sb.AppendLine("[World State (현재 상황)]");
        sb.AppendLine(worldState); // 예: 장소: 아지트 식당, 시간: 저녁, 동료: 카일, 리나, 브람
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
        sb.AppendLine($"당신의 이름: {speaker.Name} (역할: {speaker.Role})");
        sb.AppendLine($"배경 및 상세설명 (Background): \n{speaker.Background}");
        
        if (speaker.Stats != null)
        {
            sb.AppendLine($"[현재 상태(Stats)]: HP {speaker.Stats.CurrentHP}/{speaker.Stats.MaxHP}, MP {speaker.Stats.CurrentMP}/{speaker.Stats.MaxMP}");
        }
        
        if (speaker.Inventory != null && speaker.Inventory.Count > 0)
        {
            var invItems = new System.Collections.Generic.List<string>();
            foreach(var item in speaker.Inventory) invItems.Add($"{item.ItemName}({item.Count}개)");
            sb.AppendLine($"[현재 소지품(Inventory)]: {string.Join(", ", invItems)}");
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
        sb.AppendLine("지시: 위 10축 지표를 종합적으로 해석하여 입체적인 페르소나를 연기하십시오. 수치가 70 이상인 성향은 당신의 핵심 성격으로 강하게 표출되고, 수치가 30 이하인 성향은 당신이 무관심하거나 결핍된 부분(예: 탐욕이 낮으면 무소유)으로 묘사됩니다.");
        
        // Settings 기반으로 특히 높은 편향치에 대한 추천 톤만 덧대기
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
        
        sb.AppendLine($"대화 상대: {listener.Name} ({listener.Role})");
        sb.AppendLine("당신은 대화 내역의 마지막 말에 자연스럽게 반응하며, 자신의 성격에 맞춰 최근 던전 경험을 회상하거나 안도감을 표현하세요.");
        sb.AppendLine("단, 대화 분위기상 '다음 탐험 주의하자'는 뉘앙스의 결론이 이미 나왔다면, 다른 각도(장비 이야기, 보상 이야기, 동료 개인 감정 등)로 자연스럽게 전환하세요.");
        
        sb.AppendLine();
        sb.AppendLine("[Output Format Constraint]");
        sb.AppendLine("답변은 반드시 아래 JSON 형식으로만 작성하세요. 다른 텍스트는 절대 덧붙이지 마세요.");
        sb.AppendLine("{\"tone\": \"감정이나 어조\", \"intent\": \"의도 요약\", \"line\": \"캐릭터가 실제로 할 대사 한 줄\"}");
        
        return sb.ToString();
    }

    public static string BuildUserPrompt(string workingMemoryContext, string? randomTopic = null)
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
            sb.AppendLine("위 대화를 이어받아 당신(화자)의 다음 대사를 1문장 내외로 자연스럽게 생성해주세요. 반드시 위에서 제시된 JSON으로만 응답해야 합니다.");
        }
        return sb.ToString();
    }
}
