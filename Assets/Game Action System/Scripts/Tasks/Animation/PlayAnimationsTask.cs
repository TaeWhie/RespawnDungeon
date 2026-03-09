using UnityEngine;

namespace Ilumisoft.GameActionSystem
{
    [AddComponentMenu("Game Action System/Tasks/Animation/Play Animations (Task)")]
    public class PlayAnimationsTask : GameActionTask
    {
        public Animation[] animatons;

        protected override StatusCode OnExecute()
        {
            foreach(var animaton in animatons) 
            {
                animaton.Play();
            }

            return StatusCode.Completed;
        }

        public override string GetLabel()
        {
            return $"Play Animations";
        }
    }
}