using System.Collections.Generic;
using System.Linq;
using Assets.PixelFantasy.Common.Scripts.CharacterScripts;
using Assets.PixelFantasy.Common.Scripts.Utils;
using UnityEngine;
using UnityEngine.U2D.Animation;


namespace Assets.PixelFantasy.PixelHeroes.Common.Scripts.CharacterScripts
{
    public class CharacterBuilder : CharacterBuilderBase
    {
        public Character Character;

        private const int Width = 576;
        private const int Height = 928;
        public static readonly Dictionary<string, int[]> Layout = new() { { "Roll_0", new[] { 0, 0, 64, 64, 32, 8 } }, { "Roll_1", new[] { 64, 0, 64, 64, 32, 8 } }, { "Roll_2", new[] { 128, 0, 64, 64, 32, 8 } }, { "Roll_3", new[] { 192, 0, 64, 64, 32, 8 } }, { "Roll_4", new[] { 256, 0, 64, 64, 32, 8 } }, { "Roll_5", new[] { 320, 0, 64, 64, 32, 8 } }, { "Roll_6", new[] { 384, 0, 64, 64, 32, 8 } }, { "Roll_7", new[] { 448, 0, 64, 64, 32, 8 } }, { "Roll_8", new[] { 512, 0, 64, 64, 32, 8 } }, { "Death_0", new[] { 0, 64, 64, 64, 32, 8 } }, { "Death_1", new[] { 64, 64, 64, 64, 32, 8 } }, { "Death_2", new[] { 128, 64, 64, 64, 32, 8 } }, { "Death_3", new[] { 192, 64, 64, 64, 32, 8 } }, { "Death_4", new[] { 256, 64, 64, 64, 32, 8 } }, { "Death_5", new[] { 320, 64, 64, 64, 32, 8 } }, { "Death_6", new[] { 384, 64, 64, 64, 32, 8 } }, { "Death_7", new[] { 448, 64, 64, 64, 32, 8 } }, { "Death_8", new[] { 512, 64, 64, 64, 32, 8 } }, { "Block_0", new[] { 0, 128, 64, 64, 32, 8 } }, { "Block_1", new[] { 64, 128, 64, 64, 32, 8 } }, { "Block_2", new[] { 128, 128, 64, 64, 32, 8 } }, { "Block_3", new[] { 192, 128, 64, 64, 32, 8 } }, { "Block_4", new[] { 256, 128, 64, 64, 32, 8 } }, { "Block_5", new[] { 320, 128, 64, 64, 32, 8 } }, { "Block_6", new[] { 384, 128, 64, 64, 32, 8 } }, { "Block_7", new[] { 448, 128, 64, 64, 32, 8 } }, { "Block_8", new[] { 512, 128, 64, 64, 32, 8 } }, { "Fire_0", new[] { 0, 192, 64, 64, 32, 8 } }, { "Fire_1", new[] { 64, 192, 64, 64, 32, 8 } }, { "Fire_2", new[] { 128, 192, 64, 64, 32, 8 } }, { "Fire_3", new[] { 192, 192, 64, 64, 32, 8 } }, { "Fire_4", new[] { 256, 192, 64, 64, 32, 8 } }, { "Fire_5", new[] { 320, 192, 64, 64, 32, 8 } }, { "Fire_6", new[] { 384, 192, 64, 64, 32, 8 } }, { "Fire_7", new[] { 448, 192, 64, 64, 32, 8 } }, { "Fire_8", new[] { 512, 192, 64, 64, 32, 8 } }, { "Shot_0", new[] { 0, 256, 64, 64, 32, 8 } }, { "Shot_1", new[] { 64, 256, 64, 64, 32, 8 } }, { "Shot_2", new[] { 128, 256, 64, 64, 32, 8 } }, { "Shot_3", new[] { 192, 256, 64, 64, 32, 8 } }, { "Shot_4", new[] { 256, 256, 64, 64, 32, 8 } }, { "Shot_5", new[] { 320, 256, 64, 64, 32, 8 } }, { "Shot_6", new[] { 384, 256, 64, 64, 32, 8 } }, { "Shot_7", new[] { 448, 256, 64, 64, 32, 8 } }, { "Shot_8", new[] { 512, 256, 64, 64, 32, 8 } }, { "Slash_0", new[] { 0, 320, 64, 64, 32, 8 } }, { "Slash_1", new[] { 64, 320, 64, 64, 32, 8 } }, { "Slash_2", new[] { 128, 320, 64, 64, 32, 8 } }, { "Slash_3", new[] { 192, 320, 64, 64, 32, 8 } }, { "Slash_4", new[] { 256, 320, 64, 64, 32, 8 } }, { "Slash_5", new[] { 320, 320, 64, 64, 32, 8 } }, { "Slash_6", new[] { 384, 320, 64, 64, 32, 8 } }, { "Slash_7", new[] { 448, 320, 64, 64, 32, 8 } }, { "Slash_8", new[] { 512, 320, 64, 64, 32, 8 } }, { "Jab_0", new[] { 0, 384, 64, 64, 32, 8 } }, { "Jab_1", new[] { 64, 384, 64, 64, 32, 8 } }, { "Jab_2", new[] { 128, 384, 64, 64, 32, 8 } }, { "Jab_3", new[] { 192, 384, 64, 64, 32, 8 } }, { "Jab_4", new[] { 256, 384, 64, 64, 32, 8 } }, { "Jab_5", new[] { 320, 384, 64, 64, 32, 8 } }, { "Jab_6", new[] { 384, 384, 64, 64, 32, 8 } }, { "Jab_7", new[] { 448, 384, 64, 64, 32, 8 } }, { "Jab_8", new[] { 512, 384, 64, 64, 32, 8 } }, { "Push_0", new[] { 0, 448, 64, 64, 32, 8 } }, { "Push_1", new[] { 64, 448, 64, 64, 32, 8 } }, { "Push_2", new[] { 128, 448, 64, 64, 32, 8 } }, { "Push_3", new[] { 192, 448, 64, 64, 32, 8 } }, { "Push_4", new[] { 256, 448, 64, 64, 32, 8 } }, { "Push_5", new[] { 320, 448, 64, 64, 32, 8 } }, { "Push_6", new[] { 384, 448, 64, 64, 32, 8 } }, { "Push_7", new[] { 448, 448, 64, 64, 32, 8 } }, { "Push_8", new[] { 512, 448, 64, 64, 32, 8 } }, { "Jump_0", new[] { 0, 512, 64, 64, 32, 8 } }, { "Jump_1", new[] { 64, 512, 64, 64, 32, 8 } }, { "Jump_2", new[] { 128, 512, 64, 64, 32, 8 } }, { "Jump_3", new[] { 192, 512, 64, 64, 32, 8 } }, { "Jump_4", new[] { 256, 512, 64, 64, 32, 8 } }, { "Jump_5", new[] { 320, 512, 64, 64, 32, 8 } }, { "Jump_6", new[] { 384, 512, 64, 64, 32, 8 } }, { "Jump_7", new[] { 448, 512, 64, 64, 32, 8 } }, { "Jump_8", new[] { 512, 512, 64, 64, 32, 8 } }, { "Climb_0", new[] { 0, 576, 64, 64, 32, 8 } }, { "Climb_1", new[] { 64, 576, 64, 64, 32, 8 } }, { "Climb_2", new[] { 128, 576, 64, 64, 32, 8 } }, { "Climb_3", new[] { 192, 576, 64, 64, 32, 8 } }, { "Climb_4", new[] { 256, 576, 64, 64, 32, 8 } }, { "Climb_5", new[] { 320, 576, 64, 64, 32, 8 } }, { "Climb_6", new[] { 384, 576, 64, 64, 32, 8 } }, { "Climb_7", new[] { 448, 576, 64, 64, 32, 8 } }, { "Climb_8", new[] { 512, 576, 64, 64, 32, 8 } }, { "Crawl_0", new[] { 0, 640, 64, 64, 32, 8 } }, { "Crawl_1", new[] { 64, 640, 64, 64, 32, 8 } }, { "Crawl_2", new[] { 128, 640, 64, 64, 32, 8 } }, { "Crawl_3", new[] { 192, 640, 64, 64, 32, 8 } }, { "Crawl_4", new[] { 256, 640, 64, 64, 32, 8 } }, { "Crawl_5", new[] { 320, 640, 64, 64, 32, 8 } }, { "Crawl_6", new[] { 384, 640, 64, 64, 32, 8 } }, { "Crawl_7", new[] { 448, 640, 64, 64, 32, 8 } }, { "Crawl_8", new[] { 512, 640, 64, 64, 32, 8 } }, { "Run_0", new[] { 0, 704, 64, 64, 32, 8 } }, { "Run_1", new[] { 64, 704, 64, 64, 32, 8 } }, { "Run_2", new[] { 128, 704, 64, 64, 32, 8 } }, { "Run_3", new[] { 192, 704, 64, 64, 32, 8 } }, { "Run_4", new[] { 256, 704, 64, 64, 32, 8 } }, { "Run_5", new[] { 320, 704, 64, 64, 32, 8 } }, { "Run_6", new[] { 384, 704, 64, 64, 32, 8 } }, { "Run_7", new[] { 448, 704, 64, 64, 32, 8 } }, { "Run_8", new[] { 512, 704, 64, 64, 32, 8 } }, { "Ready_0", new[] { 0, 768, 64, 64, 32, 8 } }, { "Ready_1", new[] { 64, 768, 64, 64, 32, 8 } }, { "Ready_2", new[] { 128, 768, 64, 64, 32, 8 } }, { "Ready_3", new[] { 192, 768, 64, 64, 32, 8 } }, { "Ready_4", new[] { 256, 768, 64, 64, 32, 8 } }, { "Ready_5", new[] { 320, 768, 64, 64, 32, 8 } }, { "Ready_6", new[] { 384, 768, 64, 64, 32, 8 } }, { "Ready_7", new[] { 448, 768, 64, 64, 32, 8 } }, { "Ready_8", new[] { 512, 768, 64, 64, 32, 8 } }, { "Idle_0", new[] { 0, 832, 64, 64, 32, 8 } }, { "Idle_1", new[] { 64, 832, 64, 64, 32, 8 } }, { "Idle_2", new[] { 128, 832, 64, 64, 32, 8 } }, { "Idle_3", new[] { 192, 832, 64, 64, 32, 8 } }, { "Idle_4", new[] { 256, 832, 64, 64, 32, 8 } }, { "Idle_5", new[] { 320, 832, 64, 64, 32, 8 } }, { "Idle_6", new[] { 384, 832, 64, 64, 32, 8 } }, { "Idle_7", new[] { 448, 832, 64, 64, 32, 8 } }, { "Idle_8", new[] { 512, 832, 64, 64, 32, 8 } } };
        private Dictionary<string, Sprite> _sprites;
        
        public override void Rebuild(bool forceMerge = false)
        {
            var layers = BuildLayers(forceMerge);

            if (Texture == null) Texture = new Texture2D(Width, Height) { filterMode = FilterMode.Point };

            if (Shield != "")
            {
                var shield = layers["Shield"];
                var last = layers.Last(i => i.Key != "Weapon");
                var copy = last.Value.ToArray();

                for (var i = 2 * 64 * Width; i < 3 * 64 * Width; i++)
                {
                    if (shield[i].a > 0) copy[i] = shield[i];
                }

                layers[last.Key] = copy;
            }

            if (Firearm != "")
            {
                foreach (var layerName in new[] { "Head", "Ears", "Eyes", "Mask", "Hair", "Helmet" })
                {
                    if (!layers.ContainsKey(layerName)) continue;

                    var copy = layers[layerName].ToArray();

                    for (var y = 11 * 64 - 1; y >= 10 * 64 - 1; y--)
                    {
                        for (var x = 0; x < Width; x++)
                        {
                            copy[x + y * Width] = copy[x + (y - 1) * Width];
                        }
                    }

                    layers[layerName] = copy;
                }
            }
            
            Texture = TextureHelper.MergeLayers(Texture, layers.Values.ToArray());
            Texture.SetPixels(0, Texture.height - 32, 32, 32, new Color[32 * 32]);

            if (Cape != "") CapeOverlay(layers["Cape"]);

            _sprites ??= Layout.ToDictionary(i => i.Key, i => Sprite.Create(Texture, new Rect(i.Value[0], i.Value[1], i.Value[2], i.Value[3]), new Vector2((float)i.Value[4] / i.Value[2], (float)i.Value[5] / i.Value[3]), 16, 0, SpriteMeshType.FullRect));
            
            var spriteLibraryAsset = ScriptableObject.CreateInstance<SpriteLibraryAsset>();

            foreach (var sprite in _sprites)
            {
                var split = sprite.Key.Split('_');

                spriteLibraryAsset.AddCategoryLabel(sprite.Value, split[0], split[1]);
            }

            Character.Body.GetComponent<SpriteLibrary>().spriteLibraryAsset = spriteLibraryAsset;

            if (Character.Firearm.Renderer != null)
            {
                
                if (Firearm == "")
                {
                    Character.Firearm.Renderer.enabled = false;
                }
                else
                {
                    Character.Firearm.Renderer.enabled = true;

                    var texture = new Texture2D(64, 64) { filterMode = FilterMode.Point };
                    var pixels = SpriteCollection.Layers.Single(i => i.Name == "Firearm").GetPixels(Firearm);
                    
                    for (var x = 0; x < 64; x++)
                    {
                        for (var y = 0; y < 64; y++)
                        {
                            texture.SetPixel(x, y, pixels[x + (y + 12 * 64) * Width]);
                        }
                    }

                    texture.Apply();

                    Character.Firearm.Renderer.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.125f), 16);
                    Character.Firearm.FireMuzzlePosition = GetMuzzlePosition(texture);
                    Character.Firearm.FireMuzzle.localPosition = Character.Firearm.FireMuzzlePosition / 16;
                }

                Character.Firearm.Renderer.gameObject.SetActive(Character.Firearm.Detached);
            }
        }

        public Dictionary<string, Color32[]> BuildLayers(bool forceMerge = false)
        {
            var dict = SpriteCollection.Layers.ToDictionary(i => i.Name, i => i);
            var layers = new Dictionary<string, Color32[]>();

            if (Head.Contains("Lizard")) Hair = Helmet = Mask = "";

            if (Back != "") layers.Add("Back", dict["Back"].GetPixels(Back));
            if (Shield != "") layers.Add("Shield", dict["Shield"].GetPixels(Shield));

            if (Body != "")
            {
                layers.Add("Body", dict["Body"].GetPixels(Body));

                if (Firearm == "")
                {
                    var arms = dict["Arms"].GetPixels(Body).ToArray();

                    layers.Add("Arms", arms);
                }
            }

            if (Head != "") layers.Add("Head", dict["Head"].GetPixels(Head));
            if (Ears != "" && (Helmet == "" || Helmet.Contains("[ShowEars]"))) layers.Add("Ears", dict["Ears"].GetPixels(Ears));

            if (Armor != "")
            {
                layers.Add("Armor", dict["Armor"].GetPixels(Armor));

                if (Firearm == "")
                {
                    layers.Add("Bracers", dict["Bracers"].GetPixels(Armor));
                }
            }

            if (Eyes != "") layers.Add("Eyes", dict["Eyes"].GetPixels(Eyes));
            if (Hair != "") layers.Add("Hair", dict["Hair"].GetPixels(Hair, Helmet == "" ? null : layers["Head"]));
            if (Cape != "") layers.Add("Cape", dict["Cape"].GetPixels(Cape));
            if (Helmet != "") layers.Add("Helmet", dict["Helmet"].GetPixels(Helmet));
            if (Weapon != "") layers.Add("Weapon", dict["Weapon"].GetPixels(Weapon));

            if (Firearm != "")
            {
                var firearm = dict["Firearm"].GetPixels(Firearm).ToArray();

                if (Character.Firearm.Detached && !forceMerge)
                {
                    for (var y = 0; y < Height; y++)
                    {
                        if (y >= 0 * 64 && y < 1 * 64) continue; // Roll
                        if (y >= 1 * 64 && y < 2 * 64) continue; // Death
                        if (y >= 2 * 64 && y < 3 * 64) continue; // Block
                        if (y >= 5 * 64 && y < 6 * 64) continue; // Slash
                        if (y >= 6 * 64 && y < 7 * 64) continue; // Jab
                        if (y >= 9 * 64 && y < 10 * 64) continue; // Climb
                        if (y >= 13 * 64 && y < 14 * 64) continue; // Idle

                        for (var x = 0; x < Width; x++)
                        {
                            firearm[x + y * Width] = new Color32();
                        }
                    }
                }

                if (Armor != "" || Body != "") // Replace gloves color.
                {
                    var index = 27 + 844 * Width;
                    var source = Armor == "" ? "Arms" : "Bracers";
                    var data = Armor == "" ? Body : Armor;
                    var pixels = dict[source].GetPixels(data);

                    if (pixels == null && source == "Bracers")
                    {
                        pixels = dict["Arms"].GetPixels(Body);
                    }

                    if (pixels != null)
                    {
                        var replacement = pixels[index];

                        if (replacement.a > 0)
                        {
                            var hand = new Color32(246, 202, 159, 255);

                            for (var i = 0; i < firearm.Length; i++)
                            {
                                if (firearm[i].FastEquals(hand))
                                {
                                    firearm[i] = replacement;
                                }
                            }
                        }
                    }
                }

                layers.Add("Firearm", firearm);
            }

            if (Mask != "") layers.Add("Mask", dict["Mask"].GetPixels(Mask));
            if (Horns != "" && Helmet == "") layers.Add("Horns", dict["Horns"].GetPixels(Horns));

            var order = SpriteCollection.Layers.Select(i => i.Name).ToList();

            layers = layers.Where(i => i.Value != null).OrderBy(i => order.IndexOf(i.Key)).ToDictionary(i => i.Key, i => i.Value);

            return layers;
        }

        private void CapeOverlay(Color32[] cape)
        {
            if (Cape == "") return;
            
            var pixels = Texture.GetPixels32();
            var width = Texture.width;
            var height = Texture.height;

            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    //if (x >= 0 && x < 2 * 64 && y >= 9 * 64 && y < 10 * 64 // "Climb_0", "Climb_1"
                    //    || x >= 64 && x < 64 + 2 * 64 && y >= 6 * 64 && y < 7 * 64 // "Jab_1", "Jab_2"
                    //    || x >= 128 && x < 128 + 2 * 64 && y >= 5 * 64 && y < 6 * 64 // "Slash_2", "Slash_3"
                    //    || x >= 0 && x < 4 * 64 && y >= 4 * 64 && y < 5 * 64) // "Shot_0", "Shot_1", "Shot_2", "Shot_3"
                    if (x >= 0 && x < 2 * 64 && y >= 9 * 64 && y < 10 * 64 // "Climb_0", "Climb_1"
                        || x >= 64 && x < 64 + 2 * 64 && y >= 6 * 64 && y < 7 * 64 // "Jab_1", "Jab_2"
                        || x >= 128 && x < 128 + 2 * 64 && y >= 5 * 64 && y < 6 * 64 // "Slash_2", "Slash_3"
                        || x >= 0 && x < 4 * 64 && y >= 4 * 64 && y < 5 * 64) // "Shot_0", "Shot_1", "Shot_2", "Shot_3"
                    {
                        var i = x + y * width;

                        if (cape[i].a > 0) pixels[i] = cape[i];
                    }
                }
            }

            Texture.SetPixels32(pixels);
            Texture.Apply();
        }

        private static Vector2 GetMuzzlePosition(Texture2D texture)
        {
            var muzzlePosition = new Vector2(texture.width / 2f - 1, 6);

            for (var x = 63; x >= 0; x--)
            {
                for (var y = 0; y < 64; y++)
                {
                    if (texture.GetPixel(x, y).a > 0)
                    {
                        return muzzlePosition;
                    }
                }

                muzzlePosition.x = x - 1 - texture.width / 2f;
            }

            return muzzlePosition;
        }
    }
}