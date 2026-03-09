using UnityEngine;

namespace Ilumisoft.GameActionSystem
{
    [AddComponentMenu("Game Action System/Tasks/Animation/Set Animator Trigger (Task)")]
    public class SetAnimatorTriggerTask : GameActionTask
    {
        public Animator animator;

        public string triggerName;

        protected override StatusCode OnExecute()
        {
            if (animator != null)
            {
                animator.SetTrigger(triggerName);
            }

            return StatusCode.Completed;
        }

        public override string GetLabel()
        {
            return $"Set Animator Trigger '{triggerName}'";
        }
    }
}