using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 격자 기반 맵에 최적화된 경로 탐색.
/// 8방향(대각선 포함) 이동. 대각선은 두 인접 cardinal이 통과 가능할 때만 허용(벽/장애물 코너 관통 방지).
/// </summary>
public class Pathfinder : MonoBehaviour
{
    private static readonly float CostCardinal = 1f;
    private static readonly float CostDiagonal = 1.41421356f; // sqrt(2)

    /// <summary>
    /// MapManager 기준으로 from → to 최단 경로를 반환합니다. 경로가 없으면 null.
    /// allowObstacles: true면 장애물 셀을 경로에 포함(리더가 부수며 진행), false면 장애물을 피해 경로 계산(동료용).
    /// </summary>
    public List<Vector2Int> GetPath(MapManager mapManager, Vector2Int from, Vector2Int to, bool allowObstacles = true)
    {
        if (mapManager == null || !mapManager.IsInitialized) return null;
        bool fromOk = allowObstacles ? mapManager.IsPassableForPathfinding(from) : mapManager.IsWalkable(from);
        bool toOk = allowObstacles ? mapManager.IsPassableForPathfinding(to) : mapManager.IsWalkable(to);
        if (!fromOk || !toOk) return null;
        if (from == to) return new List<Vector2Int> { from };

        var open = new List<(Vector2Int pos, float f, float g)>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float> { [from] = 0f };

        float Heuristic(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(b.x - a.x);
            int dy = Mathf.Abs(b.y - a.y);
            return Mathf.Sqrt((float)(dx * dx + dy * dy));
        }

        bool IsPassable(Vector2Int cell) => allowObstacles
            ? mapManager.IsPassableForPathfinding(cell)
            : mapManager.IsWalkable(cell);

        /// <summary>대각선 이동 시 끼인 두 cardinal 셀이 바닥(walkable)인지. 코너에 장애물이 있으면 대각선 불가 → 장애물은 한 칸씩 밟고 부수도록.</summary>
        bool CanMoveDiagonal(Vector2Int cur, Vector2Int diagDir)
        {
            if (diagDir.x == 0 || diagDir.y == 0) return true;
            var c1 = cur + new Vector2Int(diagDir.x, 0);
            var c2 = cur + new Vector2Int(0, diagDir.y);
            return mapManager.IsWalkable(c1) && mapManager.IsWalkable(c2);
        }

        open.Add((from, Heuristic(from, to), 0f));

        while (open.Count > 0)
        {
            open.Sort((a, b) => a.f.CompareTo(b.f));
            var current = open[0];
            open.RemoveAt(0);
            Vector2Int cur = current.pos;

            if (current.g > (gScore.TryGetValue(cur, out float cg) ? cg : float.MaxValue))
                continue;

            if (cur == to)
            {
                var path = new List<Vector2Int>();
                var p = to;
                while (cameFrom.TryGetValue(p, out var prev))
                {
                    path.Add(p);
                    p = prev;
                }
                path.Add(from);
                path.Reverse();
                return path;
            }

            foreach (var dir in Direction2D.eightDirectionsList)
            {
                Vector2Int next = cur + dir;
                if (!IsPassable(next)) continue;
                if (!CanMoveDiagonal(cur, dir)) continue;

                bool isDiagonal = dir.x != 0 && dir.y != 0;
                float stepCost = isDiagonal ? CostDiagonal : CostCardinal;
                float tentativeG = (gScore.TryGetValue(cur, out float g) ? g : float.MaxValue) + stepCost;
                if (tentativeG >= (gScore.TryGetValue(next, out float ng) ? ng : float.MaxValue))
                    continue;

                cameFrom[next] = cur;
                gScore[next] = tentativeG;
                open.Add((next, tentativeG + Heuristic(next, to), tentativeG));
            }
        }

        return null;
    }

    /// <summary>from에서 to까지 도달 가능한지</summary>
    public bool IsReachable(MapManager mapManager, Vector2Int from, Vector2Int to)
    {
        var path = GetPath(mapManager, from, to);
        return path != null && path.Count > 0;
    }
}
