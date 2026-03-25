using UnityEngine;

namespace Mindrift.World
{
    public sealed class PlatformFlicker : MonoBehaviour
    {
        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField] private float baseFrequency = 6f;
        [SerializeField] private float maxFrequency = 18f;
        [SerializeField] private float minVisibleWindow = 0.25f;
        [SerializeField] private float maxVisibleWindow = 0.7f;

        private float intensity;

        private void Awake()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<Renderer>();
            }
        }

        private void Update()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                return;
            }

            float frequency = Mathf.Lerp(baseFrequency, maxFrequency, intensity);
            float wave = Mathf.Repeat(Time.time * frequency, 1f);
            float visibleWindow = Mathf.Lerp(maxVisibleWindow, minVisibleWindow, intensity);
            bool visible = wave < visibleWindow;

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer renderer = targetRenderers[i];
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }

        public void SetIntensity(float normalizedIntensity)
        {
            intensity = Mathf.Clamp01(normalizedIntensity);
        }
    }
}
