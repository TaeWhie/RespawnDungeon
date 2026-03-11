using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// MapManager의 탐색/시야 단계에 따라 안개를 그리고, 3단계 셀에서는 바닥·벽을 렌더링하지 않습니다.
/// 3단계=짙은 안개만 표시(바닥/벽/오브젝트 미렌더), 2단계=옅은 안개, 1단계=안개 없음.
/// </summary>
[RequireComponent(typeof(Tilemap))]
public class ExplorationFogView : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("비워두면 씬에서 자동 검색")]
    [SerializeField] private MapManager _mapManager;
    [Tooltip("안개 타일. 3단계에서 짙게, 2단계에서 옅게 같은 타일로 색상만 조절합니다.")]
    [SerializeField] private TileBase _fogTile;
    [Tooltip("3단계에서 렌더링 끄기 위해 필요. 비워두면 안개만 칠함")]
    [SerializeField] private Tilemap _floorTilemap;
    [Tooltip("바닥 타일 (복원용). _floorTilemap 쓸 때 할당")]
    [SerializeField] private TileBase _floorTile;
    [Tooltip("3단계에서 벽 렌더링 끄기. 비워두면 벽은 그대로 그림")]
    [SerializeField] private Tilemap _wallTilemap;

    [Header("안개 강도")]
    [Tooltip("3단계(미탐험) 안개 알파. 1=완전 불투명")]
    [SerializeField, Range(0f, 1f)] private float _thickFogAlpha = 1f;
    [Tooltip("2단계(구조만) 안개 알파. 0에 가까울수록 옅음")]
    [SerializeField, Range(0f, 1f)] private float _lightFogAlpha = 0.45f;
    [Tooltip("매 프레임 안개/바닥/벽을 갱신할 반경(파티 기준). 이 구역만 갱신해 프레임 절약. 맵 전체 갱신은 초기화 시 1회만.")]
    [SerializeField] private int _viewUpdateRadius = 10;

    private Tilemap _fogTilemap;
    private Dictionary<Vector2Int, TileBase> _wallTilesCache = new Dictionary<Vector2Int, TileBase>();
    private HashSet<Vector2Int> _cellsToUpdate = new HashSet<Vector2Int>();
    private VisibilityByViewStage[] _cachedVisibilityObjects = Array.Empty<VisibilityByViewStage>();

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
    }

    private void OnDisable()
    {
        if (_mapManager != null)
            _mapManager.OnMapInitialized -= OnMapInitialized;
    }

    private void Update()
    {
        if (_mapManager == null || !_mapManager.IsInitialized || _fogTilemap == null || _fogTile == null)
            return;

        // 개발용: 전체 시야 보기 모드일 때는 맵 전체를 강제로 갱신
        if (_mapManager.DebugRevealAll)
        {
            RefreshFogAllDebugReveal();
            RefreshObjectsVisibility();
            return;
        }

        RefreshFogInViewRegion();
        RefreshObjectsVisibility();
    }

    /// <summary>파티(Player+Ally) 위치 기준 시야 반경 안의 셀만 안개/바닥/벽 갱신. 전체 맵은 초기화 시 1회만.</summary>
    private void RefreshFogInViewRegion()
    {
        _cellsToUpdate.Clear();
        int r = _viewUpdateRadius;
        int minX = _mapManager.MinX, maxX = _mapManager.MinX + _mapManager.Width - 1;
        int minY = _mapManager.MinY, maxY = _mapManager.MinY + _mapManager.Height - 1;

        var player = GameObject.FindWithTag("Player");
        if (player != null)
            AddCellsInRadius(_cellsToUpdate, _mapManager.WorldToCell(player.transform.position), r, minX, maxX, minY, maxY);
        var allies = GameObject.FindGameObjectsWithTag("Ally");
        for (int i = 0; i < allies.Length; i++)
        {
            if (allies[i] != null)
                AddCellsInRadius(_cellsToUpdate, _mapManager.WorldToCell(allies[i].transform.position), r, minX, maxX, minY, maxY);
        }

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
        bool stage3 = !_mapManager.IsVisited(cell);
        var pos = new Vector3Int(cell.x, cell.y, 0);

        if (stage3)
        {
            SetFog(cell, _thickFogAlpha);
            if (_floorTilemap != null && _mapManager.IsWalkable(cell))
                _floorTilemap.SetTile(pos, null);
            if (_wallTilemap != null && _wallTilesCache.TryGetValue(cell, out _))
                _wallTilemap.SetTile(pos, null);
        }
        else
        {
            if (_mapManager.IsInFullView(cell))
            {
                _fogTilemap.SetTile(pos, null);
                if (_floorTilemap != null && _floorTile != null && _mapManager.IsWalkable(cell))
                    _floorTilemap.SetTile(pos, _floorTile);
                if (_wallTilemap != null && _wallTilesCache.TryGetValue(cell, out var wt))
                    _wallTilemap.SetTile(pos, wt);
            }
            else
            {
                SetFog(cell, _lightFogAlpha);
                if (_floorTilemap != null && _floorTile != null && _mapManager.IsWalkable(cell))
                    _floorTilemap.SetTile(pos, _floorTile);
                if (_wallTilemap != null && _wallTilesCache.TryGetValue(cell, out var wt))
                    _wallTilemap.SetTile(pos, wt);
            }
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
        for (int x = _mapManager.MinX; x <= _mapManager.MinX + _mapManager.Width - 1; x++)
        for (int y = _mapManager.MinY; y <= _mapManager.MinY + _mapManager.Height - 1; y++)
        {
            var cell = new Vector2Int(x, y);
            var pos = new Vector3Int(cell.x, cell.y, 0);
            var tile = _wallTilemap.GetTile(pos);
            if (tile != null)
                _wallTilesCache[cell] = tile;
        }
    }

    /// <summary>맵 전체 안개 + 3단계 셀 바닥/벽 미렌더.</summary>
    private void RefreshFogAll()
    {
        if (_mapManager == null || _fogTilemap == null || _fogTile == null)
            return;

        _fogTilemap.ClearAllTiles();

        for (int x = _mapManager.MinX; x <= _mapManager.MinX + _mapManager.Width - 1; x++)
        for (int y = _mapManager.MinY; y <= _mapManager.MinY + _mapManager.Height - 1; y++)
        {
            var cell = new Vector2Int(x, y);
            bool stage3 = !_mapManager.IsVisited(cell);

            if (stage3)
            {
                SetFog(cell, _thickFogAlpha);
                if (_floorTilemap != null && _mapManager.IsWalkable(cell))
                    _floorTilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), null);
                if (_wallTilemap != null && _wallTilesCache.TryGetValue(cell, out _))
                    _wallTilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), null);
            }
            else
            {
                if (_mapManager.IsInFullView(cell))
                {
                    if (_floorTilemap != null && _floorTile != null && _mapManager.IsWalkable(cell))
                        _floorTilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), _floorTile);
                    if (_wallTilemap != null && _wallTilesCache.TryGetValue(cell, out var wt))
                        _wallTilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), wt);
                }
                else
                {
                    SetFog(cell, _lightFogAlpha);
                    if (_floorTilemap != null && _floorTile != null && _mapManager.IsWalkable(cell))
                        _floorTilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), _floorTile);
                    if (_wallTilemap != null && _wallTilesCache.TryGetValue(cell, out var wt))
                        _wallTilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), wt);
                }
            }
        }
    }

    /// <summary>개발용: 시야/안개를 무시하고 맵 전체 바닥·벽을 항상 보이게 합니다.</summary>
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

            // 안개 없음 + 바닥/벽 항상 표시
            if (_floorTilemap != null && _floorTile != null && _mapManager.IsWalkable(cell))
                _floorTilemap.SetTile(pos, _floorTile);
            if (_wallTilemap != null && _wallTilesCache.TryGetValue(cell, out var wt))
                _wallTilemap.SetTile(pos, wt);
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
