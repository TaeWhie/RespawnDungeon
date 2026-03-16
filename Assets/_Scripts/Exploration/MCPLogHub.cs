using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

/// <summary>
/// MCP/탐험 디버그 로그 감시를 한 곳에서 관리하는 허브.
/// 필요할 때 이 클래스에 감시 로직을 추가하고, 각 시스템은 호출만 하도록 유지합니다.
/// </summary>
public static class MCPLogHub
{
    private sealed class TraceChannelConfig
    {
        public bool Enabled;
        public float Interval;
    }

    private static readonly HashSet<string> EnabledIssueLogs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, TraceChannelConfig> TraceChannels = new Dictionary<string, TraceChannelConfig>(StringComparer.OrdinalIgnoreCase);

    public static void SetIssueLogEnabled(string issueId, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(issueId))
            return;

        if (enabled)
            EnabledIssueLogs.Add(issueId);
        else
            EnabledIssueLogs.Remove(issueId);
    }

    public static bool IsIssueLogEnabled(string issueId)
    {
        return !string.IsNullOrWhiteSpace(issueId) && EnabledIssueLogs.Contains(issueId);
    }

    public static void LogIssueStepIfEnabled(string issueId, string step, string details = null)
    {
        if (!IsIssueLogEnabled(issueId))
            return;

        LogIssueStep(issueId, step, details);
    }

    public static void ConfigureTraceChannel(string channel, bool enabled, float intervalSeconds = 0.2f)
    {
        var config = GetOrCreateTraceChannel(channel);
        config.Enabled = enabled;
        config.Interval = Mathf.Max(0.05f, intervalSeconds);
    }

    public static void SetTraceChannelEnabled(string channel, bool enabled)
    {
        var config = GetOrCreateTraceChannel(channel);
        config.Enabled = enabled;
    }

    public static bool IsTraceChannelEnabled(string channel)
    {
        return TryGetTraceChannel(channel, out var config) && config.Enabled;
    }

    public static void SetTraceChannelInterval(string channel, float intervalSeconds)
    {
        var config = GetOrCreateTraceChannel(channel);
        config.Interval = Mathf.Max(0.05f, intervalSeconds);
    }

    public static float GetTraceChannelInterval(string channel, float fallback = 0.2f)
    {
        return TryGetTraceChannel(channel, out var config) ? config.Interval : Mathf.Max(0.05f, fallback);
    }

    public static void LogTraceIfChannelEnabled(
        string channel,
        ref float traceTimer,
        float traceDeltaTime,
        string label,
        Vector2 position,
        Vector2Int cell,
        int pathIndex,
        int lastWaypointIndex,
        Vector2Int waypoint,
        float distanceToWaypoint,
        bool isLastWaypoint,
        Vector2 velocity,
        Vector2Int? targetCell = null)
    {
        if (!IsTraceChannelEnabled(channel))
            return;

        float interval = GetTraceChannelInterval(channel);
        LogTrace(
            true,
            ref traceTimer,
            traceDeltaTime,
            interval,
            label,
            position,
            cell,
            pathIndex,
            lastWaypointIndex,
            waypoint,
            distanceToWaypoint,
            isLastWaypoint,
            velocity,
            targetCell);
    }

    public static void LogIssueStart(string issueId, string summary)
    {
        Debug.Log($"[MCP][{issueId}] START {summary}");
    }

    public static void LogIssueStep(string issueId, string step, string details = null)
    {
        if (string.IsNullOrEmpty(details))
        {
            Debug.Log($"[MCP][{issueId}] STEP {step}");
            return;
        }

        Debug.Log($"[MCP][{issueId}] STEP {step} | {details}");
    }

    public static void LogIssueWarning(string issueId, string warning)
    {
        Debug.LogWarning($"[MCP][{issueId}] WARN {warning}");
    }

    public static void LogIssueError(string issueId, string error)
    {
        Debug.LogError($"[MCP][{issueId}] ERROR {error}");
    }

    public static void LogIssueResolved(string issueId, string resolution)
    {
        Debug.Log($"[MCP][{issueId}] RESOLVED {resolution}");
    }

    private static TraceChannelConfig GetOrCreateTraceChannel(string channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
            channel = "DEFAULT";

        if (TraceChannels.TryGetValue(channel, out var config))
            return config;

        config = new TraceChannelConfig
        {
            Enabled = false,
            Interval = 0.2f
        };
        TraceChannels[channel] = config;
        return config;
    }

    private static bool TryGetTraceChannel(string channel, out TraceChannelConfig config)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            config = null;
            return false;
        }

        return TraceChannels.TryGetValue(channel, out config);
    }

    public static IDisposable BindPeriodicMonitor(
        float intervalSeconds,
        Action onTick,
        CompositeDisposable disposables = null)
    {
        if (onTick == null)
            return Disposable.Empty;

        IDisposable subscription;
        if (intervalSeconds <= 0f)
        {
            subscription = Observable.EveryUpdate()
                .Subscribe(_ => onTick());
        }
        else
        {
            subscription = Observable.Interval(TimeSpan.FromSeconds(intervalSeconds))
                .StartWith(0L)
                .Subscribe(_ => onTick());
        }

        if (disposables != null)
            subscription.AddTo(disposables);

        return subscription;
    }

    public static void ResetStuckWatch(
        ref int watchWaypointIndex,
        ref float watchElapsed,
        ref float watchStartDistance,
        ref bool warnedForCurrentWaypoint)
    {
        watchWaypointIndex = -1;
        watchElapsed = 0f;
        watchStartDistance = 0f;
        warnedForCurrentWaypoint = false;
    }

    public static void UpdateStuckWatch(
        ref int watchWaypointIndex,
        ref float watchElapsed,
        ref float watchStartDistance,
        ref bool warnedForCurrentWaypoint,
        bool active,
        int currentWaypointIndex,
        int lastWaypointIndex,
        float currentDistance,
        float reachRadius,
        bool isLastWaypoint,
        float deltaTime,
        Vector2 velocity,
        Vector2Int cell,
        string label)
    {
        if (!active)
        {
            ResetStuckWatch(ref watchWaypointIndex, ref watchElapsed, ref watchStartDistance, ref warnedForCurrentWaypoint);
            return;
        }

        if (currentWaypointIndex != watchWaypointIndex)
        {
            watchWaypointIndex = currentWaypointIndex;
            watchElapsed = 0f;
            watchStartDistance = currentDistance;
            warnedForCurrentWaypoint = false;
            return;
        }

        watchElapsed += deltaTime;
        if (warnedForCurrentWaypoint || isLastWaypoint || watchElapsed < 2.5f)
            return;

        float progressed = watchStartDistance - currentDistance;
        bool stillOutside = currentDistance > Mathf.Max(reachRadius * 2f, 0.2f);
        bool lowProgress = progressed < 0.15f;
        bool tryingToMove = velocity.sqrMagnitude > 0.2f * 0.2f;

        if (stillOutside && lowProgress && tryingToMove)
        {
            warnedForCurrentWaypoint = true;
            Debug.LogWarning(
                $"[{label} STUCK] wpIdx={currentWaypointIndex}/{lastWaypointIndex} cell={cell} " +
                $"toWp={currentDistance:F3} progressed={progressed:F3} elapsed={watchElapsed:F2} vel={velocity.ToString("F3")}");
        }
    }

    public static void LogTrace(
        bool enabled,
        ref float traceTimer,
        float traceDeltaTime,
        float traceInterval,
        string label,
        Vector2 position,
        Vector2Int cell,
        int pathIndex,
        int lastWaypointIndex,
        Vector2Int waypoint,
        float distanceToWaypoint,
        bool isLastWaypoint,
        Vector2 velocity,
        Vector2Int? targetCell = null)
    {
        if (!enabled)
            return;

        traceTimer -= traceDeltaTime;
        if (traceTimer > 0f)
            return;
        traceTimer = Mathf.Max(0.05f, traceInterval);

        string targetPart = targetCell.HasValue ? $" target={targetCell.Value}" : string.Empty;
        Debug.Log(
            $"[{label}] pos={position.ToString("F3")} cell={cell} " +
            $"wpIdx={pathIndex}/{lastWaypointIndex} wp={waypoint} toWp={distanceToWaypoint:F3} " +
            $"last={isLastWaypoint} vel={velocity.ToString("F3")}{targetPart}");
    }
}
