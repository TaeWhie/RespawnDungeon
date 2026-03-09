using UnityEngine;
using UnityEngine.Events;

namespace Ilumisoft.GameActionSystem
{
    [AddComponentMenu("Game Action System/Game Events/On Trigger Event")]
    public class OnTriggerEvent : MonoBehaviour
    {
        [SerializeField]
        LayerMask layers = -1;

        [SerializeField]
        UnityEvent onEnter = new();

        [SerializeField]
        UnityEvent onExit = new();

        private void OnTriggerEnter(Collider other)
        {
            if ((layers.value & 1 << other.gameObject.layer) != 0)
            {
                onEnter?.Invoke();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if ((layers.value & 1 << other.gameObject.layer) != 0)
            {
                onExit?.Invoke();
            }
        }

        private void Reset()
        {
            // Log a warning if there is a collider component, but it is not set to trigger
            if (TryGetComponent<Collider>(out var collider))
            {
                if (!collider.isTrigger)
                {
                    Debug.LogWarning("OnTriggerEvent: The attached collider component is not a trigger. Please enable 'Is Trigger'.", this);
                }
            }
            else
            {
                // Automatically add a sphere trigger if no collider has already been added
                gameObject.AddComponent<SphereCollider>().isTrigger = true;
            }
        }
    }
}