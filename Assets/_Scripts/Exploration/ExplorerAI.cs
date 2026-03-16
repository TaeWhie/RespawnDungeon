using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TriInspector;
using UniRx;

/// <summary>
/// Frontier 기반 지능형 탐험 AI.
/// 시야 반경 내 안개 제거, 인접 미방문 타일 탐험, 막다른 길 시 프론티어 재타겟팅 후 A*로 이동.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class ExplorerAI : MonoBehaviour
{
    /// <summary>상태 머신: 대기 / 로컬 탐험 / 글로벌 목표로 이동 / 상자 픽 중 / 장애물 부수기 중</summary>
    public enum State
    {
        Idle,
        Exploring,
        Navigating,
        PickingChest,
        PickingObstacle
    }

    [Title("참조")]
    [Tooltip("비워두면 씬에서 자동 검색")]
    [SerializeField] private MapManager _mapManager;
    [Tooltip("비워두면 씬에서 자동 검색")]
    [SerializeField] private Pathfinder _pathfinder;
    [Tooltip("비워두면 Tag 'Exit' 오브젝트를 씬에서 검색")]
    [SerializeField] private Transform _exitTransform;
    [Tooltip("걷기/뛰기 애니메이션용. 비워두면 같은 오브젝트의 Animator 사용")]
    [SerializeField] private Animator _animator;

    [Title("탐험 설정")]
    [Tooltip("현재 위치 기준 시야 반경(타일 수). 이 범위 내 타일을 즉시 visited 처리")]
    [Slider(1, 15)]
    [SerializeField] private int _viewRadius = 4;
    [Tooltip("2단계 시야 반경(구조만 보임). 이 범위까지 벽/바닥이 보이고, _viewRadius 안에서만 장애물 등 전부 보임")]
    [Slider(1, 20)]
    [SerializeField] private int _viewRadiusStructure = 8;
    [Tooltip("이동 속도")]
    [Slider(0.5f, 10f)]
    [SerializeField] private float _speed = 2f;
    [Tooltip("웨이포인트 도착 판정 반경")]
    [Slider(0.1f, 2f)]
    [SerializeField] private float _waypointReachRadius = 0.05f;
    [Tooltip("목적지(마지막 웨이포인트) 도착 판정 반경. 작을수록 셀 중심에 정확히 도달")]
    [Slider(0.01f, 0.5f)]
    [SerializeField] private float _destinationReachRadius = 0.05f;
    [Tooltip("인접 탐색 시 여러 방향이 있을 때: true=시야를 가장 많이 밝힐 수 있는 방향 우선, 동점일 때 랜덤 / false=동점일 때만 첫 번째 우선")]
    [SerializeField] private bool _randomizeLocalChoice = true;
    [Tooltip("모험심 (0~1). 0=2단계 시야 가장자리만 확장(안전), 1=미탐험(3단계) 방향도 자주 선택. 2단계로 갈 곳이 없으면 어쩔 수 없이 3단계로 감")]
    [Slider(0f, 1f)]
    [SerializeField] private float _adventurousness = 0f;
    [Header("점수 가중치 (거리 vs 밝히는 양)")]
    [Tooltip("모험심 0일 때: 밝히는 양(stage2InView) 계수. score = bright*값 - dist*거리")]
    [SerializeField] private float _scoreBrightWeightAdv0 = 1f;
    [Tooltip("모험심 0일 때: 거리 계수. 클수록 가까운 셀 선호")]
    [SerializeField] private float _scoreDistWeightAdv0 = 5f;
    [Tooltip("모험심 1일 때: 밝히는 양(stage2InView) 계수")]
    [SerializeField] private float _scoreBrightWeightAdv1 = 10f;
    [Tooltip("모험심 1일 때: 거리 계수")]
    [SerializeField] private float _scoreDistWeightAdv1 = 1f;
    [Tooltip("모험심 1일 때 재타겟팅에서만: 밝히는 양 전체(2+3단계) 보조 계수. 0이면 stage2만 반영")]
    [SerializeField] private float _scoreTotalBrightWeightAdv1 = 0.5f;
    [Tooltip("재타겟팅 시 A*를 돌릴 프론티어 후보 수 상한. 낮을수록 프레임 유리")]
    [Slider(1, 20)]
    [SerializeField] private int _maxFrontierCandidatesForPath = 5;
    [Tooltip("경로 복귀(뛰기) 시 속도 배율. 탐험(걷기)=1배, 복귀=이 배율")]
    [Slider(1f, 3f)]
    [SerializeField] private float _runSpeedMultiplier = 1.5f;
    [Title("코너 라운딩 이동")]
    [Tooltip("코너에서 직각 꺾임 대신 호(arc) 형태로 회전합니다.")]
    [SerializeField] private bool _useCornerArcSteering = true;
    [Tooltip("이 각도(도) 이상 꺾일 때만 코너 라운딩을 적용합니다.")]
    [Slider(5f, 120f)]
    [SerializeField] private float _cornerArcMinAngle = 20f;
    [Tooltip("코너 진입 전부터 곡선 회전을 시작하는 거리.")]
    [Slider(0.1f, 2f)]
    [SerializeField] private float _cornerArcStartDistance = 0.8f;
    [Tooltip("최대 회전 속도(도/초). 낮을수록 더 큰 곡선.")]
    [Slider(90f, 1080f)]
    [SerializeField] private float _maxTurnRateDeg = 420f;
    [Tooltip("코너 주변 장애물을 피하기 위한 옆 밀어내기 강도.")]
    [Slider(0f, 1f)]
    [SerializeField] private float _cornerObstacleBias = 0.35f;
    [Tooltip("코너 양옆 안전도 샘플링 거리.")]
    [Slider(0.1f, 1f)]
    [SerializeField] private float _cornerProbeDistance = 0.35f;
    [Tooltip("코너에 충분히 가까워지면 곡선 조향을 줄이고 목표점으로 강제 수렴합니다.")]
    [Slider(0.05f, 1f)]
    [SerializeField] private float _arcConvergeDistance = 0.3f;
    [Tooltip("조향 벡터가 목표점 방향을 최소 이 정도 이상 포함하도록 보정합니다. (원형 오비팅 방지)")]
    [Slider(0f, 1f)]
    [SerializeField] private float _minApproachDot = 0.25f;
    [Tooltip("파티(Ally) 캐시 재검색 주기(초). 0이면 매 프레임.")]
    [Slider(0f, 2f)]
    [SerializeField] private float _partyRescanInterval = 0.4f;

    [Title("출구")]
    [Tooltip("출구 셀에 도착했을 때 호출 (다음 씬 로드 등)")]
    [SerializeField] private UnityEvent _onReachedExit;

    [Title("보물상자")]
    [Tooltip("상자 인접 칸에 도착 후 픽 애니메이션 재생 시간(초)")]
    [Slider(0.1f, 5f)]
    [SerializeField] private float _pickDuration = 1f;
    [Tooltip("장애물 부수기 대기 시간(초). 상자보다 짧게 두면 자연스러움")]
    [Slider(0.1f, 2f)]
    [SerializeField] private float _obstacleBreakDuration = 0.35f;

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
    /// <summary>PickingChest 시 픽할 상자 셀</summary>
    private Vector2Int? _pickingChestCell;
    private float _pickingTimer;
    private bool _didTriggerPick;
    /// <summary>같은 자리에서 픽 완료 횟수. 2가 되면 꺼고 Exploring으로</summary>
    private int _pickingCount;
    /// <summary>Navigating 시 목표가 상자 인접 칸일 때 그 상자 셀. 도착 후 PickingChest로 전환할 때 사용</summary>
    private Vector2Int? _navigatingToChestCell;
    /// <summary>PickingObstacle 시 부술 장애물 셀</summary>
    private Vector2Int? _pickingObstacleCell;
    /// <summary>장애물 부수기 후 경로 재개할지 (경로상 장애물이면 true)</summary>
    private bool _resumePathAfterObstacle;
    private Vector2 _steeringDirection;
    private bool _hasSteeringDirection;
    private float _debugTraceTimer;
    private int _stuckWatchWaypointIndex = -1;
    private float _stuckWatchElapsed;
    private float _stuckWatchStartDistance;
    private bool _stuckWarnedForCurrentWaypoint;
    private readonly CompositeDisposable _subscriptions = new CompositeDisposable();
    private const string ExplorerTraceChannel = "EXPLORER_NAV";

    public State CurrentState => _state;

    /// <summary>모험심 (0~1). 디버그 패널 등에서 런타임 조절 가능.</summary>
    public float Adventurousness
    {
        get => _adventurousness;
        set => _adventurousness = Mathf.Clamp01(value);
    }

    /// <summary>점수 가중치. 디버그 패널에서 런타임 조절 가능.</summary>
    public float ScoreBrightWeightAdv0 { get => _scoreBrightWeightAdv0; set => _scoreBrightWeightAdv0 = Mathf.Max(0f, value); }
    public float ScoreDistWeightAdv0 { get => _scoreDistWeightAdv0; set => _scoreDistWeightAdv0 = Mathf.Max(0f, value); }
    public float ScoreBrightWeightAdv1 { get => _scoreBrightWeightAdv1; set => _scoreBrightWeightAdv1 = Mathf.Max(0f, value); }
    public float ScoreDistWeightAdv1 { get => _scoreDistWeightAdv1; set => _scoreDistWeightAdv1 = Mathf.Max(0f, value); }
    public float ScoreTotalBrightWeightAdv1 { get => _scoreTotalBrightWeightAdv1; set => _scoreTotalBrightWeightAdv1 = Mathf.Max(0f, value); }

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

    private void OnEnable()
    {
        _subscriptions.Clear();
        MCPLogHub.BindPeriodicMonitor(
            _partyRescanInterval,
            RefreshPartyCacheFromHub,
            _subscriptions);
    }

    private void OnDisable()
    {
        _subscriptions.Clear();
    }

    private void RefreshPartyCacheFromHub()
    {
        ExplorationPartyCache.RefreshIfStale(_partyRescanInterval);
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

        // 워커블이 아닌 셀(벽 등)에 있으면 스킵. 상자/장애물 셀 위에서는 경로 완료·픽 로직이 동작하도록 허용
        if (!_mapManager.IsPassableForPathfinding(myCell))
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
                    StartNavigatingWithPath(pathToExit, myCell, exitCell, null, "exit_path");
                }
            }
        }

        // 보물상자 픽 중: 이동 없이 픽 애니 재생 후 탐험 재개
        if (_state == State.PickingChest)
        {
            if (!_didTriggerPick && _animator != null)
            {
                _animator.SetBool("Pick", true);
                _didTriggerPick = true;
            }
            _pickingTimer -= Time.deltaTime;
            if (_pickingTimer <= 0f && _pickingChestCell.HasValue)
            {
                _pickingCount++;
                if (_pickingCount >= 2)
                {
                    _mapManager.MarkChestPicked(_pickingChestCell.Value);
                    _pickingChestCell = null;
                    if (_animator != null)
                        _animator.SetBool("Pick", false);
                    _state = State.Exploring;
                    _globalTargetCell = null;
                    _mapManager.SetGlobalTarget(null);
                    _currentPath = null;
                    _pathIndex = 0;
                    _currentTargetCell = null;
                }
                else
                {
                    _pickingTimer = _pickDuration;
                    _didTriggerPick = false;
                    if (_animator != null)
                        _animator.SetBool("Pick", true);
                    _didTriggerPick = true;
                }
            }
            UpdateMovementAnimation(myCell);
            return;
        }

        // 장애물 부수기 중: 잠깐 멈췄다가 Slash 재생 후 부수고 탐험/경로 재개
        if (_state == State.PickingObstacle)
        {
            if (!_didTriggerPick && _pickingObstacleCell.HasValue)
            {
                Vector2Int dir = _pickingObstacleCell.Value - myCell;
                if (dir.x != 0)
                {
                    var scale = transform.localScale;
                    if ((dir.x > 0 && scale.x < 0) || (dir.x < 0 && scale.x > 0))
                        transform.localScale = new Vector3(-scale.x, scale.y, scale.z);
                }
                if (_animator != null)
                    _animator.SetTrigger("Slash");
                _didTriggerPick = true;
            }
            _pickingTimer -= Time.deltaTime;
            if (_pickingTimer <= 0f && _pickingObstacleCell.HasValue)
            {
                _mapManager.MarkObstacleBroken(_pickingObstacleCell.Value);
                _pickingObstacleCell = null;
                if (_animator != null)
                    _animator.ResetTrigger("Slash");
                if (_resumePathAfterObstacle && _currentPath != null && _pathIndex < _currentPath.Count)
                {
                    _resumePathAfterObstacle = false;
                    Vector2Int goal = _globalTargetCell ?? _currentPath[_currentPath.Count - 1];
                    var newPath = _pathfinder.GetPath(_mapManager, myCell, goal);
                    if (newPath != null && newPath.Count > 0)
                    {
                        StartNavigatingWithPath(newPath, myCell, _globalTargetCell ?? goal, _navigatingToChestCell, "resume_after_obstacle");
                    }
                    else
                        _pathIndex++;
                    _state = State.Navigating;
                }
                else
                {
                    _resumePathAfterObstacle = false;
                    _state = State.Exploring;
                    _globalTargetCell = null;
                    _mapManager.SetGlobalTarget(null);
                    _currentPath = null;
                    _pathIndex = 0;
                    _currentTargetCell = null;
                }
                UpdateMovementAnimation(myCell);
                return;
            }
            UpdateMovementAnimation(myCell);
            return;
        }

        // 보물상자 발견:
        bool goingToExit = exitCell.HasValue && _globalTargetCell == exitCell.Value;
        if (!goingToExit)
        {
            var unpicked = _mapManager.GetUnpickedChestCellsInFullView();
            if (unpicked.Count > 0)
            {
                Vector2Int? bestChest = null;
                int bestPathLen = int.MaxValue;
                foreach (var chestCell in unpicked)
                {
                    var path = _pathfinder.GetPath(_mapManager, myCell, chestCell);
                    if (path != null && path.Count > 0 && path.Count < bestPathLen)
                    {
                        bestPathLen = path.Count;
                        bestChest = chestCell;
                    }
                }
                if (bestChest.HasValue)
                {
                    bool alreadyGoingThere = _state == State.Navigating && _globalTargetCell == bestChest.Value;
                    if (!alreadyGoingThere)
                    {
                        var path = _pathfinder.GetPath(_mapManager, myCell, bestChest.Value);
                        if (path != null && path.Count > 0)
                        {
                            StartNavigatingWithPath(path, myCell, bestChest, bestChest, "chest_path");
                        }
                    }
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

            case State.PickingObstacle:
                break;
        }

        UpdateMovementAnimation(myCell);
    }

    private void OnDrawGizmos()
    {
        if (_mapManager == null || !_mapManager.DrawGizmos) return;
        if (_currentPath == null || _currentPath.Count < 2) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < _currentPath.Count - 1; i++)
        {
            var a = _mapManager.CellToWorld(_currentPath[i]);
            var b = _mapManager.CellToWorld(_currentPath[i + 1]);
            Gizmos.DrawLine(a, b);
        }

        DrawCurvedPathGizmos();

        if (_globalTargetCell.HasValue)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(_mapManager.CellToWorld(_globalTargetCell.Value), 0.1f);
        }
    }

    private void DrawCurvedPathGizmos()
    {
        if (!_useCornerArcSteering || _currentPath == null || _currentPath.Count < 3)
            return;

        Gizmos.color = new Color(1f, 0.55f, 0.1f, 1f);
        Vector2 last = _mapManager.CellToWorld(_currentPath[0]);

        for (int i = 1; i < _currentPath.Count - 1; i++)
        {
            Vector2 prev = _mapManager.CellToWorld(_currentPath[i - 1]);
            Vector2 curr = _mapManager.CellToWorld(_currentPath[i]);
            Vector2 next = _mapManager.CellToWorld(_currentPath[i + 1]);

            Vector2 inDir = (curr - prev).normalized;
            Vector2 outDir = (next - curr).normalized;
            float angle = Vector2.Angle(inDir, outDir);
            if (angle < _cornerArcMinAngle)
                continue;

            float inLen = Vector2.Distance(prev, curr);
            float outLen = Vector2.Distance(curr, next);
            float pullback = Mathf.Min(_cornerArcStartDistance * 0.5f, inLen * 0.45f, outLen * 0.45f);
            if (pullback <= 0.01f)
                continue;

            Vector2 entry = curr - inDir * pullback;
            Vector2 exit = curr + outDir * pullback;

            Gizmos.DrawLine(last, entry);
            DrawQuadraticBezierGizmo(entry, curr, exit, 10);
            last = exit;
        }

        Vector2 end = _mapManager.CellToWorld(_currentPath[_currentPath.Count - 1]);
        Gizmos.DrawLine(last, end);
    }

    private static void DrawQuadraticBezierGizmo(Vector2 p0, Vector2 p1, Vector2 p2, int segments)
    {
        Vector2 prev = p0;
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float omt = 1f - t;
            Vector2 p = (omt * omt * p0) + (2f * omt * t * p1) + (t * t * p2);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }

    /// <summary>시야 거리: 셀 간 체비쇼프 거리 (시야가 정사각형이므로)</summary>
    private static int CellDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
    }

    private bool IsChestUnpicked(Vector2Int cell) => _mapManager != null && !_mapManager.IsChestPicked(cell);

    /// <summary>목표까지 거리만으로 걷기/뛰기.</summary>
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

    /// <summary>상자 트리거에 닿았을 때 ChestOpenable에서 호출. 해당 셀 상자 픽을 시작합니다.</summary>
    public void BeginPickChest(Vector2Int cell)
    {
        if (_mapManager == null || _mapManager.IsChestPicked(cell)) return;
        if (_pickingChestCell == cell) return;
        _pickingChestCell = cell;
        _pickingTimer = _pickDuration;
        _didTriggerPick = false;
        _pickingCount = 0;
        _state = State.PickingChest;
        _mapManager.SetGlobalTarget(null);
        _globalTargetCell = null;
        _currentPath = null;
        _pathIndex = 0;
        _navigatingToChestCell = null;
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
        var allies = ExplorationPartyCache.AllyTransforms;
        for (int i = allies.Count - 1; i >= 0; i--)
        {
            var ally = allies[i];
            if (ally == null) continue;
            Vector2Int allyCell = _mapManager.WorldToCell(ally.position);
            if (!_mapManager.IsWalkable(allyCell)) continue;
            if (IsExitVisible(allyCell, exitCell))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 로컬 탐험:
    /// - 인접 타일 중 안개 3단계 셀을 기본 후보로 삼고, 그 안에서 "가장자리(2단계 인접) 성향" / "깊은 3단계 성향"으로 나눈 뒤
    /// - 모험심(adventurousness)에 따라 어느 성향을 따를지 확률적으로 결정해 한 칸 이동합니다.
    /// - 인접 3단계가 없을 때만 예외적으로 2단계 셀을 사용하며, 더 갈 곳이 없으면 재타겟팅 코루틴을 시작합니다.
    /// - 대각선 셀은 벽/장애물에 끼지 않을 때만 후보에 포함(두 인접 cardinal 통과 가능 시).
    /// </summary>
    private void UpdateExploring(Vector2Int myCell)
    {
        var neighbors = new List<Vector2Int>(8);
        foreach (var dir in Direction2D.eightDirectionsList)
        {
            var c = myCell + dir;
            if (!_mapManager.IsWalkable(c)) continue;
            if (dir.x != 0 && dir.y != 0 && !CanMoveDiagonal(myCell, dir)) continue;
            int stage = _mapManager.GetFogStage(c);
            if (stage == 2 || stage == 3)
                neighbors.Add(c);
        }

        if (neighbors.Count > 0)
        {
            Vector2Int nextCell = ChooseBestExplorationDirection(neighbors, myCell);

            Vector3 targetWorld = _mapManager.CellToWorld(nextCell);
            Vector2 toTarget = (Vector2)(targetWorld - transform.position);

            // 도착 직전이면 다음 셀로 바로 전환 → 멈췄다 다시 가는 끊김 방지
            float r2 = _waypointReachRadius * _waypointReachRadius;
            if (toTarget.sqrMagnitude <= r2)
            {
                var nextNeighbors = new List<Vector2Int>(8);
                foreach (var dir in Direction2D.eightDirectionsList)
                {
                    var c = nextCell + dir;
                    if (!_mapManager.IsWalkable(c)) continue;
                    if (dir.x != 0 && dir.y != 0 && !CanMoveDiagonal(nextCell, dir)) continue;
                    int stage = _mapManager.GetFogStage(c);
                    if (stage == 2 || stage == 3)
                        nextNeighbors.Add(c);
                }
                if (nextNeighbors.Count > 0)
                {
                    nextCell = ChooseBestExplorationDirection(nextNeighbors, nextCell);
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

    /// <summary>대각선 이동 시 끼인 두 cardinal 셀이 통과 가능한지 (벽/장애물 코너 관통 방지)</summary>
    private bool CanMoveDiagonal(Vector2Int from, Vector2Int diagDir)
    {
        if (diagDir.x == 0 || diagDir.y == 0) return true;
        return _mapManager.IsWalkable(from + new Vector2Int(diagDir.x, 0))
            && _mapManager.IsWalkable(from + new Vector2Int(0, diagDir.y));
    }

    /// <summary>
    /// 모험심 0: 목적지는 안개 2단계 셀(방문됐지만 완전히 밝혀지지 않은 셀). 그곳으로 가서 시야를 밝힘.
    /// 모험심 1: 목적지는 안개 3단계 셀(2단계 성향+3단계 성향 합친 풀). 가장자리와 깊은 안개 모두 대상.
    /// 판정: 모험심 0 → 거리(가까울수록) 우선, 동점이면 많이 밝히는 순. 모험심 1 → 많이 밝히는 순 우선, 동점이면 거리.
    /// </summary>
    private Vector2Int ChooseBestExplorationDirection(List<Vector2Int> candidates, Vector2Int fromCell)
    {
        if (candidates == null || candidates.Count == 0) return default;
        if (candidates.Count == 1) return candidates[0];

        // 모험심 0: 목적지는 안개 2단계 셀(방문됐지만 아직 완전 시야가 아닌 셀)
        var fog2Candidates = new List<Vector2Int>(candidates.Count);
        var stage2Like = new List<Vector2Int>(candidates.Count); // 3단계 셀 중 가장자리 성향
        var stage3Like = new List<Vector2Int>(candidates.Count);  // 3단계 셀 중 깊은 성향
        var fallback = new List<Vector2Int>(candidates.Count);

        foreach (var cell in candidates)
        {
            int stage = _mapManager.GetFogStage(cell);
            if (stage == 2)
                fog2Candidates.Add(cell);
            if (stage == 3)
            {
                fallback.Add(cell);
                int adjStage2 = 0;
                foreach (var dir in Direction2D.cardinalDirectionsList)
                {
                    var n = cell + dir;
                    if (_mapManager.GetFogStage(n) == 2)
                        adjStage2++;
                }
                if (adjStage2 > 0)
                    stage2Like.Add(cell);
                else
                    stage3Like.Add(cell);
            }
        }

        List<Vector2Int> pool;
        if (Mathf.Approximately(_adventurousness, 0f))
        {
            // 모험심 0: 2단계(아직 완전히 밝혀지지 않은) 셀만 목적지
            pool = fog2Candidates.Count > 0 ? fog2Candidates : fallback;
        }
        else if (fallback.Count > 0)
        {
            if (Mathf.Approximately(_adventurousness, 1f) && (stage2Like.Count > 0 || stage3Like.Count > 0))
            {
                pool = new List<Vector2Int>(stage2Like.Count + stage3Like.Count);
                pool.AddRange(stage2Like);
                pool.AddRange(stage3Like);
            }
            else if (stage3Like.Count > 0 && Random.value < _adventurousness)
                pool = stage3Like;
            else if (stage2Like.Count > 0)
                pool = stage2Like;
            else
                pool = fallback;
        }
        else
        {
            pool = candidates;
        }
        return PickFromCandidatesWithTieBreak(pool, fromCell);
    }

    /// <summary>
    /// 거리와 밝히는 양(stage2InView) 둘 다 반영한 가중 점수로 선택. 거리는 실제 이동 경로 길이(path length) 사용.
    /// score = brightWeight*stage2InView - distWeight*distance (클수록 좋음)
    /// </summary>
    private Vector2Int PickFromCandidatesWithTieBreak(List<Vector2Int> pool, Vector2Int fromCell)
    {
        if (pool == null || pool.Count == 0) return default;
        if (pool.Count == 1) return pool[0];

        float brightWeight = Mathf.Approximately(_adventurousness, 0f) ? _scoreBrightWeightAdv0 : Mathf.Lerp(_scoreBrightWeightAdv0, _scoreBrightWeightAdv1, _adventurousness);
        float distWeight = Mathf.Approximately(_adventurousness, 0f) ? _scoreDistWeightAdv0 : Mathf.Lerp(_scoreDistWeightAdv0, _scoreDistWeightAdv1, _adventurousness);

        float bestScore = float.MinValue;
        var best = new List<Vector2Int>(pool.Count);
        foreach (var cell in pool)
        {
            var path = _pathfinder.GetPath(_mapManager, fromCell, cell);
            int dist = (path != null && path.Count > 0) ? path.Count : int.MaxValue;
            int stage2InView = _mapManager.GetStage2CountInRadius(cell, _viewRadius);
            float score = brightWeight * stage2InView - distWeight * dist;
            if (score > bestScore)
            {
                bestScore = score;
                best.Clear();
                best.Add(cell);
            }
            else if (Mathf.Approximately(score, bestScore))
                best.Add(cell);
        }
        return best.Count == 1 ? best[0] : (_randomizeLocalChoice ? best[Random.Range(0, best.Count)] : best[0]);
    }

    /// <summary>
    /// 목표를 잃었거나 막다른 길에 도달했을 때만 실행.
    /// 모험심 0: 목적지는 안개 2단계 셀(방문됐지만 완전 시야 아닌 셀). 모험심 1: 안개 3단계 셀(2+3 성향 합친 풀).
    /// 그 안에서 가장 많이 밝힐 수 있는 셀(및 그 셀로 가는 가장 짧은 경로)을 선택합니다.
    /// </summary>
    private IEnumerator ReTargetingCoroutine(Vector2Int fromCell)
    {
        _isReTargeting = true;

        int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        int maxCheck = _maxFrontierCandidatesForPath;

        var fog2OnlyOptions = new List<(List<Vector2Int> path, Vector2Int cell)>(); // 모험심 0용: 목적지가 안개 2단계 셀
        var stage2Options = new List<(List<Vector2Int> path, Vector2Int cell)>();    // 3단계 셀 중 2단계 성향
        var stage3Options = new List<(List<Vector2Int> path, Vector2Int cell)>();    // 3단계 셀 중 3단계 성향

        if (_mapManager.IsInitialized)
        {
            void AddReachable(List<Vector2Int> targets, List<(List<Vector2Int> path, Vector2Int cell)> dest)
            {
                int limit = Mathf.Min(maxCheck, targets.Count);
                for (int idx = 0; idx < limit; idx++)
                {
                    var cell = targets[idx];
                    List<Vector2Int> path = null;
                    if (_mapManager.IsWalkable(cell))
                    {
                        path = _pathfinder.GetPath(_mapManager, fromCell, cell);
                        if (path == null || path.Count == 0)
                        {
                            _mapManager.MarkUnreachable(cell);
                            continue;
                        }
                    }
                    else if (_mapManager.IsObstacleCell(cell))
                    {
                        var stands = _mapManager.GetWalkableNeighbors(cell);
                        int bestLen = int.MaxValue;
                        foreach (var stand in stands)
                        {
                            var p = _pathfinder.GetPath(_mapManager, fromCell, stand);
                            if (p != null && p.Count > 0 && p.Count < bestLen)
                            {
                                bestLen = p.Count;
                                path = p;
                            }
                        }
                        if (path == null) continue;
                    }
                    else if (_mapManager.IsChestCell(cell))
                    {
                        path = _pathfinder.GetPath(_mapManager, fromCell, cell);
                        if (path == null || path.Count == 0)
                        {
                            _mapManager.MarkUnreachable(cell);
                            continue;
                        }
                    }
                    else
                        continue;
                    dest.Add((path, cell));
                }
            }

            // 모험심 0: 목적지는 안개 2단계 셀(방문됐지만 완전 시야가 아닌 셀). 워커블 + 장애물 셀 모두 후보.
            if (Mathf.Approximately(_adventurousness, 0f))
            {
                var fog2OnlyTargets = new List<Vector2Int>();
                for (int i = 0; i < _mapManager.Width; i++)
                for (int j = 0; j < _mapManager.Height; j++)
                {
                    var cell = _mapManager.IndexToCell(i, j);
                    if (!_mapManager.IsWalkable(cell) && !_mapManager.IsObstacleCell(cell) && !_mapManager.IsChestCell(cell)) continue;
                    if (_mapManager.IsWalkable(cell) && _mapManager.IsUnreachable(cell)) continue;
                    if (_mapManager.GetFogStage(cell) == 2)
                        fog2OnlyTargets.Add(cell);
                }
                fog2OnlyTargets.Sort((a, b) => Manhattan(fromCell, a).CompareTo(Manhattan(fromCell, b)));
                AddReachable(fog2OnlyTargets, fog2OnlyOptions);
            }
            else
            {
                // 모험심 > 0: 목적지는 안개 3단계 셀. 워커블 + 장애물 셀 모두 후보. 2단계 성향 / 3단계 성향으로 나눔.
                var stage2LikeTargets = new List<Vector2Int>();
                var stage3LikeTargets = new List<Vector2Int>();

                for (int i = 0; i < _mapManager.Width; i++)
                for (int j = 0; j < _mapManager.Height; j++)
                {
                    var cell = _mapManager.IndexToCell(i, j);
                    if (!_mapManager.IsWalkable(cell) && !_mapManager.IsObstacleCell(cell) && !_mapManager.IsChestCell(cell)) continue;
                    if (_mapManager.IsWalkable(cell) && _mapManager.IsUnreachable(cell)) continue;

                    int stage = _mapManager.GetFogStage(cell);
                    if (stage != 3) continue;

                    int adjStage2 = 0;
                    foreach (var dir in Direction2D.cardinalDirectionsList)
                    {
                        var n = cell + dir;
                        if (_mapManager.GetFogStage(n) == 2)
                            adjStage2++;
                    }
                    if (adjStage2 > 0)
                        stage2LikeTargets.Add(cell);
                    else
                        stage3LikeTargets.Add(cell);
                }

                stage2LikeTargets.Sort((a, b) => Manhattan(fromCell, a).CompareTo(Manhattan(fromCell, b)));
                stage3LikeTargets.Sort((a, b) => Manhattan(fromCell, a).CompareTo(Manhattan(fromCell, b)));
                AddReachable(stage2LikeTargets, stage2Options);
                AddReachable(stage3LikeTargets, stage3Options);
            }
        }

        List<Vector2Int> chosenPath = null;
        Vector2Int? chosenTarget = null;

        List<(List<Vector2Int> path, Vector2Int cell)> pool;
        if (Mathf.Approximately(_adventurousness, 0f))
        {
            pool = fog2OnlyOptions;
        }
        else if (stage2Options.Count > 0 || stage3Options.Count > 0)
        {
            if (Mathf.Approximately(_adventurousness, 1f))
            {
                pool = new List<(List<Vector2Int> path, Vector2Int cell)>(stage2Options.Count + stage3Options.Count);
                pool.AddRange(stage2Options);
                pool.AddRange(stage3Options);
            }
            else if (stage3Options.Count > 0 && Random.value < _adventurousness)
                pool = stage3Options;
            else if (stage2Options.Count > 0)
                pool = stage2Options;
            else
                pool = stage3Options;
        }
        else
        {
            pool = new List<(List<Vector2Int> path, Vector2Int cell)>();
        }

        if (pool.Count > 0)
        {
            // 거리와 밝히는 양 둘 다 반영한 가중 점수. 거리는 실제 이동 경로 길이(path.Count) 사용.
            float brightWeight = Mathf.Approximately(_adventurousness, 0f) ? _scoreBrightWeightAdv0 : Mathf.Lerp(_scoreBrightWeightAdv0, _scoreBrightWeightAdv1, _adventurousness);
            float distWeight = Mathf.Approximately(_adventurousness, 0f) ? _scoreDistWeightAdv0 : Mathf.Lerp(_scoreDistWeightAdv0, _scoreDistWeightAdv1, _adventurousness);
            float totalFogWeight = Mathf.Approximately(_adventurousness, 0f) ? 0f : _scoreTotalBrightWeightAdv1 * _adventurousness;

            float bestScore = float.MinValue;
            var bestCells = new List<Vector2Int>();
            foreach (var (path, cell) in pool)
            {
                int pathLen = path != null ? path.Count : int.MaxValue;
                int stage2InView = _mapManager.GetStage2CountInRadius(cell, _viewRadius);
                int totalFogInView = stage2InView + _mapManager.GetUnvisitedCountInRadius(cell, _viewRadius);
                float brightTerm = brightWeight * stage2InView + totalFogWeight * totalFogInView;
                float score = brightTerm - distWeight * pathLen;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCells.Clear();
                    bestCells.Add(cell);
                }
                else if (Mathf.Approximately(score, bestScore))
                    bestCells.Add(cell);
            }

            if (bestCells.Count > 0)
            {
                Vector2Int targetCell = bestCells.Count == 1
                    ? bestCells[0]
                    : (_randomizeLocalChoice ? bestCells[Random.Range(0, bestCells.Count)] : bestCells[0]);

                int minLen = int.MaxValue;
                foreach (var (path, cell) in pool)
                {
                    if (cell != targetCell) continue;
                    if (path.Count < minLen)
                    {
                        minLen = path.Count;
                        chosenPath = path;
                        chosenTarget = cell;
                    }
                }
            }
        }

        if (chosenPath != null && chosenTarget.HasValue)
        {
            StartNavigatingWithPath(chosenPath, fromCell, chosenTarget, null, "retarget_path");
        }
        else
        {
            // 더 이상 갈 탐험 목표가 없으면 목적지를 입구로 설정
            var startCell = _mapManager.GetStartCell();
            if (startCell.HasValue && _mapManager.IsWalkable(startCell.Value) && startCell.Value != fromCell)
            {
                var pathToStart = _pathfinder.GetPath(_mapManager, fromCell, startCell.Value);
                if (pathToStart != null && pathToStart.Count > 0)
                {
                    StartNavigatingWithPath(pathToStart, fromCell, startCell, null, "fallback_to_start");
                }
            }
        }

        _isReTargeting = false;
        yield break;
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
            ResetSteeringDirection();
            if (_globalTargetCell.HasValue && _mapManager.IsObstacleCell(_globalTargetCell.Value))
            {
                _pickingObstacleCell = _globalTargetCell;
                _pickingTimer = _obstacleBreakDuration;
                _didTriggerPick = false;
                _resumePathAfterObstacle = false;
                _state = State.PickingObstacle;
                _mapManager.SetGlobalTarget(null);
                _globalTargetCell = null;
                _currentTargetCell = null;
                _currentPath = null;
                _pathIndex = 0;
                _navigatingToChestCell = null;
                return;
            }
            _state = State.Exploring;
            _mapManager.SetGlobalTarget(null);
            _globalTargetCell = null;
            _currentTargetCell = null;
            _navigatingToChestCell = null;
            return;
        }

        // 경로상 다음 웨이포인트가 장애물이고 인접해 있으면 잠깐 멈췄다가 부수고 경로 재개
        Vector2Int waypoint = _currentPath[_pathIndex];
        if (_mapManager.IsObstacleCell(waypoint) && CellDistance(myCell, waypoint) == 1)
        {
            _pickingObstacleCell = waypoint;
            _pickingTimer = _obstacleBreakDuration;
            _didTriggerPick = false;
            _resumePathAfterObstacle = true;
            _state = State.PickingObstacle;
            return;
        }

        Vector3 targetWorld = _mapManager.CellToWorld(waypoint);
        Vector2 toTarget = (Vector2)(targetWorld - transform.position);
        bool isLastWaypoint = _pathIndex >= _currentPath.Count - 1;
        float reachR = isLastWaypoint ? _destinationReachRadius : _waypointReachRadius;
        float r2 = reachR * reachR;

        // 중간 웨이포인트 근접 시 강제 패스스루(오비팅 방지)
        if (!isLastWaypoint && toTarget.magnitude <= Mathf.Max(0.35f, _arcConvergeDistance * 1.2f))
        {
            _pathIndex++;
            if (_pathIndex < _currentPath.Count)
            {
                waypoint = _currentPath[_pathIndex];
                targetWorld = _mapManager.CellToWorld(waypoint);
                toTarget = (Vector2)(targetWorld - transform.position);
                isLastWaypoint = _pathIndex >= _currentPath.Count - 1;
                reachR = isLastWaypoint ? _destinationReachRadius : _waypointReachRadius;
                r2 = reachR * reachR;
            }
        }

        // 웨이포인트 도착 시 다음으로 즉시 전환. 목적지(마지막)는 작은 반경으로 셀 중심까지 도달.
        bool reachedWaypoint = HasReachedWaypoint(_pathIndex, toTarget, reachR, isLastWaypoint);
        while (reachedWaypoint)
        {
            bool wasLastWaypoint = _pathIndex >= _currentPath.Count - 1;
            _pathIndex++;
            if (_pathIndex >= _currentPath.Count)
            {
                if (wasLastWaypoint)
                {
                    var finalPos = new Vector3(targetWorld.x, targetWorld.y, transform.position.z);
                    transform.position = finalPos;
                    if (_rigidbody != null)
                        _rigidbody.position = new Vector2(finalPos.x, finalPos.y);
                }
                ResetSteeringDirection();
                // 출구에 도착했으면 이벤트 호출 후 대기, 아니면 탐험 재개 또는 상자 픽
                Vector2Int? exitCell = GetExitCell();
                if (exitCell.HasValue && _globalTargetCell == exitCell.Value)
                {
                    _state = State.Idle;
                    _mapManager.SetGlobalTarget(null);
                    _globalTargetCell = null;
                    _currentTargetCell = null;
                    _navigatingToChestCell = null;
                    _onReachedExit?.Invoke();
                    return;
                }
                if (_globalTargetCell.HasValue && _mapManager.IsObstacleCell(_globalTargetCell.Value))
                {
                    _pickingObstacleCell = _globalTargetCell;
                    _pickingTimer = _obstacleBreakDuration;
                    _didTriggerPick = false;
                    _resumePathAfterObstacle = false;
                    _state = State.PickingObstacle;
                    _mapManager.SetGlobalTarget(null);
                    _globalTargetCell = null;
                    _currentTargetCell = null;
                    _currentPath = null;
                    _pathIndex = 0;
                    _navigatingToChestCell = null;
                    return;
                }
                _state = State.Exploring;
                _mapManager.SetGlobalTarget(null);
                _globalTargetCell = null;
                _currentTargetCell = null;
                _navigatingToChestCell = null;
                UpdateExploring(myCell);
                return;
            }
            waypoint = _currentPath[_pathIndex];
            targetWorld = _mapManager.CellToWorld(waypoint);
            // 중간 웨이포인트에서는 스냅하지 않아 곡선 궤적을 유지.
            toTarget = (Vector2)(targetWorld - transform.position);
            isLastWaypoint = _pathIndex >= _currentPath.Count - 1;
            reachR = isLastWaypoint ? _destinationReachRadius : _waypointReachRadius;
            r2 = reachR * reachR;
            reachedWaypoint = HasReachedWaypoint(_pathIndex, toTarget, reachR, isLastWaypoint);
        }

        if (toTarget.sqrMagnitude >= 0.0001f)
        {
            // 목적지(마지막 웨이포인트)에 아주 가까우면 셀 중심으로 스냅해 정확히 도달
            if (isLastWaypoint && toTarget.sqrMagnitude <= 0.01f)
            {
                var pos = new Vector3(targetWorld.x, targetWorld.y, transform.position.z);
                transform.position = pos;
                if (_rigidbody != null)
                    _rigidbody.position = new Vector2(pos.x, pos.y);
            }
            else
            {
                int distToGoal = _globalTargetCell.HasValue ? CellDistance(myCell, _globalTargetCell.Value) : 0;
                int runThreshold = _viewRadius + 1;
                float moveSpeed = distToGoal > runThreshold ? _speed * _runSpeedMultiplier : _speed;
                Vector2 moveDir = GetArcSteeringDirection(_pathIndex, toTarget);
                _desiredVelocity = moveDir * moveSpeed;
            }
        }
        else
            ResetSteeringDirection();

        UpdateStuckWatch(toTarget, reachR, isLastWaypoint);
        LogNavigationTrace(waypoint, toTarget, isLastWaypoint);
    }

    private void StartNavigatingWithPath(
        List<Vector2Int> path,
        Vector2Int currentCell,
        Vector2Int? globalTarget,
        Vector2Int? chestTarget,
        string reason)
    {
        if (path == null || path.Count == 0)
            return;

        _globalTargetCell = globalTarget;
        _mapManager.SetGlobalTarget(globalTarget);
        _currentPath = path;
        _pathIndex = 0;
        _state = State.Navigating;
        _currentTargetCell = null;
        _navigatingToChestCell = chestTarget;
        ResetSteeringDirection();
        AnchorPathStart(currentCell, reason);
    }

    private void AnchorPathStart(Vector2Int currentCell, string reason)
    {
        if (_currentPath == null || _currentPath.Count == 0)
            return;

        int prevPathIndex = _pathIndex;
        Vector2Int firstWaypoint = _currentPath[0];
        float firstDistance = Vector2.Distance(transform.position, _mapManager.CellToWorld(firstWaypoint));
        if (firstWaypoint == currentCell)
        {
            if (_currentPath.Count > 1)
                _pathIndex = 1;
        }

        MCPLogHub.LogIssueStepIfEnabled(
            "NAV_START",
            reason,
            $"cell={currentCell} first={firstWaypoint} firstDist={firstDistance:F3} idx={prevPathIndex}->{_pathIndex}");
    }

    private Vector2 GetArcSteeringDirection(int waypointIndex, Vector2 toTarget)
    {
        if (toTarget.sqrMagnitude < 0.0001f)
            return Vector2.zero;

        Vector2 desired = toTarget.normalized;
        float distToTarget = toTarget.magnitude;
        if (distToTarget <= _arcConvergeDistance)
        {
            // 근접 구간에서는 즉시 목표 방향 수렴으로 오비팅 방지
            _steeringDirection = desired;
            _hasSteeringDirection = true;
            return desired;
        }

        Vector2 steering = desired;

        bool canRoundCorner =
            _useCornerArcSteering &&
            _currentPath != null &&
            waypointIndex > 0 &&
            waypointIndex < _currentPath.Count - 1;

        if (canRoundCorner)
        {
            Vector2 prev = _mapManager.CellToWorld(_currentPath[waypointIndex - 1]);
            Vector2 corner = _mapManager.CellToWorld(_currentPath[waypointIndex]);
            Vector2 next = _mapManager.CellToWorld(_currentPath[waypointIndex + 1]);

            Vector2 inDir = (corner - prev).normalized;
            Vector2 outDir = (next - corner).normalized;
            float angle = Vector2.Angle(inDir, outDir);
            if (angle >= _cornerArcMinAngle)
            {
                float distToCorner = Vector2.Distance(transform.position, corner);
                float startDist = Mathf.Max(_cornerArcStartDistance, _waypointReachRadius * 2f);
                float t = Mathf.Clamp01(1f - (distToCorner / Mathf.Max(0.001f, startDist)));
                t = t * t * (3f - 2f * t); // smoothstep

                steering = Vector2.Lerp(desired, outDir, t).normalized;

                if (_cornerObstacleBias > 0f && t > 0f)
                {
                    Vector2 saferNormal = GetSaferNormal(corner, steering);
                    steering = (steering + saferNormal * (_cornerObstacleBias * t)).normalized;
                }
            }
        }

        // 목표점 접근 성분이 너무 작아지면(코너에서 원형 오비팅) 강제로 목표 방향을 섞음.
        float approachDot = Vector2.Dot(steering, desired);
        if (approachDot < _minApproachDot)
        {
            float w = Mathf.InverseLerp(-1f, _minApproachDot, approachDot);
            steering = Vector2.Lerp(desired, steering, w).normalized;
        }

        return ApplyTurnRateLimit(steering, Time.deltaTime);
    }

    private Vector2 GetSaferNormal(Vector2 center, Vector2 forwardDir)
    {
        Vector2 left = new Vector2(-forwardDir.y, forwardDir.x).normalized;
        Vector2 right = -left;
        float leftScore = SampleSideWalkableScore(center, left);
        float rightScore = SampleSideWalkableScore(center, right);
        return leftScore >= rightScore ? left : right;
    }

    private float SampleSideWalkableScore(Vector2 center, Vector2 normalDir)
    {
        float score = 0f;
        for (int i = 1; i <= 3; i++)
        {
            Vector2 p = center + normalDir * (_cornerProbeDistance * i);
            if (_mapManager.IsWalkable(_mapManager.WorldToCell(p)))
                score += 1f;
        }
        return score;
    }

    private Vector2 ApplyTurnRateLimit(Vector2 targetDir, float deltaTime)
    {
        if (targetDir.sqrMagnitude < 0.0001f)
            return Vector2.zero;

        if (!_hasSteeringDirection)
        {
            _steeringDirection = targetDir.normalized;
            _hasSteeringDirection = true;
            return _steeringDirection;
        }

        if (_maxTurnRateDeg <= 0f)
        {
            _steeringDirection = targetDir.normalized;
            return _steeringDirection;
        }

        float maxDeltaDeg = _maxTurnRateDeg * deltaTime;
        float fromAngle = Mathf.Atan2(_steeringDirection.y, _steeringDirection.x) * Mathf.Rad2Deg;
        float toAngle = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg;
        float newAngle = Mathf.MoveTowardsAngle(fromAngle, toAngle, maxDeltaDeg);
        float rad = newAngle * Mathf.Deg2Rad;
        _steeringDirection = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
        return _steeringDirection;
    }

    private void ResetSteeringDirection()
    {
        _hasSteeringDirection = false;
        _steeringDirection = Vector2.zero;
    }

    private void UpdateStuckWatch(Vector2 toTarget, float reachR, bool isLastWaypoint)
    {
        bool active = _state == State.Navigating && _currentPath != null && _pathIndex >= 0 && _pathIndex < _currentPath.Count;
        MCPLogHub.UpdateStuckWatch(
            ref _stuckWatchWaypointIndex,
            ref _stuckWatchElapsed,
            ref _stuckWatchStartDistance,
            ref _stuckWarnedForCurrentWaypoint,
            active,
            _pathIndex,
            _currentPath != null ? _currentPath.Count - 1 : -1,
            toTarget.magnitude,
            reachR,
            isLastWaypoint,
            Time.deltaTime,
            _desiredVelocity,
            _mapManager != null ? _mapManager.WorldToCell(transform.position) : Vector2Int.zero,
            "ExplorerAI");
    }

    private void ResetStuckWatch()
    {
        MCPLogHub.ResetStuckWatch(
            ref _stuckWatchWaypointIndex,
            ref _stuckWatchElapsed,
            ref _stuckWatchStartDistance,
            ref _stuckWarnedForCurrentWaypoint);
    }

    private bool HasReachedWaypoint(int waypointIndex, Vector2 toTarget, float reachRadius, bool isLastWaypoint)
    {
        if (toTarget.sqrMagnitude <= reachRadius * reachRadius)
            return true;

        if (isLastWaypoint || _currentPath == null || waypointIndex <= 0 || waypointIndex >= _currentPath.Count)
            return false;

        // 코너 라운딩 중 오비팅 방지: 중간 웨이포인트는 더 넓은 유효 반경으로 통과 허용.
        float intermediateReach = Mathf.Max(reachRadius, _arcConvergeDistance * 1.2f, 0.25f);
        if (toTarget.sqrMagnitude <= intermediateReach * intermediateReach)
            return true;

        Vector2 prev = _mapManager.CellToWorld(_currentPath[waypointIndex - 1]);
        Vector2 curr = _mapManager.CellToWorld(_currentPath[waypointIndex]);
        Vector2 seg = curr - prev;
        float segLen = seg.magnitude;
        if (segLen < 0.0001f)
            return false;

        Vector2 segDir = seg / segLen;
        float along = Vector2.Dot((Vector2)transform.position - prev, segDir);
        return along >= segLen;
    }

    private void LogNavigationTrace(Vector2Int waypoint, Vector2 toTarget, bool isLastWaypoint)
    {
        if (_state != State.Navigating)
            return;

        MCPLogHub.LogTraceIfChannelEnabled(
            ExplorerTraceChannel,
            ref _debugTraceTimer,
            Time.deltaTime,
            "ExplorerAI Trace",
            transform.position,
            _mapManager != null ? _mapManager.WorldToCell(transform.position) : Vector2Int.zero,
            _pathIndex,
            _currentPath != null ? _currentPath.Count - 1 : -1,
            waypoint,
            toTarget.magnitude,
            isLastWaypoint,
            _desiredVelocity);
    }
}
