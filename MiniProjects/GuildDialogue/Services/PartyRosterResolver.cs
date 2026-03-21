using System;
using System.Collections.Generic;
using System.Linq;
using GuildDialogue.Data;

namespace GuildDialogue.Services;

/// <summary>
/// Character.PartyId 기반으로 프롬프트에 넣을 "동료 한 줄" 목록을 고릅니다.
/// </summary>
public static class PartyRosterResolver
{
    /// <summary>
    /// 길드장(master)이 말할 때: master 제외 전원.
    /// 화자에게 PartyId가 있으면 같은 파티만; 없으면 master 제외 전원(하위 호환).
    /// 같은 PartyId인 동료가 0명이면 master 제외 전원으로 폴백.
    /// </summary>
    public static List<Character> ResolveForPrompt(IReadOnlyList<Character> all, Character speaker)
    {
        if (all == null || all.Count == 0) return new List<Character>();

        var noMaster = all
            .Where(c => !c.Id.Equals("master", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (speaker.Id.Equals("master", StringComparison.OrdinalIgnoreCase))
            return noMaster;

        if (string.IsNullOrWhiteSpace(speaker.PartyId))
            return noMaster;

        var sameParty = noMaster
            .Where(c =>
                !string.IsNullOrWhiteSpace(c.PartyId) &&
                c.PartyId.Equals(speaker.PartyId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return sameParty.Count > 0 ? sameParty : noMaster;
    }

    /// <summary>
    /// 길드장 집무실 1:1: 프롬프트 [파티(등장인물 요약)]에 화자만 넣어
    /// 리나·브람 등이 같은 방에 있는 것처럼 말하는 오인을 줄입니다.
    /// </summary>
    public static List<Character> ResolveForGuildMasterOneOnOne(Character buddy) =>
        buddy == null ? new List<Character>() : new List<Character> { buddy };
}
