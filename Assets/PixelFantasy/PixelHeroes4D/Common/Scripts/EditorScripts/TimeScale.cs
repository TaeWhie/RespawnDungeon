using UnityEngine;

namespace Assets.PixelFantasy.PixelHeroes4D.Common.Scripts.EditorScripts
{
    public class TimeScale : MonoBehaviour
    {
        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) && Input.GetKey(KeyCode.LeftShift))
            {
                Time.timeScale = 0.25f;
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) && Input.GetKey(KeyCode.LeftShift))
            {
                Time.timeScale = 0.5f;
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3) && Input.GetKey(KeyCode.LeftShift))
            {
                Time.timeScale = 0.75f;
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4) && Input.GetKey(KeyCode.LeftShift))
            {
                Time.timeScale = 1f;
            }
            else if (Input.GetKeyDown(KeyCode.Alpha5) && Input.GetKey(KeyCode.LeftShift))
            {
                Time.timeScale = 1.25f;
            }
        }
    }
}