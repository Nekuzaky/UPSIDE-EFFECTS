using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Mindrift.Core;

namespace Mindrift.Effects
{
    public sealed class PsychedelicVolumeController : MonoBehaviour
    {
        [Header("Volume Setup")]
        [SerializeField] private Volume globalVolume;
        [SerializeField] private bool createMissingOverrides = true;
        [SerializeField] private float intensitySmoothing = 3.5f;

        [Header("Global Intensity")]
        [SerializeField] private AnimationCurve globalIntensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Chromatic Aberration")]
        [SerializeField] private Vector2 chromaticRange = new Vector2(0.08f, 0.95f);
        [SerializeField] private AnimationCurve chromaticCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Lens Distortion")]
        [SerializeField] private Vector2 lensDistortionRange = new Vector2(-0.08f, -0.58f);
        [SerializeField] private AnimationCurve lensDistortionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Bloom")]
        [SerializeField] private Vector2 bloomRange = new Vector2(0.08f, 0.95f);
        [SerializeField] private AnimationCurve bloomCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Color")]
        [SerializeField] private Vector2 saturationRange = new Vector2(4f, 38f);
        [SerializeField] private Vector2 contrastRange = new Vector2(0f, 26f);
        [SerializeField] private Vector2 postExposureRange = new Vector2(0f, 0.55f);
        [SerializeField] private float hueJitterAmplitude = 22f;
        [SerializeField] private float hueJitterFrequency = 0.8f;

        [Header("Vignette + Grain")]
        [SerializeField] private Vector2 vignetteRange = new Vector2(0.04f, 0.52f);
        [SerializeField] private Vector2 grainRange = new Vector2(0.03f, 0.62f);
        [SerializeField] private AnimationCurve vignetteCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve grainCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Surge")]
        [SerializeField] private float surgeDecay = 1.3f;
        [SerializeField] private float maxSurgeBoost = 0.35f;

        [Header("Debug")]
        [SerializeField] private bool logWarnings;

        private ChromaticAberration chromaticAberration;
        private LensDistortion lensDistortion;
        private Bloom bloom;
        private ColorAdjustments colorAdjustments;
        private Vignette vignette;
        private FilmGrain filmGrain;

        private float targetProgression;
        private float smoothedProgression;
        private float surge;

        public float CurrentProgression => smoothedProgression;
        public SideEffectStage CurrentStage { get; private set; } = SideEffectStage.Stable;

        private void Awake()
        {
            if (globalVolume == null)
            {
                globalVolume = GetComponent<Volume>();
            }

            CacheOverrides();
        }

        private void Update()
        {
            if (globalVolume == null || globalVolume.profile == null)
            {
                return;
            }

            smoothedProgression = Mathf.Lerp(smoothedProgression, targetProgression, 1f - Mathf.Exp(-intensitySmoothing * Time.deltaTime));
            surge = Mathf.Max(0f, surge - surgeDecay * Time.deltaTime);

            float curveProgress = Mathf.Clamp01(globalIntensityCurve.Evaluate(smoothedProgression));
            float finalProgress = Mathf.Clamp01(curveProgress + Mathf.Min(maxSurgeBoost, surge));
            ApplyToOverrides(finalProgress);
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

        [ContextMenu("Recache Overrides")]
        public void CacheOverrides()
        {
            if (globalVolume == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning("[MINDRIFT] PsychedelicVolumeController has no Volume reference.");
                }

                return;
            }

            if (globalVolume.profile == null)
            {
                globalVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            }

            VolumeProfile profile = globalVolume.profile;
            chromaticAberration = GetOrCreate(profile, chromaticAberration, createMissingOverrides);
            lensDistortion = GetOrCreate(profile, lensDistortion, createMissingOverrides);
            bloom = GetOrCreate(profile, bloom, createMissingOverrides);
            colorAdjustments = GetOrCreate(profile, colorAdjustments, createMissingOverrides);
            vignette = GetOrCreate(profile, vignette, createMissingOverrides);
            filmGrain = GetOrCreate(profile, filmGrain, createMissingOverrides);

            if (filmGrain != null)
            {
                filmGrain.type.value = FilmGrainLookup.Medium3;
            }
        }

        private void ApplyToOverrides(float progression)
        {
            if (chromaticAberration != null)
            {
                chromaticAberration.active = true;
                chromaticAberration.intensity.overrideState = true;
                chromaticAberration.intensity.value = LerpRange(chromaticRange, chromaticCurve.Evaluate(progression));
            }

            if (lensDistortion != null)
            {
                lensDistortion.active = true;
                lensDistortion.intensity.overrideState = true;
                lensDistortion.xMultiplier.overrideState = true;
                lensDistortion.yMultiplier.overrideState = true;
                lensDistortion.scale.overrideState = true;

                lensDistortion.intensity.value = LerpRange(lensDistortionRange, lensDistortionCurve.Evaluate(progression));
                lensDistortion.xMultiplier.value = 1f;
                lensDistortion.yMultiplier.value = 1f;
                lensDistortion.scale.value = Mathf.Lerp(1f, 1.16f, progression);
            }

            if (bloom != null)
            {
                bloom.active = true;
                bloom.intensity.overrideState = true;
                bloom.scatter.overrideState = true;
                bloom.threshold.overrideState = true;

                bloom.intensity.value = LerpRange(bloomRange, bloomCurve.Evaluate(progression));
                bloom.scatter.value = Mathf.Lerp(0.58f, 0.94f, progression);
                bloom.threshold.value = Mathf.Lerp(1.02f, 0.82f, progression);
            }

            if (colorAdjustments != null)
            {
                float hue = Mathf.Sin(Time.time * hueJitterFrequency) * hueJitterAmplitude * progression;

                colorAdjustments.active = true;
                colorAdjustments.saturation.overrideState = true;
                colorAdjustments.contrast.overrideState = true;
                colorAdjustments.postExposure.overrideState = true;
                colorAdjustments.hueShift.overrideState = true;

                colorAdjustments.saturation.value = Mathf.Lerp(saturationRange.x, saturationRange.y, progression);
                colorAdjustments.contrast.value = Mathf.Lerp(contrastRange.x, contrastRange.y, progression);
                colorAdjustments.postExposure.value = Mathf.Lerp(postExposureRange.x, postExposureRange.y, progression);
                colorAdjustments.hueShift.value = hue;
            }

            if (vignette != null)
            {
                vignette.active = true;
                vignette.intensity.overrideState = true;
                vignette.smoothness.overrideState = true;
                vignette.rounded.overrideState = true;
                vignette.rounded.value = true;

                vignette.intensity.value = LerpRange(vignetteRange, vignetteCurve.Evaluate(progression));
                vignette.smoothness.value = Mathf.Lerp(0.22f, 0.68f, progression);
            }

            if (filmGrain == null)
            {
                return;
            }

            filmGrain.active = true;
            filmGrain.intensity.overrideState = true;
            filmGrain.response.overrideState = true;
            filmGrain.intensity.value = LerpRange(grainRange, grainCurve.Evaluate(progression));
            filmGrain.response.value = Mathf.Lerp(0.78f, 0.42f, progression);
        }

        private static float LerpRange(Vector2 range, float t)
        {
            return Mathf.Lerp(range.x, range.y, Mathf.Clamp01(t));
        }

        private static T GetOrCreate<T>(VolumeProfile profile, T current, bool createMissing) where T : VolumeComponent
        {
            if (profile.TryGet(out T component))
            {
                return component;
            }

            if (!createMissing)
            {
                return current;
            }

            return profile.Add<T>(true);
        }
    }
}
