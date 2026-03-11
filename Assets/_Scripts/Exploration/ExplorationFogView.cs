using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// MapManager의 탐색 상태에 따라 미탐색 타일 위에 폭(블러/다크) 오버레이를 그립니다.
/// 맵 초기화 시 모든 이동 가능 셀에 폭 타일을 깔고, 셀이 방문될 때마다 해당 셀의 폭을 제거합니다.
/// 사용법: 빈 GameObject에 Tilemap + 이 스크립트를 붙이고, 폭 타일맵의 Sort Order를 바닥 타일맵보다 크게 두세요.
/// 블러 느낌: 폭 타일맵에 머티리얼로 Shaders/ExplorationFogDarken을 적용하면 뿌옇게 보입니다.
/// </summary>
[RequireComponent(typeof(Tilemap))]
public class ExplorationFogView : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("비워두면 씬에서 자동 검색")]
    [SerializeField] private MapManager _mapManager;
    [Tooltip("미탐색 타일에 칠할 타일. 반투명 검정 타일 또는 블러 느낌의 타일을 사용하세요.")]
    [SerializeField] private TileBase _fogTile;

    private Tilemap _fogTilemap;

    private void Awake()
    {
        _fogTilemap = GetComponent<Tilemap>();
    }

    private void OnEnable()
    {
        if (_mapManager == null)
            _mapManager = FindFirstObjectByType<MapManager>();

        if (_mapManager == null)
            return;

        _mapManager.OnMapInitialized += PaintFogAll;
        _mapManager.OnCellVisited += ClearFogAt;

        if (_mapManager.IsInitialized)
            PaintFogAll();
    }

    private void OnDisable()
    {
        if (_mapManager != null)
        {
            _mapManager.OnMapInitialized -= PaintFogAll;
            _mapManager.OnCellVisited -= ClearFogAt;
        }
    }

    /// <summary>모든 이동 가능 셀에 폭 타일을 칠합니다. 장애물(비워커블) 셀도 폭으로 덮어 미탐험 시 가려둡니다.</summary>
    private void PaintFogAll()
    {
        if (_mapManager == null || _fogTilemap == null || _fogTile == null)
            return;

        _fogTilemap.ClearAllTiles();

        for (int x = _mapManager.MinX; x <= _mapManager.MinX + _mapManager.Width - 1; x++)
        for (int y = _mapManager.MinY; y <= _mapManager.MinY + _mapManager.Height - 1; y++)
        {
            var cell = new Vector2Int(x, y);
            if (!_mapManager.IsVisited(cell))
                SetFogTile(cell, true);
        }
    }

    /// <summary>해당 셀의 폭을 제거합니다.</summary>
    private void ClearFogAt(Vector2Int cell)
    {
        if (_fogTilemap == null) return;
        SetFogTile(cell, false);
    }

    private void SetFogTile(Vector2Int cell, bool set)
    {
        var pos = new Vector3Int(cell.x, cell.y, 0);
        if (set)
            _fogTilemap.SetTile(pos, _fogTile);
        else
            _fogTilemap.SetTile(pos, null);
    }
}
