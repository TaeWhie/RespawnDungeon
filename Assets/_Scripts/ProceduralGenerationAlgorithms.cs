using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public static class ProceduralGenerationAlgorithms
{
    
    public static HashSet<Vector2Int> SimpleRandomWalk(Vector2Int startPosition, int walkLength)
    {
        HashSet<Vector2Int> path = new HashSet<Vector2Int>();

        path.Add(startPosition);
        var previousPosition = startPosition;

        for (int i = 0; i < walkLength; i++)
        {
            var newPosition = previousPosition + Direction2D.GetRandomCardinalDirection();
            path.Add(newPosition);
            previousPosition = newPosition;
        }
        return path;
    }

    public static List<Vector2Int> RandomWalkCorridor(Vector2Int startPosition, int corridorLength)
    {
        List<Vector2Int> corridor = new List<Vector2Int>();
        var direction = Direction2D.GetRandomCardinalDirection();
        var currentPosition = startPosition;
        corridor.Add(currentPosition);

        for (int i = 0; i < corridorLength; i++)
        {
            currentPosition += direction;
            corridor.Add(currentPosition);
        }
        return corridor;
    }

    public static List<BoundsInt> BinarySpacePartitioning(BoundsInt spaceToSplit, int minWidth, int minHeight)
    {
        Queue<BoundsInt> roomsQueue = new Queue<BoundsInt>();
        List<BoundsInt> roomsList = new List<BoundsInt>();
        roomsQueue.Enqueue(spaceToSplit);
        while(roomsQueue.Count > 0)
        {
            var room = roomsQueue.Dequeue();
            if(room.size.y >= minHeight && room.size.x >= minWidth)
            {
                if(Random.value < 0.5f)
                {
                    if(room.size.y >= minHeight * 2)
                    {
                        SplitHorizontally(minHeight, roomsQueue, room);
                    }else if(room.size.x >= minWidth * 2)
                    {
                        SplitVertically(minWidth, roomsQueue, room);
                    }else if(room.size.x >= minWidth && room.size.y >= minHeight)
                    {
                        roomsList.Add(room);
                    }
                }
                else
                {
                    if (room.size.x >= minWidth * 2)
                    {
                        SplitVertically(minWidth, roomsQueue, room);
                    }
                    else if (room.size.y >= minHeight * 2)
                    {
                        SplitHorizontally(minHeight, roomsQueue, room);
                    }
                    else if (room.size.x >= minWidth && room.size.y >= minHeight)
                    {
                        roomsList.Add(room);
                    }
                }
            }
        }
        return roomsList;
    }

    private static void SplitVertically(int minWidth, Queue<BoundsInt> roomsQueue, BoundsInt room)
    {
        var xSplit = Random.Range(1, room.size.x);
        BoundsInt room1 = new BoundsInt(room.min, new Vector3Int(xSplit, room.size.y, room.size.z));
        BoundsInt room2 = new BoundsInt(new Vector3Int(room.min.x + xSplit, room.min.y, room.min.z),
            new Vector3Int(room.size.x - xSplit, room.size.y, room.size.z));
        roomsQueue.Enqueue(room1);
        roomsQueue.Enqueue(room2);
    }

    private static void SplitHorizontally(int minHeight, Queue<BoundsInt> roomsQueue, BoundsInt room)
    {
        var ySplit = Random.Range(1, room.size.y);
        BoundsInt room1 = new BoundsInt(room.min, new Vector3Int(room.size.x, ySplit, room.size.z));
        BoundsInt room2 = new BoundsInt(new Vector3Int(room.min.x, room.min.y + ySplit, room.min.z),
            new Vector3Int(room.size.x, room.size.y - ySplit, room.size.z));
        roomsQueue.Enqueue(room1);
        roomsQueue.Enqueue(room2);
    }

    public static (Vector2Int start, Vector2Int end) GetLongestPathPoints(HashSet<Vector2Int> floorPositions, IEnumerable<Vector2Int> roomCenters)
    {
        if (floorPositions.Count == 0 || !roomCenters.Any()) return (Vector2Int.zero, Vector2Int.zero);

        Vector2Int bestStart = Vector2Int.zero;
        Vector2Int bestEnd = Vector2Int.zero;
        int maxDistance = -1;

        // 모든 방 센터들 사이의 실제 이동 거리를 BFS로 계산하여 가장 먼 쌍을 찾음
        var centers = roomCenters.ToList();
        for (int i = 0; i < centers.Count; i++)
        {
            var distances = GetAllDistancesFrom(centers[i], floorPositions);
            for (int j = i + 1; j < centers.Count; j++)
            {
                if (distances.TryGetValue(centers[j], out int dist))
                {
                    if (dist > maxDistance)
                    {
                        maxDistance = dist;
                        bestStart = centers[i];
                        bestEnd = centers[j];
                    }
                }
            }
        }

        return (bestStart, bestEnd);
    }

    private static Dictionary<Vector2Int, int> GetAllDistancesFrom(Vector2Int start, HashSet<Vector2Int> floorPositions)
    {
        Dictionary<Vector2Int, int> distances = new Dictionary<Vector2Int, int>();
        Queue<(Vector2Int pos, int dist)> queue = new Queue<(Vector2Int pos, int dist)>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        queue.Enqueue((start, 0));
        visited.Add(start);
        distances[start] = 0;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var direction in Direction2D.cardinalDirectionsList)
            {
                var next = current.pos + direction;
                if (floorPositions.Contains(next) && !visited.Contains(next))
                {
                    visited.Add(next);
                    int nextDist = current.dist + 1;
                    distances[next] = nextDist;
                    queue.Enqueue((next, nextDist));
                }
            }
        }
        return distances;
    }

    public static (Vector2Int start, Vector2Int end) GetLongestPathPoints(HashSet<Vector2Int> floorPositions)
    {
        if (floorPositions.Count == 0) return (Vector2Int.zero, Vector2Int.zero);

        // 아무 지점에서 시작하여 가장 먼 지점 p1을 찾음
        Vector2Int anyPoint = Vector2Int.zero;
        foreach (var pos in floorPositions) { anyPoint = pos; break; }
        
        var (p1, _) = FindFarthestPoint(anyPoint, floorPositions);
        // p1에서 가장 먼 지점 p2를 찾으면 (p1, p2)가 맵 상에서 가장 먼 경로의 양 끝점
        var (p2, _) = FindFarthestPoint(p1, floorPositions);

        return (p1, p2);
    }

    private static (Vector2Int point, int distance) FindFarthestPoint(Vector2Int start, HashSet<Vector2Int> floorPositions)
    {
        Queue<(Vector2Int pos, int dist)> queue = new Queue<(Vector2Int pos, int dist)>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

        queue.Enqueue((start, 0));
        visited.Add(start);

        Vector2Int farthestPoint = start;
        int maxDistance = 0;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (current.dist > maxDistance)
            {
                maxDistance = current.dist;
                farthestPoint = current.pos;
            }

            foreach (var direction in Direction2D.cardinalDirectionsList)
            {
                var next = current.pos + direction;
                if (floorPositions.Contains(next) && !visited.Contains(next))
                {
                    visited.Add(next);
                    queue.Enqueue((next, current.dist + 1));
                }
            }
        }

        return (farthestPoint, maxDistance);
    }
}

public static class Direction2D
{
    public static List<Vector2Int> cardinalDirectionsList = new List<Vector2Int>
    {
        new Vector2Int(0,1), //UP
        new Vector2Int(1,0), //RIGHT
        new Vector2Int(0, -1), // DOWN
        new Vector2Int(-1, 0) //LEFT
    };

    public static List<Vector2Int> diagonalDirectionsList = new List<Vector2Int>
    {
        new Vector2Int(1,1), //UP-RIGHT
        new Vector2Int(1,-1), //RIGHT-DOWN
        new Vector2Int(-1, -1), // DOWN-LEFT
        new Vector2Int(-1, 1) //LEFT-UP
    };

    public static List<Vector2Int> eightDirectionsList = new List<Vector2Int>
    {
        new Vector2Int(0,1), //UP
        new Vector2Int(1,1), //UP-RIGHT
        new Vector2Int(1,0), //RIGHT
        new Vector2Int(1,-1), //RIGHT-DOWN
        new Vector2Int(0, -1), // DOWN
        new Vector2Int(-1, -1), // DOWN-LEFT
        new Vector2Int(-1, 0), //LEFT
        new Vector2Int(-1, 1) //LEFT-UP

    };

    public static Vector2Int GetRandomCardinalDirection()
    {
        return cardinalDirectionsList[UnityEngine.Random.Range(0, cardinalDirectionsList.Count)];
    }
}