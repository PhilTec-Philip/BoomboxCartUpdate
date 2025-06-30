using UnityEngine;

namespace BoomBoxCartMod
{
    public class VisualEffects : MonoBehaviour
    {
        private Light frontLight;
        private Light backLight;
        private float rgbSpeed = 0.5f;
        private bool lightsOn = false;

        private void Start()
        {
            // Front Light
            GameObject front = new GameObject("BoomboxFrontLight");
            front.transform.SetParent(transform);
            front.transform.localPosition = new Vector3(0f, 0f, 1f);
            frontLight = front.AddComponent<Light>();
            frontLight.type = LightType.Point;
            frontLight.range = 6f;
            frontLight.intensity = 2f;
            frontLight.enabled = false;

            // Back Light
            GameObject back = new GameObject("BoomboxBackLight");
            back.transform.SetParent(transform);
            back.transform.localPosition = new Vector3(0f, 0f, -1f);
            backLight = back.AddComponent<Light>();
            backLight.type = LightType.Point;
            backLight.range = 6f;
            backLight.intensity = 2f;
            backLight.enabled = false;
        }

        private void Update()
        {
            if (lightsOn)
            {
                float t = Time.time * rgbSpeed;
                Color rgb = Color.HSVToRGB((t % 1f), 1f, 1f);
                if (frontLight != null) frontLight.color = rgb;
                if (backLight != null) backLight.color = rgb;
            }
        }

        public void SetLights(bool on)
        {
            lightsOn = on;
            if (frontLight != null) frontLight.enabled = on;
            if (backLight != null) backLight.enabled = on;
        }

        public bool AreLightsOn()
        {
            return lightsOn;
        }
    }
}