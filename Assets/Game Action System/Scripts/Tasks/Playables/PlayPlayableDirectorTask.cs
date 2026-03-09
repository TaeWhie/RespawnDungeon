using UnityEngine;
using UnityEngine.Playables;

namespace Ilumisoft.GameActionSystem
{
    [AddComponentMenu("Game Action System/Tasks/Playables/Play Playable Director (Task)")]
    public class PlayPlayableDirectorTask : GameActionTask
    {
        public PlayableDirector playableDirector;

        protected override StatusCode OnExecute()
        {
            if (playableDirector != null)
            {
                playableDirector.Play();
            }

            return StatusCode.Completed;
        }

        public override string GetLabel()
        {
            return $"Play Playable Director";
        }
    }
}