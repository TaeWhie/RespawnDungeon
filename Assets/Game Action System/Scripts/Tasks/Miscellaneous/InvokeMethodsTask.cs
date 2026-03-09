using UnityEngine;
using UnityEngine.Events;

namespace Ilumisoft.GameActionSystem
{
    [AddComponentMenu("Game Action System/Tasks/Miscellaneous/Invoke Methods (Task)")]
    public class InvokeMethodsTask : GameActionTask
    {
        public UnityEvent methods = new UnityEvent();

        protected override StatusCode OnExecute()
        {
            methods?.Invoke();

            return StatusCode.Completed;
        }

        public override string GetLabel()
        {
            return $"Invoke Methods";
        }
    }
}