using UnityEngine;
using TriInspector;

/// <summary>
/// 2단계 시야(구조만)에서는 렌더러만 끄고, 1단계 시야(전부 보임)에서만 표시합니다.
/// 3단계(미탐험)에서는 ExplorationFogView가 오브젝트를 SetActive(false)로 끔.
/// </summary>
public class VisibilityByViewStage : MonoBehaviour
{
    [Title("참조")]
    [Tooltip("비워두면 씬에서 자동 검색")]
    [SerializeField] private MapManager _mapManager;

    private Renderer[] _renderers;
    private bool _lastVisible = true;

    private void Start()
    {
        if (_mapManager == null)
            _mapManager = FindFirstObjectByType<MapManager>();
        _renderers = GetComponentsInChildren<Renderer>(true);
        _lastVisible = false;
        UpdateVisibility();
    }

    private void Update()
    {
        if (_mapManager == null || !_mapManager.IsInitialized || _renderers == null)
            return;
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        if (_mapManager == null || !_mapManager.IsInitialized || _renderers == null)
            return;

        Vector2Int cell = _mapManager.WorldToCell(transform.position);
        // 1단계에서만 보이거나, 한 번 1단계로 밝혀진 셀은 2단계에서도 계속 표시
        bool visible = _mapManager.DebugRevealAll || _mapManager.IsInFullView(cell);

        if (visible == _lastVisible)
            return;
        _lastVisible = visible;

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
                _renderers[i].enabled = visible;
        }
    }
}
