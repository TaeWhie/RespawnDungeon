using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using TriInspector;
using UniRx;

/// <summary>
/// MapManager의 탐색/시야 단계에 따라 안개만 그립니다.
/// 3단계=짙은 안개 + 바닥/벽 타일 제거(안 보임), 2단계=옅은 안개 + 구조 복원, 1단계=안개 제거.
/// </summary>
[RequireComponent(typeof(Tilemap))]
public class ExplorationFogView : MonoBehaviour
{
    [Title("참조")]
    [Tooltip("비워두면 씬에서 자동 검색")]
    [SerializeField] private MapManager _mapManager;
    [Tooltip("안개 타일. 3단계에서 짙게, 2단계에서 옅게 같은 타일로 색상만 조절합니다.")]
    [SerializeField] private TileBase _fogTile;
    [Tooltip("3단계에서 바닥/벽을 숨기고 복원할 때 필요. 비워두면 TilemapVisualizer에서 자동 검색")]
    [SerializeField] private Tilemap _floorTilemap;
    [Tooltip("바닥 타일 (복원용)")]
    [SerializeField] private TileBase _floorTile;
    [Tooltip("벽 타일맵. 3단계에서 숨기고 2/1단계에서 복원")]
    [SerializeField] private Tilemap _wallTilemap;

    [Title("안개 강도")]
    [Tooltip("3단계(미탐험) 안개 알파. 1=완전 불투명")]
    [Slider(0f, 1f)]
    [SerializeField] private float _thickFogAlpha = 1f;
    [Tooltip("2단계(구조만) 안개 알파. 0에 가까울수록 옅음")]
    [Slider(0f, 1f)]
    [SerializeField] private float _lightFogAlpha = 0.45f;
    [Tooltip("매 프레임 안개/바닥/벽을 갱신할 반경(파티 기준). 이 구역만 갱신해 프레임 절약. 맵 전체 갱신은 초기화 시 1회만.")]
    [Slider(5, 30)]
    [SerializeField] private int _viewUpdateRadius = 10;
    [Tooltip("맵 그리드 밖 가장자리(벽 셀 등)까지 안개를 칠할 여유 칸 수. 0이면 그리드만, 1~2면 끝자락까지 짙은 안개 적용")]
    [Slider(0, 5)]
    [SerializeField] private int _edgeFogPadding = 2;
    [Tooltip("파티 타깃(Player/Ally) 재검색 주기(초). 0이면 매 프레임.")]
    [Slider(0f, 2f)]
    [SerializeField] private float _partyTargetRescanInterval = 0.5f;

    private Tilemap _fogTilemap;
    private Dictionary<Vector2Int, TileBase> _wallTilesCache = new Dictionary<Vector2Int, TileBase>();
    private HashSet<Vector2Int> _cellsToUpdate = new HashSet<Vector2Int>();
    private VisibilityByViewStage[] _cachedVisibilityObjects = Array.Empty<VisibilityByViewStage>();
    private readonly CompositeDisposable _subscriptions = new CompositeDisposable();
    private readonly List<Transform> _partyTargets = new List<Transform>();

    /// <summary>갱신할 때 floor만이 아니라 벽 셀도 포함. 벽은 인접 floor 단계에 따라 표시.</summary>
    private static readonly Vector2Int[] Cardinal4 = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    private void Awake()
    {
        _fogTilemap = GetComponent<Tilemap>();
    }

    private void OnEnable()
    {
        if (_mapManager == null)
            _mapManager = FindFirstObjectByType<MapManager>();
        if (_floorTilemap == null)
        {
            var vis = FindFirstObjectByType<TilemapVisualizer>();
            if (vis != null)
            {
                _floorTilemap = vis.FloorTilemap;
                if (_floorTile == null) _floorTile = vis.FloorTile;
                if (_wallTilemap == null) _wallTilemap = vis.WallTilemap;
            }
        }

        if (_mapManager == null)
            return;

        _mapManager.OnMapInitialized += OnMapInitialized;

        if (_mapManager.IsInitialized)
        {
            CacheWallTiles();
            RefreshFogAll();
        }

        SetupRuntimeStreams();
    }

    private void OnDisable()
    {
        if (_mapManager != null)
            _mapManager.OnMapInitialized -= OnMapInitialized;
        _subscriptions.Clear();
        _partyTargets.Clear();
    }

    private void SetupRuntimeStreams()
    {
        _subscriptions.Clear();
        if (_partyTargetRescanInterval <= 0f)
        {
            Observable.EveryUpdate()
                .Subscribe(_ => CachePartyTargets())
                .AddTo(_subscriptions);
        }
        else
        {
            Observable.Interval(TimeSpan.FromSeconds(_partyTargetRescanInterval))
                .StartWith(0L)
                .Subscribe(_ => CachePartyTargets())
                .AddTo(_subscriptions);
        }

        Observable.EveryUpdate()
            .Subscribe(_ => TickFog())
            .AddTo(_subscriptions);
    }

    private void TickFog()
    {
        if (_mapManager == null || !_mapManager.IsInitialized || _fogTilemap == null || _fogTile == null)
            return;

        if (_mapManager.DebugRevealAll)
        {
            RefreshFogAllDebugReveal();
            RefreshObjectsVisibility();
            return;
        }

        RefreshFogInViewRegion();
        RefreshObjectsVisibility();
    }

    private void CachePartyTargets()
    {
        ExplorationPartyCache.RefreshIfStale(_partyTargetRescanInterval);
        _partyTargets.Clear();

        var leader = ExplorationPartyCache.Leader;
        if (leader != null)
            _partyTargets.Add(leader);

        var allies = ExplorationPartyCache.AllyTransforms;
        for (int i = 0; i < allies.Count; i++)
        {
            if (allies[i] != null)
                _partyTargets.Add(allies[i]);
        }
    }

    /// <summary>파티(Player+Ally) 위치 기준 시야 반경 안의 셀만 안개/바닥/벽 갱신. 전체 맵은 초기화 시 1회만.</summary>
    private void RefreshFogInViewRegion()
    {
        _cellsToUpdate.Clear();
        int r = _viewUpdateRadius;
        int pad = _edgeFogPadding;
        int minX = _mapManager.MinX - pad, maxX = _mapManager.MinX + _mapManager.Width - 1 + pad;
        int minY = _mapManager.MinY - pad, maxY = _mapManager.MinY + _mapManager.Height - 1 + pad;

        for (int i = _partyTargets.Count - 1; i >= 0; i--)
        {
            var t = _partyTargets[i];
            if (t == null)
            {
                _partyTargets.RemoveAt(i);
                continue;
            }

            AddCellsInRadius(_cellsToUpdate, _mapManager.WorldToCell(t.position), r, minX, maxX, minY, maxY);
        }

        // wall 셀도 갱신: 반경 안 셀들의 인접(상하좌우) 추가
        var withWalls = new HashSet<Vector2Int>(_cellsToUpdate);
        foreach (var c in _cellsToUpdate)
        {
            foreach (var dir in Cardinal4)
            {
                var n = c + dir;
                if (n.x >= minX && n.x <= maxX && n.y >= minY && n.y <= maxY)
                    withWalls.Add(n);
            }
        }
        _cellsToUpdate = withWalls;

        foreach (var cell in _cellsToUpdate)
            RefreshFogSingleCell(cell);
    }

    private static void AddCellsInRadius(HashSet<Vector2Int> set, Vector2Int center, int radius, int minX, int maxX, int minY, int maxY)
    {
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            int x = center.x + dx, y = center.y + dy;
            if (x >= minX && x <= maxX && y >= minY && y <= maxY)
                set.Add(new Vector2Int(x, y));
        }
    }

    private void RefreshFogSingleCell(Vector2Int cell)
    {
        var pos = new Vector3Int(cell.x, cell.y, 0);
        bool isWallCell = _wallTilesCache.TryGetValue(cell, out var cachedWallTile);

        if (isWallCell)
        {
            // 벽 셀: 인접 floor의 단계로 표시 여부 결정
            bool anyVisited = false;
            bool anyFullView = false;
            foreach (var dir in Cardinal4)
            {
                var n = cell + dir;
                if (!_mapManager.CellToIndex(n, out _, out _)) continue;
                if (_mapManager.IsVisited(n)) anyVisited = true;
                if (_mapManager.IsInFullView(n)) anyFullView = true;
            }
            if (!anyVisited)
            {
                SetFog(cell, _thickFogAlpha);
                if (_wallTilemap != null) _wallTilemap.SetTile(pos, null);
            }
            else if (anyFullView)
            {
                _fogTilemap.SetTile(pos, null);
                if (_wallTilemap != null) _wallTilemap.SetTile(pos, cachedWallTile);
            }
            else
            {
                SetFog(cell, _lightFogAlpha);
                if (_wallTilemap != null) _wallTilemap.SetTile(pos, cachedWallTile);
            }
            return;
        }

        // 오브젝트(장애물·상자) 셀: 밟지 않으므로 방문 안 됨. 인접 floor 방문 시 2단계 적용 → 바닥은 보이고, 오브젝트는 1단계일 때만 보임(RefreshObjectsVisibility).
        if (_mapManager.CellToIndex(cell, out _, out _) && (_mapManager.IsObstacleCell(cell) || _mapManager.IsChestCell(cell)))
        {
            bool anyVisited = false;
            bool anyFullView = false;
            foreach (var dir in Cardinal4)
            {
                var n = cell + dir;
                if (!_mapManager.CellToIndex(n, out _, out _)) continue;
                if (_mapManager.IsVisited(n)) anyVisited = true;
                if (_mapManager.IsInFullView(n)) anyFullView = true;
            }
            if (!anyVisited)
            {
                SetFog(cell, _thickFogAlpha);
                if (_floorTilemap != null) _floorTilemap.SetTile(pos, null);
            }
            else if (anyFullView)
            {
                _fogTilemap.SetTile(pos, null);
                if (_floorTilemap != null && _floorTile != null)
                    _floorTilemap.SetTile(pos, _floorTile);
            }
            else
            {
                SetFog(cell, _lightFogAlpha);
                if (_floorTilemap != null && _floorTile != null)
                    _floorTilemap.SetTile(pos, _floorTile);
            }
            return;
        }

        // floor 셀
        bool inBounds = _mapManager.CellToIndex(cell, out _, out _);
        bool stage3 = !inBounds || !_mapManager.IsVisited(cell);

        if (stage3)
        {
            SetFog(cell, _thickFogAlpha);
            if (_floorTilemap != null)
                _floorTilemap.SetTile(pos, null);
            if (_wallTilemap != null)
                _wallTilemap.SetTile(pos, null);
        }
        else if (_mapManager.IsInFullView(cell))
        {
            _fogTilemap.SetTile(pos, null);
            if (_floorTilemap != null && _floorTile != null && (_mapManager.IsWalkable(cell) || _mapManager.IsObstacleCell(cell) || _mapManager.IsChestCell(cell)))
                _floorTilemap.SetTile(pos, _floorTile);
            if (_wallTilemap != null && _wallTilesCache.TryGetValue(cell, out var wt))
                _wallTilemap.SetTile(pos, wt);
        }
        else
        {
            SetFog(cell, _lightFogAlpha);
            if (_floorTilemap != null && _floorTile != null && (_mapManager.IsWalkable(cell) || _mapManager.IsObstacleCell(cell) || _mapManager.IsChestCell(cell)))
                _floorTilemap.SetTile(pos, _floorTile);
            if (_wallTilemap != null && _wallTilesCache.TryGetValue(cell, out var wt))
                _wallTilemap.SetTile(pos, wt);
        }
    }

    /// <summary>1단계(전부 보임)이거나, 한 번이라도 1단계로 밝힌 셀의 오브젝트만 활성화. 캐시된 목록 사용.</summary>
    private void RefreshObjectsVisibility()
    {
        for (int i = 0; i < _cachedVisibilityObjects.Length; i++)
        {
            var v = _cachedVisibilityObjects[i];
            if (v == null || !v.gameObject) continue;
            Vector2Int cell = _mapManager.WorldToCell(v.transform.position);
            bool shouldBeActive = _mapManager.DebugRevealAll || _mapManager.IsInFullView(cell);
            if (v.gameObject.activeSelf != shouldBeActive)
                v.gameObject.SetActive(shouldBeActive);
        }
    }

    private void OnMapInitialized()
    {
        CacheWallTiles();
        RefreshFogAll();
        _cachedVisibilityObjects = FindObjectsByType<VisibilityByViewStage>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        RefreshObjectsVisibility();
    }

    private void CacheWallTiles()
    {
        _wallTilesCache.Clear();
        if (_wallTilemap == null || !_mapManager.IsInitialized) return;
        int pad = _edgeFogPadding;
        int minX = _mapManager.MinX - pad, maxX = _mapManager.MinX + _mapManager.Width - 1 + pad;
        int minY = _mapManager.MinY - pad, maxY = _mapManager.MinY + _mapManager.Height - 1 + pad;
        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY; y++)
        {
            var cell = new Vector2Int(x, y);
            var pos = new Vector3Int(cell.x, cell.y, 0);
            var tile = _wallTilemap.GetTile(pos);
            if (tile != null)
                _wallTilesCache[cell] = tile;
        }
    }

    /// <summary>맵 전체 안개 + 3단계 셀은 바닥/벽 제거(안 보이게), 2/1단계는 복원.</summary>
    private void RefreshFogAll()
    {
        if (_mapManager == null || _fogTilemap == null || _fogTile == null)
            return;

        _fogTilemap.ClearAllTiles();

        int pad = _edgeFogPadding;
        int minX = _mapManager.MinX - pad, maxX = _mapManager.MinX + _mapManager.Width - 1 + pad;
        int minY = _mapManager.MinY - pad, maxY = _mapManager.MinY + _mapManager.Height - 1 + pad;

        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY; y++)
        {
            var cell = new Vector2Int(x, y);
            var pos = new Vector3Int(cell.x, cell.y, 0);
            bool isWallCell = _wallTilesCache.TryGetValue(cell, out var cachedWallTile);

            if (isWallCell)
            {
                bool anyVisited = false;
                bool anyFullView = false;
                foreach (var dir in Cardinal4)
                {
                    var n = cell + dir;
                    if (!_mapManager.CellToIndex(n, out _, out _)) continue;
                    if (_mapManager.IsVisited(n)) anyVisited = true;
                    if (_mapManager.IsInFullView(n)) anyFullView = true;
                }
                if (!anyVisited)
                {
                    SetFog(cell, _thickFogAlpha);
                    if (_wallTilemap != null) _wallTilemap.SetTile(pos, null);
                }
                else if (anyFullView)
                {
                    _fogTilemap.SetTile(pos, null);
                    if (_wallTilemap != null) _wallTilemap.SetTile(pos, cachedWallTile);
                }
                else
                {
                    SetFog(cell, _lightFogAlpha);
                    if (_wallTilemap != null) _wallTilemap.SetTile(pos, cachedWallTile);
                }
                continue;
            }

            bool inBounds = _mapManager.CellToIndex(cell, out _, out _);
            // 오브젝트(장애물·상자) 셀: 인접 floor 방문 시 2단계 적용
            if (inBounds && (_mapManager.IsObstacleCell(cell) || _mapManager.IsChestCell(cell)))
            {
                bool anyVisited = false;
                bool anyFullView = false;
                foreach (var dir in Cardinal4)
                {
                    var n = cell + dir;
                    if (!_mapManager.CellToIndex(n, out _, out _)) continue;
                    if (_mapManager.IsVisited(n)) anyVisited = true;
                    if (_mapManager.IsInFullView(n)) anyFullView = true;
                }
                if (!anyVisited)
                {
                    SetFog(cell, _thickFogAlpha);
                    if (_floorTilemap != null) _floorTilemap.SetTile(pos, null);
                }
                else if (anyFullView)
                {
                    _fogTilemap.SetTile(pos, null);
                    if (_floorTilemap != null && _floorTile != null)
                        _floorTilemap.SetTile(pos, _floorTile);
                }
                else
                {
                    SetFog(cell, _lightFogAlpha);
                    if (_floorTilemap != null && _floorTile != null)
                        _floorTilemap.SetTile(pos, _floorTile);
                }
                continue;
            }

            bool stage3 = !inBounds || !_mapManager.IsVisited(cell);

            if (stage3)
            {
                SetFog(cell, _thickFogAlpha);
                if (_floorTilemap != null)
                    _floorTilemap.SetTile(pos, null);
                if (_wallTilemap != null)
                    _wallTilemap.SetTile(pos, null);
            }
            else if (_mapManager.IsInFullView(cell))
            {
                _fogTilemap.SetTile(pos, null);
                if (_floorTilemap != null && _floorTile != null && (_mapManager.IsWalkable(cell) || _mapManager.IsObstacleCell(cell) || _mapManager.IsChestCell(cell)))
                    _floorTilemap.SetTile(pos, _floorTile);
                if (_wallTilemap != null && _wallTilesCache.TryGetValue(cell, out var wt))
                    _wallTilemap.SetTile(pos, wt);
            }
            else
            {
                SetFog(cell, _lightFogAlpha);
                if (_floorTilemap != null && _floorTile != null && (_mapManager.IsWalkable(cell) || _mapManager.IsObstacleCell(cell) || _mapManager.IsChestCell(cell)))
                    _floorTilemap.SetTile(pos, _floorTile);
                if (_wallTilemap != null && _wallTilesCache.TryGetValue(cell, out var wt))
                    _wallTilemap.SetTile(pos, wt);
            }
        }
    }

    /// <summary>개발용: 안개 제거 + 바닥/벽 전부 복원.</summary>
    private void RefreshFogAllDebugReveal()
    {
        if (_mapManager == null || _fogTilemap == null)
            return;
        _fogTilemap.ClearAllTiles();
        for (int x = _mapManager.MinX; x <= _mapManager.MinX + _mapManager.Width - 1; x++)
        for (int y = _mapManager.MinY; y <= _mapManager.MinY + _mapManager.Height - 1; y++)
        {
            var cell = new Vector2Int(x, y);
            var pos = new Vector3Int(cell.x, cell.y, 0);
            if (_floorTilemap != null && _floorTile != null && (_mapManager.IsWalkable(cell) || _mapManager.IsObstacleCell(cell) || _mapManager.IsChestCell(cell)))
                _floorTilemap.SetTile(pos, _floorTile);
        }
        if (_wallTilemap != null)
        {
            foreach (var kv in _wallTilesCache)
                _wallTilemap.SetTile(new Vector3Int(kv.Key.x, kv.Key.y, 0), kv.Value);
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>디버그 패널에서 Reveal All을 켰을 때, 전체 맵을 한 번 갱신해 줍니다.</summary>
    public void DebugForceRevealAll()
    {
        RefreshFogAllDebugReveal();
        RefreshObjectsVisibility();
    }

    /// <summary>디버그 패널에서 Reveal All을 껐을 때, 원래 안개 규칙으로 전체를 한 번 리셋합니다.</summary>
    public void DebugForceNormalFog()
    {
        CacheWallTiles();
        RefreshFogAll();
        RefreshObjectsVisibility();
    }
#endif

    private void SetFog(Vector2Int cell, float alpha)
    {
        var pos = new Vector3Int(cell.x, cell.y, 0);
        _fogTilemap.SetTile(pos, _fogTile);
        _fogTilemap.SetColor(pos, new Color(1f, 1f, 1f, alpha));
    }
}
