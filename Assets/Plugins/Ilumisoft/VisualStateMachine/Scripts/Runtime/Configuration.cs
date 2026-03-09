using UnityEngine;

namespace Ilumisoft.VisualStateMachine
{
    public class Configuration : ScriptableObject
    {
        public const string ConfigurationPath = "Visual State Machine/Configuration";

        public TransitionMode TransitionMode;

        public static Configuration Find()
        {
            var result = Resources.Load<Configuration>(ConfigurationPath);

            return result;
        }
    }
}