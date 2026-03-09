using UnityEngine;

namespace Ilumisoft.GameActionSystem
{
    [AddComponentMenu("Game Action System/Tasks/Game Object/Toggle GameObjects Active State (Task)")]
    public class ToggletGameObjectsActiveStateTask : GameActionTask
    {
        public GameObject[] gameObjects = null;

        protected override StatusCode OnExecute()
        {
            foreach (var gameObject in gameObjects)
            {
                if (gameObject == null)
                {
                    continue;
                }

                gameObject.SetActive(!gameObject.activeSelf);
            }

            return StatusCode.Completed;
        }

        public override string GetLabel()
        {
            return $"Toggle GameObjects active state";
        }
    }
}