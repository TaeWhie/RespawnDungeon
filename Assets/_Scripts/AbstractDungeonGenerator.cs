using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

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

    [Header("맵 시드")]
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

    /// <summary>던전 생성 후 이동 가능 타일을 MapManager에 등록 (탐험 AI가 사용)</summary>
    protected void SetMapManagerWalkable(HashSet<Vector2Int> floor)
    {
        if (mapManager == null) mapManager = FindFirstObjectByType<MapManager>();
        if (mapManager != null) mapManager.SetWalkableTiles(floor);
    }

    protected abstract void RunProceduralGeneration();
}
