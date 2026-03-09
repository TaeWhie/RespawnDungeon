using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace Assets.PixelFantasy.Common.Scripts.CharacterScripts
{
    public class SpriteLibraryAssetDumper : MonoBehaviour
    {
        public string Output;

        public void OnEnable()
        {
            var spriteLibraryAsset = GetComponentInChildren<SpriteLibrary>().spriteLibraryAsset;
            var categoryNames = spriteLibraryAsset.GetCategoryNames();
            var data = new Dictionary<string, int[]>();

            foreach (var category in categoryNames)
            {
                var labels = spriteLibraryAsset.GetCategoryLabelNames(category);

                foreach (var label in labels)
                {
                    var sprite = spriteLibraryAsset.GetSprite(category, label);

                    data.Add(category + "_" + label, new[] { (int)sprite.rect.x, (int)sprite.rect.y, (int)sprite.rect.width, (int)sprite.rect.height, (int)sprite.pivot.x, (int)sprite.pivot.y });
                }
            }

            Output = $"new() {{ {string.Join(',', data.Select(i => $"{{ \"{i.Key}\", new[] {{ {string.Join(',', i.Value)} }} }}"))} }};";
        }
    }
}