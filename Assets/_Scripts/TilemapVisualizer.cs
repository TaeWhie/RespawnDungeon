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

    private List<GameObject> spawnedObjects = new List<GameObject>();

    public Tilemap FloorTilemap => floorTilemap;

    public void PaintFloorTiles(IEnumerable<Vector2Int> floorPositions)
    {
        PaintTiles(floorPositions, floorTilemap, floorTile);
    }

    public (GameObject start, GameObject exit) PlaceSpecialObjects(Vector2Int start, Vector2Int exit)
    {
        GameObject startObj = null;
        GameObject exitObj = null;
        Vector3 startWorld = floorTilemap.CellToWorld((Vector3Int)start) + floorTilemap.cellSize * 0.5f;
        Vector3 exitWorld = floorTilemap.CellToWorld((Vector3Int)exit) + floorTilemap.cellSize * 0.5f;

        // 씬에 이미 Player가 있으면 새로 생성하지 않고 시작 위치로만 이동
        var existingPlayer = GameObject.FindWithTag("Player");
        if (existingPlayer != null)
        {
            existingPlayer.transform.position = startWorld;
            startObj = existingPlayer;
        }
        else if (startPrefab != null)
        {
            startObj = SpawnObject(startPrefab, start);
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

        return (startObj, exitObj);
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
