using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 격자 기반 맵에 최적화된 경량 A* 알고리즘.
/// visited 상태와 무관하게 오직 '벽(장애물)'의 유무만으로 경로를 계산합니다.
/// </summary>
public class Pathfinder : MonoBehaviour
{
    /// <summary>
    /// MapManager 기준으로 from → to 최단 경로를 반환합니다. 경로가 없으면 null.
    /// 장애물(비이동가능)만 피하고, visited 여부는 사용하지 않습니다.
    /// </summary>
    public List<Vector2Int> GetPath(MapManager mapManager, Vector2Int from, Vector2Int to)
    {
        if (mapManager == null || !mapManager.IsInitialized) return null;
        if (!mapManager.IsWalkable(from) || !mapManager.IsWalkable(to)) return null;
        if (from == to) return new List<Vector2Int> { from };

        var open = new List<(Vector2Int pos, float f, float g)>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float> { [from] = 0f };

        float Heuristic(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
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

            foreach (var dir in Direction2D.cardinalDirectionsList)
            {
                Vector2Int next = cur + dir;
                if (!mapManager.IsWalkable(next)) continue;

                float tentativeG = (gScore.TryGetValue(cur, out float g) ? g : float.MaxValue) + 1f;
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
