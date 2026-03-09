using UnityEngine;

namespace Ilumisoft.GameActionSystem
{
    [AddComponentMenu("Game Action System/Tasks/Game Object/Destroy GameObject (Task)")]
    public class DestroyGameObjectTask : GameActionTask
    {
        public GameObject target = null;

        protected override StatusCode OnExecute()
        {
            if (target != null)
            {
                Destroy(target);
            }

            return StatusCode.Completed;
        }

        public override string GetLabel()
        {
            if (target != null)
            {
                return $"Destroy {target.name}";
            }
            else
            {
                return $"Destroy GameObject";
            }
        }

        public override string GetDescription()
        {
            return "Destroys the target GameObject";
        }
    }
}