using UnityEngine;
using UniRx;

/// <summary>
/// 탐험 관련 디버그 기능을 한 곳에서 모아 관리하는 패널입니다.
/// - Reveal Map (Dev): 시야/안개를 무시하고 전체 맵을 항상 보이게 토글
/// - Simulation Speed: Time.timeScale로 게임 재생 속도 조절 (빠른 시뮬레이션용)
/// 에디터 / Development 빌드에서만 동작합니다.
/// </summary>
public class ExplorationDebugPanel : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private MapManager _mapManager;
    private ExplorationFogView _fogView;
    private ExplorerAI _explorerAI;
    private Transform _playerTransform;
    private bool _showAdventurousness;
    private bool _showFogStageNumbers;
    private int _fogStageNumbersRadius = 12;
    private readonly CompositeDisposable _subscriptions = new CompositeDisposable();

    private const float SIM_SPEED_MIN = 0.25f;
    private const float SIM_SPEED_MAX = 8f;

    private void Awake()
    {
        CacheRefs();
    }

    private void OnEnable()
    {
        SetupSubscriptions();
    }

    private void OnDisable()
    {
        _subscriptions.Clear();
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

    private void SetupSubscriptions()
    {
        _subscriptions.Clear();
        Observable.Interval(System.TimeSpan.FromMilliseconds(300))
            .StartWith(0L)
            .Subscribe(_ => TryCachePlayer())
            .AddTo(_subscriptions);
    }

    private void TryCachePlayer()
    {
        if (_playerTransform != null)
            return;

        ExplorationPartyCache.RefreshIfStale(0.3f);
        _playerTransform = ExplorationPartyCache.Leader;
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

        GUILayout.Space(3);

        // 안개 단계 숫자 표시 (1=풀뷰, 2=구조만, 3=미탐험)
        _showFogStageNumbers = GUILayout.Toggle(_showFogStageNumbers, "Show Fog Stage (1/2/3)");
        if (_showFogStageNumbers)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Radius:", GUILayout.Width(40));
            int r = (int)GUILayout.HorizontalSlider(_fogStageNumbersRadius, 5, 25, GUILayout.Width(80));
            GUILayout.Label(r.ToString(), GUILayout.Width(20));
            GUILayout.EndHorizontal();
            _fogStageNumbersRadius = Mathf.Clamp(r, 5, 25);
        }

        GUILayout.Space(5);

        // Simulation Speed (빠른 시뮬레이션용)
        GUILayout.Label("Simulation Speed");
        GUILayout.BeginHorizontal();
        float currentScale = Time.timeScale;
        float newScale = GUILayout.HorizontalSlider(currentScale, SIM_SPEED_MIN, SIM_SPEED_MAX, GUILayout.Width(120));
        GUILayout.Label($"{newScale:F2}x", GUILayout.Width(36));
        GUILayout.EndHorizontal();
        if (Mathf.Abs(newScale - currentScale) > 0.01f)
            Time.timeScale = Mathf.Clamp(newScale, SIM_SPEED_MIN, SIM_SPEED_MAX);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("0.5x", GUILayout.Width(40))) Time.timeScale = 0.5f;
        if (GUILayout.Button("1x", GUILayout.Width(40))) Time.timeScale = 1f;
        if (GUILayout.Button("2x", GUILayout.Width(40))) Time.timeScale = 2f;
        if (GUILayout.Button("4x", GUILayout.Width(40))) Time.timeScale = 4f;
        if (GUILayout.Button("8x", GUILayout.Width(40))) Time.timeScale = 8f;
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        // Adventurousness
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

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!_showFogStageNumbers || _mapManager == null || !_mapManager.IsInitialized)
            return;

        if (_playerTransform == null)
            TryCachePlayer();

        Vector2Int centerCell = _playerTransform != null
            ? _mapManager.WorldToCell(_playerTransform.position)
            : new Vector2Int(_mapManager.MinX + _mapManager.Width / 2, _mapManager.MinY + _mapManager.Height / 2);

        int r = _fogStageNumbersRadius;
        int minX = centerCell.x - r, maxX = centerCell.x + r;
        int minY = centerCell.y - r, maxY = centerCell.y + r;

        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY; y++)
        {
            var cell = new Vector2Int(x, y);
            if (!_mapManager.CellToIndex(cell, out _, out _))
                continue;

            int stage = _mapManager.GetFogStage(cell);
            Vector3 worldPos = _mapManager.CellToWorld(cell);

            // 단계별 색: 1=초록, 2=노랑, 3=빨강
            switch (stage)
            {
                case 1: UnityEditor.Handles.color = Color.green; break;
                case 2: UnityEditor.Handles.color = Color.yellow; break;
                default: UnityEditor.Handles.color = Color.red; break;
            }
            UnityEditor.Handles.Label(worldPos + Vector3.up * 0.3f, stage.ToString());
        }
    }
#endif

    private void OnDestroy()
    {
        Time.timeScale = 1f;
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

