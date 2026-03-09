using System.Linq;
using Assets.PixelFantasy.Common.Scripts.CollectionScripts;
using UnityEngine;

namespace Assets.PixelFantasy.Common.Scripts.CharacterScripts
{
    public abstract class  CharacterBuilderBase : MonoBehaviour
    {
        public SpriteCollection SpriteCollection;
        public string Body = "Human";
        public string Head = "Human";
        public string Ears = "Human";
        public string Eyes = "Human";
        public string Mouth;
        public string Hair;
        public string Armor;
        public string Helmet;
        public string Weapon;
        public string Firearm;
        public string Shield;
        public string Cape;
        public string Back;
        public string Mask;
        public string Horns;
        public string Arms = "Human";
        public string Legs = "Human";
        public bool RebuildOnStart = true;

        public Texture2D Texture { get; protected set; }
        
        public void Awake()
        {
            if (RebuildOnStart)
            {
                Rebuild();
            }
        }

        public abstract void Rebuild(bool forceMerge = false);

        public virtual void Reset()
        {
            Head = Ears = Eyes = Body = Hair = Armor = Helmet = Weapon = Firearm = Shield = Cape = Back = Mask = Horns = "";
            Head = "Human";
            Ears = "Human";
            Eyes = "Human";
            Body = "Human";

            Rebuild();
        }

        public void RandomizeEquipment(bool helmet = true, bool armor = true, bool weapon = true, bool shield = true)
        {
            if (helmet) Helmet = Randomize("Helmet", 20);
            if (armor) Armor = Randomize("Armor", 20);
            if (weapon) Weapon = Randomize("Weapon");

            var bow = Weapon.Contains("Bow"); // TODO:
            var gun = Weapon.Contains("Gun"); // TODO:

            if (bow || gun)
            {
                Shield = "";
            }
            else
            {
                if (shield) Shield = Randomize("Shield", 50);
            }

            Back = bow ? "LeatherQuiver" : "";
        }

        public void RandomizeHumanAppearance()
        {
            var colors = new[]
            {
                "3D3D3D", "5D5D5D", "858585", "C7CFDD", "5D2C28", "8A4836", "BF6F4A", "E69C69", "F6CA9F", "C64524",
                "E07438", "FFA214", "891E2B", "C42430", "622461", "93388F", "F389F5", "0098DC", "00CDF9", "657392",
                "134C4C", "1E6F50", "33984B", "5AC54F"
            };
            var color = colors[Random.Range(0, colors.Length)];

            Hair = $"{Randomize("Hair", 20)}#{color}";

        }

        public void RandomizeRace()
        {
            Body = Randomize("Body");
            Head = Body;
            Eyes = Body;
            Ears = Body;
        }

        private string Randomize(string part, int emptyChance = 0)
        {
            var options = SpriteCollection.Layers.Single(i => i.Name == part).Textures;

            if (Random.Range(0, 100) < emptyChance) return "";

            return options[Random.Range(0, options.Count)].name;
        }
    }
}