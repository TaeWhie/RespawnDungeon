using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using TriInspector;

/// <summary>
/// 격자 기반 맵의 탐험 상태(visited)와 도달 불가 타일(unreachable)을 관리합니다.
/// 타일맵 할당 시 맵 크기를 자동 계산하며, Gizmos로 탐험 과정을 시각화합니다.
/// </summary>
public class MapManager : MonoBehaviour
{
    [Title("Tilemap 연동")]
    [Tooltip("바닥 타일맵. 비워두면 TilemapVisualizer에서 자동 검색합니다. 맵 크기 계산에 사용됩니다.")]
    [SerializeField] private Tilemap _floorTilemap;

    [Title("시각화 (Gizmos)")]
    [Tooltip("체크 해제하면 Gizmo 그리기를 끄고 프레임이 안정됩니다. 디버깅할 때만 켜세요.")]
    [SerializeField] private bool _drawGizmos = true;
    [SerializeField] private Color _visitedColor = new Color(0.2f, 0.4f, 0.9f, 0.35f);
    [SerializeField] private Color _unreachableColor = Color.red;
    [SerializeField] private Color _globalTargetColor = Color.yellow;
    [Slider(0.3f, 1.5f)]
    [SerializeField] private float _gizmoCellSize = 0.9f;

    [Title("Debug")]
    [Tooltip("개발용: 시야/안개 무시하고 맵 전체를 항상 보이게 할지 여부")]
    [SerializeField] private bool _debugRevealAll = false;

    // 맵 그리드: 셀 좌표 (minX + i, minY + j) -> 배열 인덱스 [i, j]
    private int _minX, _minY, _width, _height;
    private bool[,] _walkable;
    private bool[,] _visited;
    private bool[,] _unreachable;

    /// <summary>현재 프레임에서 "1단계(전부 보임)" 시야 안에 있는 셀. 매 프레임 갱신.</summary>
    private HashSet<Vector2Int> _currentFullView = new HashSet<Vector2Int>();
    /// <summary>한 번이라도 1단계로 밝혀진 셀. 맵 재생성 시 초기화.</summary>
    private HashSet<Vector2Int> _everFullView = new HashSet<Vector2Int>();
    private int _lastViewUpdateFrame = -1;

    /// <summary>현재 글로벌 목표 셀 (Navigating 시 시각화용)</summary>
    private Vector2Int? _globalTargetCell;

    /// <summary>보물/황금상자 셀 (던전 생성 시 등록). 비워커블이므로 이동 불가.</summary>
    private HashSet<Vector2Int> _chestCells = new HashSet<Vector2Int>();
    /// <summary>이미 픽한 상자 셀</summary>
    private HashSet<Vector2Int> _pickedChests = new HashSet<Vector2Int>();
    /// <summary>셀 → ChestOpenable (Open 애니 후 제거용)</summary>
    private Dictionary<Vector2Int, ChestOpenable> _chestViews = new Dictionary<Vector2Int, ChestOpenable>();

    /// <summary>맵 데이터가 준비되었는지</summary>
    public bool IsInitialized => _walkable != null && _width > 0 && _height > 0;

    public int Width => _width;
    public int Height => _height;
    public int MinX => _minX;
    public int MinY => _minY;
    public int VisitedCount { get; private set; }
    public int WalkableCount { get; private set; }

    /// <summary>개발용: 시야/안개 무시하고 맵 전체를 항상 보이게 할지 여부.</summary>
    public bool DebugRevealAll
    {
        get => _debugRevealAll;
        set => _debugRevealAll = value;
    }

    /// <summary>개발용: Gizmo 그리기 on/off.</summary>
    public bool DrawGizmos
    {
        get => _drawGizmos;
        set => _drawGizmos = value;
    }

    private void Start()
    {
        EnsureFloorTilemap();
    }

    private void EnsureFloorTilemap()
    {
        if (_floorTilemap != null) return;
        var vis = FindFirstObjectByType<TilemapVisualizer>();
        if (vis != null) _floorTilemap = vis.FloorTilemap;
    }

    /// <summary>
    /// 던전 생성기가 호출합니다. 이동 가능(바닥) 타일 집합으로 맵을 초기화하고,
    /// visited / unreachable 배열을 맵 크기에 맞게 생성합니다.
    /// </summary>
    public void SetWalkableTiles(HashSet<Vector2Int> walkableSet)
    {
        EnsureFloorTilemap();
        if (walkableSet == null || walkableSet.Count == 0)
        {
            _walkable = null;
            _visited = null;
            _unreachable = null;
            _width = _height = 0;
            _currentFullView?.Clear();
            _everFullView?.Clear();
            _chestCells?.Clear();
            _pickedChests?.Clear();
            _chestViews?.Clear();
            return;
        }

        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var c in walkableSet)
        {
            if (c.x < minX) minX = c.x;
            if (c.y < minY) minY = c.y;
            if (c.x > maxX) maxX = c.x;
            if (c.y > maxY) maxY = c.y;
        }

        _minX = minX;
        _minY = minY;
        _width = maxX - minX + 1;
        _height = maxY - minY + 1;

        _walkable = new bool[_width, _height];
        _visited = new bool[_width, _height];
        _unreachable = new bool[_width, _height];
        _everFullView?.Clear();

        WalkableCount = 0;
        VisitedCount = 0;
        foreach (var c in walkableSet)
        {
            if (CellToIndex(c, out int i, out int j))
            {
                _walkable[i, j] = true;
                WalkableCount++;
            }
        }
        OnMapInitialized?.Invoke();
    }

    /// <summary>셀 좌표를 배열 인덱스로 변환. 범위 밖이면 false.</summary>
    public bool CellToIndex(Vector2Int cell, out int i, out int j)
    {
        i = cell.x - _minX;
        j = cell.y - _minY;
        return i >= 0 && i < _width && j >= 0 && j < _height;
    }

    /// <summary>배열 인덱스를 셀 좌표로 변환</summary>
    public Vector2Int IndexToCell(int i, int j)
    {
        return new Vector2Int(_minX + i, _minY + j);
    }

    public bool IsWalkable(Vector2Int cell)
    {
        if (!CellToIndex(cell, out int i, out int j)) return false;
        return _walkable[i, j];
    }

    /// <summary>특정 셀을 이동 가능/불가로 설정. 상자 제거 시 해당 셀을 다시 floor로 쓸 때 사용.</summary>
    public void SetCellWalkable(Vector2Int cell, bool walkable)
    {
        if (!CellToIndex(cell, out int i, out int j)) return;
        bool wasWalkable = _walkable[i, j];
        _walkable[i, j] = walkable;
        if (walkable && !wasWalkable)
            WalkableCount++;
        else if (!walkable && wasWalkable)
            WalkableCount--;
    }

    public bool IsVisited(Vector2Int cell)
    {
        if (!CellToIndex(cell, out int i, out int j)) return false;
        return _visited[i, j];
    }

    public bool IsUnreachable(Vector2Int cell)
    {
        if (!CellToIndex(cell, out int i, out int j)) return false;
        return _unreachable[i, j];
    }

    /// <summary>시야 반경 내 타일을 모두 방문 처리합니다. 벽 너머는 보이지 않습니다 (라인 오브 사이트).
    /// 장애물(비워커블) 셀도 시야에 들어오면 방문 처리해 안개가 벗겨지도록 합니다.</summary>
    /// <param name="center">시야 중심 셀</param>
    /// <param name="viewRadius">방문 처리할 반경(타일 수). 이 반경까지 안개 제거(2단계: 구조만 보임).</param>
    public void MarkVisitedInRadius(Vector2Int center, int viewRadius)
    {
        MarkVisitedInRadius(center, viewRadius, viewRadius);
    }

    /// <summary>2단계 시야(넓은 반경)와 1단계 시야(좁은 반경)를 구분해 갱신합니다.</summary>
    /// <param name="center">시야 중심 셀</param>
    /// <param name="fullViewRadius">1단계 반경: 이 안이면 장애물 등 전부 보임</param>
    /// <param name="structureViewRadius">2단계 반경: 이 안이면 벽/바닥만 보임(안개 제거). fullViewRadius 이상이어야 함.</param>
    public void MarkVisitedInRadius(Vector2Int center, int fullViewRadius, int structureViewRadius)
    {
        if (!IsInitialized) return;

        int frame = Time.frameCount;
        if (frame != _lastViewUpdateFrame)
        {
            _lastViewUpdateFrame = frame;
            _currentFullView.Clear();
        }

        int outer = Mathf.Max(structureViewRadius, fullViewRadius);

        for (int dx = -outer; dx <= outer; dx++)
        for (int dy = -outer; dy <= outer; dy++)
        {
            var cell = new Vector2Int(center.x + dx, center.y + dy);
            if (!CellToIndex(cell, out _, out _)) continue;

            int dist = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
            bool inStructure = dist <= outer;
            bool inFull = dist <= fullViewRadius;

            if (inStructure)
            {
                if (IsWalkable(cell) && !IsVisited(cell) && HasLineOfSight(center, cell))
                    MarkVisited(cell);
                else if (!IsWalkable(cell) && !IsVisited(cell) && HasLineOfSightToCell(center, cell))
                    MarkVisited(cell);
            }

            if (inFull)
            {
                if (IsWalkable(cell) && HasLineOfSight(center, cell))
                {
                    _currentFullView.Add(cell);
                    _everFullView.Add(cell);
                }
                else if (!IsWalkable(cell) && HasLineOfSightToCell(center, cell))
                {
                    _currentFullView.Add(cell);
                    _everFullView.Add(cell);
                }
            }
        }
    }

    /// <summary>해당 셀이 현재 1단계 시야(전부 보임) 안에 있거나, 한 번이라도 1단계로 밝혀진 적 있으면 true. (안개·바닥 복원용)</summary>
    public bool IsInFullView(Vector2Int cell)
    {
        if (_currentFullView != null && _currentFullView.Contains(cell)) return true;
        return _everFullView != null && _everFullView.Contains(cell);
    }

    /// <summary>해당 셀이 지금 이 프레임 1단계 시야 안에 있을 때만 true. 오브젝트는 이걸로만 표시(2단계에서 숨김).</summary>
    public bool IsInCurrentFullView(Vector2Int cell)
    {
        return _currentFullView != null && _currentFullView.Contains(cell);
    }

    /// <summary>
    /// 안개 단계(1/2/3)를 반환합니다.
    /// - 3: 아직 한 번도 방문하지 않은 셀 (미탐험)
    /// - 2: 방문했지만 full view는 아닌 셀 (구조만 보이는 링)
    /// - 1: 현재/과거에 full view 안에 들어온 적 있는 셀 (전부 보임)
    /// </summary>
    public int GetFogStage(Vector2Int cell)
    {
        if (!IsVisited(cell)) return 3;
        if (IsInFullView(cell)) return 1;
        return 2;
    }

    /// <summary>
    /// 주어진 중심 셀 기준 반경 내에서, 안개 2단계 셀(visited 이지만 full view가 아닌 셀) 개수를 셉니다.
    /// 리더/동료가 이동했을 때 구조만 보이는 링(2단계)을 얼마나 많이 1단계로 바꿀 수 있을지 평가할 때 사용합니다.
    /// </summary>
    public int GetStage2CountInRadius(Vector2Int center, int viewRadius)
    {
        if (!IsInitialized) return 0;
        int count = 0;
        for (int dx = -viewRadius; dx <= viewRadius; dx++)
        for (int dy = -viewRadius; dy <= viewRadius; dy++)
        {
            var cell = new Vector2Int(center.x + dx, center.y + dy);
            if (!CellToIndex(cell, out _, out _)) continue;
            // 안개 2단계 셀만 대상으로, 라인 오브 사이트가 보장되는 경우만 센다.
            if (GetFogStage(cell) == 2 && HasLineOfSight(center, cell))
                count++;
        }
        return count;
    }

    /// <summary>목표 셀까지 라인 오브 사이트. 목표 셀 자체는 비워커블(장애물)이어도 true 가능.</summary>
    private bool HasLineOfSightToCell(Vector2Int from, Vector2Int to)
    {
        int x0 = from.x, y0 = from.y;
        int x1 = to.x, y1 = to.y;
        int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
        if (steps == 0)
            return true;

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            int cx = Mathf.RoundToInt(x0 + t * (x1 - x0));
            int cy = Mathf.RoundToInt(y0 + t * (y1 - y0));
            var cur = new Vector2Int(cx, cy);
            if (cur == to) continue;
            if (!IsWalkable(cur))
                return false;
        }
        return true;
    }

    /// <summary>두 셀 사이에 벽이 없으면 true. 그리드 직선 경로상 비-워커블(벽) 셀이 있으면 false.</summary>
    public bool HasLineOfSight(Vector2Int from, Vector2Int to)
    {
        int x0 = from.x, y0 = from.y;
        int x1 = to.x, y1 = to.y;
        int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
        if (steps == 0)
            return IsWalkable(from);

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            int cx = Mathf.RoundToInt(x0 + t * (x1 - x0));
            int cy = Mathf.RoundToInt(y0 + t * (y1 - y0));
            if (!IsWalkable(new Vector2Int(cx, cy)))
                return false;
        }
        return true;
    }

    public void MarkVisited(Vector2Int cell)
    {
        if (!CellToIndex(cell, out int i, out int j)) return;
        if (_visited[i, j]) return;
        _visited[i, j] = true;
        VisitedCount++;
        OnCellVisited?.Invoke(cell);
    }

    public void MarkUnreachable(Vector2Int cell)
    {
        if (!CellToIndex(cell, out int i, out int j)) return;
        _unreachable[i, j] = true;
    }

    /// <summary>글로벌 목표 셀 설정 (Navigating 시 Gizmos 표시용)</summary>
    public void SetGlobalTarget(Vector2Int? cell)
    {
        _globalTargetCell = cell;
    }

    /// <summary>던전 생성 후 보물/황금상자 셀·뷰 등록. SetWalkableTiles 이후에 호출.</summary>
    public void RegisterChests(IEnumerable<(Vector2Int cell, ChestOpenable openable)> chests)
    {
        _chestCells?.Clear();
        _pickedChests?.Clear();
        _chestViews?.Clear();
        if (chests == null) return;
        _chestCells = new HashSet<Vector2Int>();
        _chestViews = new Dictionary<Vector2Int, ChestOpenable>();
        foreach (var t in chests)
        {
            _chestCells.Add(t.cell);
            if (t.openable != null)
                _chestViews[t.cell] = t.openable;
        }
    }

    /// <summary>던전 생성 후 보물/황금상자 셀만 등록 (뷰 없이). RegisterChests 사용 권장.</summary>
    public void RegisterChestCells(IEnumerable<Vector2Int> cells)
    {
        _chestCells?.Clear();
        _pickedChests?.Clear();
        _chestViews?.Clear();
        if (cells == null) return;
        _chestCells = new HashSet<Vector2Int>(cells);
    }

    /// <summary>해당 셀이 상자 셀인지</summary>
    public bool IsChestCell(Vector2Int cell) => _chestCells != null && _chestCells.Contains(cell);

    /// <summary>해당 상자를 픽 완료 처리. ChestOpenable이 있으면 Open 애니 재생 후 제거.</summary>
    public void MarkChestPicked(Vector2Int cell)
    {
        if (_pickedChests != null) _pickedChests.Add(cell);
        if (_chestViews != null && _chestViews.TryGetValue(cell, out var view))
        {
            _chestViews.Remove(cell);
            view?.Open();
        }
    }

    /// <summary>이미 픽한 상자인지</summary>
    public bool IsChestPicked(Vector2Int cell) => _pickedChests != null && _pickedChests.Contains(cell);

    /// <summary>현재 1단계 시야 안에 있고 아직 픽하지 않은 상자 셀 목록</summary>
    public List<Vector2Int> GetUnpickedChestCellsInFullView()
    {
        var list = new List<Vector2Int>();
        if (_chestCells == null || _pickedChests == null || _currentFullView == null) return list;
        foreach (var cell in _chestCells)
        {
            if (!_pickedChests.Contains(cell) && _currentFullView.Contains(cell))
                list.Add(cell);
        }
        return list;
    }

    /// <summary>상자 셀에 인접한 이동 가능(서 있을 수 있는) 셀 목록. 상하좌우만.</summary>
    public List<Vector2Int> GetStandCellsNextToChest(Vector2Int chestCell)
    {
        var list = new List<Vector2Int>(4);
        foreach (var dir in Direction2D.cardinalDirectionsList)
        {
            var adj = chestCell + dir;
            if (IsWalkable(adj)) list.Add(adj);
        }
        return list;
    }

    /// <summary>맵 초기화 직후 한 번 호출됩니다. (폭/블러 뷰에서 미탐색 타일을 그릴 때 사용)</summary>
    public event System.Action OnMapInitialized;
    /// <summary>해당 셀이 방문 처리될 때마다 호출됩니다. (폭 타일 제거 등)</summary>
    public event System.Action<Vector2Int> OnCellVisited;

    /// <summary>인접 4방향 중 방문하지 않은 이동 가능 타일 목록</summary>
    public List<Vector2Int> GetUnvisitedNeighbors(Vector2Int cell)
    {
        var list = new List<Vector2Int>(4);
        foreach (var dir in Direction2D.cardinalDirectionsList)
        {
            var next = cell + dir;
            if (IsWalkable(next) && !IsVisited(next) && !IsUnreachable(next))
                list.Add(next);
        }
        return list;
    }

    /// <summary>해당 셀의 인접 4방향 중 방문된(2단계 이상) 셀 개수. 2단계 기준 이동 시 "가장자리" 선호용.</summary>
    public int GetVisitedNeighborCount(Vector2Int cell)
    {
        int count = 0;
        foreach (var dir in Direction2D.cardinalDirectionsList)
        {
            var next = cell + dir;
            if (IsWalkable(next) && IsVisited(next))
                count++;
        }
        return count;
    }

    /// <summary>해당 셀을 중심으로 시야 반경 내 미방문·이동가능 타일 개수 (방문 처리 없음). 벽 너머는 제외.</summary>
    public int GetUnvisitedCountInRadius(Vector2Int center, int viewRadius)
    {
        if (!IsInitialized) return 0;
        int count = 0;
        for (int dx = -viewRadius; dx <= viewRadius; dx++)
        for (int dy = -viewRadius; dy <= viewRadius; dy++)
        {
            var cell = new Vector2Int(center.x + dx, center.y + dy);
            if (IsWalkable(cell) && !IsVisited(cell) && !IsUnreachable(cell) && HasLineOfSight(center, cell))
                count++;
        }
        return count;
    }

    /// <summary>접근 가능한 모든 타일을 방문했는지</summary>
    public bool IsExplorationComplete()
    {
        if (!IsInitialized) return false;
        return VisitedCount >= WalkableCount;
    }

    /// <summary>방문 타일과 미방문 타일의 경계선인 프론티어 셀 목록을 반환합니다.</summary>
    public List<Vector2Int> GetFrontierCells()
    {
        var frontier = new HashSet<Vector2Int>();
        if (!IsInitialized) return new List<Vector2Int>(frontier);

        for (int i = 0; i < _width; i++)
        for (int j = 0; j < _height; j++)
        {
            if (!_walkable[i, j] || _visited[i, j] || _unreachable[i, j]) continue;
            var cell = IndexToCell(i, j);
            foreach (var dir in Direction2D.cardinalDirectionsList)
            {
                var neighbor = cell + dir;
                if (IsWalkable(neighbor) && IsVisited(neighbor))
                {
                    frontier.Add(cell);
                    break;
                }
            }
        }
        return new List<Vector2Int>(frontier);
    }

    /// <summary>월드 좌표 → 그리드 셀</summary>
    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        EnsureFloorTilemap();
        if (_floorTilemap == null) return Vector2Int.zero;
        var c = _floorTilemap.WorldToCell(worldPos);
        return new Vector2Int(c.x, c.y);
    }

    /// <summary>그리드 셀 → 월드 좌표 (타일 중심)</summary>
    public Vector3 CellToWorld(Vector2Int cell)
    {
        EnsureFloorTilemap();
        if (_floorTilemap == null) return (Vector3)(Vector2)cell;
        return _floorTilemap.CellToWorld((Vector3Int)cell) + _floorTilemap.cellSize * 0.5f;
    }

    private void OnDrawGizmos()
    {
        if (!_drawGizmos || !IsInitialized) return;
        // 셀 수가 많으면 매 N번째만 그려서 프레임 보호 (대략 500개 이하로 제한)
        int step = 1;
        int total = _width * _height;
        if (total > 600) step = 2;
        if (total > 1200) step = 3;

        for (int i = 0; i < _width; i += step)
        for (int j = 0; j < _height; j += step)
        {
            var cell = IndexToCell(i, j);
            Vector3 center = CellToWorld(cell);
            bool unreachable = _unreachable[i, j];
            bool visited = _visited[i, j];

            if (unreachable)
            {
                Gizmos.color = _unreachableColor;
                float s = _gizmoCellSize * 0.4f;
                Gizmos.DrawLine(center + new Vector3(-s, -s), center + new Vector3(s, s));
                Gizmos.DrawLine(center + new Vector3(-s, s), center + new Vector3(s, -s));
            }
            else if (visited)
            {
                Gizmos.color = _visitedColor;
                Gizmos.DrawCube(center, Vector3.one * _gizmoCellSize * 0.5f);
            }
        }

        if (_globalTargetCell.HasValue && IsWalkable(_globalTargetCell.Value))
        {
            Gizmos.color = _globalTargetColor;
            Gizmos.DrawSphere(CellToWorld(_globalTargetCell.Value), _gizmoCellSize * 0.6f);
        }
    }
}
