using UnityEngine;
using TriInspector;
using UniRx;

public class CameraFollow : MonoBehaviour
{
    [Title("따라가기 설정")]
    [SerializeField] private Transform target;
    [Tooltip("0에 가까울수록 부드럽게 따라감")]
    [Slider(0.01f, 1f)]
    [SerializeField] private float smoothSpeed = 0.125f;
    [SerializeField] private Vector3 offset = new Vector3(0, 0, -10);
    private readonly CompositeDisposable _subscriptions = new CompositeDisposable();

    private void OnEnable()
    {
        _subscriptions.Clear();

        // target 미지정 상태에서는 주기적으로만 플레이어를 찾음 (매 프레임 Find 비용 절감)
        Observable.Interval(System.TimeSpan.FromMilliseconds(300))
            .StartWith(0L)
            .Where(_ => target == null)
            .Subscribe(_ => TryFindPlayerTarget())
            .AddTo(_subscriptions);
    }

    private void OnDisable()
    {
        _subscriptions.Clear();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;
    }

    private void TryFindPlayerTarget()
    {
        var player = GameObject.FindWithTag("Player");
        if (player != null)
            target = player.transform;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
