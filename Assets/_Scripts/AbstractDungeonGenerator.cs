using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using TriInspector;

public abstract class AbstractDungeonGenerator : MonoBehaviour
{
    [Title("참조")]
    [Required]
    [SerializeField]
    protected TilemapVisualizer tilemapVisualizer = null;
    [SerializeField]
    protected Vector2Int startPosition = Vector2Int.zero;
    [Tooltip("탐험 AI용. 비워두면 씬에서 자동 검색. 던전 생성 시 바닥 타일을 등록합니다.")]
    [SerializeField]
    protected MapManager mapManager = null;
    [Tooltip("체크 시 플레이 시작 시 던전 자동 생성 (탐험 AI가 맵 데이터를 사용하려면 필요)")]
    [SerializeField] protected bool generateOnStart = true;

    [Title("맵 시드")]
    [Tooltip("0이면 매번 랜덤 시드, 0이 아니면 같은 시드에서 동일한 맵이 생성됩니다. 재현용으로 사용하세요.")]
    [SerializeField] protected int _mapSeed = 0;
    /// <summary>마지막 생성에 사용된 시드 (0이었을 때 자동 할당된 값 포함). UI/디버그용.</summary>
    public int LastUsedSeed { get; private set; }

    private void Start()
    {
        if (generateOnStart && tilemapVisualizer != null)
            GenerateDungeon();
    }

    public void GenerateDungeon()
    {
        tilemapVisualizer.Clear();
        int seedToUse = _mapSeed != 0 ? _mapSeed : (int)(System.DateTime.UtcNow.Ticks & 0x7FFFFFFF);
        if (seedToUse == 0) seedToUse = 1;
        Random.InitState(seedToUse);
        LastUsedSeed = seedToUse;
        RunProceduralGeneration();
    }

    /// <summary>던전 생성 후 이동 가능 타일을 MapManager에 등록 (탐험 AI가 사용). 장애물·상자 셀은 경계에 포함해 부순 뒤에도 floor로 인식되게 합니다.</summary>
    protected void SetMapManagerWalkable(HashSet<Vector2Int> floor)
    {
        if (mapManager == null) mapManager = FindFirstObjectByType<MapManager>();
        if (mapManager != null)
            mapManager.SetWalkableTiles(floor, GetBoundsExtensionForWalkable());
    }

    /// <summary>장애물·상자 셀 목록. 맵 경계에 포함해 SetCellWalkable이 동작하도록 합니다.</summary>
    private IEnumerable<Vector2Int> GetBoundsExtensionForWalkable()
    {
        if (tilemapVisualizer == null) return null;
        var list = new List<Vector2Int>();
        list.AddRange(tilemapVisualizer.GetLastPlacedObstacleCells());
        foreach (var (cell, _) in tilemapVisualizer.GetLastPlacedChests())
            list.Add(cell);
        return list.Count > 0 ? list : null;
    }

    /// <summary>던전 생성 후 보물/황금상자 셀을 MapManager에 등록 (픽 대상·Open 후 제거용)</summary>
    protected void RegisterChestCellsToMapManager()
    {
        if (mapManager == null) mapManager = FindFirstObjectByType<MapManager>();
        if (tilemapVisualizer != null && mapManager != null)
            mapManager.RegisterChests(tilemapVisualizer.GetLastPlacedChests());
    }

    /// <summary>던전 생성 후 장애물 셀·뷰를 MapManager에 등록 (시야는 막지 않음, 목적지 도착 시 부수기용)</summary>
    protected void RegisterObstacleCellsToMapManager()
    {
        if (mapManager == null) mapManager = FindFirstObjectByType<MapManager>();
        if (tilemapVisualizer != null && mapManager != null)
            mapManager.RegisterObstacles(tilemapVisualizer.GetLastPlacedObstacles());
    }

    /// <summary>던전 생성 후 입구(시작) 셀을 MapManager에 등록. 이동할 곳이 없을 때 목적지로 사용.</summary>
    protected void SetStartCellToMapManager(Vector2Int startCell)
    {
        if (mapManager == null) mapManager = FindFirstObjectByType<MapManager>();
        if (mapManager != null)
            mapManager.SetStartCell(startCell);
    }

    protected abstract void RunProceduralGeneration();
}
