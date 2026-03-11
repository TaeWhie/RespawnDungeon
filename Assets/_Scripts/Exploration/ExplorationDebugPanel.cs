using UnityEngine;

/// <summary>
/// 탐험 관련 디버그 기능을 한 곳에서 모아 관리하는 패널입니다.
/// - Reveal Map (Dev): 시야/안개를 무시하고 전체 맵을 항상 보이게 토글
/// 에디터 / Development 빌드에서만 동작합니다.
/// </summary>
public class ExplorationDebugPanel : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private MapManager _mapManager;
    private ExplorationFogView _fogView;

    private void Awake()
    {
        CacheRefs();
    }

    private void CacheRefs()
    {
        if (_mapManager == null)
            _mapManager = FindFirstObjectByType<MapManager>();
        if (_fogView == null)
            _fogView = FindFirstObjectByType<ExplorationFogView>();
    }

    private void OnGUI()
    {
        if (_mapManager == null)
            return;

        const int width = 220;
        const int height = 90;
        var rect = new Rect(10, 10, width, height);

        GUILayout.BeginArea(rect, "Debug Tools", GUI.skin.window);

        GUILayout.Label("Exploration");

        // Reveal all fog
        bool revealAll = _mapManager.DebugRevealAll;
        bool newRevealAll = GUILayout.Toggle(revealAll, "Reveal Map (Dev)");
        if (newRevealAll != revealAll)
        {
            _mapManager.DebugRevealAll = newRevealAll;
            if (_fogView != null)
            {
                if (newRevealAll)
                    _fogView.DebugForceRevealAll();
                else
                    _fogView.DebugForceNormalFog();
            }
        }

        GUILayout.Space(5);

        // Gizmos
        bool drawGizmos = _mapManager.DrawGizmos;
        bool newDrawGizmos = GUILayout.Toggle(drawGizmos, "Draw Map Gizmos");
        if (newDrawGizmos != drawGizmos)
            _mapManager.DrawGizmos = newDrawGizmos;

        GUILayout.EndArea();
    }
#endif
}

