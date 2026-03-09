using UnityEngine;
using UnityEngine.Playables;

namespace Ilumisoft.GameActionSystem
{
    [AddComponentMenu("Game Action System/Tasks/Playables/Stop Playable Director (Task)")]
    public class StopPlayableDirectorTask : GameActionTask
    {
        public PlayableDirector playableDirector;

        protected override StatusCode OnExecute()
        {
            if (playableDirector != null)
            {
                playableDirector.Stop();
            }

            return StatusCode.Completed;
        }

        public override string GetLabel()
        {
            return $"Stop Playable Director";
        }
    }
}