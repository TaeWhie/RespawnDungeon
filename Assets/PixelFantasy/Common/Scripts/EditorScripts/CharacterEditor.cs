using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Assets.PixelFantasy.Common.Scripts.CharacterScripts;
using Assets.PixelFantasy.Common.Scripts.CollectionScripts;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.PixelFantasy.Common.Scripts.EditorScripts
{
    public class CharacterEditor : MonoBehaviour
    {
        public SpriteCollection SpriteCollection;
        public List<LayerEditor> Layers;
        public CharacterBuilderBase CharacterBuilder;
        public int Type;
        public Sprite EmptyIcon;
        public AudioSource AudioSource;
        public AudioClip EquipSound;

        public static event Action<string> SliceTextureRequest = _ => {};
        public static event Action<string> CreateSpriteLibraryRequest = _ => { };

        public void Start()
        {
            foreach (var layer in Layers)
            {
                if (layer.Controls)
                {
                    if (layer.Default != "")
                    {
                        layer.Index = SpriteCollection.Layers.Single(i => i.Name == layer.Name).Textures.FindIndex(i => i.name == layer.Default);
                    }
                    else
                    {
                        layer.Index = -1;
                    }

                    layer.Content = SpriteCollection.Layers.Single(i => i.Name == layer.Name);
                    layer.Controls.Dropdown.options = new List<Dropdown.OptionData>();

                    if (layer.CanBeEmpty) layer.Controls.Dropdown.options.Add(new Dropdown.OptionData("Empty", EmptyIcon));

                    layer.Controls.Dropdown.options.AddRange(layer.Content.Textures.Select(i => new Dropdown.OptionData(GetDisplayName(i.name), layer.Content.GetIcon(SpriteCollection.Type, i))));
                    layer.Controls.Dropdown.value = -1;
                    layer.Controls.Dropdown.value = layer.Index + (layer.CanBeEmpty ? 1 : 0);
                    layer.Controls.Dropdown.onValueChanged.AddListener(value => SetIndex(layer, value));
                    layer.Controls.Prev.onClick.AddListener(() => Switch(layer, -1));
                    layer.Controls.Next.onClick.AddListener(() => Switch(layer, +1));
                    layer.Controls.Hide.onClick.AddListener(() => Hide(layer));
                    layer.Controls.Paint.onClick.AddListener(() => Paint(layer));
                    layer.Controls.Hue.onValueChanged.AddListener(_ => Rebuild());
                    layer.Controls.Saturation.onValueChanged.AddListener(_ => Rebuild());
                    layer.Controls.Brightness.onValueChanged.AddListener(_ => Rebuild());
                    layer.Controls.OnSelectFixedColor = color => Repaint(layer, color);
                    layer.Controls.Lock.onValueChanged.AddListener(value => Lock(layer, value));
                }
            }

            Rebuild();
        }

        private void Rebuild()
        {
            var layers = Layers.ToDictionary(i => i.Name, i => i.SpriteData);

            CharacterBuilder.Body = layers["Body"];
            CharacterBuilder.Head = layers["Head"];
            CharacterBuilder.Ears = layers["Ears"];
            CharacterBuilder.Eyes = layers["Eyes"];
            CharacterBuilder.Mouth = layers["Mouth"];
            CharacterBuilder.Hair = layers["Hair"];
            CharacterBuilder.Armor = layers["Armor"];
            CharacterBuilder.Helmet = layers["Helmet"];
            CharacterBuilder.Weapon = layers["Weapon"];
            CharacterBuilder.Firearm = layers["Firearm"];
            CharacterBuilder.Shield = layers["Shield"];
            CharacterBuilder.Cape = layers["Cape"];
            CharacterBuilder.Back = layers["Back"];
            CharacterBuilder.Mask = layers["Mask"];
            CharacterBuilder.Horns = layers["Horns"];
            CharacterBuilder.Arms = layers.ContainsKey("Arm") ? layers["Arm"] : "";
            CharacterBuilder.Legs = layers.ContainsKey("Leg") ? layers["Leg"] : "";
            CharacterBuilder.Rebuild();

            if (AudioSource && EquipSound)
            {
                AudioSource.PlayOneShot(EquipSound, 0.5f);
            }

            //Debug.Log("Rebuild");
        }

        public void Hide(LayerEditor layer)
        {
            layer.Hide = !layer.Hide;
            Rebuild();
        }

        public void Lock(LayerEditor layer, bool value)
        {
            layer.Lock = value;
            layer.Controls.Dropdown.interactable = !value;
        }

        public void Paint(LayerEditor layer)
        {
            #if UNITY_EDITOR

            ColorPicker.Open(layer.Color);
            ColorPicker.OnColorPicked = color => { layer.Color = color; Rebuild(); };

            #endif
        }

        public void Repaint(LayerEditor layer, Color color)
        {
            layer.Color = color;

            if (layer.Name == "Body")
            {
                Layers.Single(i => i.Name == "Head").Color = color;
                Layers.Single(i => i.Name == "Ears").Color = color;
            }

            if (layer.Name == "Head")
            {
                Layers.Single(i => i.Name == "Ears").Color = color;
            }

            Rebuild();
        }

        private void Switch(LayerEditor layer, int direction)
        {
            layer.Switch(direction);
            Rebuild();
        }

        private LayerEditor _layer;
        private int _index;

        private void SetIndex(LayerEditor layer, int index)
        {
            if (layer == _layer && index == _index) return;

            _layer = layer;
            _index = index;

            var caption = layer.Controls.Dropdown.options[index].text;

            layer.SetIndex(index);

            if (layer.Name == "Body")
            {
                var head = Layers.Single(i => i.Name == "Head");
                var ears = Layers.Single(i => i.Name == "Ears");
                var eyes = Layers.Single(i => i.Name == "Eyes");
                var mouth = Layers.Single(i => i.Name == "Mouth");
                var hair = Layers.Single(i => i.Name == "Hair");
                var horns = Layers.Single(i => i.Name == "Horns");

                head.SetValue(caption);
                ears.SetValue(caption);

                if (Type == 0)
                {
                    eyes.SetValue(caption);
                    horns.SetValue(caption);
                }
                else if (Type == 1)
                {
                    eyes.SetValue("Empty");
                    mouth.SetValue("Empty");
                }
                
                head.Color = layer.Color;
                ears.Color = layer.Color;

                if (!caption.Contains("Human"))
                {
                    hair.Controls.Dropdown.value = 0;
                }
            }
            else if (layer.Name == "Weapon" && index > 0)
            {
                Layers.Single(i => i.Name == "Firearm").Controls.Dropdown.value = 0;
            }
            else if (layer.Name == "Firearm" && index > 0)
            {
                Layers.Single(i => i.Name == "Weapon").Controls.Dropdown.value = 0;
            }

            Rebuild();
        }

        private static string GetDisplayName(string fileName)
        {
            var displayName = Regex.Replace(fileName, @"\[\w+\]", "");

            displayName = Regex.Replace(displayName, "([a-z])([A-Z])", "$1 $2");

            return displayName.Trim();
        }

        public void Reset()
        {
            CharacterBuilder.Reset();

            foreach (var layer in Layers)
            {
                layer.Index = layer.CanBeEmpty ? -1 : 0;
                layer.Color = Color.white;
                layer.Controls.Dropdown.SetValueWithoutNotify(0);
            }

            InitDropdowns();
        }

        public void Preset(string preset)
        {
            _layer = null;

            var body = Layers.Single(i => i.Name == "Body");
            var dropdown = body.Controls.Dropdown;
            var index = dropdown.options.FindIndex(i => i.text == GetDisplayName(preset));

            body.Color = Color.white;

            if (index == -1)
            {
                dropdown.value = 0;
            }
            else if (index == dropdown.value)
            {
                dropdown.onValueChanged.Invoke(index);
            }
            else
            {
                dropdown.value = index;
            }

            Layers.Single(i => i.Name == "Body").Color = Color.white;
            Layers.Single(i => i.Name == "Head").Color = Color.white;
            Layers.Single(i => i.Name == "Ears").Color = Color.white;
            Layers.Single(i => i.Name == "Eyes").Color = Color.white;
            Layers.Single(i => i.Name == "Mouth").Color = Color.white;
            Layers.Single(i => i.Name == "Horns").Color = Color.white;
        }

        public void PresetEx(string preset)
        {
            _layer = null;

            var parts = preset.Split(',');

            foreach (var part in parts)
            {
                var layerName = part.Split('=')[0];
                var value = part.Split('=')[1];

                var layer = Layers.Single(i => i.Name == layerName);
                var dropdown = layer.Controls.Dropdown;
                var index = dropdown.options.FindIndex(i => i.text == GetDisplayName(value));

                //if (index != -1 && layer.CanBeEmpty) index++;

                layer.Color = Color.white;

                if (index == -1)
                {
                    dropdown.value = 0;
                }
                else if (index == dropdown.value)
                {
                    dropdown.onValueChanged.Invoke(index);
                }
                else
                {
                    dropdown.value = index;
                }
            }
        }

        public void Randomize()
        {
            CharacterBuilder.RandomizeEquipment(
                armor: !Layers.Single(i => i.Name == "Armor").Lock,
                helmet: !Layers.Single(i => i.Name == "Helmet").Lock,
                weapon: !Layers.Single(i => i.Name == "Weapon").Lock,
                shield: !Layers.Single(i => i.Name == "Shield").Lock);

            CharacterBuilder.Rebuild();

            InitDropdowns();
        }

        private void InitDropdown(string part, string value)
        {
            var layer = Layers.Single(i => i.Name == part);
            var index = SpriteCollection.Layers.Single(i => i.Name == part).Textures.FindIndex(i => i.name == value);

            layer.Index = index;

            if (layer.CanBeEmpty) index++;

            layer.Controls.Dropdown.SetValueWithoutNotify(index);
        }

        private void InitDropdowns()
        {
            InitDropdown("Head", CharacterBuilder.Head);
            InitDropdown("Ears", CharacterBuilder.Ears);
            InitDropdown("Eyes", CharacterBuilder.Eyes);
            InitDropdown("Body", CharacterBuilder.Body);

            InitDropdown("Armor", CharacterBuilder.Armor);
            InitDropdown("Helmet", CharacterBuilder.Helmet);
            InitDropdown("Weapon", CharacterBuilder.Weapon);
            InitDropdown("Shield", CharacterBuilder.Shield);
            InitDropdown("Back", CharacterBuilder.Back);
        }

        public void Save()
        {
            CharacterBuilder.Rebuild(forceMerge: true);

            var bytes = CharacterBuilder.Texture.EncodeToPNG();

            CharacterBuilder.Rebuild(forceMerge: false);
            SaveFileDialog("Save as PNG", "SpriteSheet", "Image", ".png", bytes);
        }

        private void SaveFileDialog(string title, string fileName, string fileType, string extension, byte[] bytes)
        {
            #if UNITY_EDITOR

            var path = EditorUtility.SaveFilePanel(title, null, fileName + extension, extension.Replace(".", ""));

            if (path == "") return;

            File.WriteAllBytes(path, bytes);

            if (path.StartsWith(Application.dataPath))
            {
                path = "Assets" + path.Substring(Application.dataPath.Length);
                
                AssetDatabase.Refresh();

                var importer = (TextureImporter) AssetImporter.GetAtPath(path);

                importer.textureType = TextureImporterType.Sprite;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();

                AssetDatabase.Refresh();

                SliceTextureRequest(path);

                if (EditorUtility.DisplayDialog("Success", $"Texture saved and sliced:\n{path}\n\nDo you want to create Sprite Library Asset for it?", "Yes", "No"))
                {
                    CreateSpriteLibraryRequest(path);
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Success", $"Texture saved:\n{path}\n\nTip: textures are automatically sliced when saving to Assets.", "OK");
            }

            #elif UNITY_STANDALONE

            #if FILE_BROWSER

            StartCoroutine(SimpleFileBrowserForWindows.WindowsFileBrowser.SaveFile(title, "", fileName, fileType, extension, bytes, (success, p) => { }));
            
            #else

            Debug.LogWarning("Please import this asset: http://u3d.as/2QLg");

            #endif
            
            #elif UNITY_WEBGL

            #if FILE_BROWSER

            SimpleFileBrowserForWebGL.WebFileBrowser.Download(fileName + extension, bytes);

            #else

            Debug.LogWarning("Please import this asset: http://u3d.as/2W52");

            #endif

            #endif
        }
    }
}