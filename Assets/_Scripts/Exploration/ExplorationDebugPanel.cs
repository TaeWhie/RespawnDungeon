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
    private ExplorerAI _explorerAI;
    private bool _showAdventurousness;

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
        if (_explorerAI == null)
            _explorerAI = FindFirstObjectByType<ExplorerAI>();
    }

    private void OnGUI()
    {
        if (_mapManager == null)
            return;
        if (_explorerAI == null)
            CacheRefs();

        const int width = 260;
        const int height = 300;
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

        GUILayout.Space(5);

        // Adventurousness (토글 펼치면 모험심 슬라이더 + 점수 가중치)
        _showAdventurousness = GUILayout.Toggle(_showAdventurousness, "Adventurousness");
        if (_showAdventurousness && _explorerAI != null)
        {
            GUILayout.Space(3);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{_explorerAI.Adventurousness:F2}", GUILayout.Width(36));
            float newAdv = GUILayout.HorizontalSlider(_explorerAI.Adventurousness, 0f, 1f);
            GUILayout.EndHorizontal();
            if (Mathf.Abs(newAdv - _explorerAI.Adventurousness) > 0.001f)
                _explorerAI.Adventurousness = newAdv;

            GUILayout.Space(3);
            GUILayout.Label("Score Weights (Adv0 / Adv1)");
            SliderWeight("Bright Adv0", () => _explorerAI.ScoreBrightWeightAdv0, v => _explorerAI.ScoreBrightWeightAdv0 = v, 0f, 20f);
            SliderWeight("Dist Adv0", () => _explorerAI.ScoreDistWeightAdv0, v => _explorerAI.ScoreDistWeightAdv0 = v, 0f, 20f);
            SliderWeight("Bright Adv1", () => _explorerAI.ScoreBrightWeightAdv1, v => _explorerAI.ScoreBrightWeightAdv1 = v, 0f, 20f);
            SliderWeight("Dist Adv1", () => _explorerAI.ScoreDistWeightAdv1, v => _explorerAI.ScoreDistWeightAdv1 = v, 0f, 20f);
            SliderWeight("TotalBright Adv1", () => _explorerAI.ScoreTotalBrightWeightAdv1, v => _explorerAI.ScoreTotalBrightWeightAdv1 = v, 0f, 2f);
            GUILayout.EndVertical();
        }

        GUILayout.EndArea();
    }

    private void SliderWeight(string label, System.Func<float> getVal, System.Action<float> setVal, float min, float max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(110));
        float current = getVal();
        float v = GUILayout.HorizontalSlider(current, min, max, GUILayout.Width(100));
        GUILayout.Label(v.ToString("F1"), GUILayout.Width(28));
        GUILayout.EndHorizontal();
        if (Mathf.Abs(v - current) > 0.001f)
            setVal(v);
    }
#endif
}

