using UnityEngine;

namespace Ilumisoft.VisualStateMachine
{
    [System.Serializable]
    public enum TransitionMode
    {
        Default = 0,
        Locked = 1,
    }

    [AddComponentMenu("")]
    public class StateMachineManager : MonoBehaviour
    {
        public static StateMachineManager Instance;

        public Configuration Configuration;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void InitializeOnLoad()
        {
            var container = new GameObject("State Machine Manager");
            var manager = container.AddComponent<StateMachineManager>();

            manager.LoadSettings();

            DontDestroyOnLoad(container);

            Instance = manager;

            container.hideFlags = HideFlags.HideInHierarchy;
        }

        private void OnDestroy()
        {
            Instance = null;
        }

        void LoadSettings()
        {
            var config = Configuration.Find();

            if (config == null)
            {
                config = ScriptableObject.CreateInstance<Configuration>();
            }

            Configuration = config;
        }
    }
}