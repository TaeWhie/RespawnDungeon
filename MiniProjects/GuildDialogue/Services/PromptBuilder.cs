using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// RAG 방식: 설정에서 N값을 읽어 컨텍스트를 자르고, 프롬프트 문자열을 조합한다.
/// </summary>
public class PromptBuilder
{
    private readonly DialogueSettings _settings;
    private readonly bool _useFallbackCounts;

    public PromptBuilder(DialogueSettings settings, bool useFallbackCounts = false)
    {
        _settings = settings;
        _useFallbackCounts = useFallbackCounts;
    }

    public int MaxRecentTurns => _useFallbackCounts ? _settings.MaxRecentTurnsFallback : _settings.MaxRecentTurns;
    public int MaxDungeonLogs => _useFallbackCounts ? _settings.MaxDungeonLogsFallback : _settings.MaxDungeonLogs;

    /// <summary>구조화된 던전 이벤트만으로 프롬프트용 한 줄 문자열 생성(줄글 필드 사용 안 함).</summary>
    public static string FormatDungeonEvent(DungeonEvent e)
    {
        var parts = new List<string>();
        switch (e.EventType)
        {
            case "Trap":
                parts.Add(e.Location != null ? $"{e.Location} 함정 발동" : "함정 발동");
                if (e.TargetId != null) parts.Add($"{e.TargetId} 데미지 {e.Damage ?? 0}");
                if (e.HpBefore.HasValue && e.HpAfter.HasValue) parts.Add($"HP {e.HpBefore}→{e.HpAfter}");
                break;
            case "Combat":
                if (e.Enemies != null && e.Enemies.Count > 0)
                    parts.Add(string.Join(", ", e.Enemies.Select(x => x.Count > 1 ? $"{x.Name} {x.Count}마리" : $"{x.Name} 1마리")) + " 전투");
                if (e.ActorId != null) parts.Add($"{e.ActorId} 선공");
                if (e.Turns.HasValue) parts.Add($"{e.Turns}턴 만에 격파");
                if (e.BlockCount.HasValue && e.BlockCount > 0) parts.Add($"방패 막기 {e.BlockCount}회");
                break;
            case "Artifact":
                parts.Add(e.ItemName != null ? $"유물 '{e.ItemName}' 획득" : "유물 획득");
                if (e.Location != null) parts.Add(e.Location);
                break;
            case "Heal":
                if (e.ActorId != null && e.TargetId != null) parts.Add($"{e.ActorId}가 {e.TargetId}에게 치유");
                if (e.HpBefore.HasValue && e.HpAfter.HasValue) parts.Add($"HP {e.HpBefore}→{e.HpAfter}");
                break;
            case "ConsumePotion":
                if (e.ActorId != null) parts.Add($"{e.ActorId}가 포션 사용");
                if (e.ItemName != null) parts.Add(e.ItemName);
                if (e.ItemCount.HasValue) parts.Add($"{e.ItemCount}개");
                if (e.MpBefore.HasValue && e.MpAfter.HasValue) parts.Add($"MP {e.MpBefore}→{e.MpAfter}");
                break;
            case "Loot":
                if (e.LootItems != null && e.LootItems.Count > 0)
                    parts.Add("루팅: " + string.Join(", ", e.LootItems.Select(x => x.Count > 1 ? $"{x.ItemName} {x.Count}" : x.ItemName)));
                if (e.Location != null) parts.Add(e.Location);
                if (e.ActorId != null) parts.Add($"수령 {e.ActorId}");
                break;
            case "TrapAvoided":
                parts.Add(e.Location != null ? $"{e.Location} 함정 회피" : "함정 회피");
                if (e.AvoidedCount.HasValue && e.AvoidedCount > 0) parts.Add($"{e.AvoidedCount}곳 미발동");
                break;
            case "ItemTransfer":
                if (e.FromId != null && e.ToId != null && e.ItemName != null)
                    parts.Add($"{e.FromId}→{e.ToId} {e.ItemName} {(e.ItemCount ?? 1)}개");
                break;
            case "InventorySort":
                if (e.ActorId != null) parts.Add($"{e.ActorId} 가방 정리");
                if (e.Location != null) parts.Add(e.Location);
                break;
            case "PartyCheck":
                if (e.ActorId != null) parts.Add($"{e.ActorId} 파티 확인");
                if (e.CheckType != null) parts.Add(e.CheckType);
                break;
            case "DebuffClear":
                if (e.ActorId != null && e.TargetId != null) parts.Add($"{e.ActorId}가 {e.TargetId} 디버프 해제");
                if (e.ClearCount.HasValue) parts.Add($"{e.ClearCount}회");
                break;
        }
        return parts.Count > 0 ? string.Join(", ", parts) : e.EventType;
    }

    /// <summary>
    /// 화자/상대 관점이 드러나도록 이벤트를 다시 기술한다.
    /// 예: "리나가 포션 사용" → 화자가 리나인 경우 "내가 회복 포션 1개 사용".
    /// </summary>
    private static string FormatDungeonEventForSpeaker(DungeonEvent e, Character speaker, Character? other)
    {
        string AsActor(string? id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            if (speaker.Name == id) return "내가";
            if (other != null && other.Name == id) return $"{other.Name}가";
            return $"{id}가";
        }

        string AsTarget(string? id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            if (speaker.Name == id) return "나에게";
            if (other != null && other.Name == id) return $"{other.Name}에게";
            return $"{id}에게";
        }

        var parts = new List<string>();
        switch (e.EventType)
        {
            case "Trap":
                {
                    var who = AsTarget(e.TargetId);
                    var loc = e.Location ?? "어딘가에서";
                    if (!string.IsNullOrEmpty(who))
                        parts.Add($"{loc} 함정이 {who} 터짐");
                    else
                        parts.Add($"{loc} 함정 발동");
                    if (e.Damage.HasValue) parts.Add($"데미지 {e.Damage}");
                    if (e.HpBefore.HasValue && e.HpAfter.HasValue) parts.Add($"HP {e.HpBefore}→{e.HpAfter}");
                    break;
                }
            case "Combat":
                {
                    if (e.Enemies != null && e.Enemies.Count > 0)
                        parts.Add(string.Join(", ", e.Enemies.Select(x => x.Count > 1 ? $"{x.Name} {x.Count}마리" : $"{x.Name} 1마리")) + "와 전투");
                    var who = AsActor(e.ActorId);
                    if (!string.IsNullOrEmpty(who)) parts.Add($"{who} 선공");
                    if (e.Turns.HasValue) parts.Add($"{e.Turns}턴 만에 격파");
                    if (e.BlockCount.HasValue && e.BlockCount > 0) parts.Add($"방패 막기 {e.BlockCount}회");
                    break;
                }
            case "Artifact":
                {
                    var name = e.ItemName ?? "유물";
                    var loc = e.Location != null ? $"({e.Location})" : "";
                    parts.Add($"{name} 획득 {loc}".Trim());
                    break;
                }
            case "Heal":
                {
                    var who = AsActor(e.ActorId);
                    var to = AsTarget(e.TargetId);
                    if (!string.IsNullOrEmpty(who) && !string.IsNullOrEmpty(to))
                        parts.Add($"{who} {to} 치유");
                    if (e.HpBefore.HasValue && e.HpAfter.HasValue) parts.Add($"HP {e.HpBefore}→{e.HpAfter}");
                    break;
                }
            case "ConsumePotion":
                {
                    var who = AsActor(e.ActorId);
                    if (!string.IsNullOrEmpty(who))
                        parts.Add($"{who} {e.ItemName ?? "포션"} 사용");
                    else
                        parts.Add($"{e.ItemName ?? "포션"} 사용");
                    if (e.ItemCount.HasValue) parts.Add($"{e.ItemCount}개");
                    if (e.MpBefore.HasValue && e.MpAfter.HasValue) parts.Add($"MP {e.MpBefore}→{e.MpAfter}");
                    break;
                }
            case "Loot":
                {
                    if (e.LootItems != null && e.LootItems.Count > 0)
                        parts.Add("루팅: " + string.Join(", ", e.LootItems.Select(x => x.Count > 1 ? $"{x.ItemName} {x.Count}" : x.ItemName)));
                    if (e.Location != null) parts.Add(e.Location);
                    var who = AsActor(e.ActorId);
                    if (!string.IsNullOrEmpty(who)) parts.Add($"{who} 수령");
                    break;
                }
            case "TrapAvoided":
                {
                    var loc = e.Location ?? "어딘가에서";
                    var who = AsActor(e.ActorId);
                    if (!string.IsNullOrEmpty(who))
                        parts.Add($"{loc} 함정을 {who} 회피");
                    else
                        parts.Add($"{loc} 함정 회피");
                    if (e.AvoidedCount.HasValue && e.AvoidedCount > 0) parts.Add($"{e.AvoidedCount}곳 미발동");
                    break;
                }
            case "ItemTransfer":
                {
                    if (e.FromId != null && e.ToId != null && e.ItemName != null)
                    {
                        var from = AsActor(e.FromId).Replace("가", "");
                        var to = AsTarget(e.ToId).Replace("에게", "");
                        parts.Add($"{from}→{to} {e.ItemName} {(e.ItemCount ?? 1)}개 전달");
                    }
                    break;
                }
            case "InventorySort":
                {
                    var who = AsActor(e.ActorId);
                    if (!string.IsNullOrEmpty(who))
                        parts.Add($"{who} 가방 정리");
                    if (e.Location != null) parts.Add(e.Location);
                    break;
                }
            case "PartyCheck":
                {
                    var who = AsActor(e.ActorId);
                    if (!string.IsNullOrEmpty(who))
                        parts.Add($"{who} 파티 확인({e.CheckType})");
                    break;
                }
            case "DebuffClear":
                {
                    var who = AsActor(e.ActorId);
                    var to = AsTarget(e.TargetId);
                    if (!string.IsNullOrEmpty(who) && !string.IsNullOrEmpty(to))
                        parts.Add($"{who} {to} 디버프 해제");
                    if (e.ClearCount.HasValue) parts.Add($"{e.ClearCount}회");
                    break;
                }
        }

        return parts.Count > 0 ? string.Join(", ", parts) : FormatDungeonEvent(e);
    }

    /// <summary>성격 수치 상위 2~3개를 설정의 TraitToToneKeywords로 말투 가이드 문자열 생성.</summary>
    public string BuildToneGuide(Character character)
    {
        var p = character.Personality;
        var traits = new List<(string name, int value)>
        {
            ("Courage", p.Courage), ("Caution", p.Caution), ("Greed", p.Greed),
            ("Orderliness", p.Orderliness), ("Impulsiveness", p.Impulsiveness),
            ("Cooperation", p.Cooperation), ("Aggression", p.Aggression),
            ("Focus", p.Focus), ("Adaptability", p.Adaptability), ("Frugality", p.Frugality)
        };
        var top = traits.OrderByDescending(x => x.value).Take(3).ToList();
        var parts = new List<string>();
        foreach (var (name, _) in top)
        {
            if (_settings.TraitToToneKeywords.TryGetValue(name, out var keywords))
                parts.Add(keywords);
        }
        if (parts.Count == 0) return "말투: 중립.";
        return "말투: " + string.Join(", ", parts.Distinct()) + ".";
    }

    public string BuildPrompt(DialogueRequest request)
    {
        var speaker = request.Speaker ?? throw new ArgumentNullException(nameof(request.Speaker));
        var recentTurns = request.RecentTurns.Take(MaxRecentTurns).ToList();
        var logs = request.DungeonLogs.Take(MaxDungeonLogs).ToList();

        var toneGuide = BuildToneGuide(speaker);
        var logLines = logs.Select(l =>
        {
            var head = $"[{l.DungeonName} {l.FloorOrZone}]";
            var body = string.Join("; ", l.Events.Select(ev => FormatDungeonEventForSpeaker(ev, speaker, request.OtherCharacter)));
            var tail = l.Outcome != null ? $" → 결과: {l.Outcome}" : "";
            var party = l.PartyMembers != null && l.PartyMembers.Count > 0 ? $" (파티: {string.Join(", ", l.PartyMembers)})" : "";
            return $"{head}{party} {body}{tail}";
        }).ToList();
        var turnLines = recentTurns.Select(t => $"{t.SpeakerId}: {t.Text}").ToList();

        var blocks = new List<string>
        {
            "[캐릭터]",
            $"이름: {speaker.Name}, 역할: {speaker.Role}",
            $"배경: {speaker.Background}",
            toneGuide
        };

        if (speaker.Stats != null)
        {
            var s = speaker.Stats;
            blocks.Add($"스탯(현재): HP {s.CurrentHP}/{s.MaxHP}, MP {s.CurrentMP}/{s.MaxMP}, 공격력 {s.Atk}, 방어력 {s.Def}");
        }
        if (speaker.Inventory != null && speaker.Inventory.Count > 0)
        {
            var invLines = speaker.Inventory
                .Select(i => i.Count > 1 ? $"{i.ItemName}({i.ItemType}) {i.Count}개" : $"{i.ItemName}({i.ItemType})")
                .ToList();
            blocks.Add($"소지품: {string.Join(", ", invLines)}");
        }

        if (request.IsCompanionDialogue && request.OtherCharacter != null)
        {
            blocks.Add("[상대]");
            blocks.Add($"이름: {request.OtherCharacter.Name}, 역할: {request.OtherCharacter.Role}");
        }

        // 던전 로그 전체를 훑어서, 이번 대화에서 다룰 핵심 주제 후보를 뽑는다.
        // 화자의 성격(Personality)에 따라 어떤 종류의 사건을 더 말하고 싶어할지 가중치를 준다.
        var p = speaker.Personality;
        var topics = new List<(string text, int score)>();

        foreach (var log in logs)
        {
            foreach (var ev in log.Events)
            {
                // 기본 점수와 유형별 가중치
                int score = 0;
                string? text = null;

                switch (ev.EventType)
                {
                    case "Trap" when ev.TargetId == speaker.Name:
                        // 조심성/질서/집중이 높은 캐릭터는 함정·리스크를 많이 이야기하고 싶어함
                        score = 10 + p.Caution + p.Orderliness + p.Focus;
                        text = $"함정: {log.DungeonName} {log.FloorOrZone} {ev.Location}에서 {speaker.Name}가 데미지 {ev.Damage ?? 0}을 입은 일";
                        break;
                    case "Artifact" when ev.ItemName != null:
                        // 탐욕/집중/용기가 높은 캐릭터는 유물·보상을 더 강조
                        score = 10 + p.Greed + p.Focus + p.Courage;
                        text = $"유물: {log.DungeonName} {log.FloorOrZone}에서 '{ev.ItemName}'을(를) 획득한 일";
                        break;
                    case "ConsumePotion" when ev.ActorId == request.OtherCharacter?.Name:
                        // 절약/조심성/협동이 높은 캐릭터는 동료의 포션 사용·자원 관리에 관심
                        score = 10 + p.Frugality + p.Caution + p.Cooperation;
                        text = $"포션: 동료 {ev.ActorId}가 {ev.ItemName ?? "포션"} {ev.ItemCount ?? 1}개를 사용한 일";
                        break;
                    case "Combat" when ev.ActorId == speaker.Name:
                        // 용기/공격성/집중이 높은 캐릭터는 전투·자신의 활약을 말하고 싶어함
                        score = 8 + p.Courage + p.Aggression + p.Focus;
                        text = $"전투: {log.DungeonName} {log.FloorOrZone}에서 {speaker.Name}가 {string.Join(", ", ev.Enemies?.Select(x => $"{x.Name} {x.Count}마리") ?? Array.Empty<string>())}와 싸운 일";
                        break;
                }

                if (score > 0 && text != null)
                    topics.Add((text, score));
            }
        }

        if (topics.Count > 0)
        {
            var picked = topics
                .GroupBy(t => t.text)
                .Select(g => (text: g.Key, score: g.Max(x => x.score)))
                .OrderByDescending(x => x.score)
                .Take(3)
                .Select(x => x.text)
                .ToList();

            blocks.Add("[오늘 대화할 주제]");
            blocks.Add(string.Join("\n- ", picked.Prepend("- ")));
        }

        blocks.Add("[상태]");
        // 테스트 데이터의 CurrentSituation 값을 그대로 사용한다.
        blocks.Add($"상황: {request.CurrentSituation}");
        blocks.Add($"직전 발화(상대): {request.LastUtterance}");
        if (request.IsLastTurn)
            blocks.Add("이번 턴은 이 대화를 마무리하는 마지막 턴이다. 새로운 질문이나 전략 제안을 시작하지 말고, 오늘 모험에서 인상 깊었던 점 한 가지와 상대에게 건네고 싶은 한 마디(감사, 격려, 농담 등)를 짧게 말한 뒤 대화를 끝낸다.");

        blocks.Add("[기억]");
        blocks.Add("[던전 로그 - 화자 시점]");
        blocks.Add($"{speaker.Name}가 직접 겪은 기록이다. 아래 로그에서 {speaker.Name}는 '나'에 해당하며, 다른 이름은 파티원이다.");
        blocks.Add(string.Join(" | ", logLines));
        blocks.Add($"장기 요약: {request.LongTerm.SummarySentence}");

        blocks.Add("[최근 턴]");
        blocks.Add(string.Join("\n", turnLines));

        blocks.Add("[좋은 예시]");
        blocks.Add("※ 아래 예시는 '분위기와 흐름'만 설명한다. 문장을 그대로 복사하지 말고, 각 캐릭터의 성격과 오늘 로그에 맞게 완전히 새로운 표현으로 말해야 한다.");
        blocks.Add("예시-좋은 대화(요약): 전투가 잘 풀렸다는 짧은 정리 + 서로에 대한 한 줄 감사/칭찬 + 내일을 위한 간단한 계획 한 줄로 마무리한다.");
        blocks.Add("예시-나쁜 대화(피해야 함): 서로 같은 질문이나 같은 문장을 그대로 반복하는 대화.");

        blocks.Add("[규칙]");
        blocks.Add("1) 컨텍스트(최근 이벤트, 던전 로그, 현재 상황)에 있는 내용만 사용할 것. 없는 설정은 새로 만들지 않는다.");
        blocks.Add("2) 던전 로그는 화자(지금 말을 하는 캐릭터)의 시점이다. 포션을 쓴 행동이 로그에 있다면, 그 캐릭터는 '내가 포션을 썼다'고 1인칭으로 말한다.");
        blocks.Add("3) 아이템 이름은 던전 로그/인벤토리에 적힌 그대로 사용할 것. '포션'이면 '포션', '회복 포션'이면 그 표현만 쓰고, '보충제' 같은 새로운 이름이나 더 넓은 분류어로 바꾸지 않는다.");
        blocks.Add("4) 데미지·피로의 원인은 로그에 적힌 원인만 사용한다. 예를 들어 '함정으로 카일 데미지 15'라면 함정 때문에 아팠다고만 말하고, '잊혀진 룬 때문에 데미지'처럼 로그에 없는 인과관계를 새로 만들지 않는다.");
        blocks.Add("5) 첫 턴에서는 질문부터 시작하지 말고, 오늘 로그에서 인상 깊었던 사건 하나를 화자의 입장에서 자연스럽게 풀어 말한 뒤, 필요하면 짧은 질문이나 한두 마디 의견으로 마무리한다.");
        blocks.Add("6) 위에 정리된 '오늘 대화할 주제' 목록에서 아직 이야기하지 않은 것 하나를 골라, 이번 차례에는 그 주제에 대해서만 짧게 한 번 왔다 갔다(한 쪽이 사건을 꺼내고, 다른 쪽이 거기에 대해 대답·소감 한 번) 하고 끝낸다.");
        blocks.Add("7) 이미 한 번 왔다 간 주제(예: 같은 함정, 같은 유물, 같은 포션 사용)는 이후 턴에서는 다시 꺼내지 말고, 남아 있는 다른 주제로 자연스럽게 넘긴다.");
        blocks.Add("8) 직전 발화가 질문이라면, 이번 턴은 질문으로만 끝내지 말고 대답 + 짧은 의견 또는 소감까지 포함해서 자연스럽게 마무리한다.");
        blocks.Add("9) 이번 턴이 마지막 턴이라고 표시되어 있으면, 새로운 질문이나 전략 제안을 시작하지 말고 오늘 모험에서 인상 깊었던 점 한 가지와 상대에게 건네고 싶은 한 마디(감사, 격려, 농담 등)를 짧게 말한 뒤 자연스럽게 대화를 끝낸다.");
        blocks.Add("10) 상대 발화를 그대로 반복하지 말 것. 같은 의미라도 표현을 바꾸고, 이미 같은 내용을 두 번 이상 물었으면 다른 화제로 자연스럽게 넘긴다.");
        blocks.Add("11) 포션 사용량·유물 개수 같은 숫자 정보는 한 번만 명확히 확인하고, 이후에는 감정(호불호)이나 평가, 앞으로의 계획/제안 위주로 말한다.");
        blocks.Add("12) 한 문장에는 1~2개의 생각만 담고, 보고서/명령조보다는 동료와 대화하듯 자연스럽게 말한다. 말끝 스타일은 캐릭터 말투 안에서 -해/-해요 계열로 유지한다.");
        blocks.Add("13) 출력은 JSON 한 덩어리로만 응답: {\"tone\":\"\", \"intent\":\"\", \"line\":\"\", \"innerThought\":\"\"}. line에는 실제 대사 한 번만 쓰고, tone/intnet/innerThought는 설명용으로 사용한다.");

        return string.Join("\n\n", blocks);
    }
}
