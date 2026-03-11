using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 동료(Ally)가 리더(Player)를 자연스럽게 따라가도록 합니다.
/// MapManager·Pathfinder를 사용해 장애물(벽)을 우회하며 리더 뒤쪽 포메이션으로 이동합니다.
/// 시야는 MapManager를 통해 파티 전체와 공유됩니다(동료 위치 기준 반경 내 타일 방문 처리).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PartyFollower : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("비워두면 씬에서 Tag 'Player' 오브젝트를 자동 검색")]
    [SerializeField] private Transform _leader;
    [Tooltip("걷기/뛰기 애니메이션용. 비워두면 같은 오브젝트·자식의 Animator 사용")]
    [SerializeField] private Animator _animator;
    [Tooltip("비워두면 씬에서 자동 검색")]
    [SerializeField] private MapManager _mapManager;
    [Tooltip("비워두면 씬에서 자동 검색")]
    [SerializeField] private Pathfinder _pathfinder;

    [Header("포메이션")]
    [Tooltip("리더와의 유지 거리 (월드 단위). 리더 이동 방향 반대쪽으로 이만큼 떨어짐")]
    [SerializeField] private float _followDistance = 1f;
    [Tooltip("맨앞 동료가 리더를 따르는 거리. 뒤쪽 동료도 앞 동료를 이 거리로 따라감 (0이면 _followDistance 사용)")]
    [SerializeField] private float _firstFollowerDistance = 0.5f;
    [Tooltip("이 동료의 포메이션 슬롯 (0=리더 바로 뒤, 1=그 다음, …). 뒤쪽은 앞쪽 동료를 이 거리로 따라감")]
    [SerializeField] private int _slotIndex = 0;
    [Tooltip("이동 속도. 리더와 비슷하거나 약간 낮추면 자연스러움")]
    [SerializeField] private float _speed = 2f;
    [Tooltip("뛸 때 속도 배율 (걷기=1배, 리더와 비슷하게 1.5 등)")]
    [SerializeField] private float _runSpeedMultiplier = 1.5f;
    [Tooltip("웨이포인트 도착 판정 반경 (경로 따라갈 때)")]
    [SerializeField] private float _waypointReachRadius = 0.35f;
    [Tooltip("목표 지점 도착으로 보는 반경. 이 안이면 멈춤")]
    [SerializeField] private float _arrivalRadius = 0.35f;
    [Tooltip("리더가 멈춰 있을 때 사용할 기본 방향 (월드). 예: (0,-1)=아래쪽 뒤")]
    [SerializeField] private Vector2 _idleFormationDirection = new Vector2(0f, -1f);
    [Tooltip("동료끼리 겹치지 않도록 포메이션 직선의 좌우로 번갈아 밀어내는 거리 (월드 단위). 0이면 오프셋 없음")]
    [SerializeField] private float _formationSpread = 0.35f;
    [Tooltip("뒤쪽 동료의 포메이션 방향을 이 시간만큼 부드럽게 보간 (커브 시 자연스러운 곡선). 0이면 즉시 반영")]
    [SerializeField] private float _formationDirSmoothTime = 0.1f;
    [Tooltip("슬롯별 거리 미세 변동 (0~0.1). 0이면 균일 간격")]
    [SerializeField] private float _distanceVariation = 0.05f;
    [Tooltip("동료마다 랜덤한 좌우 오프셋 범위 (일자 감소). 0이면 없음")]
    [SerializeField] private float _personalSpreadRange = 0.25f;
    [Tooltip("목표 위치를 이 시간만큼 부드럽게 보간 (궤적이 휘어져 보임). 0이면 즉시")]
    [SerializeField] private float _targetSmoothTime = 0.15f;
    [Tooltip("리더 또는 앞쪽 동료와 이 거리보다 가까우면 멈춤 (동선 겹침 시 뒤쪽이 서서 간격 벌어짐). 0이면 비활성")]
    [SerializeField] private float _overlapStopRadius = 0.5f;

    [Header("시야 (파티 공유)")]
    [Tooltip("현재 위치 기준 시야 반경(타일 수). 이 범위 내 타일을 파티 공유 맵에 방문 처리합니다.")]
    [SerializeField] private int _viewRadius = 2;

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

        _smoothedFormationDir = _idleFormationDirection.normalized;
        _smoothedTargetWorld = _leader != null ? (Vector2)_leader.position : (Vector2)transform.position;
        if (_personalSpreadRange > 0f)
            _personalSpreadOffset = Random.Range(-_personalSpreadRange, _personalSpreadRange);
        _personalDistanceOffset = Random.Range(-0.08f, 0.08f);
    }

    private void FixedUpdate()
    {
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
        _mapManager.MarkVisitedInRadius(myCell, _viewRadius);

        // 리더 이동 방향 갱신
        Vector2 delta = leaderPos - _lastLeaderPosition;
        if (delta.sqrMagnitude >= 0.0001f)
        {
            _lastLeaderDirection = delta.normalized;
            _hasStoredDirection = true;
        }
        _lastLeaderPosition = leaderPos;

        // 맨앞(slot 0)은 리더를, 뒤쪽(slot≥1)은 바로 앞 동료를 맨앞이 리더 따르듯이 따라감
        Vector2 myLeaderPos;
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

        Vector2 formationDir = myFormationDir;
        Vector2 targetWorld = myLeaderPos - formationDir * totalDistance;

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

        Vector2Int targetCell = _mapManager.WorldToCell(targetWorld);

        // 목표 셀이 벽이면 따라가는 대상(리더 또는 앞 동료) 셀로 경로 목표 설정
        if (!_mapManager.IsWalkable(targetCell))
            targetCell = _mapManager.WorldToCell(myLeaderPos);

        // 목표가 바뀌었거나 경로가 없/끝났으면 경로 재계산
        bool needNewPath = targetCell != _lastTargetCell
            || _currentPath == null
            || _pathIndex >= _currentPath.Count;

        if (needNewPath)
        {
            var path = _pathfinder.GetPath(_mapManager, myCell, targetCell);
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
        }

        Vector2 desiredVelocity = Vector2.zero;

        if (_currentPath != null && _pathIndex < _currentPath.Count)
        {
            Vector2Int waypoint = _currentPath[_pathIndex];
            Vector3 waypointWorld = _mapManager.CellToWorld(waypoint);
            Vector2 toWaypoint = (Vector2)waypointWorld - (Vector2)transform.position;
            float r2 = _waypointReachRadius * _waypointReachRadius;

            // 웨이포인트 도착 시 다음으로
            while (toWaypoint.sqrMagnitude <= r2 && _pathIndex < _currentPath.Count)
            {
                _pathIndex++;
                if (_pathIndex >= _currentPath.Count)
                    break;
                waypoint = _currentPath[_pathIndex];
                waypointWorld = _mapManager.CellToWorld(waypoint);
                toWaypoint = (Vector2)waypointWorld - (Vector2)transform.position;
            }

            if (_pathIndex < _currentPath.Count && toWaypoint.sqrMagnitude >= 0.0001f)
                desiredVelocity = toWaypoint.normalized * _speed;
        }
        else
        {
            // 경로가 없으면 직선 이동 금지(벽 뚫림 방지). 경로를 다 따라오면 해당 셀에서 정지.
        }

        // 동선 겹침 시 뒤쪽이 멈춤: 리더 또는 더 앞 슬롯 동료와 너무 가까우면 정지
        if (_overlapStopRadius > 0f && IsTooCloseToSomeoneInFront(leaderPos))
            desiredVelocity = Vector2.zero;

        // 뛰기 조건: 이동 중이고 목표(포메이션)까지 셀 거리가 시야+1보다 멀 때 (리더와 동일)
        int distToTarget = CellDistance(myCell, targetCell);
        int runThreshold = _viewRadius + 1;
        bool useRun = desiredVelocity.sqrMagnitude >= 0.01f && distToTarget > runThreshold;

        if (useRun && desiredVelocity.sqrMagnitude >= 0.0001f)
            desiredVelocity = desiredVelocity.normalized * (_speed * _runSpeedMultiplier);

        _rigidbody.linearVelocity = desiredVelocity;
        UpdateMovementAnimation(desiredVelocity, useRun);
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

        var allies = GameObject.FindGameObjectsWithTag("Ally");
        foreach (var go in allies)
        {
            if (go == null || go == gameObject) continue;
            var other = go.GetComponent<PartyFollower>();
            if (other == null || other.SlotIndex != _slotIndex - 1) continue;
            position = go.transform.position;
            var rb = go.GetComponent<Rigidbody2D>();
            movementDirection = rb != null ? rb.linearVelocity : Vector2.zero;
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

        var allies = GameObject.FindGameObjectsWithTag("Ally");
        foreach (var go in allies)
        {
            if (go == null || go == gameObject) continue;
            var other = go.GetComponent<PartyFollower>();
            if (other == null || other.SlotIndex >= _slotIndex) continue;
            if (((Vector2)go.transform.position - myPos).sqrMagnitude < r2)
                return true;
        }
        return false;
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
