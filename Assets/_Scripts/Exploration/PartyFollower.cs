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
    [Tooltip("이 동료의 포메이션 슬롯 (0=리더 바로 뒤, 1=한 칸 더 뒤, …). 동일 슬롯이면 겹칠 수 있음")]
    [SerializeField] private int _slotIndex = 0;
    [Tooltip("이동 속도. 리더와 비슷하거나 약간 낮추면 자연스러움")]
    [SerializeField] private float _speed = 2f;
    [Tooltip("웨이포인트 도착 판정 반경 (경로 따라갈 때)")]
    [SerializeField] private float _waypointReachRadius = 0.35f;
    [Tooltip("목표 지점 도착으로 보는 반경. 이 안이면 멈춤")]
    [SerializeField] private float _arrivalRadius = 0.35f;
    [Tooltip("리더가 멈춰 있을 때 사용할 기본 방향 (월드). 예: (0,-1)=아래쪽 뒤")]
    [SerializeField] private Vector2 _idleFormationDirection = new Vector2(0f, -1f);
    [Tooltip("동료끼리 겹치지 않도록 포메이션 직선의 좌우로 번갈아 밀어내는 거리 (월드 단위). 0이면 오프셋 없음")]
    [SerializeField] private float _formationSpread = 0.35f;
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

        Vector2 formationDir = _hasStoredDirection ? _lastLeaderDirection : _idleFormationDirection.normalized;
        float totalDistance = _followDistance * (_slotIndex + 1);
        Vector2 targetWorld = leaderPos - formationDir * totalDistance;

        // 동료끼리 겹치지 않도록 슬롯별로 포메이션 직선의 수직 방향으로 번갈아 오프셋
        if (_formationSpread > 0f)
        {
            Vector2 perp = new Vector2(formationDir.y, -formationDir.x);
            float offset = (_slotIndex % 2 == 0 ? -1f : 1f) * _formationSpread;
            targetWorld += perp * offset;
        }

        Vector2Int targetCell = _mapManager.WorldToCell(targetWorld);

        // 목표 셀이 벽이면 리더 셀로 경로 목표 설정 (가까이만 가면 됨)
        if (!_mapManager.IsWalkable(targetCell))
            targetCell = _mapManager.WorldToCell(leaderPos);

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

        _rigidbody.linearVelocity = desiredVelocity;
        UpdateMovementAnimation(desiredVelocity);
    }

    private void UpdateMovementAnimation(Vector2 velocity)
    {
        if (_animator == null)
            return;

        bool hasVelocity = velocity.sqrMagnitude >= 0.01f;
        _animator.SetBool("Idle", !hasVelocity);
        _animator.SetBool("Walk", hasVelocity);
        _animator.SetBool("Run", false);

        if (hasVelocity && Mathf.Abs(velocity.x) >= 0.01f)
        {
            var scale = transform.localScale;
            if ((velocity.x > 0 && scale.x < 0) || (velocity.x < 0 && scale.x > 0))
                transform.localScale = new Vector3(-scale.x, scale.y, scale.z);
        }
    }

    /// <summary>포메이션 슬롯 (0=리더 바로 뒤). 앞쪽 동료 판정용.</summary>
    public int SlotIndex => _slotIndex;

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
    }

    /// <summary>포메이션 슬롯 설정 (0=리더 바로 뒤)</summary>
    public void SetSlotIndex(int slotIndex)
    {
        _slotIndex = Mathf.Max(0, slotIndex);
    }
}
