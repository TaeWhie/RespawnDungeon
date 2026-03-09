using UnityEngine;

namespace Ilumisoft.GameActionSystem
{
    [System.Serializable]
    public enum GameObjectTargetState
    {
        Active,
        Inactive
    }

    [AddComponentMenu("Game Action System/Tasks/Game Object/Set GameObjects Active (Task)")]
    public class SetGameObjectsActiveTask : GameActionTask
    {
        public GameObject[] gameObjects = null;

        public GameObjectTargetState targetState;

        protected override StatusCode OnExecute()
        {
            foreach (var gameObject in gameObjects)
            {
                if (gameObject == null)
                {
                    continue;
                }

                switch (targetState)
                {
                    case GameObjectTargetState.Active:
                        gameObject.SetActive(true);
                        break;
                    case GameObjectTargetState.Inactive:
                        gameObject.SetActive(false);
                        break;
                }
            }

            return StatusCode.Completed;
        }

        public override string GetLabel()
        {
            return $"Set GameObjects {targetState}";
        }
    }
}