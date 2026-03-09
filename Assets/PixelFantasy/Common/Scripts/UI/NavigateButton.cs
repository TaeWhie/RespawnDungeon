using UnityEngine;
using UnityEngine.UI;

namespace Assets.PixelFantasy.Common.Scripts.UI
{
    public class NavigateButton : MonoBehaviour
    {
        public string Url;

        public void Start()
        {
            GetComponent<Button>().onClick.AddListener(Navigate);
        }

        public void Navigate()
        {
            Application.OpenURL(Url);
        }
    }
}