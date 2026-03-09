using UnityEngine;

namespace Ilumisoft.GameActionSystem
{
    [AddComponentMenu("Game Action System/Tasks/Audio/Play Audio Sources (Task)")]
    public class PlayAudioSourcesTask : GameActionTask
    {
        public AudioSource[] audioSources;

        protected override StatusCode OnExecute()
        {
            foreach (var audioSource in audioSources)
            {
                audioSource.Play();
            }

            return StatusCode.Completed;
        }

        public override string GetLabel()
        {
            return $"Play Audio Sources";
        }
    }
}