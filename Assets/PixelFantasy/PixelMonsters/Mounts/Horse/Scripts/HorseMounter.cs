using System.Collections.Generic;
using Assets.PixelFantasy.PixelHeroes.Common.Scripts.CharacterScripts;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace Assets.PixelFantasy.PixelMonsters.Mounts.Horse.Scripts
{
    public class HorseMounter : MonoBehaviour
    {
        public SpriteLibrary SpriteLibrary;
        public SpriteLibraryAsset SpriteLibraryAsset;
        public Texture2D HorseTexture;
        public Character Character;

        public void Start()
        {
            if (Character) Mount(Character.GetComponent<CharacterBuilder>());
        }

        public void Mount(CharacterBuilder characterBuilder, string savePath = null)
        {
            var layers = characterBuilder.BuildLayers();
            var layout = CharacterBuilder.Layout;
            var spriteLibraryAsset = ScriptableObject.CreateInstance<SpriteLibraryAsset>();
            var pixels = HorseTexture.GetPixels32();
            var newTexture = new Texture2D(HorseTexture.width, HorseTexture.height) { filterMode = FilterMode.Point };
            var targets = new Dictionary<Vector2, string>
            {
                { new Vector2(0, 259), "Idle_0" }, { new Vector2(64, 259), "Idle_0" }, { new Vector2(128, 259), "Idle_1" }, { new Vector2(192, 259), "Idle_1" },
                { new Vector2(0, 196), "Ready_1" }, { new Vector2(64, 196), "Ready_0" }, { new Vector2(128, 195), "Ready_0" }, { new Vector2(192, 195), "Ready_1" }, { new Vector2(256, 195), "Ready_0" }, { new Vector2(320, 195), "Ready_1" },
                { new Vector2(0, 131), "Jab_0" }, { new Vector2(64, 131), "Jab_1" }, { new Vector2(128, 131), "Jab_2" },
                { new Vector2(0, 67), "Slash_0" }, { new Vector2(64, 67), "Slash_1" }, { new Vector2(128, 67), "Slash_2" }, { new Vector2(192, 67), "Slash_3" },
                { new Vector2(0, 2), "Jump_1" }, { new Vector2(64, 2), "Jump_1" }, { new Vector2(128, 1), "Jump_1" }, { new Vector2(192, -2), "Jump_1" }, { new Vector2(256, -2), "Jump_1" },
            };

            newTexture.SetPixels32(pixels);

            foreach (var target in targets)
            {
                var block = layout[target.Value];
                var frame = characterBuilder.Texture.GetPixels(block[0], block[1], block[2], block[3]);

                for (var x = 0; x < block[2]; x++)
                {
                    for (var y = 0; y < block[3]; y++)
                    {
                        var pixel = frame[x + y * block[2]];
                        var dx = (int)target.Key.x + x;
                        var dy = (int)target.Key.y + y;

                        if (pixel.a > 0)
                        {
                            if (x < 32 || pixels[dx + dy * HorseTexture.width].a == 0)
                            {
                                newTexture.SetPixel(dx, dy, pixel);
                            }
                        }

                        if (target.Value.Contains("Idle") || target.Value.Contains("Ready"))
                        {
                            Overlay("Weapon");
                        }

                        if (target.Value.Contains("Jab") || target.Value.Contains("Slash") || target.Value.Contains("Jump"))
                        {
                            if (!(target.Value == "Jab_0" && x > 32) && !(target.Value == "Slash_0" && x > 32) && !(target.Value == "Slash_1" && x > 32))
                            {
                                Overlay("Arms");
                                Overlay("Bracers");
                            }

                            Overlay("Weapon");
                        }

                        void Overlay(string layer)
                        {
                            if (layers.ContainsKey(layer))
                            {
                                var p = layers[layer][block[0] + x + (block[1] + y) * characterBuilder.Texture.width];

                                if (p.a > 0) newTexture.SetPixel(dx, dy, p);
                            }
                        }
                    }
                }
            }

            newTexture.Apply();

            foreach (var category in SpriteLibraryAsset.GetCategoryNames())
            {
                foreach (var label in SpriteLibraryAsset.GetCategoryLabelNames(category))
                {
                    var sprite = SpriteLibraryAsset.GetSprite(category, label);
                    var s = Sprite.Create(newTexture, sprite.rect, new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height), sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);

                    s.name = $"{category}_{label}";
                    spriteLibraryAsset.AddCategoryLabel(s, category, label);
                }
            }

            SpriteLibrary.spriteLibraryAsset = spriteLibraryAsset;

            #if !UNITY_WEBGL

            if (savePath != null)
            {
                var png = newTexture.EncodeToPNG();

                System.IO.File.WriteAllBytes(savePath, png);
            }

            #endif
        }
    }
}