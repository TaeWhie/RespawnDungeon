using System.Collections.Generic;
using UnityEngine;
using TriInspector;

/// <summary>
/// 파티 전체를 화면에 담도록 카메라를 자동으로 위치/줌 조절합니다.
/// - Tag "Player" 또는 "Ally" 를 가진 오브젝트들을 파티원으로 간주합니다.
/// - 파티원의 Bounds 를 계산해 그 중심을 따라가고, OrthographicSize 를 조절해 모두 보이게 합니다.
/// </summary>
[RequireComponent(typeof(Camera))]
public class PartyCameraController : MonoBehaviour
{
    [Title("카메라 설정")]
    [Tooltip("파티원을 다시 스캔하는 주기(초). 0이면 매 프레임 스캔.")]
    [Slider(0f, 5f)]
    [SerializeField] private float _rescanInterval = 1f;

    [Tooltip("파티 주변에 둘 여백 (유닛). 값이 클수록 카메라가 더 멀어집니다.")]
    [Slider(0f, 5f)]
    [SerializeField] private float _padding = 1.5f;

    [Tooltip("카메라 이동 스무딩 속도.")]
    [Slider(0.1f, 20f)]
    [SerializeField] private float _moveSmoothTime = 5f;

    [Tooltip("카메라 줌(Orthographic Size) 스무딩 속도.")]
    [Slider(0.1f, 20f)]
    [SerializeField] private float _zoomSmoothTime = 5f;

    [Tooltip("카메라가 따라갈 최소/최대 줌 범위.")]
    [MinMaxSlider(2f, 20f)]
    [SerializeField] private Vector2 _zoomLimits = new Vector2(3f, 12f);

    private Camera _cam;
    private readonly List<Transform> _targets = new List<Transform>();
    private float _lastScanTime;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        if (_cam != null)
            _cam.orthographic = true;
        ScanTargetsNow();
    }

    private void LateUpdate()
    {
        if (_rescanInterval <= 0f || Time.time - _lastScanTime >= _rescanInterval)
            ScanTargetsNow();

        if (_targets.Count == 0)
            return;

        // Bounds 계산
        Vector3 firstPos = _targets[0].position;
        Bounds bounds = new Bounds(firstPos, Vector3.zero);
        for (int i = 1; i < _targets.Count; i++)
        {
            if (_targets[i] == null) continue;
            bounds.Encapsulate(_targets[i].position);
        }

        // 카메라 위치: 파티 중심을 따라감
        Vector3 targetPos = bounds.center;
        targetPos.z = transform.position.z;
        transform.position = Vector3.Lerp(transform.position, targetPos, _moveSmoothTime * Time.deltaTime);

        // 카메라 줌: 파티 Bounds 를 모두 포함하도록 Orthographic Size 조정
        if (_cam != null && _cam.orthographic)
        {
            float halfHeight = bounds.extents.y + _padding;
            float halfWidth = bounds.extents.x + _padding;
            float aspect = _cam.aspect > 0 ? _cam.aspect : 1f;

            float sizeForWidth = halfWidth / aspect;
            float desiredSize = Mathf.Max(halfHeight, sizeForWidth);
            desiredSize = Mathf.Clamp(desiredSize, _zoomLimits.x, _zoomLimits.y);

            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, desiredSize, _zoomSmoothTime * Time.deltaTime);
        }
    }

    private void ScanTargetsNow()
    {
        _targets.Clear();

        var players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var go in players)
        {
            if (go != null)
                _targets.Add(go.transform);
        }

        var allies = GameObject.FindGameObjectsWithTag("Ally");
        foreach (var go in allies)
        {
            if (go != null)
                _targets.Add(go.transform);
        }

        _lastScanTime = Time.time;
    }

    /// <summary>
    /// 현재 파티원을 즉시 스캔하고, 카메라 위치/줌을 바로 스냅합니다.
    /// 던전 생성 직후 한 번 호출하면, 시작 프레임부터 파티 전체가 화면에 들어옵니다.
    /// </summary>
    public void SnapToPartyImmediate()
    {
        ScanTargetsNow();
        if (_targets.Count == 0 || _cam == null || !_cam.orthographic)
            return;

        Vector3 firstPos = _targets[0].position;
        Bounds bounds = new Bounds(firstPos, Vector3.zero);
        for (int i = 1; i < _targets.Count; i++)
        {
            if (_targets[i] == null) continue;
            bounds.Encapsulate(_targets[i].position);
        }

        Vector3 center = bounds.center;
        center.z = transform.position.z;
        transform.position = center;

        float halfHeight = bounds.extents.y + _padding;
        float halfWidth = bounds.extents.x + _padding;
        float aspect = _cam.aspect > 0 ? _cam.aspect : 1f;
        float sizeForWidth = halfWidth / aspect;
        float desiredSize = Mathf.Max(halfHeight, sizeForWidth);
        desiredSize = Mathf.Clamp(desiredSize, _zoomLimits.x, _zoomLimits.y);

        _cam.orthographicSize = desiredSize;
    }
}

