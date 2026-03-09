using UnityEngine;

namespace Ilumisoft.GameActionSystem
{
    [AddComponentMenu("Game Action System/Tasks/Audio/Stop Audio Sources (Task)")]
    public class StopAudioSourcesTask : GameActionTask
    {
        public AudioSource[] audioSources;

        protected override StatusCode OnExecute()
        {
            foreach (var audioSource in audioSources)
            {
                audioSource.Stop();
            }

            return StatusCode.Completed;
        }

        public override string GetLabel()
        {
            return $"Stop Audio Sources";
        }
    }
}