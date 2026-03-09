using System.Linq;
using Assets.PixelFantasy.Common.Scripts.CharacterScripts;
using UnityEngine;

namespace Assets.PixelFantasy.PixelHeroes2.Scripts
{
    public class CharacterBuilder : CharacterBuilderBase
    {
        public Character Character;
       
        public override void Rebuild(bool forceMerge = false)
        {
            var dict = SpriteCollection.Layers.ToDictionary(i => i.Name, i => i);

            Character.Head.sprite = Head == "" ? null : CreateSprite(dict["Head"].Textures.Single(i => i.name == Head.Split('#')[0]), new Vector2(16, 5) / 32);
            Character.Hair.sprite = Hair == "" ? null : CreateSprite(dict["Hair"].Textures.Single(i => i.name == Hair.Split('#')[0]), new Vector2(16, 5) / 32);
            Character.Eyes.sprite = Eyes == "" ? null : CreateSprite(dict["Eyes"].Textures.Single(i => i.name == Eyes.Split('#')[0]), new Vector2(8, 8) / 16);
            Character.Ears.sprite = Ears == "" ? null : CreateSprite(dict["Ears"].Textures.Single(i => i.name == Ears.Split('#')[0]), new Vector2(5, 4) / 8);
            Character.Body.sprite = Body == "" ? null : CreateSprite(dict["Body"].Textures.Single(i => i.name == Body.Split('#')[0]), new Vector2(8, 8) / 16);
            
            foreach (var arm in Character.Arms)
            {
                arm.sprite = Arms == "" ? null : CreateSprite(dict["Arm"].Textures.Single(i => i.name == Arms.Split('#')[0]), new Vector2(10, 11) / 16);
            }

            foreach (var leg in Character.Legs)
            {
                leg.sprite = Legs == "" ? null : CreateSprite(dict["Leg"].Textures.Single(i => i.name == Legs.Split('#')[0]), new Vector2(4, 6) / 8);
            }

            Character.Helmet.sprite = Helmet == "" ? null : CreateSprite(dict["Helmet"].Textures.Single(i => i.name == Helmet.Split('#')[0]), new Vector2(16, 5) / 32);
            Character.Armor.sprite = Armor == "" ? null : CreateSprite(dict["Armor"].Textures.Single(i => i.name == Armor.Split('#')[0]), new Vector2(8, 8) / 16);
            Character.Bracers[0].sprite = Character.Bracers[1].sprite = Armor == "" ? null : CreateSprite(dict["Bracer"].Textures.Single(i => i.name == Armor.Split('#')[0]), new Vector2(10, 11) / 16);
            Character.Leggings[0].sprite = Character.Leggings[1].sprite = Armor == "" ? null : CreateSprite(dict["Legging"].Textures.Single(i => i.name == Armor.Split('#')[0]), new Vector2(4, 6) / 8);

            Character.Weapons[0].sprite = Character.Weapons[1].sprite = Weapon == "" ? null : CreateSprite(dict["Weapon"].Textures.Single(i => i.name == Weapon.Split('#')[0]), new Vector2(16, 7) / 32);

            if (Hair != "" && Hair.Contains("[HideEars]")) Character.Ears.sprite = null;
            if (Helmet != "" && !Helmet.Contains("[ShowEars]")) Character.Ears.sprite = null;
        }

        private Sprite CreateSprite(Texture2D texture, Vector2 pivot)
        {
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, 16);
        }
    }
}