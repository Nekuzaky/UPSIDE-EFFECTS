using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Mindrift.Core;

namespace Mindrift.Effects
{
    [RequireComponent(typeof(CustomPassVolume))]
    public sealed class PsychedelicCustomPassController : MonoBehaviour
    {
        private static readonly int IntensityId = Shader.PropertyToID("_UE_Intensity");
        private static readonly int WarpId = Shader.PropertyToID("_UE_WarpStrength");
        private static readonly int RgbSplitId = Shader.PropertyToID("_UE_RGBSplit");
        private static readonly int PulseSpeedId = Shader.PropertyToID("_UE_PulseSpeed");
        private static readonly int ScanId = Shader.PropertyToID("_UE_ScanStrength");
        private static readonly int TimeScaleId = Shader.PropertyToID("_UE_TimeScale");

        [Header("Setup")]
        [SerializeField] private CustomPassVolume customPassVolume;
        [SerializeField] private Material fullscreenEffectMaterial;
        [SerializeField] private float intensitySmoothing = 4f;

        [Header("Curves")]
        [SerializeField] private AnimationCurve intensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve warpCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve rgbCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve scanCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Ranges")]
        [SerializeField] private Vector2 warpRange = new Vector2(0.005f, 0.055f);
        [SerializeField] private Vector2 rgbSplitRange = new Vector2(0.0008f, 0.012f);
        [SerializeField] private Vector2 scanRange = new Vector2(0.01f, 0.22f);
        [SerializeField] private Vector2 pulseSpeedRange = new Vector2(0.8f, 4.5f);

        [Header("Surge")]
        [SerializeField] private float surgeDecay = 1.6f;
        [SerializeField] private float surgeBoost = 0.25f;

        private FullScreenCustomPass fullScreenPass;
        private float targetProgression;
        private float smoothedProgression;
        private float surge;

        public float CurrentProgression => smoothedProgression;
        public SideEffectStage CurrentStage { get; private set; } = SideEffectStage.Stable;

        private void Awake()
        {
            if (customPassVolume == null)
            {
                customPassVolume = GetComponent<CustomPassVolume>();
            }

            EnsurePass();
        }

        private void Update()
        {
            if (fullscreenEffectMaterial == null)
            {
                return;
            }

            smoothedProgression = Mathf.Lerp(smoothedProgression, targetProgression, 1f - Mathf.Exp(-intensitySmoothing * Time.deltaTime));
            surge = Mathf.Max(0f, surge - surgeDecay * Time.deltaTime);

            float intensity = Mathf.Clamp01(intensityCurve.Evaluate(smoothedProgression) + surge * surgeBoost);
            float warp = Mathf.Lerp(warpRange.x, warpRange.y, warpCurve.Evaluate(intensity));
            float rgb = Mathf.Lerp(rgbSplitRange.x, rgbSplitRange.y, rgbCurve.Evaluate(intensity));
            float scan = Mathf.Lerp(scanRange.x, scanRange.y, scanCurve.Evaluate(intensity));
            float pulseSpeed = Mathf.Lerp(pulseSpeedRange.x, pulseSpeedRange.y, intensity);

            fullscreenEffectMaterial.SetFloat(IntensityId, intensity);
            fullscreenEffectMaterial.SetFloat(WarpId, warp);
            fullscreenEffectMaterial.SetFloat(RgbSplitId, rgb);
            fullscreenEffectMaterial.SetFloat(ScanId, scan);
            fullscreenEffectMaterial.SetFloat(PulseSpeedId, pulseSpeed);
            fullscreenEffectMaterial.SetFloat(TimeScaleId, Mathf.Lerp(0.75f, 2.1f, intensity));
        }

        public void ApplyProgression(float progression, SideEffectStage stage)
        {
            targetProgression = Mathf.Clamp01(progression);
            CurrentStage = stage;
        }

        public void TriggerVisualSurge(float amount)
        {
            surge = Mathf.Clamp01(surge + Mathf.Abs(amount));
        }

        [ContextMenu("Ensure Fullscreen Pass")]
        public void EnsurePass()
        {
            if (customPassVolume == null)
            {
                return;
            }

            customPassVolume.isGlobal = true;
            customPassVolume.injectionPoint = CustomPassInjectionPoint.BeforePostProcess;

            fullScreenPass = null;
            for (int i = 0; i < customPassVolume.customPasses.Count; i++)
            {
                if (customPassVolume.customPasses[i] is FullScreenCustomPass pass)
                {
                    fullScreenPass = pass;
                    break;
                }
            }

            if (fullScreenPass == null)
            {
                fullScreenPass = customPassVolume.AddPassOfType<FullScreenCustomPass>();
            }

            if (fullScreenPass == null)
            {
                return;
            }

            fullScreenPass.name = "MINDRIFT Fullscreen Psyche";
            fullScreenPass.fullscreenPassMaterial = fullscreenEffectMaterial;
            fullScreenPass.fetchColorBuffer = true;
            fullScreenPass.enabled = true;
        }
    }
}
