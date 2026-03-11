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

    [Header("펄린 노이즈 배치 (장애물·보물상자)")]
    [Tooltip("노이즈 조밀도. 작을수록 군집이 커짐")]
    [SerializeField] private float _perlinScale = 0.1f;
    [Tooltip("이 값 이상인 구역에 장애물 배치 (군집)")]
    [SerializeField] [Range(0f, 1f)] private float _obstacleThreshold = 0.6f;
    [Tooltip("이 값 이상인 구역에 보물상자 배치 (희귀)")]
    [SerializeField] [Range(0f, 1f)] private float _treasureThreshold = 0.85f;
    [Tooltip("장애물 프리팹 (바위, 나무 등). 비우면 장애물 미배치")]
    [SerializeField] private GameObject _obstaclePrefab;
    [Tooltip("보물상자 프리팹. 비우면 보물상자 미배치")]
    [SerializeField] private GameObject _treasureChestPrefab;
    [Tooltip("황금상자 프리팹. 비우면 황금상자 미배치. 시야 확인 어렵거나 고립된 곳에만 배치")]
    [SerializeField] private GameObject _goldenChestPrefab;
    [Tooltip("황금상자 배치 확률(노이즈 기준). 이 값 이상일 때만 후보 셀에 배치")]
    [SerializeField] [Range(0f, 1f)] private float _goldenChestThreshold = 0.92f;
    [Tooltip("황금상자 후보: 최장 경로에서 이 거리 이상 떨어진 셀 또는 막다른 길(인접 floor 1개)")]
    [SerializeField] private int _goldenChestMinPathDistance = 3;
    [Tooltip("황금상자 개수 (고립/막다른 길 후보에서 최소 간격 유지하며 배치)")]
    [SerializeField] private int _goldenChestCount = 3;
    [Tooltip("일반 보물상자 개수 (최소 간격 유지하며 배치)")]
    [SerializeField] private int _treasureChestCount = 8;
    [Tooltip("상자 간 최소 맨해튼 거리 (이 값 미만이면 뭉침으로 간주)")]
    [SerializeField] private int _chestMinSpacing = 4;

    [Header("문 배치")]
    [Tooltip("중요 복도(출구/황금상자/고립 방 경로)에 배치할 문 프리팹. 비우면 문 미배치")]
    [SerializeField] private GameObject _doorPrefab;
    [Tooltip("최대 문 개수")]
    [SerializeField] private int _doorCount = 4;
    [Tooltip("문 간 최소 맨해튼 거리")]
    [SerializeField] private int _doorMinSpacing = 5;

    [Header("파티 스폰")]
    [Tooltip("던전 생성 시 리더(플레이어) 포함 파티 인원 수. 1이면 리더만 스폰합니다.")]
    [SerializeField] private int _partyCount = 1;

    [Tooltip("리더와 동료 모두에 사용할 공용 캐릭터 프리팹 (예: Assets/Character).")]
    [SerializeField] private GameObject _characterPrefab;

    private List<GameObject> spawnedObjects = new List<GameObject>();
    /// <summary>마지막 PlacePerlinObstaclesAndTreasures 호출 시 배치한 (셀, ChestOpenable) 목록</summary>
    private List<(Vector2Int cell, ChestOpenable openable)> _lastPlacedChests = new List<(Vector2Int, ChestOpenable)>();

    public Tilemap FloorTilemap => floorTilemap;
    public Tilemap WallTilemap => wallTilemap;
    public TileBase FloorTile => floorTile;

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
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
                leader.layer = playerLayer;

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
                int allyLayer = LayerMask.NameToLayer("Ally");
                if (allyLayer >= 0)
                    follower.layer = allyLayer;

                // 리더를 자연스럽게 따라오는 Follow 로직
                var partyFollower = follower.GetComponent<PartyFollower>();
                if (partyFollower == null)
                    partyFollower = follower.AddComponent<PartyFollower>();
                partyFollower.SetLeader(leader.transform);
                partyFollower.SetSlotIndex(i - 1);
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

    /// <summary>
    /// 펄린 노이즈로 장애물·보물상자를 군집감 있게 배치합니다.
    /// 타일 위에 오브젝트를 생성하며, 장애물이 있는 셀은 floor에서 제거해 벽(비이동) 취급합니다.
    /// 입구~출구 최장 경로 상의 셀에는 장애물/보물을 배치하지 않습니다.
    /// </summary>
    public void PlacePerlinObstaclesAndTreasures(HashSet<Vector2Int> floor, Vector2Int start, Vector2Int exit, int seed)
    {
        if (floor == null) return;

        _lastPlacedChests.Clear();
        // 문/상자/장애물 경로 계산용으로, 원본 floor를 보존한 복사본을 사용합니다.
        var floorForPaths = new HashSet<Vector2Int>(floor);

        HashSet<Vector2Int> pathCells = GetCellsOnLongestPath(floorForPaths, start, exit);
        Dictionary<Vector2Int, int> distanceFromPath = pathCells != null
            ? MultiSourceBFS(floorForPaths, pathCells)
            : new Dictionary<Vector2Int, int>();

        var validChestCells = new List<Vector2Int>();
        var goldenCandidatesList = new List<Vector2Int>();
        foreach (var c in floor)
        {
            if (c == start || c == exit) continue;
            if (pathCells != null && pathCells.Contains(c)) continue;
            if (Mathf.Abs(c.x - start.x) <= 1 && Mathf.Abs(c.y - start.y) <= 1) continue;
            if (Mathf.Abs(c.x - exit.x) <= 1 && Mathf.Abs(c.y - exit.y) <= 1) continue;
            validChestCells.Add(c);
            if (_goldenChestPrefab != null && _goldenChestMinPathDistance > 0)
            {
                int pathDist;
                bool farFromPath = distanceFromPath.TryGetValue(c, out pathDist) && pathDist >= _goldenChestMinPathDistance;
                bool isDeadEnd = CountFloorNeighbors(floorForPaths, c) <= 1;
                if (farFromPath || isDeadEnd)
                    goldenCandidatesList.Add(c);
            }
        }

        UnityEngine.Random.InitState(seed);
        int minSpacing = Mathf.Max(1, _chestMinSpacing);

        var chosenGolden = new List<Vector2Int>();
        if (_goldenChestPrefab != null && goldenCandidatesList.Count > 0)
            chosenGolden = PickChestPositionsWithSpacing(goldenCandidatesList, _goldenChestCount, minSpacing, null);
        foreach (var cell in chosenGolden)
        {
            var obj = SpawnObject(_goldenChestPrefab, cell);
            EnsureVisibilityByViewStage(obj);
            var openable = GetOrAddChestOpenable(obj, cell);
            if (openable != null) _lastPlacedChests.Add((cell, openable));
            floor.Remove(cell);
        }

        var chosenNormal = PickChestPositionsWithSpacing(validChestCells, _treasureChestCount, minSpacing, chosenGolden);
        if (_treasureChestPrefab != null)
        {
            foreach (var cell in chosenNormal)
            {
                var obj = SpawnObject(_treasureChestPrefab, cell);
                EnsureVisibilityByViewStage(obj);
                var openable = GetOrAddChestOpenable(obj, cell);
                if (openable != null) _lastPlacedChests.Add((cell, openable));
                floor.Remove(cell);
            }
        }

        float seedOffset = (seed % 10000) * 0.01f;

        foreach (var cell in new List<Vector2Int>(floor))
        {
            if (cell == start || cell == exit) continue;
            if (Mathf.Abs(cell.x - start.x) <= 1 && Mathf.Abs(cell.y - start.y) <= 1) continue;
            if (Mathf.Abs(cell.x - exit.x) <= 1 && Mathf.Abs(cell.y - exit.y) <= 1) continue;
            if (pathCells != null && pathCells.Contains(cell)) continue;

            float nx = cell.x * _perlinScale + seedOffset;
            float ny = cell.y * _perlinScale + seedOffset;
            float obstacleNoise = Mathf.PerlinNoise(nx, ny);

            if (_obstaclePrefab != null && obstacleNoise >= _obstacleThreshold)
            {
                var obj = SpawnObject(_obstaclePrefab, cell);
                EnsureVisibilityByViewStage(obj);
                floor.Remove(cell);
            }
        }

        // -------------------------------
        // 중요 복도에만 문 배치
        // - 출구로 가는 최단 경로
        // - 각 황금 상자까지의 최단 경로
        // 위 경로에서 "양쪽이 복도인" 좁은 복도 셀만 문 후보로 사용합니다.
        // -------------------------------
        if (_doorPrefab != null && _doorCount > 0)
        {
            var doorCandidates = new HashSet<Vector2Int>();

            // 1) 시작 → 출구 최단 경로
            var exitShortest = GetCellsOnShortestPath(floorForPaths, start, exit);
            if (exitShortest != null)
            {
                foreach (var c in exitShortest)
                {
                    if (c == start || c == exit) continue;
                    if (Mathf.Abs(c.x - start.x) <= 1 && Mathf.Abs(c.y - start.y) <= 1) continue;
                    if (Mathf.Abs(c.x - exit.x) <= 1 && Mathf.Abs(c.y - exit.y) <= 1) continue;
                    // 양쪽이 복도(인접 floor 2개)인 좁은 복도이면서, 좌우 또는 상하에 벽이 끼인 "문틀" 위치만 후보로 사용
                    if (CountFloorNeighbors(floorForPaths, c) == 2 && IsGoodDoorCell(floorForPaths, c))
                        doorCandidates.Add(c);
                }
            }

            // 2) 각 황금 상자까지의 최단 경로
            if (chosenGolden != null)
            {
                foreach (var gold in chosenGolden)
                {
                    var toGold = GetCellsOnShortestPath(floorForPaths, start, gold);
                    if (toGold == null) continue;
                    foreach (var c in toGold)
                    {
                        if (c == start || c == exit || c == gold) continue;
                        if (Mathf.Abs(c.x - start.x) <= 1 && Mathf.Abs(c.y - start.y) <= 1) continue;
                        if (Mathf.Abs(c.x - exit.x) <= 1 && Mathf.Abs(c.y - exit.y) <= 1) continue;
                        if (Mathf.Abs(c.x - gold.x) <= 1 && Mathf.Abs(c.y - gold.y) <= 1) continue;
                        if (CountFloorNeighbors(floorForPaths, c) == 2 && IsGoodDoorCell(floorForPaths, c))
                            doorCandidates.Add(c);
                    }
                }
            }

            if (doorCandidates.Count > 0)
            {
                int doorSpacing = Mathf.Max(1, _doorMinSpacing);
                var doorList = new List<Vector2Int>(doorCandidates);
                var doorCells = PickChestPositionsWithSpacing(doorList, _doorCount, doorSpacing, null);
                foreach (var cell in doorCells)
                {
                    // 문은 일단 "닫힌 문 = 벽"처럼 취급: floor에서 제거해서 탐험 AI에겐 비이동 셀로 보이게 함
                    var obj = SpawnObject(_doorPrefab, cell);
                    EnsureVisibilityByViewStage(obj);
                    floor.Remove(cell);
                }
            }
        }
    }

    /// <summary>입구~출구 최장 경로(4방향, 단순 경로) 상의 셀 집합을 반환합니다. DFS 백트래킹 + 반복 한도로 근사합니다. 경로가 없으면 null.</summary>
    private static HashSet<Vector2Int> GetCellsOnLongestPath(HashSet<Vector2Int> floor, Vector2Int start, Vector2Int exit)
    {
        if (floor == null || !floor.Contains(start) || !floor.Contains(exit)) return null;

        List<Vector2Int> bestPath = null;
        int bestLen = -1;
        const int maxIterations = 300000;
        int iterations = 0;

        var path = new List<Vector2Int> { start };
        var pathSet = new HashSet<Vector2Int> { start };

        void Dfs(Vector2Int cur)
        {
            if (iterations++ >= maxIterations) return;
            if (cur == exit)
            {
                if (path.Count > bestLen)
                {
                    bestLen = path.Count;
                    bestPath = new List<Vector2Int>(path);
                }
                return;
            }

            foreach (var dir in FourDirs)
            {
                var next = cur + dir;
                if (!floor.Contains(next) || pathSet.Contains(next)) continue;
                path.Add(next);
                pathSet.Add(next);
                Dfs(next);
                pathSet.Remove(next);
                path.RemoveAt(path.Count - 1);
            }
        }

        Dfs(start);

        if (bestPath == null) return null;
        var set = new HashSet<Vector2Int>(bestPath);
        return set;
    }

    /// <summary>입구~출구 최단 경로(4방향) 상에 있는 셀 집합을 반환합니다. 경로가 없으면 null.</summary>
    private static HashSet<Vector2Int> GetCellsOnShortestPath(HashSet<Vector2Int> floor, Vector2Int start, Vector2Int exit)
    {
        if (floor == null || !floor.Contains(start) || !floor.Contains(exit)) return null;

        var distFromStart = BFSDistances(floor, start);
        var distFromExit = BFSDistances(floor, exit);

        if (!distFromStart.TryGetValue(exit, out int pathLen) || pathLen < 0) return null;

        var pathCells = new HashSet<Vector2Int>();
        foreach (var c in floor)
        {
            if (distFromStart.TryGetValue(c, out int ds) && distFromExit.TryGetValue(c, out int de)
                && ds + de == pathLen)
                pathCells.Add(c);
        }
        return pathCells;
    }

    private static readonly Vector2Int[] FourDirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    private static Dictionary<Vector2Int, int> BFSDistances(HashSet<Vector2Int> floor, Vector2Int from)
    {
        var dist = new Dictionary<Vector2Int, int> { [from] = 0 };
        var q = new Queue<Vector2Int>();
        q.Enqueue(from);

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            int d = dist[cur];
            foreach (var dir in FourDirs)
            {
                var next = cur + dir;
                if (floor.Contains(next) && !dist.ContainsKey(next))
                {
                    dist[next] = d + 1;
                    q.Enqueue(next);
                }
            }
        }
        return dist;
    }

    /// <summary>여러 출발 셀(pathCells)에서 BFS해 각 floor 셀까지의 최소 거리를 반환합니다.</summary>
    private static Dictionary<Vector2Int, int> MultiSourceBFS(HashSet<Vector2Int> floor, HashSet<Vector2Int> pathCells)
    {
        var dist = new Dictionary<Vector2Int, int>();
        var q = new Queue<Vector2Int>();
        foreach (var p in pathCells)
        {
            if (floor.Contains(p))
            {
                dist[p] = 0;
                q.Enqueue(p);
            }
        }
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            int d = dist[cur];
            foreach (var dir in FourDirs)
            {
                var next = cur + dir;
                if (floor.Contains(next) && !dist.ContainsKey(next))
                {
                    dist[next] = d + 1;
                    q.Enqueue(next);
                }
            }
        }
        return dist;
    }

    /// <summary>4방향 인접 셀 중 floor에 포함된 개수를 반환합니다. 막다른 길은 1 이하.</summary>
    private static int CountFloorNeighbors(HashSet<Vector2Int> floor, Vector2Int cell)
    {
        int n = 0;
        foreach (var dir in FourDirs)
            if (floor.Contains(cell + dir)) n++;
        return n;
    }

    /// <summary>
    /// 문 배치용 "좋은" 복도 셀인지 여부.
    /// - 좌우가 모두 floor이고, 위/아래는 floor가 아닌 경우 (가로 복도, 위·아래가 벽)
    /// - 위/아래가 모두 floor이고, 좌/우는 floor가 아닌 경우 (세로 복도, 좌·우가 벽)
    /// 이런 셀은 룸/복도 사이에 끼인 전형적인 문 위치가 됩니다.
    /// </summary>
    private static bool IsGoodDoorCell(HashSet<Vector2Int> floor, Vector2Int cell)
    {
        bool left = floor.Contains(cell + Vector2Int.left);
        bool right = floor.Contains(cell + Vector2Int.right);
        bool up = floor.Contains(cell + Vector2Int.up);
        bool down = floor.Contains(cell + Vector2Int.down);

        // 가로 복도: 좌우가 길, 위/아래는 벽
        bool horizontalDoor = left && right && !up && !down;
        // 세로 복도: 위/아래가 길, 좌/우는 벽
        bool verticalDoor = up && down && !left && !right;

        return horizontalDoor || verticalDoor;
    }

    /// <summary>후보 셀에서 최대 count개를 골라, 서로 및 excludePositions와 최소 minSpacing(맨해튼) 이상 떨어지게 합니다.</summary>
    private static List<Vector2Int> PickChestPositionsWithSpacing(
        List<Vector2Int> candidates, int count, int minSpacing, List<Vector2Int> excludePositions)
    {
        var chosen = new List<Vector2Int>();
        var pool = new List<Vector2Int>(candidates);
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            var valid = new List<Vector2Int>();
            foreach (var c in pool)
            {
                bool ok = true;
                foreach (var p in chosen)
                    if (Manhattan(c, p) < minSpacing) { ok = false; break; }
                if (ok && excludePositions != null)
                    foreach (var p in excludePositions)
                        if (Manhattan(c, p) < minSpacing) { ok = false; break; }
                if (ok) valid.Add(c);
            }
            if (valid.Count == 0) break;
            int idx = UnityEngine.Random.Range(0, valid.Count);
            var pick = valid[idx];
            chosen.Add(pick);
            pool.Remove(pick);
        }
        return chosen;
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
        => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    private GameObject SpawnObject(GameObject prefab, Vector2Int position)
    {
        var worldPos = floorTilemap.CellToWorld((Vector3Int)position) + floorTilemap.cellSize * 0.5f;
        var spawned = Instantiate(prefab, worldPos, Quaternion.identity);
        spawnedObjects.Add(spawned);
        return spawned;
    }

    private static void EnsureVisibilityByViewStage(GameObject obj)
    {
        if (obj == null) return;
        if (obj.GetComponent<VisibilityByViewStage>() == null)
            obj.AddComponent<VisibilityByViewStage>();
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
        _lastPlacedChests.Clear();
    }

    /// <summary>마지막 PlacePerlinObstaclesAndTreasures 호출 시 배치한 (셀, ChestOpenable) 목록. MapManager 등록용.</summary>
    public List<(Vector2Int cell, ChestOpenable openable)> GetLastPlacedChests()
    {
        return new List<(Vector2Int, ChestOpenable)>(_lastPlacedChests);
    }

    private static ChestOpenable GetOrAddChestOpenable(GameObject obj, Vector2Int cell)
    {
        if (obj == null) return null;
        var openable = obj.GetComponent<ChestOpenable>();
        if (openable == null) openable = obj.AddComponent<ChestOpenable>();
        openable.SetChestCell(cell);
        return openable;
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
