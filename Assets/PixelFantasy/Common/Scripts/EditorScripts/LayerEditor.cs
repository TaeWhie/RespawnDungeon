using System;
using Assets.PixelFantasy.Common.Scripts.CollectionScripts;
using Assets.PixelFantasy.Common.Scripts.UI;
using UnityEngine;

namespace Assets.PixelFantasy.Common.Scripts.EditorScripts
{
    [Serializable]
    public class LayerEditor
    {
        public string Name;
        public LayerControls Controls;
        public int Index;
        public bool CanBeEmpty = true;
        public string Default;

        public Layer Content { set; get; }
        public LayerEditor Mask { set; get; }

        [HideInInspector] public bool Hide;
        [HideInInspector] public bool Lock;
        [HideInInspector] public Color Color = Color.white;

        public string SpriteData => Index == -1 || Hide ? "" : $"{Content.Textures[Index].name}#{ColorUtility.ToHtmlStringRGB(Color)}/{Controls.Hue.value}:{Controls.Saturation.value}:{Controls.Brightness.value}";

        public void Switch(int direction)
        {
            if (Lock) return;

            Index += direction;

            var min = CanBeEmpty ? -1 : 0;

            if (Index < min) Index = Content.Textures.Count - 1;
            if (Index == Content.Textures.Count) Index = min;
        }

        public void SetIndex(int index)
        {
            if (Lock) return;

            Index = index != -1 && CanBeEmpty ? index - 1 : index;
            Controls.Dropdown.SetValueWithoutNotify(index == - 1 ? 0 : index);
        }

        public void SetValue(string value)
        {
            var index = Controls.Dropdown.options.FindIndex(i => i.text == value);

            SetIndex(index);
        }

        public Color32[] GetPixels()
        {
            var mask = Mask != null && Mask.Index != -1 ? Mask.Content.Textures[Mask.Index].GetPixels32() : null;

            return Content.GetPixels(Index, Color, Controls.Hue.value, Controls.Saturation.value, Controls.Brightness.value, mask);
        }

        public Color[] GetPixels(int x, int y, int blockWidth, int blockHeight)
        {
            return Content.GetPixels(Index, Color, Controls.Hue.value, Controls.Saturation.value, Controls.Brightness.value, x, y, blockWidth, blockHeight);
        }
    }
}