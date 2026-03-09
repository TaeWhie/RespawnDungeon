using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class AbstractDungeonGenerator : MonoBehaviour
{
    [SerializeField]
    protected TilemapVisualizer tilemapVisualizer = null;
    [SerializeField]
    protected Vector2Int startPosition = Vector2Int.zero;
    [Tooltip("탐험 AI용. 비워두면 씬에서 자동 검색. 던전 생성 시 바닥 타일을 등록합니다.")]
    [SerializeField]
    protected MapManager mapManager = null;
    [Tooltip("체크 시 플레이 시작 시 던전 자동 생성 (탐험 AI가 맵 데이터를 사용하려면 필요)")]
    [SerializeField] protected bool generateOnStart = true;

    private void Start()
    {
        if (generateOnStart && tilemapVisualizer != null)
            GenerateDungeon();
    }

    public void GenerateDungeon()
    {
        tilemapVisualizer.Clear();
        RunProceduralGeneration();
    }

    /// <summary>던전 생성 후 이동 가능 타일을 MapManager에 등록 (탐험 AI가 사용)</summary>
    protected void SetMapManagerWalkable(HashSet<Vector2Int> floor)
    {
        if (mapManager == null) mapManager = FindFirstObjectByType<MapManager>();
        if (mapManager != null) mapManager.SetWalkableTiles(floor);
    }

    protected abstract void RunProceduralGeneration();
}
