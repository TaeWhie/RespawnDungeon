using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 파티 관련 런타임 검색 결과를 공유하는 캐시.
/// 여러 시스템이 같은 프레임에 Find*를 중복 호출하지 않도록 통합합니다.
/// </summary>
public static class ExplorationPartyCache
{
    private static readonly List<Transform> AllyTransformsBuffer = new List<Transform>();
    private static readonly List<PartyFollower> FollowersBuffer = new List<PartyFollower>();
    private static Transform _leader;
    private static float _lastScanUnscaledTime = -999f;

    public static Transform Leader => _leader;
    public static IReadOnlyList<Transform> AllyTransforms => AllyTransformsBuffer;
    public static IReadOnlyList<PartyFollower> Followers => FollowersBuffer;

    public static void RefreshIfStale(float intervalSeconds)
    {
        float minInterval = Mathf.Max(0f, intervalSeconds);
        float now = Time.unscaledTime;
        if (minInterval > 0f && now - _lastScanUnscaledTime < minInterval)
            return;

        Rebuild();
    }

    public static void Invalidate()
    {
        _lastScanUnscaledTime = -999f;
    }

    private static void Rebuild()
    {
        _lastScanUnscaledTime = Time.unscaledTime;

        AllyTransformsBuffer.Clear();
        FollowersBuffer.Clear();

        var player = GameObject.FindWithTag("Player");
        _leader = player != null ? player.transform : null;

        var allies = GameObject.FindGameObjectsWithTag("Ally");
        for (int i = 0; i < allies.Length; i++)
        {
            var go = allies[i];
            if (go == null) continue;

            AllyTransformsBuffer.Add(go.transform);

            var follower = go.GetComponent<PartyFollower>();
            if (follower != null)
                FollowersBuffer.Add(follower);
        }
    }
}
