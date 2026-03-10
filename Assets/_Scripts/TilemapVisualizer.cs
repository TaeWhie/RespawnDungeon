using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapVisualizer : MonoBehaviour
{
    [SerializeField]
    private Tilemap floorTilemap, wallTilemap;
    [SerializeField]
    private TileBase floorTile, wallTop, wallSideRight, wallSiderLeft, wallBottom, wallFull, 
        wallInnerCornerDownLeft, wallInnerCornerDownRight, 
        wallDiagonalCornerDownRight, wallDiagonalCornerDownLeft, wallDiagonalCornerUpRight, wallDiagonalCornerUpLeft;
    [SerializeField]
    private GameObject startPrefab, exitPrefab;

    [Header("파티 스폰")]
    [Tooltip("던전 생성 시 리더(플레이어) 포함 파티 인원 수. 1이면 리더만 스폰합니다.")]
    [SerializeField] private int _partyCount = 1;

    [Tooltip("리더와 동료 모두에 사용할 공용 캐릭터 프리팹 (예: Assets/Character).")]
    [SerializeField] private GameObject _characterPrefab;

    private List<GameObject> spawnedObjects = new List<GameObject>();

    public Tilemap FloorTilemap => floorTilemap;

    public void PaintFloorTiles(IEnumerable<Vector2Int> floorPositions)
    {
        PaintTiles(floorPositions, floorTilemap, floorTile);
    }

    public (GameObject start, GameObject exit) PlaceSpecialObjects(Vector2Int start, Vector2Int exit)
    {
        GameObject startMarker = null;
        GameObject exitObj = null;
        Vector3 startWorld = floorTilemap.CellToWorld((Vector3Int)start) + floorTilemap.cellSize * 0.5f;
        Vector3 exitWorld = floorTilemap.CellToWorld((Vector3Int)exit) + floorTilemap.cellSize * 0.5f;

        // 시작 마커: 순수 시각적 오브젝트
        if (startPrefab != null)
        {
            startMarker = SpawnObject(startPrefab, start);
        }

        // 리더(플레이어) 스폰 또는 위치 이동
        GameObject leader = GameObject.FindWithTag("Player");
        if (leader != null)
        {
            leader.transform.position = startWorld;
        }
        else if (_characterPrefab != null)
        {
            leader = Instantiate(_characterPrefab, startWorld, Quaternion.identity);
        }

        // 리더 컴포넌트 보장: Tag, Rigidbody2D, Collider2D, ExplorerAI
        if (leader != null)
        {
            leader.tag = "Player";

            var rb = leader.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = leader.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }

            if (leader.GetComponent<Collider2D>() == null)
            {
                var col = leader.AddComponent<CapsuleCollider2D>();
                col.isTrigger = false;
            }

            if (leader.GetComponent<ExplorerAI>() == null)
            {
                leader.AddComponent<ExplorerAI>();
            }
        }

        // 동료 스폰: 리더 포함 _partyCount 명이 되도록 공용 캐릭터 프리팹에서 생성
        if (_partyCount > 1 && _characterPrefab != null && leader != null)
        {
            for (int i = 1; i < _partyCount; i++)
            {
                var follower = Instantiate(_characterPrefab, startWorld, Quaternion.identity);

                // 최소한의 물리 컴포넌트만 보장
                var rbF = follower.GetComponent<Rigidbody2D>();
                if (rbF == null)
                {
                    rbF = follower.AddComponent<Rigidbody2D>();
                    rbF.gravityScale = 0f;
                    rbF.constraints = RigidbodyConstraints2D.FreezeRotation;
                }

                if (follower.GetComponent<Collider2D>() == null)
                {
                    var col = follower.AddComponent<CapsuleCollider2D>();
                    col.isTrigger = false;
                }

                // 카메라용 태그
                follower.tag = "Ally";

                // 동료는 AI를 직접 붙이지 않고, 나중에 따로 Follow 로직을 추가할 수 있도록 비워둠
            }
        }

        // 카메라가 처음부터 파티를 보도록 즉시 스냅
        var mainCam = Camera.main;
        if (mainCam != null)
        {
            var partyCam = mainCam.GetComponent<PartyCameraController>();
            if (partyCam != null)
                partyCam.SnapToPartyImmediate();
        }

        // 씬에 이미 Exit이 있으면 새로 생성하지 않고 출구 위치로만 이동
        var existingExit = GameObject.FindWithTag("Exit");
        if (existingExit != null)
        {
            existingExit.transform.position = exitWorld;
            exitObj = existingExit;
        }
        else if (exitPrefab != null)
        {
            exitObj = SpawnObject(exitPrefab, exit);
        }

        // start 반환값은 실제 리더(플레이어) 오브젝트를 의미하도록 설정
        return (leader, exitObj);
    }

    private GameObject SpawnObject(GameObject prefab, Vector2Int position)
    {
        var worldPos = floorTilemap.CellToWorld((Vector3Int)position) + floorTilemap.cellSize * 0.5f;
        var spawned = Instantiate(prefab, worldPos, Quaternion.identity);
        spawnedObjects.Add(spawned);
        return spawned;
    }

    private void PaintTiles(IEnumerable<Vector2Int> positions, Tilemap tilemap, TileBase tile)
    {
        foreach (var position in positions)
        {
            PaintSingleTile(tilemap, tile, position);
        }
    }

    internal void PaintSingleBasicWall(Vector2Int position, string binaryType)
    {
        int typeAsInt = Convert.ToInt32(binaryType, 2);
        TileBase tile = null;
        if (WallTypesHelper.wallTop.Contains(typeAsInt))
        {
            tile = wallTop;
        }else if (WallTypesHelper.wallSideRight.Contains(typeAsInt))
        {
            tile = wallSideRight;
        }
        else if (WallTypesHelper.wallSideLeft.Contains(typeAsInt))
        {
            tile = wallSiderLeft;
        }
        else if (WallTypesHelper.wallBottm.Contains(typeAsInt))
        {
            tile = wallBottom;
        }
        else if (WallTypesHelper.wallFull.Contains(typeAsInt))
        {
            tile = wallFull;
        }

        if (tile!=null)
            PaintSingleTile(wallTilemap, tile, position);
    }

    private void PaintSingleTile(Tilemap tilemap, TileBase tile, Vector2Int position)
    {
        var tilePosition = tilemap.WorldToCell((Vector3Int)position);
        tilemap.SetTile(tilePosition, tile);
    }

    public void Clear()
    {
        floorTilemap.ClearAllTiles();
        wallTilemap.ClearAllTiles();
        foreach (var obj in spawnedObjects)
        {
            if (obj != null) DestroyImmediate(obj);
        }
        spawnedObjects.Clear();
    }

    internal void PaintSingleCornerWall(Vector2Int position, string binaryType)
    {
        int typeASInt = Convert.ToInt32(binaryType, 2);
        TileBase tile = null;

        if (WallTypesHelper.wallInnerCornerDownLeft.Contains(typeASInt))
        {
            tile = wallInnerCornerDownLeft;
        }
        else if (WallTypesHelper.wallInnerCornerDownRight.Contains(typeASInt))
        {
            tile = wallInnerCornerDownRight;
        }
        else if (WallTypesHelper.wallDiagonalCornerDownLeft.Contains(typeASInt))
        {
            tile = wallDiagonalCornerDownLeft;
        }
        else if (WallTypesHelper.wallDiagonalCornerDownRight.Contains(typeASInt))
        {
            tile = wallDiagonalCornerDownRight;
        }
        else if (WallTypesHelper.wallDiagonalCornerUpRight.Contains(typeASInt))
        {
            tile = wallDiagonalCornerUpRight;
        }
        else if (WallTypesHelper.wallDiagonalCornerUpLeft.Contains(typeASInt))
        {
            tile = wallDiagonalCornerUpLeft;
        }
        else if (WallTypesHelper.wallFullEightDirections.Contains(typeASInt))
        {
            tile = wallFull;
        }
        else if (WallTypesHelper.wallBottmEightDirections.Contains(typeASInt))
        {
            tile = wallBottom;
        }

        if (tile != null)
            PaintSingleTile(wallTilemap, tile, position);
    }
}
