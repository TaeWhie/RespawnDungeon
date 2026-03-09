using System;
using System.Collections.Generic;
using System.Linq;
using Assets.PixelFantasy.Common.Scripts;
using Assets.PixelFantasy.Common.Scripts.CharacterScripts;
using UnityEngine;

namespace Assets.PixelFantasy.PixelHeroes4D.Common.Scripts.CharacterScripts
{
    public class CharacterBuilder : CharacterBuilderBase
    {
        public Creature Character;

        private const int Width = 512;
        private const int Height = 2048;
        private static readonly Dictionary<string, int[]> Layout = new() { { "BlockB_0", new[] { 0, 256, 64, 64, 32, 16 } }, { "BlockB_1", new[] { 64, 256, 64, 64, 32, 16 } }, { "BlockF_0", new[] { 0, 384, 64, 64, 32, 16 } }, { "BlockF_1", new[] { 64, 384, 64, 64, 32, 16 } }, { "BlockS_0", new[] { 0, 320, 64, 64, 32, 16 } }, { "BlockS_1", new[] { 64, 320, 64, 64, 32, 16 } }, { "CastB_0", new[] { 0, 832, 64, 64, 32, 16 } }, { "CastB_1", new[] { 64, 832, 64, 64, 32, 16 } }, { "CastB_2", new[] { 128, 832, 64, 64, 32, 16 } }, { "CastB_3", new[] { 192, 832, 64, 64, 32, 16 } }, { "CastB_4", new[] { 256, 832, 64, 64, 32, 16 } }, { "CastF_0", new[] { 0, 960, 64, 64, 32, 16 } }, { "CastF_1", new[] { 64, 960, 64, 64, 32, 16 } }, { "CastF_2", new[] { 128, 960, 64, 64, 32, 16 } }, { "CastF_3", new[] { 192, 960, 64, 64, 32, 16 } }, { "CastF_4", new[] { 256, 960, 64, 64, 32, 16 } }, { "CastS_0", new[] { 0, 896, 64, 64, 32, 16 } }, { "CastS_1", new[] { 64, 896, 64, 64, 32, 16 } }, { "CastS_2", new[] { 128, 896, 64, 64, 32, 16 } }, { "CastS_3", new[] { 192, 896, 64, 64, 32, 16 } }, { "CastS_4", new[] { 256, 896, 64, 64, 32, 16 } }, { "DeathB_0", new[] { 0, 64, 64, 64, 32, 16 } }, { "DeathB_1", new[] { 64, 64, 64, 64, 32, 16 } }, { "DeathB_2", new[] { 128, 64, 64, 64, 32, 16 } }, { "DeathB_3", new[] { 192, 64, 64, 64, 32, 16 } }, { "DeathB_4", new[] { 256, 64, 64, 64, 32, 16 } }, { "DeathF_0", new[] { 0, 192, 64, 64, 32, 16 } }, { "DeathF_1", new[] { 64, 192, 64, 64, 32, 16 } }, { "DeathF_2", new[] { 128, 192, 64, 64, 32, 16 } }, { "DeathF_3", new[] { 192, 192, 64, 64, 32, 16 } }, { "DeathF_4", new[] { 256, 192, 64, 64, 32, 16 } }, { "DeathS_0", new[] { 0, 128, 64, 64, 32, 16 } }, { "DeathS_1", new[] { 64, 128, 64, 64, 32, 16 } }, { "DeathS_2", new[] { 128, 128, 64, 64, 32, 16 } }, { "DeathS_3", new[] { 192, 128, 64, 64, 32, 16 } }, { "DeathS_4", new[] { 256, 128, 64, 64, 32, 16 } }, { "FireB_0", new[] { 0, 448, 64, 64, 32, 16 } }, { "FireF_0", new[] { 0, 576, 64, 64, 32, 16 } }, { "FireS_0", new[] { 0, 512, 64, 64, 32, 16 } }, { "IdleB_0", new[] { 0, 1792, 64, 64, 32, 16 } }, { "IdleB_1", new[] { 64, 1792, 64, 64, 32, 16 } }, { "IdleB_2", new[] { 128, 1792, 64, 64, 32, 16 } }, { "IdleB_3", new[] { 192, 1792, 64, 64, 32, 16 } }, { "IdleF_0", new[] { 0, 1920, 64, 64, 32, 16 } }, { "IdleF_1", new[] { 64, 1920, 64, 64, 32, 16 } }, { "IdleF_2", new[] { 128, 1920, 64, 64, 32, 16 } }, { "IdleF_3", new[] { 192, 1920, 64, 64, 32, 16 } }, { "IdleS_0", new[] { 0, 1856, 64, 64, 32, 16 } }, { "IdleS_1", new[] { 64, 1856, 64, 64, 32, 16 } }, { "IdleS_2", new[] { 128, 1856, 64, 64, 32, 16 } }, { "IdleS_3", new[] { 192, 1856, 64, 64, 32, 16 } }, { "JabB_0", new[] { 0, 1024, 64, 64, 32, 24 } }, { "JabB_1", new[] { 64, 1024, 64, 64, 32, 24 } }, { "JabB_2", new[] { 128, 1024, 64, 64, 32, 24 } }, { "JabB_3", new[] { 192, 1024, 64, 64, 32, 16 } }, { "JabB_4", new[] { 256, 1024, 64, 64, 32, 16 } }, { "JabF_0", new[] { 0, 1152, 64, 64, 32, 24 } }, { "JabF_1", new[] { 64, 1152, 64, 64, 32, 24 } }, { "JabF_2", new[] { 128, 1152, 64, 64, 32, 24 } }, { "JabF_3", new[] { 192, 1152, 64, 64, 32, 16 } }, { "JabF_4", new[] { 256, 1152, 64, 64, 32, 16 } }, { "JabS_0", new[] { 0, 1088, 64, 64, 32, 24 } }, { "JabS_1", new[] { 64, 1088, 64, 64, 32, 24 } }, { "JabS_2", new[] { 128, 1088, 64, 64, 32, 24 } }, { "JabS_3", new[] { 192, 1088, 64, 64, 32, 16 } }, { "JabS_4", new[] { 256, 1088, 64, 64, 32, 16 } }, { "JumpB_0", new[] { 128, 1600, 64, 64, 32, 16 } }, { "JumpB_1", new[] { 192, 1600, 64, 64, 32, 16 } }, { "JumpB_3", new[] { 256, 1600, 64, 64, 32, 16 } }, { "JumpF_0", new[] { 128, 1728, 64, 64, 32, 16 } }, { "JumpF_1", new[] { 192, 1728, 64, 64, 32, 16 } }, { "JumpF_2", new[] { 256, 1728, 64, 64, 32, 16 } }, { "JumpS_0", new[] { 128, 1664, 64, 64, 32, 16 } }, { "JumpS_1", new[] { 192, 1664, 64, 64, 32, 16 } }, { "JumpS_2", new[] { 256, 1664, 64, 64, 32, 16 } }, { "ReadyB_0", new[] { 0, 1600, 64, 64, 32, 16 } }, { "ReadyB_1", new[] { 64, 1600, 64, 64, 32, 16 } }, { "ReadyF_0", new[] { 0, 1728, 64, 64, 32, 16 } }, { "ReadyF_1", new[] { 64, 1728, 64, 64, 32, 16 } }, { "ReadyS_0", new[] { 0, 1664, 64, 64, 32, 16 } }, { "ReadyS_1", new[] { 64, 1664, 64, 64, 32, 16 } }, { "RunB_0", new[] { 0, 1408, 64, 64, 32, 16 } }, { "RunB_1", new[] { 64, 1408, 64, 64, 32, 16 } }, { "RunB_2", new[] { 128, 1408, 64, 64, 32, 16 } }, { "RunB_3", new[] { 192, 1408, 64, 64, 32, 16 } }, { "RunF_0", new[] { 0, 1536, 64, 64, 32, 16 } }, { "RunF_1", new[] { 64, 1536, 64, 64, 32, 16 } }, { "RunF_2", new[] { 128, 1536, 64, 64, 32, 16 } }, { "RunF_3", new[] { 192, 1536, 64, 64, 32, 16 } }, { "RunS_0", new[] { 0, 1472, 64, 64, 32, 16 } }, { "RunS_1", new[] { 64, 1472, 64, 64, 32, 16 } }, { "RunS_2", new[] { 128, 1472, 64, 64, 32, 16 } }, { "RunS_3", new[] { 192, 1472, 64, 64, 32, 16 } }, { "ShotB_0", new[] { 0, 640, 64, 64, 32, 16 } }, { "ShotB_1", new[] { 64, 640, 64, 64, 32, 16 } }, { "ShotB_2", new[] { 128, 640, 64, 64, 32, 16 } }, { "ShotB_3", new[] { 192, 640, 64, 64, 32, 16 } }, { "ShotF_0", new[] { 0, 768, 64, 64, 32, 16 } }, { "ShotF_1", new[] { 64, 768, 64, 64, 32, 16 } }, { "ShotF_2", new[] { 128, 768, 64, 64, 32, 16 } }, { "ShotF_3", new[] { 192, 768, 64, 64, 32, 16 } }, { "ShotS_0", new[] { 0, 704, 64, 64, 32, 16 } }, { "ShotS_1", new[] { 64, 704, 64, 64, 32, 16 } }, { "ShotS_2", new[] { 128, 704, 64, 64, 32, 16 } }, { "ShotS_3", new[] { 192, 704, 64, 64, 32, 16 } }, { "SlashB_0", new[] { 0, 1216, 64, 64, 32, 24 } }, { "SlashB_1", new[] { 64, 1216, 64, 64, 32, 24 } }, { "SlashB_2", new[] { 128, 1216, 64, 64, 32, 24 } }, { "SlashB_3", new[] { 192, 1216, 64, 64, 32, 24 } }, { "SlashB_4", new[] { 256, 1216, 64, 64, 32, 24 } }, { "SlashF_0", new[] { 0, 1344, 64, 64, 32, 24 } }, { "SlashF_1", new[] { 64, 1344, 64, 64, 32, 24 } }, { "SlashF_2", new[] { 128, 1344, 64, 64, 32, 24 } }, { "SlashF_3", new[] { 192, 1344, 64, 64, 32, 24 } }, { "SlashF_4", new[] { 256, 1344, 64, 64, 32, 24 } }, { "SlashS_0", new[] { 0, 1280, 64, 64, 32, 24 } }, { "SlashS_1", new[] { 64, 1280, 64, 64, 32, 24 } }, { "SlashS_2", new[] { 128, 1280, 64, 64, 32, 24 } }, { "SlashS_3", new[] { 192, 1280, 64, 64, 32, 24 } }, { "SlashS_4", new[] { 256, 1280, 64, 64, 32, 24 } }, { "WinkB_0", new[] { 256, 1792, 64, 64, 32, 16 } }, { "WinkB_1", new[] { 320, 1792, 64, 64, 32, 16 } }, { "WinkB_2", new[] { 384, 1792, 64, 64, 32, 16 } }, { "WinkB_3", new[] { 448, 1792, 64, 64, 32, 16 } }, { "WinkF_0", new[] { 256, 1920, 64, 64, 32, 16 } }, { "WinkF_1", new[] { 320, 1920, 64, 64, 32, 16 } }, { "WinkF_2", new[] { 384, 1920, 64, 64, 32, 16 } }, { "WinkF_3", new[] { 448, 1920, 64, 64, 32, 16 } }, { "WinkS_0", new[] { 256, 1856, 64, 64, 32, 16 } }, { "WinkS_1", new[] { 320, 1856, 64, 64, 32, 16 } }, { "WinkS_2", new[] { 384, 1856, 64, 64, 32, 16 } }, { "WinkS_3", new[] { 448, 1856, 64, 64, 32, 16 } }, { "ClimbB_0", new[] { 128, 256, 64, 64, 32, 16 } }, { "ClimbB_1", new[] { 192, 256, 64, 64, 32, 16 } }, { "ClimbF_0", new[] { 128, 384, 64, 64, 32, 16 } }, { "ClimbF_1", new[] { 192, 384, 64, 64, 32, 16 } }, { "ClimbS_0", new[] { 128, 320, 64, 64, 32, 16 } }, { "ClimbS_1", new[] { 192, 320, 64, 64, 32, 16 } } };
        private Dictionary<string, Sprite> _sprites;

        public override void Reset()
        {
            Body = Head = Ears = Eyes = Mouth = Hair = Armor = Helmet = Weapon = Firearm = Shield = Cape = Back = Mask = Horns = "";
            Head = "Human1";
            Ears = "Human1";
            Eyes = "Human1";
            Body = "Human1";

            Rebuild();
        }
        
        public override void Rebuild(bool forceMerge = false)
        {
            var dict = SpriteCollection.Layers.ToDictionary(i => i.Name, i => i);
            var layers = new Dictionary<string, Color32[]>();

            if (Back != "") layers.Add("Back", dict["Back"].GetPixels(Back));
            if (Body != "") { layers.Add("Body", dict["Body"].GetPixels(Body)); layers.Add("Arms", dict["Arms"].GetPixels(Body).ToArray()); }
            if (Head != "") layers.Add("Head", dict["Head"].GetPixels(Head));
            if (Ears != "" && (Helmet == "" || Helmet.Contains("[ShowEars]"))) layers.Add("Ears", dict["Ears"].GetPixels(Ears));
            if (Armor != "") { layers.Add("Armor", dict["Armor"].GetPixels(Armor)); layers.Add("Bracers", dict["Bracers"].GetPixels(Armor)); }
            if (Eyes != "") layers.Add("Eyes", dict["Eyes"].GetPixels(Eyes));
            if (Mouth != "") layers.Add("Mouth", dict["Mouth"].GetPixels(Mouth));
            if (Hair != "") layers.Add("Hair", dict["Hair"].GetPixels(Hair, Helmet == "" ? null : layers["Head"]));
            if (Cape != "") layers.Add("Cape", dict["Cape"].GetPixels(Cape));
            if (Helmet != "") layers.Add("Helmet", dict["Helmet"].GetPixels(Helmet));
            if (Shield != "") layers.Add("Shield", dict["Shield"].GetPixels(Shield));
            if (Weapon != "") layers.Add("Weapon", dict["Weapon"].GetPixels(Weapon));
            if (Firearm != "") throw new NotImplementedException();
            if (Mask != "") layers.Add("Mask", dict["Mask"].GetPixels(Mask));
            if (Horns != "" && Helmet == "") layers.Add("Horns", dict["Horns"].GetPixels(Horns));

            var order = new Dictionary<string, string[]>
            {
                { "F_", new[] { "Cape", "Back", "Body", "Armor", "Arms", "Bracers", "Head", "Horns", "Eyes", "Mouth", "Mask", "Hair", "Ears", "Helmet", "Shield", "Weapon" } },
                { "S_", new[] { "Cape", "Back", "Shield", "Body", "Armor", "Arms", "Bracers", "Head", "Horns", "Eyes", "Mouth", "Mask", "Hair", "Ears", "Helmet", "Weapon" } },
                { "BlockS_", new[] { "Shield" } },
                { "SlashS_2", new[] { "Arms", "Bracers" } },
                { "B_", new[] { "Weapon", "Shield", "Body", "Armor", "Arms", "Bracers", "Cape", "Back", "Head", "Horns", "Hair", "Ears", "Helmet" } },
                { "DeathB_", new[] { "Cape", "Back", "Body", "Armor", "Arms", "Bracers", "Head", "Horns", "Eyes", "Mouth", "Mask", "Hair", "Ears", "Helmet", "Shield", "Weapon" } }
            };

            Texture ??= new Texture2D(Width, Height) { filterMode = FilterMode.Point };
            Texture.SetPixels32(new Color32[Width * Height]);

            foreach (var side in order.Keys)
            foreach (var key in order[side])
            {
                if (!layers.ContainsKey(key)) continue;

                var layer = layers[key];

                if (layer == null) continue;

                foreach (var frame in Layout.Keys.Where(i => i.Contains(side)))
                {
                    var rect = Layout[frame];
                    
                    for (var x = 0; x < rect[2]; x++)
                    {
                        for (var y = 0; y < rect[3]; y++)
                        {
                            var p = layer[x + rect[0] + (y + rect[1]) * Width];

                            if (p.a > 0)
                            {
                                Texture.SetPixel(x + rect[0], y + rect[1], p);
                            }
                        }
                    }
                }
            }

            Texture.Apply();
            SetIcon(layers);

            _sprites ??= Layout.ToDictionary(i => i.Key, i => Sprite.Create(Texture, new Rect(i.Value[0], i.Value[1], i.Value[2], i.Value[3]), new Vector2((float)i.Value[4] / i.Value[2], (float)i.Value[5] / i.Value[3]), 16, 0, SpriteMeshType.FullRect));

            var spriteLibraryAsset = ScriptableObject.CreateInstance<UnityEngine.U2D.Animation.SpriteLibraryAsset>();

            foreach (var sprite in _sprites)
            {
                var split = sprite.Key.Split('_');

                spriteLibraryAsset.AddCategoryLabel(sprite.Value, split[0], split[1]);
            }

            Character.Body.GetComponent<UnityEngine.U2D.Animation.SpriteLibrary>().spriteLibraryAsset = spriteLibraryAsset;
        }

        private void SetIcon(Dictionary<string, Color32[]> layers)
        {
            const int size = 64;

            if (!layers.TryGetValue("Helmet", out var icon))
            {
                if (!layers.TryGetValue("Hair", out icon))
                {
                    layers.TryGetValue("Head", out icon);
                }
            }

            Texture.SetPixels(0, Texture.height - size, size, size, new Color[size * size]);

            if (icon != null)
            {
                for (var x = 0; x < size; x++)
                {
                    for (var y = Texture.height - size - 1; y < Texture.height; y++)
                    {
                        Texture.SetPixel(x, y, icon[x + y * Texture.width]);
                    }
                }
            }

            Texture.Apply();
        }
    }
}