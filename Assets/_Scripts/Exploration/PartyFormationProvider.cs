using UnityEngine;
using TriInspector;

/// <summary>
/// 리더에 부착. 리더가 **새 셀에 들어설 때만** 슬롯별 포메이션 목표를 계산해 둡니다.
/// 동료는 그 좌표를 한 번 정해주면 계속 그대로 사용합니다.
/// </summary>
public class PartyFormationProvider : MonoBehaviour
{
    [Tooltip("리더와의 유지 거리 (월드 단위). 동료 포메이션 파라미터와 맞추는 것이 좋음")]
    [Slider(0.1f, 5f)]
    [SerializeField] private float _followDistance = 1f;
    [Tooltip("맨앞 동료(슬롯0)가 리더를 따르는 거리. 0이면 _followDistance 사용")]
    [Slider(0f, 3f)]
    [SerializeField] private float _firstFollowerDistance = 0.5f;
    [Tooltip("리더가 멈춰 있을 때 사용할 기본 방향 (월드). 예: (0,-1)=아래쪽 뒤")]
    [SerializeField] private Vector2 _idleFormationDirection = new Vector2(0f, -1f);
    [Tooltip("동료끼리 겹치지 않도록 포메이션 직선의 좌우로 번갈아 밀어내는 거리")]
    [Slider(0f, 2f)]
    [SerializeField] private float _formationSpread = 0.35f;
    [Tooltip("슬롯별 거리 미세 변동 (0이면 균일 간격)")]
    [Slider(0f, 0.2f)]
    [SerializeField] private float _distanceVariation = 0.05f;
    [Tooltip("계산할 슬롯 개수 상한 (슬롯 0 ~ 이 값-1)")]
    [Min(1)]
    [SerializeField] private int _maxSlots = 8;
    [Tooltip("목적지 셀을 슬롯마다 옆/대각선으로 비틀어서 경로가 일자로 가지 않게 함. 0이면 비틀기 없음")]
    [Slider(0, 2)]
    [SerializeField] private int _formationCellJitter = 1;

    [Tooltip("비워두면 씬에서 자동 검색")]
    [SerializeField] private MapManager _mapManager;

    private Vector2 _lastPosition;
    private Vector2 _formationDirection;
    private bool _hasStoredDirection;
    private Vector2[] _targetBySlot;
    private Vector2Int[] _targetCellBySlot;
    private Vector2Int _lastLeaderCell = new Vector2Int(int.MinValue, int.MinValue);

    /// <summary>현재 포메이션 방향 (리더 이동 방향 반대 = 뒤쪽).</summary>
    public Vector2 FormationDirection => _formationDirection;

    /// <summary>지정한 슬롯의 포메이션 목표 월드 좌표 (비틀기 적용 후). 슬롯이 범위 밖이면 리더 위치 반환.</summary>
    public Vector2 GetFormationTarget(int slotIndex)
    {
        if (_targetBySlot == null || slotIndex < 0 || slotIndex >= _targetBySlot.Length)
            return transform != null ? (Vector2)transform.position : Vector2.zero;
        return _targetBySlot[slotIndex];
    }

    /// <summary>지정한 슬롯의 포메이션 목표 셀 (비틀기 적용 후). 동료가 겹침 판정 등에 사용.</summary>
    public Vector2Int GetFormationTargetCell(int slotIndex)
    {
        if (_targetCellBySlot == null || slotIndex < 0 || slotIndex >= _targetCellBySlot.Length)
            return _mapManager != null ? _mapManager.WorldToCell(transform != null ? transform.position : Vector3.zero) : Vector2Int.zero;
        return _targetCellBySlot[slotIndex];
    }

    private void Awake()
    {
        _targetBySlot = new Vector2[_maxSlots];
        _targetCellBySlot = new Vector2Int[_maxSlots];
        _lastPosition = transform != null ? (Vector2)transform.position : Vector2.zero;
        _formationDirection = _idleFormationDirection.normalized;
        if (_mapManager == null) _mapManager = FindFirstObjectByType<MapManager>();
    }

    private void FixedUpdate()
    {
        Vector2 pos = transform != null ? (Vector2)transform.position : Vector2.zero;
        if (_mapManager == null || !_mapManager.IsInitialized)
            return;

        Vector2Int leaderCell = _mapManager.WorldToCell(pos);

        // 리더가 새 셀에 들어설 때만 포메이션 갱신 (한 번 정해준 좌표 유지)
        if (leaderCell == _lastLeaderCell)
            return;

        bool isFirstUpdate = (_lastLeaderCell.x == int.MinValue && _lastLeaderCell.y == int.MinValue);
        // 한 칸 이동이면 좌표 갱신 안 함 (첫 설정 시에는 갱신)
        if (!isFirstUpdate && CellDistance(leaderCell, _lastLeaderCell) <= 1)
            return;

        _lastLeaderCell = leaderCell;

        // 리더 이동 방향 갱신 (포메이션 라인 방향용)
        Vector2 delta = pos - _lastPosition;
        if (delta.sqrMagnitude >= 0.0001f)
        {
            _formationDirection = delta.normalized;
            _hasStoredDirection = true;
        }
        _lastPosition = pos;

        Vector2 dir = _hasStoredDirection ? _formationDirection : _idleFormationDirection.normalized;
        if (dir.sqrMagnitude < 0.01f)
            dir = _idleFormationDirection.normalized;

        float firstDist = _firstFollowerDistance > 0f ? _firstFollowerDistance : _followDistance;
        Vector2 perp = new Vector2(dir.y, -dir.x);

        for (int i = 0; i < _targetBySlot.Length; i++)
        {
            float totalDist = (i == 0) ? firstDist : firstDist + _followDistance * i;
            if (i > 0 && _firstFollowerDistance > 0f)
                totalDist = _firstFollowerDistance + _followDistance * i;

            if (_distanceVariation > 0f)
                totalDist += _distanceVariation * ((i % 3) - 1);

            Vector2 target = pos - dir * totalDist;

            if (_formationSpread > 0f)
            {
                float offset = Mathf.Sin(i * 0.85f) * _formationSpread;
                target += perp * offset;
            }

            _targetBySlot[i] = target;
        }

        // 비틀기 적용: 슬롯 순서대로 셀 지터 후 최종 좌표 저장
        if (_formationCellJitter > 0)
        {
            Vector2 forwardDir = dir;
            var offsets = new[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
                new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1)
            };

            for (int i = 0; i < _targetBySlot.Length; i++)
            {
                Vector2Int targetCell = _mapManager.WorldToCell(_targetBySlot[i]);
                Vector2Int leaderOrFrontCell = (i == 0) ? leaderCell : _targetCellBySlot[i - 1];

                int sign = (i % 2 == 0) ? 1 : -1;
                foreach (var o in offsets)
                {
                    Vector2Int jitterDelta = o * (sign * _formationCellJitter);
                    if (Vector2.Dot((Vector2)jitterDelta, forwardDir) < 0f) continue;
                    Vector2Int jitteredCell = targetCell + jitterDelta;
                    if (_mapManager.IsWalkable(jitteredCell) && jitteredCell != leaderOrFrontCell)
                    {
                        bool occupiedByEarlier = false;
                        for (int k = 0; k < i; k++)
                        {
                            if (_targetCellBySlot[k] == jitteredCell)
                            {
                                occupiedByEarlier = true;
                                break;
                            }
                        }
                        if (!occupiedByEarlier)
                        {
                            targetCell = jitteredCell;
                            break;
                        }
                    }
                }

                if (!_mapManager.IsWalkable(targetCell))
                    targetCell = leaderOrFrontCell;
                if (targetCell == leaderOrFrontCell && _mapManager.GetWalkableNeighbors(leaderOrFrontCell) is var neighbors && neighbors != null && neighbors.Count > 0)
                {
                    Vector2 behindDir = -forwardDir;
                    Vector2Int behindCell = leaderOrFrontCell + new Vector2Int(Mathf.RoundToInt(behindDir.x), Mathf.RoundToInt(behindDir.y));
                    targetCell = neighbors.Contains(behindCell) ? behindCell : neighbors[0];
                }

                _targetCellBySlot[i] = targetCell;
                _targetBySlot[i] = _mapManager.CellToWorld(targetCell);
            }
        }
        else if (_targetCellBySlot != null)
        {
            for (int i = 0; i < _targetBySlot.Length; i++)
                _targetCellBySlot[i] = _mapManager != null ? _mapManager.WorldToCell(_targetBySlot[i]) : Vector2Int.zero;
        }
    }

    private static int CellDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
    }
}
