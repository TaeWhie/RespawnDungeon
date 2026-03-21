using System.Linq;
using System.Text;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>Base·EventType을 행동 로그/Location 필드 해석용 Instruction 블록으로 만듭니다.</summary>
public static class CodeDefinitionInstructions
{
    public static string Build(GameReferenceBundle? refs)
    {
        if (refs == null) return "";

        var sb = new StringBuilder();
        sb.AppendLine("[코드·시설 정의 — 행동 로그·아지트 Location 필드 해석 전용]");
        sb.AppendLine("다음은 **데이터 필드에 등장하는 ID·코드의 의미**입니다. 창작이 아니라 **해석**에만 사용하세요.");
        sb.AppendLine("• CharactersDatabase의 `CurrentLocationId`도 가능하면 아래 BaseId와 맞춥니다. 던전 안 등은 자유 문자열도 가능.");
        sb.AppendLine("• ActionLog.Location에 `main_hall`, `cafeteria` 등이 오면 아래 BaseId·이름과 대응합니다.");
        sb.AppendLine("• EventType 문자열은 로그 분류 코드입니다. 의미를 잘못 추측하지 말고 아래 정의를 따르세요.");
        sb.AppendLine();

        if (refs.Bases.Count > 0)
        {
            sb.AppendLine("■ Base / 아지트 시설(BaseDatabase)");
            foreach (var b in refs.Bases)
                sb.AppendLine(
                    $"  - BaseId=`{b.BaseId}` → 이름: {b.Name} ({b.Type}) | 용도: {b.Description} | 서비스: {b.AvailableServices} | 주의: {b.RiskOrLimit}");
            sb.AppendLine();
        }

        if (refs.Parties.Count > 0)
        {
            sb.AppendLine("■ 파티(PartyDatabase)");
            sb.AppendLine(
                "  - CharactersDatabase.json의 `PartyId`와 아래 `PartyId`가 같으면 동일 소속입니다. ActionLog의 PartyMembers는 보통 **표시용 이름(한글)** 입니다.");
            foreach (var p in refs.Parties)
            {
                var members = p.MemberIds.Count > 0
                    ? string.Join(", ", p.MemberIds)
                    : "(MemberIds 미기입 — Character.PartyId로 확인)";
                sb.AppendLine(
                    $"  - PartyId=`{p.PartyId}` → {p.Name} | 호칭: {p.Callsign ?? "-"} | {p.Description} | 멤버(Id): {members}");
                if (!string.IsNullOrWhiteSpace(p.Notes))
                    sb.AppendLine($"    참고: {p.Notes}");
            }

            sb.AppendLine();
        }

        if (refs.EventTypes != null)
        {
            if (refs.EventTypes.DungeonEventTypes.Count > 0)
            {
                sb.AppendLine("■ 던전 이벤트 코드(EventType — Dungeon)");
                foreach (var ev in refs.EventTypes.DungeonEventTypes)
                    sb.AppendLine($"  - `{ev.Code}` ({ev.LabelKo}): {ev.Meaning}");
                sb.AppendLine();
            }
            if (refs.EventTypes.BaseEventTypes.Count > 0)
            {
                sb.AppendLine("■ 아지트 이벤트 코드(EventType — Base)");
                foreach (var ev in refs.EventTypes.BaseEventTypes)
                    sb.AppendLine($"  - `{ev.Code}` ({ev.LabelKo}): {ev.Meaning}");
            }
        }

        return sb.ToString().TrimEnd();
    }
}
