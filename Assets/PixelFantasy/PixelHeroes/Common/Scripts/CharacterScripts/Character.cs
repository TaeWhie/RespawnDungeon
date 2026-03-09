using Assets.PixelFantasy.Common.Scripts;
using Assets.PixelFantasy.Common.Scripts.CharacterScripts;
using UnityEngine;

namespace Assets.PixelFantasy.PixelHeroes.Common.Scripts.CharacterScripts
{
    /// <summary>
    /// The main character script.
    /// </summary>
    public class Character : Creature
    {
        public Firearm Firearm;

        #if UNITY_EDITOR

        public void OnValidate()
        {
            if (Application.isPlaying && Time.time > 1)
            {
                GetComponent<CharacterBuilderBase>().Rebuild();
            }
        }

        #endif
    }
}