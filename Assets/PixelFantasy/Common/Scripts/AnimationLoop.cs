using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.PixelFantasy.Common.Scripts
{
    public class AnimationLoop : MonoBehaviour
    {
        public SpriteRenderer SpriteRenderer;
        public List<Sprite> Sprites;
        public float Interval;

        public IEnumerator Start()
        {
            var index = 0;

            while (true)
            {
                SpriteRenderer.sprite = Sprites[index];
                index++;

                if (index == Sprites.Count) index = 0;

                yield return new WaitForSeconds(Interval);
            }
        }
    }
}
