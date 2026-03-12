using System;
using UnityEngine;
using TriInspector;

public class CameraFollow : MonoBehaviour
{
    [Title("따라가기 설정")]
    [SerializeField] private Transform target;
    [Tooltip("0에 가까울수록 부드럽게 따라감")]
    [Slider(0.01f, 1f)]
    [SerializeField] private float smoothSpeed = 0.125f;
    [SerializeField] private Vector3 offset = new Vector3(0, 0, -10);

    private void LateUpdate()
    {
        if (target == null)
        {
            // 인스펙터에서 target 미지정 시 "Player" 태그 오브젝트 사용
            var player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
            return;
        }

        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
