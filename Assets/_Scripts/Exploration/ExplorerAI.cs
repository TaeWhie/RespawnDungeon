using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Frontier 기반 지능형 탐험 AI.
/// 시야 반경 내 안개 제거, 인접 미방문 타일 탐험, 막다른 길 시 프론티어 재타겟팅 후 A*로 이동.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class ExplorerAI : MonoBehaviour
{
    /// <summary>상태 머신: 대기 / 로컬 탐험 / 글로벌 목표로 이동</summary>
    public enum State
    {
        Idle,
        Exploring,
        Navigating
    }

    [Header("참조")]
    [Tooltip("비워두면 씬에서 자동 검색")]
    [SerializeField] private MapManager _mapManager;
    [Tooltip("비워두면 씬에서 자동 검색")]
    [SerializeField] private Pathfinder _pathfinder;
    [Tooltip("비워두면 Tag 'Exit' 오브젝트를 씬에서 검색")]
    [SerializeField] private Transform _exitTransform;
    [Tooltip("걷기/뛰기 애니메이션용. 비워두면 같은 오브젝트의 Animator 사용")]
    [SerializeField] private Animator _animator;

    [Header("탐험 설정")]
    [Tooltip("현재 위치 기준 시야 반경(타일 수). 이 범위 내 타일을 즉시 visited 처리")]
    [SerializeField] private int _viewRadius = 3;
    [Tooltip("2단계 시야 반경(구조만 보임). 이 범위까지 벽/바닥이 보이고, _viewRadius 안에서만 장애물 등 전부 보임")]
    [SerializeField] private int _viewRadiusStructure = 6;
    [Tooltip("이동 속도")]
    [SerializeField] private float _speed = 2f;
    [Tooltip("웨이포인트 도착 판정 반경")]
    [SerializeField] private float _waypointReachRadius = 0.35f;
    [Tooltip("인접 탐색 시 여러 방향이 있을 때: true=시야를 가장 많이 밝힐 수 있는 방향 우선, 동점일 때 랜덤 / false=동점일 때만 첫 번째 우선")]
    [SerializeField] private bool _randomizeLocalChoice = true;
    [Tooltip("모험심 (0~1). 0=2단계 시야 가장자리만 확장(안전), 1=미탐험(3단계) 방향도 자주 선택. 2단계로 갈 곳이 없으면 어쩔 수 없이 3단계로 감")]
    [Range(0f, 1f)]
    [SerializeField] private float _adventurousness = 0f;
    [Tooltip("재타겟팅 시 A*를 돌릴 프론티어 후보 수 상한. 낮을수록 프레임 유리")]
    [SerializeField] private int _maxFrontierCandidatesForPath = 5;
    [Tooltip("경로 복귀(뛰기) 시 속도 배율. 탐험(걷기)=1배, 복귀=이 배율")]
    [SerializeField] private float _runSpeedMultiplier = 1.5f;

    [Header("출구")]
    [Tooltip("출구 셀에 도착했을 때 호출 (다음 씬 로드 등)")]
    [SerializeField] private UnityEvent _onReachedExit;

    private Rigidbody2D _rigidbody;
    private State _state = State.Idle;
    private List<Vector2Int> _currentPath = new List<Vector2Int>();
    private int _pathIndex;
    private Vector2Int? _globalTargetCell;
    private bool _isReTargeting;
    /// <summary>현재 이동 목표 셀 (Exploring 시 다음 셀, 애니메이션용)</summary>
    private Vector2Int? _currentTargetCell;
    /// <summary>Update에서 계산한 속도. FixedUpdate에서 실제 적용 (물리 연산과 동기화)</summary>
    private Vector2 _desiredVelocity;

    public State CurrentState => _state;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _rigidbody.gravityScale = 0f;
        _rigidbody.bodyType = RigidbodyType2D.Dynamic;
        _rigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Start()
    {
        if (_mapManager == null) _mapManager = FindFirstObjectByType<MapManager>();
        if (_pathfinder == null) _pathfinder = FindFirstObjectByType<Pathfinder>();
        if (_exitTransform == null)
        {
            var exitGo = GameObject.FindWithTag("Exit");
            if (exitGo != null) _exitTransform = exitGo.transform;
        }
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        _rigidbody.bodyType = RigidbodyType2D.Dynamic;
        _rigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    private void FixedUpdate()
    {
        _rigidbody.linearVelocity = _desiredVelocity;
    }

    private void Update()
    {
        _desiredVelocity = Vector2.zero;

        if (_mapManager == null || _pathfinder == null || !_mapManager.IsInitialized)
            return;

        Vector2Int myCell = _mapManager.WorldToCell(transform.position);

        if (!_mapManager.IsWalkable(myCell))
            return;

        // 시야 반경 업데이트: 현재 위치 중심으로 반경 내 타일 방문 처리 (모든 모드에서 수행)
        _mapManager.MarkVisitedInRadius(myCell, _viewRadius, _viewRadiusStructure);

        // 출구가 보이면 탐색 중단 후 출구로 이동 (리더가 보든 동료가 보든 파티 중 누군가 발견하면 출구로 감)
        Vector2Int? exitCell = GetExitCell();
        if (exitCell.HasValue && (IsExitVisible(myCell, exitCell.Value) || AnyPartyMemberSeesExit(exitCell.Value)) && _mapManager.IsWalkable(exitCell.Value))
        {
            bool alreadyGoingToExit = _state == State.Navigating && _globalTargetCell == exitCell.Value;
            if (!alreadyGoingToExit)
            {
                var pathToExit = _pathfinder.GetPath(_mapManager, myCell, exitCell.Value);
                if (pathToExit != null && pathToExit.Count > 0)
                {
                    _globalTargetCell = exitCell.Value;
                    _mapManager.SetGlobalTarget(exitCell);
                    _currentPath = pathToExit;
                    _pathIndex = 0;
                    _state = State.Navigating;
                    _currentTargetCell = null;
                }
            }
        }

        switch (_state)
        {
            case State.Idle:
                _state = State.Exploring;
                break;

            case State.Exploring:
                UpdateExploring(myCell);
                break;

            case State.Navigating:
                UpdateNavigating(myCell);
                break;
        }

        UpdateMovementAnimation(myCell);
    }

    /// <summary>시야 거리: 셀 간 체비쇼프 거리 (시야가 정사각형이므로)</summary>
    private static int CellDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
    }

    /// <summary>목표까지 거리만으로 걷기/뛰기. 시야 밖이면 뛰기. 경계에서 Run 한 프레임 방지: Run은 distance > viewRadius+1 일 때만.</summary>
    private void UpdateMovementAnimation(Vector2Int myCell)
    {
        bool hasVelocity = _desiredVelocity.sqrMagnitude >= 0.01f;
        Vector2Int? targetCell = _state == State.Navigating ? _globalTargetCell : _currentTargetCell;
        int distance = targetCell.HasValue ? CellDistance(myCell, targetCell.Value) : 0;
        int runThreshold = _viewRadius + 1;
        bool useRun = hasVelocity && distance > runThreshold;
        bool useWalk = hasVelocity && distance <= runThreshold;
        if (_animator != null)
        {
            _animator.SetBool("Idle", !hasVelocity);
            _animator.SetBool("Walk", useWalk);
            _animator.SetBool("Run", useRun);
        }
        // 위/아래 이동 시 velocity.x 미세 요동으로 플립 방지: 가로 성분이 충분할 때만 좌우 반전
        const float flipHorizontalThreshold = 0.25f;
        if (hasVelocity && Mathf.Abs(_desiredVelocity.x) >= flipHorizontalThreshold)
        {
            var scale = transform.localScale;
            if ((_desiredVelocity.x > 0 && scale.x < 0) || (_desiredVelocity.x < 0 && scale.x > 0))
                transform.localScale = new Vector3(-scale.x, scale.y, scale.z);
        }
    }
    private Vector2Int? GetExitCell()
    {
        if (_exitTransform == null || _mapManager == null) return null;
        var c = _mapManager.WorldToCell(_exitTransform.position);
        return _mapManager.IsWalkable(c) ? c : (Vector2Int?)null;
    }

    private bool IsExitVisible(Vector2Int myCell, Vector2Int exitCell)
    {
        int dx = Mathf.Abs(exitCell.x - myCell.x);
        int dy = Mathf.Abs(exitCell.y - myCell.y);
        return dx <= _viewRadius && dy <= _viewRadius && _mapManager.HasLineOfSight(myCell, exitCell);
    }

    /// <summary>동료(Ally) 중 한 명이라도 출구를 시야 내에서 보면 true. 파티 공통 출구 발견 판정용.</summary>
    private bool AnyPartyMemberSeesExit(Vector2Int exitCell)
    {
        var allies = GameObject.FindGameObjectsWithTag("Ally");
        foreach (var go in allies)
        {
            if (go == null) continue;
            Vector2Int allyCell = _mapManager.WorldToCell(go.transform.position);
            if (!_mapManager.IsWalkable(allyCell)) continue;
            if (IsExitVisible(allyCell, exitCell))
                return true;
        }
        return false;
    }

    /// <summary>로컬 탐험: 인접 미방문 타일이 있으면 그쪽으로, 없으면 재타겟팅 코루틴 시작</summary>
    private void UpdateExploring(Vector2Int myCell)
    {
        var unvisitedNeighbors = _mapManager.GetUnvisitedNeighbors(myCell);

        if (unvisitedNeighbors.Count > 0)
        {
            Vector2Int nextCell = ChooseBestExplorationDirection(unvisitedNeighbors);

            Vector3 targetWorld = _mapManager.CellToWorld(nextCell);
            Vector2 toTarget = (Vector2)(targetWorld - transform.position);

            // 도착 직전이면 다음 셀로 바로 전환 → 멈췄다 다시 가는 끊김 방지
            float r2 = _waypointReachRadius * _waypointReachRadius;
            if (toTarget.sqrMagnitude <= r2)
            {
                var nextUnvisited = _mapManager.GetUnvisitedNeighbors(nextCell);
                if (nextUnvisited.Count > 0)
                {
                    nextCell = ChooseBestExplorationDirection(nextUnvisited);
                    targetWorld = _mapManager.CellToWorld(nextCell);
                    toTarget = (Vector2)(targetWorld - transform.position);
                }
            }

            if (toTarget.sqrMagnitude >= 0.0001f)
            {
                _currentTargetCell = nextCell;
                _desiredVelocity = toTarget.normalized * _speed;
            }
            return;
        }

        // 막다른 길: 재타겟팅 중에는 목표 없음
        _currentTargetCell = null;
        // 막다른 길: 글로벌 재타겟팅은 코루틴으로 한 번만 실행 (성능 최적화)
        if (!_isReTargeting)
            StartCoroutine(ReTargetingCoroutine(myCell));
    }

    /// <summary>기본은 2단계 시야 가장자리만 선택. 모험심에 비례해 3단계(미탐험) 방향을 택함. 2단계로 갈 곳이 없을 때만 예외로 3단계 진입.</summary>
    private Vector2Int ChooseBestExplorationDirection(List<Vector2Int> candidates)
    {
        if (candidates == null || candidates.Count == 0) return default;
        if (candidates.Count == 1) return candidates[0];

        int maxVisitedAdj = -1;
        foreach (var cell in candidates)
        {
            int n = _mapManager.GetVisitedNeighborCount(cell);
            if (n > maxVisitedAdj) maxVisitedAdj = n;
        }

        var stage2Candidates = new List<Vector2Int>(candidates.Count); // 2단계 가장자리 = 방문 인접 많은 셀
        var stage3Candidates = new List<Vector2Int>(candidates.Count); // 3단계 = 방문 인접 적은 셀 (미탐험 쪽)
        foreach (var cell in candidates)
        {
            int n = _mapManager.GetVisitedNeighborCount(cell);
            if (n == maxVisitedAdj)
                stage2Candidates.Add(cell);
            else
                stage3Candidates.Add(cell);
        }

        // 기본: 2단계 가장자리만. 모험심에 비례해 3단계 방향 선택
        List<Vector2Int> pool;
        if (stage3Candidates.Count > 0 && Random.value < _adventurousness)
            pool = stage3Candidates; // 모험심: 3단계(미탐험) 방향
        else
            pool = stage2Candidates; // 기본: 2단계 가장자리

        if (pool.Count == 0)
            pool = candidates;
        return PickFromCandidatesWithTieBreak(pool);
    }

    private Vector2Int PickFromCandidatesWithTieBreak(List<Vector2Int> pool)
    {
        if (pool == null || pool.Count == 0) return default;
        if (pool.Count == 1) return pool[0];

        int bestCount = -1;
        var best = new List<Vector2Int>(pool.Count);
        foreach (var cell in pool)
        {
            int unvisitedInView = _mapManager.GetUnvisitedCountInRadius(cell, _viewRadius);
            if (unvisitedInView > bestCount)
            {
                bestCount = unvisitedInView;
                best.Clear();
                best.Add(cell);
            }
            else if (unvisitedInView == bestCount)
            {
                best.Add(cell);
            }
        }
        return best.Count == 1 ? best[0] : (_randomizeLocalChoice ? best[Random.Range(0, best.Count)] : best[0]);
    }

    /// <summary>목표를 잃었거나 막다른 길에 도달했을 때만 실행. 프론티어 검색 → 최근접 목표 선정 → 도달 가능 검증</summary>
    private IEnumerator ReTargetingCoroutine(Vector2Int fromCell)
    {
        _isReTargeting = true;

        var frontier = _mapManager.GetFrontierCells();
        if (frontier.Count == 0)
        {
            _isReTargeting = false;
            yield break;
        }

        // 먼저 맨해튼 거리로 정렬해 가까운 후보만 남김 (A* 호출 횟수 제한 → 프레임 보호)
        int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        frontier.Sort((a, b) => Manhattan(fromCell, a).CompareTo(Manhattan(fromCell, b)));
        int maxCheck = Mathf.Min(_maxFrontierCandidatesForPath, frontier.Count);

        var reachableOptions = new List<(List<Vector2Int> path, Vector2Int cell)>();

        for (int idx = 0; idx < maxCheck; idx++)
        {
            var cell = frontier[idx];
            if (_mapManager.IsUnreachable(cell)) continue;
            var path = _pathfinder.GetPath(_mapManager, fromCell, cell);
            if (path == null || path.Count == 0)
            {
                _mapManager.MarkUnreachable(cell);
                continue;
            }
            reachableOptions.Add((path, cell));
            if (idx < maxCheck - 1)
                yield return null;
        }

        List<Vector2Int> chosenPath = null;
        Vector2Int? chosenTarget = null;

        if (reachableOptions.Count > 0)
        {
            // 프론티어도 2단계/3단계 기준: 방문 인접 많은 셀 = 2단계, 적은 셀 = 3단계
            int maxVisitedAdj = -1;
            foreach (var (_, cell) in reachableOptions)
            {
                int n = _mapManager.GetVisitedNeighborCount(cell);
                if (n > maxVisitedAdj) maxVisitedAdj = n;
            }
            var stage2Options = new List<(List<Vector2Int> path, Vector2Int cell)>();
            var stage3Options = new List<(List<Vector2Int> path, Vector2Int cell)>();
            foreach (var opt in reachableOptions)
            {
                int n = _mapManager.GetVisitedNeighborCount(opt.cell);
                if (n == maxVisitedAdj)
                    stage2Options.Add(opt);
                else
                    stage3Options.Add(opt);
            }

            List<(List<Vector2Int> path, Vector2Int cell)> pool;
            if (stage3Options.Count > 0 && Random.value < _adventurousness)
                pool = stage3Options; // 모험심: 3단계(방문 인접 적은) 프론티어로 이동
            else
                pool = stage2Options; // 기본: 2단계(가장자리) 프론티어로 이동
            if (pool.Count == 0)
                pool = reachableOptions;

            // 선택된 풀에서 경로 짧은 것 우선, 동점이면 방문 인접 많은 셀 우선
            int minLen = int.MaxValue;
            int bestVisitedAdj = -1;
            foreach (var (path, cell) in pool)
            {
                int visitedAdj = _mapManager.GetVisitedNeighborCount(cell);
                bool better = path.Count < minLen
                    || (path.Count == minLen && visitedAdj > bestVisitedAdj);
                if (better)
                {
                    minLen = path.Count;
                    bestVisitedAdj = visitedAdj;
                    chosenPath = path;
                    chosenTarget = cell;
                }
            }
        }

        if (chosenPath != null && chosenTarget.HasValue)
        {
            _globalTargetCell = chosenTarget;
            _mapManager.SetGlobalTarget(chosenTarget);
            _currentPath = chosenPath;
            _pathIndex = 0;
            _state = State.Navigating;
            _currentTargetCell = null;
        }

        _isReTargeting = false;
    }

    /// <summary>인접 4방향 중 이동 가능하고 이미 방문한 타일 목록</summary>
    private List<Vector2Int> GetVisitedNeighbors(Vector2Int cell)
    {
        var list = new List<Vector2Int>(4);
        foreach (var dir in Direction2D.cardinalDirectionsList)
        {
            var next = cell + dir;
            if (_mapManager.IsWalkable(next) && _mapManager.IsVisited(next))
                list.Add(next);
        }
        return list;
    }

    /// <summary>이동 모드: A* 경로를 따라 웨이포인트까지 이동. 도착 시 Exploring으로 복귀</summary>
    private void UpdateNavigating(Vector2Int myCell)
    {
        if (_currentPath == null || _pathIndex >= _currentPath.Count)
        {
            _state = State.Exploring;
            _mapManager.SetGlobalTarget(null);
            _globalTargetCell = null;
            _currentTargetCell = null;
            return;
        }

        Vector2Int waypoint = _currentPath[_pathIndex];
        Vector3 targetWorld = _mapManager.CellToWorld(waypoint);
        Vector2 toTarget = (Vector2)(targetWorld - transform.position);
        float r2 = _waypointReachRadius * _waypointReachRadius;

        // 웨이포인트 도착 시 다음으로 즉시 전환 (멈췄다 다시 가는 끊김 방지)
        while (toTarget.sqrMagnitude <= r2)
        {
            _pathIndex++;
            if (_pathIndex >= _currentPath.Count)
            {
                // 출구에 도착했으면 이벤트 호출 후 대기, 아니면 탐험 재개
                Vector2Int? exitCell = GetExitCell();
                if (exitCell.HasValue && _globalTargetCell == exitCell.Value)
                {
                    _state = State.Idle;
                    _mapManager.SetGlobalTarget(null);
                    _globalTargetCell = null;
                    _currentTargetCell = null;
                    _onReachedExit?.Invoke();
                    return;
                }
                _state = State.Exploring;
                _mapManager.SetGlobalTarget(null);
                _globalTargetCell = null;
                _currentTargetCell = null;
                UpdateExploring(myCell);
                return;
            }
            waypoint = _currentPath[_pathIndex];
            targetWorld = _mapManager.CellToWorld(waypoint);
            toTarget = (Vector2)(targetWorld - transform.position);
        }

        if (toTarget.sqrMagnitude >= 0.0001f)
        {
            int distToGoal = _globalTargetCell.HasValue ? CellDistance(myCell, _globalTargetCell.Value) : 0;
            int runThreshold = _viewRadius + 1;
            float moveSpeed = distToGoal > runThreshold ? _speed * _runSpeedMultiplier : _speed;
            _desiredVelocity = toTarget.normalized * moveSpeed;
        }
    }
}
