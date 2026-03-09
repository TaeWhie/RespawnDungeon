using UnityEngine;
using UnityEngine.Events;

namespace Ilumisoft.GameActionSystem
{
    [AddComponentMenu("Game Action System/Tasks/Miscellaneous/Counter (Task)")]
    public class CounterTask : GameActionTask
    {
        [Min(1)]
        public int targetCount = 3;

        int count = 0;

        public UnityEvent OnTargetReached = null;

        protected override StatusCode OnExecute()
        {
            // Increase the count
            count++;

            // If target is reached, trigger the trigger
            if(count == targetCount)
            {
                OnTargetReached?.Invoke();
            }

            return StatusCode.Completed;
        }

        public override string GetLabel()
        {
            return $"Count invokations until {targetCount}";
        }

        public override string GetDescription()
        {
            return "Counts how often the action is executed and invokes an event when the target count is reached.";
        }
    }
}