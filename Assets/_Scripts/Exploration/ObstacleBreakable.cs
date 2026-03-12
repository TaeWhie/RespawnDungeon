using UnityEngine;
using TriInspector;

/// <summary>
/// 장애물이 픽(부수기) 당할 때 해당 셀을 다시 이동 가능으로 복구한 뒤 Destroy 애니 재생 후 오브젝트를 제거합니다.
/// </summary>
public class ObstacleBreakable : MonoBehaviour
{
    [Tooltip("부수기 연출 후 제거까지 대기 시간(초). Destroy 애니 길이에 맞추세요.")]
    [Slider(0.1f, 5f)]
    [SerializeField] private float _breakThenDestroyDelay = 1f;

    [Tooltip("비워두면 GetComponent/GetComponentInChildren으로 자동 검색")]
    [SerializeField] private Animator _animator;

    private Vector2Int _obstacleCell;
    private MapManager _mapManager;

    private void Awake()
    {
        CacheAnimator();
    }

    private void CacheAnimator()
    {
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
    }

    /// <summary>배치된 셀 좌표 설정 (TilemapVisualizer에서 호출). 부수기 시 해당 셀을 다시 walkable로 복구하는 데 사용.</summary>
    public void SetObstacleCell(Vector2Int cell)
    {
        _obstacleCell = cell;
    }

    /// <summary>부수기 처리: 셀을 이동 가능으로 복구, Destroy 애니 재생 후 일정 시간 뒤 오브젝트 제거.</summary>
    public void Break()
    {
        if (_mapManager == null) _mapManager = FindFirstObjectByType<MapManager>();
        if (_mapManager != null)
            _mapManager.SetCellWalkable(_obstacleCell, true);

        foreach (var col in GetComponentsInChildren<Collider2D>(true))
            col.enabled = false;

        if (_animator == null) CacheAnimator();
        if (_animator != null)
        {
            _animator.SetBool("Idle", false);
            _animator.SetBool("Destroy", true);
        }

        Destroy(gameObject, _breakThenDestroyDelay);
    }
}
