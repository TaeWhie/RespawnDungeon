using UnityEngine;
using TriInspector;

/// <summary>
/// 보물상자 픽 시 Open 애니메이션 재생 후 오브젝트를 제거합니다.
/// Animator에 "Open" Bool을 두고 true로 설정한 뒤, 재생 시간 후 Destroy합니다.
/// 상자 제거 시 해당 셀을 MapManager에서 다시 이동 가능(floor)으로 복구합니다.
/// </summary>
public class ChestOpenable : MonoBehaviour
{
    [Title("상자 설정")]
    [Tooltip("Open 애니 재생 후 제거까지 대기 시간(초). Animator Open 클립 길이에 맞추세요.")]
    [Slider(0.1f, 5f)]
    [SerializeField] private float _openThenDestroyDelay = 1.2f;

    [SerializeField] private Animator _animator;

    private Vector2Int _chestCell;
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

    /// <summary>배치된 셀 좌표 설정 (TilemapVisualizer에서 호출). 상자 제거 시 해당 셀을 다시 walkable로 복구하는 데 사용.</summary>
    public void SetChestCell(Vector2Int cell)
    {
        _chestCell = cell;
    }

    /// <summary>Open 트리거 재생 후 일정 시간 뒤 오브젝트 제거. 해당 셀은 다시 이동 가능 처리.</summary>
    public void Open()
    {
        if (_mapManager == null) _mapManager = FindFirstObjectByType<MapManager>();
        if (_mapManager != null)
            _mapManager.SetCellWalkable(_chestCell, true);

        if (_animator == null) CacheAnimator();
        if (_animator != null)
        {
            _animator.SetBool("Idle", false);
            _animator.SetBool("Open", true);
        }
        Destroy(gameObject, _openThenDestroyDelay);
    }
}
