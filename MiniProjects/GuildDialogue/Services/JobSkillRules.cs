using System;
using System.Collections.Generic;
using System.Linq;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// <see cref="JobRoleData"/>에 따라 캐릭터 <see cref="Character.Skills"/>에서 직업 밖 스킬을 제거합니다.
/// </summary>
public static class JobSkillRules
{
    /// <summary>로스터 전원에 적용. 직업 정의가 없으면 해당 인물은 건드리지 않습니다.</summary>
    public static void EnforceOnRoster(IList<Character> roster, IReadOnlyList<JobRoleData> jobs)
    {
        if (roster == null || roster.Count == 0 || jobs == null || jobs.Count == 0)
            return;

        foreach (var c in roster)
            EnforceForCharacter(c, jobs);
    }

    public static void EnforceForCharacter(Character c, IReadOnlyList<JobRoleData> jobs)
    {
        if (c.Skills == null)
            c.Skills = new List<string>();
        if (c.Skills.Count == 0)
            return;

        var job = jobs.FirstOrDefault(j =>
            !string.IsNullOrWhiteSpace(j.RoleId) &&
            j.RoleId.Equals(c.Role, StringComparison.OrdinalIgnoreCase));

        if (job == null)
        {
            Console.WriteLine($"[직업 DB] RoleId='{c.Role}' 정의 없음 — {c.Name} 스킬 필터 생략.");
            return;
        }

        if (job.AllowedSkillNames == null || job.AllowedSkillNames.Count == 0)
        {
            c.Skills.Clear();
            Console.WriteLine($"[직업] {c.Name}: 직업에 허용 스킬 목록이 비어 있어 보유 스킬을 비웁니다.");
            return;
        }

        var allowed = new HashSet<string>(job.AllowedSkillNames, StringComparer.Ordinal);
        var removed = c.Skills.Where(s => !allowed.Contains(s)).ToList();
        c.Skills = c.Skills.Where(s => allowed.Contains(s)).ToList();
        if (removed.Count > 0)
            Console.WriteLine($"[직업] {c.Name}: 직업 밖 스킬 제거 → {string.Join(", ", removed)}");
    }
}
