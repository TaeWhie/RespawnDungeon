using System.Collections.Generic;
using UnityEngine;
using TriInspector;
using UniRx;

/// <summary>
/// 동료(Ally)가 리더(Player)를 자연스럽게 따라가도록 합니다.
/// MapManager·Pathfinder를 사용해 장애물(벽)을 우회하며 리더 뒤쪽 포메이션으로 이동합니다.
/// 시야는 MapManager를 통해 파티 전체와 공유됩니다(동료 위치 기준 반경 내 타일 방문 처리).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PartyFollower : MonoBehaviour
{
    [Title("참조")]
    [Tooltip("비워두면 씬에서 Tag 'Player' 오브젝트를 자동 검색")]
    [SerializeField] private Transform _leader;
    [Tooltip("걷기/뛰기 애니메이션용. 비워두면 같은 오브젝트·자식의 Animator 사용")]
    [SerializeField] private Animator _animator;
    [Tooltip("비워두면 씬에서 자동 검색")]
    [SerializeField] private MapManager _mapManager;
    [Tooltip("비워두면 씬에서 자동 검색")]
    [SerializeField] private Pathfinder _pathfinder;

    [Title("포메이션")]
    [Tooltip("리더와의 유지 거리 (월드 단위). 리더 이동 방향 반대쪽으로 이만큼 떨어짐")]
    [Slider(0.1f, 5f)]
    [SerializeField] private float _followDistance = 1f;
    [Tooltip("맨앞 동료가 리더를 따르는 거리. 뒤쪽 동료도 앞 동료를 이 거리로 따라감 (0이면 _followDistance 사용)")]
    [Slider(0f, 3f)]
    [SerializeField] private float _firstFollowerDistance = 0.5f;
    [Tooltip("이 동료의 포메이션 슬롯 (0=리더 바로 뒤, 1=그 다음, …). 뒤쪽은 앞쪽 동료를 이 거리로 따라감")]
    [SerializeField] private int _slotIndex = 0;
    [Tooltip("이동 속도. 리더와 비슷하거나 약간 낮추면 자연스러움")]
    [Slider(0.5f, 8f)]
    [SerializeField] private float _speed = 2f;
    [Tooltip("뛸 때 속도 배율 (걷기=1배, 리더와 비슷하게 1.5 등)")]
    [Slider(1f, 3f)]
    [SerializeField] private float _runSpeedMultiplier = 1.5f;
    [Tooltip("리더가 뛸 때 동료가 리더 속도 이상으로 따라가도록 할지. 켜면 리더 속도를 참조해 최소 그만큼 뛰어서 붙음")]
    [SerializeField] private bool _matchLeaderRunSpeed = true;
    [Tooltip("목표(포메이션)까지 셀 거리가 이 값 이상일 때 달림. 0=항상 달리기, 1=1칸 이상, 2=2칸 이상, 3=3칸 이상일 때 달리기")]
    [Min(0)]
    [SerializeField] private int _runDistanceThreshold = 4;
    [Tooltip("웨이포인트 도착 판정 반경 (경로 따라갈 때)")]
    [Slider(0.1f, 2f)]
    [SerializeField] private float _waypointReachRadius = 0.35f;
    [Tooltip("목표 지점 도착으로 보는 반경. 이 안이면 멈춤")]
    [Slider(0.1f, 2f)]
    [SerializeField] private float _arrivalRadius = 0.35f;
    [Tooltip("목표 셀 도착 판정 반경(리더와 동일). 이 안이어야 다음 목표로 전환. 0.05=셀 중심에 정확히 도달 후에만 전환")]
    [Slider(0.01f, 0.5f)]
    [SerializeField] private float _destinationReachRadius = 0.05f;
    [Tooltip("리더가 멈춰 있을 때 사용할 기본 방향 (월드). 예: (0,-1)=아래쪽 뒤")]
    [SerializeField] private Vector2 _idleFormationDirection = new Vector2(0f, -1f);
    [Tooltip("동료끼리 겹치지 않도록 포메이션 직선의 좌우로 번갈아 밀어내는 거리 (월드 단위). 0이면 오프셋 없음")]
    [Slider(0f, 2f)]
    [SerializeField] private float _formationSpread = 0.35f;
    [Tooltip("목적지 셀을 슬롯마다 한 칸 옆으로 비틀어서 경로가 일자로 가지 않게 함. 0이면 비틀기 없음")]
    [Slider(0, 2)]
    [SerializeField] private int _formationCellJitter = 1;
    [Tooltip("뒤쪽 동료의 포메이션 방향을 이 시간만큼 부드럽게 보간 (커브 시 자연스러운 곡선). 0이면 즉시 반영")]
    [Slider(0f, 1f)]
    [SerializeField] private float _formationDirSmoothTime = 0.1f;
    [Tooltip("슬롯별 거리 미세 변동 (0~0.1). 0이면 균일 간격")]
    [Slider(0f, 0.2f)]
    [SerializeField] private float _distanceVariation = 0.05f;
    [Tooltip("동료마다 랜덤한 좌우 오프셋 범위 (일자 감소). 0이면 없음")]
    [Slider(0f, 1f)]
    [SerializeField] private float _personalSpreadRange = 0.25f;
    [Tooltip("목표 위치를 이 시간만큼 부드럽게 보간 (궤적이 휘어져 보임). 0이면 즉시")]
    [Slider(0f, 0.5f)]
    [SerializeField] private float _targetSmoothTime = 0.15f;
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
    [Tooltip("리더 또는 앞쪽 동료와 이 거리보다 가까우면 멈춤 (동선 겹침 시 뒤쪽이 서서 간격 벌어짐). 0이면 비활성")]
    [Slider(0f, 2f)]
    [SerializeField] private float _overlapStopRadius = 0.8f;
    [Tooltip("리더/동료 캐시 재검색 주기(초). 0이면 매 프레임.")]
    [Slider(0f, 2f)]
    [SerializeField] private float _partyRescanInterval = 0.4f;

    [Title("시야 (파티 공유)")]
    [Tooltip("현재 위치 기준 시야 반경(타일 수). 이 범위 내 타일을 파티 공유 맵에 방문 처리합니다.")]
    [Slider(1, 10)]
    [SerializeField] private int _viewRadius = 2;
    [Tooltip("2단계 시야 반경(구조만 보임). 이 범위까지 벽/바닥이 보이고, _viewRadius 안에서만 장애물 등 전부 보임")]
    [Slider(1, 15)]
    [SerializeField] private int _viewRadiusStructure = 4;

    private Rigidbody2D _rigidbody;
    private Vector2 _lastLeaderPosition;
    private Vector2 _lastLeaderDirection;
    private bool _hasStoredDirection;
    private List<Vector2Int> _currentPath = new List<Vector2Int>();
    private int _pathIndex;
    private Vector2Int? _lastTargetCell;
    private Vector2 _smoothedFormationDir;
    private Vector2 _smoothedTargetWorld;
    private float _personalSpreadOffset;
    private float _personalDistanceOffset;
    private int _lastPathMapVersion = -1;
    private PartyFormationProvider _formationProvider;
    private Vector2 _steeringDirection;
    private bool _hasSteeringDirection;
    private float _debugTraceTimer;
    private int _stuckWatchWaypointIndex = -1;
    private float _stuckWatchElapsed;
    private float _stuckWatchStartDistance;
    private bool _stuckWarnedForCurrentWaypoint;
    private readonly CompositeDisposable _subscriptions = new CompositeDisposable();
    private const string FollowerTraceChannel = "FOLLOWER_NAV";

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _rigidbody.gravityScale = 0f;
        _rigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Start()
    {
        if (_leader == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
                _leader = player.transform;
        }

        if (_leader != null)
        {
            _lastLeaderPosition = _leader.position;
            _lastLeaderDirection = _idleFormationDirection.normalized;
            _hasStoredDirection = false;
        }

        if (_animator == null)
        {
            _animator = GetComponent<Animator>();
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
        }

        if (_mapManager == null) _mapManager = FindFirstObjectByType<MapManager>();
        if (_pathfinder == null) _pathfinder = FindFirstObjectByType<Pathfinder>();

        if (_leader != null)
            _formationProvider = _leader.GetComponent<PartyFormationProvider>();
        if (_leader != null && _formationProvider == null)
            _formationProvider = _leader.gameObject.AddComponent<PartyFormationProvider>();

        _smoothedFormationDir = _idleFormationDirection.normalized;
        _smoothedTargetWorld = _leader != null ? (Vector2)_leader.position : (Vector2)transform.position;
        if (_personalSpreadRange > 0f)
            _personalSpreadOffset = Random.Range(-_personalSpreadRange, _personalSpreadRange);
        _personalDistanceOffset = Random.Range(-0.08f, 0.08f);
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
        if (_leader == null)
            TryAssignLeaderFromSharedCache();
    }

    private void FixedUpdate()
    {
        if (_leader == null)
            TryAssignLeaderFromSharedCache();

        if (_leader == null || _rigidbody == null)
            return;

        if (_mapManager == null || _pathfinder == null || !_mapManager.IsInitialized)
        {
            _rigidbody.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 leaderPos = _leader.position;
        Vector2Int myCell = _mapManager.WorldToCell(transform.position);

        if (!_mapManager.IsWalkable(myCell))
        {
            _rigidbody.linearVelocity = Vector2.zero;
            return;
        }

        // 시야: 파티 공유 맵에 이 동료 위치 기준 반경 내 타일 방문 처리 (리더 시야와 동일 맵에 합쳐짐)
        _mapManager.MarkVisitedInRadius(myCell, _viewRadius, _viewRadiusStructure);

        // 리더 이동 방향 갱신
        Vector2 delta = leaderPos - _lastLeaderPosition;
        if (delta.sqrMagnitude >= 0.0001f)
        {
            _lastLeaderDirection = delta.normalized;
            _hasStoredDirection = true;
        }
        _lastLeaderPosition = leaderPos;

        Vector2 myLeaderPos;
        Vector2 formationDir;
        Vector2 targetWorld;
        Vector2Int leaderOrFrontCell;

        if (_formationProvider != null)
        {
            // 리더가 준 좌표만 그대로 사용 (스프레드/스무딩 없음)
            targetWorld = _formationProvider.GetFormationTarget(_slotIndex);
            formationDir = _formationProvider.FormationDirection;
            leaderOrFrontCell = _slotIndex == 0
                ? _mapManager.WorldToCell(leaderPos)
                : _formationProvider.GetFormationTargetCell(_slotIndex - 1);
            myLeaderPos = leaderPos;
            _smoothedTargetWorld = targetWorld;
        }
        else
        {
        // 맨앞(slot 0)은 리더를, 뒤쪽(slot≥1)은 바로 앞 동료를 맨앞이 리더 따르듯이 따라감
        Vector2 myFormationDir;
        float totalDistance;
        if (_slotIndex == 0)
        {
            myLeaderPos = leaderPos;
            myFormationDir = _hasStoredDirection ? _lastLeaderDirection : _idleFormationDirection.normalized;
            totalDistance = _firstFollowerDistance > 0f ? _firstFollowerDistance : _followDistance;
        }
        else if (TryGetFrontAlly(out Vector2 frontPos, out Vector2 frontDir))
        {
            myLeaderPos = frontPos;
            myFormationDir = frontDir.sqrMagnitude >= 0.01f ? frontDir.normalized : (frontPos - (Vector2)transform.position).normalized;
            if (myFormationDir.sqrMagnitude < 0.01f)
                myFormationDir = _idleFormationDirection.normalized;
            totalDistance = _firstFollowerDistance > 0f ? _firstFollowerDistance : _followDistance;
            if (_formationDirSmoothTime > 0f)
            {
                _smoothedFormationDir = Vector2.Lerp(_smoothedFormationDir, myFormationDir, Time.fixedDeltaTime / Mathf.Max(0.01f, _formationDirSmoothTime));
                if (_smoothedFormationDir.sqrMagnitude >= 0.01f)
                    myFormationDir = _smoothedFormationDir.normalized;
            }
        }
        else
        {
            myLeaderPos = leaderPos;
            myFormationDir = _hasStoredDirection ? _lastLeaderDirection : _idleFormationDirection.normalized;
            totalDistance = _firstFollowerDistance > 0f ? _firstFollowerDistance + _followDistance * _slotIndex : _followDistance * (_slotIndex + 1);
        }

        // 거리 미세 변동으로 일자 간격 완화 (자연스러운 느낌)
        if (_distanceVariation > 0f)
            totalDistance += _distanceVariation * ((_slotIndex % 3) - 1);
        totalDistance += _personalDistanceOffset;

        formationDir = myFormationDir;
        targetWorld = myLeaderPos - formationDir * totalDistance;

        // 수직 오프셋: 사인파 + 동료마다 고정 랜덤 (일자 감소)
        if (_formationSpread > 0f || _personalSpreadRange > 0f)
        {
            Vector2 perp = new Vector2(formationDir.y, -formationDir.x);
            float offset = Mathf.Sin(_slotIndex * 0.85f) * _formationSpread + _personalSpreadOffset;
            targetWorld += perp * offset;
        }

        // 목표 위치 스무딩: 궤적이 휘어져 보이도록 (움직임이 덜 일자)
        if (_targetSmoothTime > 0f)
        {
            _smoothedTargetWorld = Vector2.Lerp(_smoothedTargetWorld, targetWorld, Time.fixedDeltaTime / Mathf.Max(0.02f, _targetSmoothTime));
            targetWorld = _smoothedTargetWorld;
        }

        leaderOrFrontCell = _mapManager.WorldToCell(myLeaderPos);
        }

        // 리더가 비틀린 좌표까지 줬으면 그 셀 사용, 아니면 월드→셀 후 동료 쪽에서 비틀기
        Vector2Int targetCell = _formationProvider != null
            ? _formationProvider.GetFormationTargetCell(_slotIndex)
            : _mapManager.WorldToCell(targetWorld);
        // 리더가 비틀린 좌표까지 줬으면 비틀기 생략, 아니면 동료가 셀 비틀기
        if (_formationProvider == null && _formationCellJitter > 0 && CellDistance(myCell, targetCell) > 1)
        {
            Vector2 forwardDir = formationDir;
            int sign = (_slotIndex % 2 == 0) ? 1 : -1;
            var offsets = new[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
                new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
            };
            foreach (var o in offsets)
            {
                Vector2Int jitterDelta = o * (sign * _formationCellJitter);
                if (Vector2.Dot((Vector2)jitterDelta, forwardDir) < 0f) continue; // 뒤쪽·뒤쪽 대각선 제외
                Vector2Int jitteredCell = targetCell + jitterDelta;
                if (_mapManager.IsWalkable(jitteredCell) && jitteredCell != leaderOrFrontCell)
                {
                    targetCell = jitteredCell;
                    break;
                }
            }
        }

        // 목표 셀이 벽이면 따라가는 대상(리더 또는 앞 동료) 셀로 경로 목표 설정
        if (!_mapManager.IsWalkable(targetCell))
            targetCell = _mapManager.WorldToCell(myLeaderPos);

        // 동료가 리더(또는 맨 앞 동료)와 같은 셀을 목표로 하면 안 됨 → 인접 셀 중 하나로
        if (targetCell == leaderOrFrontCell)
        {
            var neighbors = _mapManager.GetWalkableNeighbors(leaderOrFrontCell);
            if (neighbors != null && neighbors.Count > 0)
            {
                Vector2 behindDir = -formationDir;
                Vector2Int behindCell = leaderOrFrontCell + new Vector2Int(Mathf.RoundToInt(behindDir.x), Mathf.RoundToInt(behindDir.y));
                if (neighbors.Contains(behindCell))
                    targetCell = behindCell;
                else
                    targetCell = neighbors[0];
            }
        }

        // 맵이 바뀌었으면(장애물/상자 부숴짐 등) 경로 무효화 후 재계산해서 꼬임 방지
        if (_mapManager.WalkableVersion != _lastPathMapVersion)
        {
            _lastPathMapVersion = _mapManager.WalkableVersion;
            _currentPath = null;
            _pathIndex = 0;
            ResetSteeringDirection();
        }

        // 목표 변경: 리더가 좌표를 줄 때는 무조건 그 좌표로 갱신. 리더 없으면 도착한 다음에만 새 목표로 전환
        bool atCurrentTarget = true;
        if (_currentPath != null && _pathIndex < _currentPath.Count)
            atCurrentTarget = false;
        else if (_currentPath != null && _pathIndex >= _currentPath.Count)
        {
            Vector2 destWorld = _mapManager.CellToWorld(_currentPath[_currentPath.Count - 1]);
            float r2 = _destinationReachRadius * _destinationReachRadius;
            atCurrentTarget = ((Vector2)transform.position - destWorld).sqrMagnitude <= r2;
        }

        bool needNewPath = _formationProvider != null
            ? (targetCell != _lastTargetCell || _currentPath == null || _pathIndex >= _currentPath.Count)
            : (atCurrentTarget && (targetCell != _lastTargetCell || _currentPath == null || _pathIndex >= _currentPath.Count));

        if (needNewPath)
        {
            // 동료는 장애물을 부수지 않으므로 장애물을 피하는 경로 사용
            var path = _pathfinder.GetPath(_mapManager, myCell, targetCell, allowObstacles: false);
            if (path != null && path.Count > 0)
            {
                _currentPath = path;
                _pathIndex = path.Count > 1 ? 1 : 0;
                _lastTargetCell = targetCell;
            }
            else
            {
                _currentPath = null;
                _pathIndex = 0;
                _lastTargetCell = targetCell;
            }
            _lastPathMapVersion = _mapManager.WalkableVersion;
            ResetSteeringDirection();
        }

        Vector2 desiredVelocity = Vector2.zero;

        if (_currentPath != null && _pathIndex < _currentPath.Count)
        {
            Vector2Int waypoint = _currentPath[_pathIndex];
            Vector3 waypointWorld = _mapManager.CellToWorld(waypoint);
            Vector2 toWaypoint = (Vector2)waypointWorld - (Vector2)transform.position;
            bool isLastWaypoint = _pathIndex >= _currentPath.Count - 1;
            float reachR = isLastWaypoint ? _destinationReachRadius : _waypointReachRadius;
            float r2 = reachR * reachR;

            // 중간 웨이포인트 근접 시 강제 패스스루(오비팅 방지)
            if (!isLastWaypoint && toWaypoint.magnitude <= Mathf.Max(0.35f, _arcConvergeDistance * 1.2f))
            {
                _pathIndex++;
                if (_pathIndex < _currentPath.Count)
                {
                    waypoint = _currentPath[_pathIndex];
                    waypointWorld = _mapManager.CellToWorld(waypoint);
                    toWaypoint = (Vector2)waypointWorld - (Vector2)transform.position;
                    isLastWaypoint = _pathIndex >= _currentPath.Count - 1;
                    reachR = isLastWaypoint ? _destinationReachRadius : _waypointReachRadius;
                    r2 = reachR * reachR;
                }
            }

            // 웨이포인트 도착 시 다음으로 (리더처럼 가까울 때만 전환 후 스냅 → 멀리서 튀지 않음)
            bool reachedWaypoint = HasReachedWaypoint(_pathIndex, toWaypoint, reachR, isLastWaypoint);
            while (reachedWaypoint && _pathIndex < _currentPath.Count)
            {
                bool wasLastWaypoint = _pathIndex >= _currentPath.Count - 1;
                _pathIndex++;
                if (wasLastWaypoint)
                {
                    var pos = new Vector3(waypointWorld.x, waypointWorld.y, transform.position.z);
                    transform.position = pos;
                    if (_rigidbody != null)
                        _rigidbody.position = new Vector2(pos.x, pos.y);
                }
                if (_pathIndex >= _currentPath.Count)
                    break;
                waypoint = _currentPath[_pathIndex];
                waypointWorld = _mapManager.CellToWorld(waypoint);
                toWaypoint = (Vector2)waypointWorld - (Vector2)transform.position;
                isLastWaypoint = _pathIndex >= _currentPath.Count - 1;
                reachR = isLastWaypoint ? _destinationReachRadius : _waypointReachRadius;
                r2 = reachR * reachR;
                reachedWaypoint = HasReachedWaypoint(_pathIndex, toWaypoint, reachR, isLastWaypoint);
            }

            if (_pathIndex < _currentPath.Count && toWaypoint.sqrMagnitude >= 0.0001f)
            {
                isLastWaypoint = _pathIndex >= _currentPath.Count - 1;
                if (isLastWaypoint && toWaypoint.sqrMagnitude <= 0.01f)
                {
                    var pos = new Vector3(waypointWorld.x, waypointWorld.y, transform.position.z);
                    transform.position = pos;
                    if (_rigidbody != null)
                        _rigidbody.position = new Vector2(pos.x, pos.y);
                }
                else
                {
                    Vector2 moveDir = GetArcSteeringDirection(_pathIndex, toWaypoint);
                    desiredVelocity = moveDir * _speed;
                }
            }
        }
        else
        {
            // 경로가 없으면 직선 이동 금지(벽 뚫림 방지). 경로를 다 따라오면 해당 셀에서 정지.
            ResetSteeringDirection();
        }

        // 목표 지점에 충분히 가까워지면 도착으로 보고 멈춤
        if (_arrivalRadius > 0f)
        {
            Vector2 toTargetWorld = targetWorld - (Vector2)transform.position;
            if (toTargetWorld.sqrMagnitude <= _arrivalRadius * _arrivalRadius)
            {
                desiredVelocity = Vector2.zero;
                ResetSteeringDirection();
            }
        }

        // 동선 겹침 시 뒤쪽이 멈춤: 리더 또는 더 앞 슬롯 동료와 너무 가까우면 정지
        if (_overlapStopRadius > 0f && IsTooCloseToSomeoneInFront(leaderPos))
            desiredVelocity = Vector2.zero;

        // 뛰기 조건:
        // - 기본: 이동 중이고 목표까지 셀 거리가 _runDistanceThreshold 이상일 때
        // - 추가: 리더가 충분히 빠르게 움직여서 "뛰는 중"이면 동료도 같이 뛰기
        int distToTarget = CellDistance(myCell, targetCell);
        bool hasMove = desiredVelocity.sqrMagnitude >= 0.01f;

        bool leaderIsRunning = false;
        float leaderSpeed = 0f;
        if (_leader != null)
        {
            var leaderRb = _leader.GetComponent<Rigidbody2D>();
            if (leaderRb != null)
            {
                leaderSpeed = leaderRb.linearVelocity.magnitude;
                if (leaderSpeed > _speed * 1.01f)
                    leaderIsRunning = true;
            }
        }

        bool useRun = hasMove && (distToTarget >= _runDistanceThreshold || leaderIsRunning);

        if (useRun && desiredVelocity.sqrMagnitude >= 0.0001f)
        {
            float moveSpeed = _speed * _runSpeedMultiplier;
            if (_matchLeaderRunSpeed && leaderIsRunning)
            {
                // 리더가 실제로 뛰는 중이면, 최소 리더 속도 이상으로 맞춰서 떨어지지 않도록
                moveSpeed = Mathf.Max(moveSpeed, leaderSpeed);
            }
            desiredVelocity = desiredVelocity.normalized * moveSpeed;
        }

        _rigidbody.linearVelocity = desiredVelocity;
        UpdateMovementAnimation(desiredVelocity, useRun);
        UpdateFollowerStuckWatch(desiredVelocity);
        LogFollowerTrace(myCell, desiredVelocity, targetCell);
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
                t = t * t * (3f - 2f * t);

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

        return ApplyTurnRateLimit(steering, Time.fixedDeltaTime);
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

    private void UpdateFollowerStuckWatch(Vector2 desiredVelocity)
    {
        bool active = _currentPath != null && _pathIndex >= 0 && _pathIndex < _currentPath.Count && _mapManager != null;
        Vector2Int wp = active ? _currentPath[_pathIndex] : Vector2Int.zero;
        float toWp = active ? ((Vector2)_mapManager.CellToWorld(wp) - (Vector2)transform.position).magnitude : 0f;
        bool isLast = active && _pathIndex >= _currentPath.Count - 1;

        MCPLogHub.UpdateStuckWatch(
            ref _stuckWatchWaypointIndex,
            ref _stuckWatchElapsed,
            ref _stuckWatchStartDistance,
            ref _stuckWarnedForCurrentWaypoint,
            active,
            _pathIndex,
            _currentPath != null ? _currentPath.Count - 1 : -1,
            toWp,
            _destinationReachRadius,
            isLast,
            Time.fixedDeltaTime,
            desiredVelocity,
            _mapManager != null ? _mapManager.WorldToCell(transform.position) : Vector2Int.zero,
            $"Follower:{name}");
    }

    private void ResetFollowerStuckWatch()
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

    private void LogFollowerTrace(Vector2Int myCell, Vector2 desiredVelocity, Vector2Int targetCell)
    {
        Vector2Int wp = (_currentPath != null && _pathIndex >= 0 && _pathIndex < _currentPath.Count) ? _currentPath[_pathIndex] : targetCell;
        float toWp = ((Vector2)_mapManager.CellToWorld(wp) - (Vector2)transform.position).magnitude;
        MCPLogHub.LogTraceIfChannelEnabled(
            FollowerTraceChannel,
            ref _debugTraceTimer,
            Time.fixedDeltaTime,
            $"Follower Trace:{name}",
            transform.position,
            myCell,
            _pathIndex,
            _currentPath != null ? _currentPath.Count - 1 : -1,
            wp,
            toWp,
            _pathIndex >= (_currentPath != null ? _currentPath.Count - 1 : 0),
            desiredVelocity,
            targetCell);
    }

    private static int CellDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
    }

    private void UpdateMovementAnimation(Vector2 velocity, bool useRun)
    {
        if (_animator == null)
            return;

        bool hasVelocity = velocity.sqrMagnitude >= 0.01f;
        _animator.SetBool("Idle", !hasVelocity);
        _animator.SetBool("Walk", hasVelocity && !useRun);
        _animator.SetBool("Run", hasVelocity && useRun);

        // 위/아래 이동 시 velocity.x 미세 요동으로 플립 방지: 가로 성분이 충분할 때만 좌우 반전
        const float flipHorizontalThreshold = 0.25f;
        if (hasVelocity && Mathf.Abs(velocity.x) >= flipHorizontalThreshold)
        {
            var scale = transform.localScale;
            if ((velocity.x > 0 && scale.x < 0) || (velocity.x < 0 && scale.x > 0))
                transform.localScale = new Vector3(-scale.x, scale.y, scale.z);
        }
    }

    /// <summary>포메이션 슬롯 (0=리더 바로 뒤). 앞쪽 동료 판정용.</summary>
    public int SlotIndex => _slotIndex;

    /// <summary>바로 앞 동료(slotIndex - 1)의 위치와 이동 방향을 반환. 없으면 false.</summary>
    private bool TryGetFrontAlly(out Vector2 position, out Vector2 movementDirection)
    {
        position = Vector2.zero;
        movementDirection = Vector2.zero;
        if (_slotIndex < 1) return false;

        var followers = ExplorationPartyCache.Followers;
        for (int i = followers.Count - 1; i >= 0; i--)
        {
            var other = followers[i];
            if (other == null) continue;
            if (other == this) continue;
            if (other == null || other.SlotIndex != _slotIndex - 1) continue;
            position = other.transform.position;
            movementDirection = other._rigidbody != null ? other._rigidbody.linearVelocity : Vector2.zero;
            return true;
        }
        return false;
    }

    /// <summary>리더 또는 슬롯이 더 앞인 동료와 _overlapStopRadius 안에 있으면 true. 뒤쪽이 멈출 때 사용.</summary>
    private bool IsTooCloseToSomeoneInFront(Vector2 leaderPos)
    {
        Vector2 myPos = transform.position;
        float r2 = _overlapStopRadius * _overlapStopRadius;

        if (_leader != null && ((Vector2)_leader.position - myPos).sqrMagnitude < r2)
            return true;

        var followers = ExplorationPartyCache.Followers;
        for (int i = followers.Count - 1; i >= 0; i--)
        {
            var other = followers[i];
            if (other == null) continue;
            if (other == this) continue;
            if (other == null || other.SlotIndex >= _slotIndex) continue;
            if (((Vector2)other.transform.position - myPos).sqrMagnitude < r2)
                return true;
        }
        return false;
    }

    private void TryAssignLeaderFromSharedCache()
    {
        var leader = ExplorationPartyCache.Leader;
        if (leader == null)
            return;

        _leader = leader;
        _formationProvider = _leader.GetComponent<PartyFormationProvider>();
        if (_formationProvider == null)
            _formationProvider = _leader.gameObject.AddComponent<PartyFormationProvider>();
        _lastLeaderPosition = _leader.position;
        _lastLeaderDirection = _idleFormationDirection.normalized;
        _hasStoredDirection = false;
        _smoothedTargetWorld = _leader.position;
    }

    private void OnDrawGizmos()
    {
        if (_mapManager == null || !_mapManager.DrawGizmos) return;
        if (_currentPath == null || _currentPath.Count < 2) return;

        Gizmos.color = Color.green;
        for (int i = 0; i < _currentPath.Count - 1; i++)
        {
            var a = _mapManager.CellToWorld(_currentPath[i]);
            var b = _mapManager.CellToWorld(_currentPath[i + 1]);
            Gizmos.DrawLine(a, b);
        }

        DrawCurvedPathGizmos();

        if (_lastTargetCell.HasValue)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(_mapManager.CellToWorld(_lastTargetCell.Value), 0.08f);
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

    /// <summary>리더 참조 설정 (런타임에 동료를 배치할 때 사용)</summary>
    public void SetLeader(Transform leader)
    {
        _leader = leader;
        if (_leader != null)
        {
            _lastLeaderPosition = _leader.position;
            _lastLeaderDirection = _idleFormationDirection.normalized;
            _hasStoredDirection = false;
        }
        _currentPath = null;
        _pathIndex = 0;
        _lastTargetCell = null;
        ResetSteeringDirection();
        _smoothedFormationDir = _idleFormationDirection.normalized;
        if (_leader != null)
            _smoothedTargetWorld = _leader.position;
    }

    /// <summary>포메이션 슬롯 설정 (0=리더 바로 뒤)</summary>
    public void SetSlotIndex(int slotIndex)
    {
        _slotIndex = Mathf.Max(0, slotIndex);
    }
}
