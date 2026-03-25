using UnityEngine;
using Mindrift.Core;

namespace Mindrift.Effects
{
    public sealed class CameraSideEffects : MonoBehaviour
    {
        [Header("Roll + Sway")]
        [SerializeField] private float maxRollDegrees = 6f;
        [SerializeField] private float rollFrequency = 0.75f;
        [SerializeField] private Vector2 swayAmplitude = new Vector2(0.3f, 0.45f);
        [SerializeField] private float swayFrequency = 1.8f;
        [SerializeField] private float intensitySmoothing = 4f;

        [Header("Trauma Shake")]
        [SerializeField] private float traumaDecayPerSecond = 1.4f;
        [SerializeField] private float traumaShakeDegrees = 4f;
        [SerializeField] private float traumaShakePosition = 0.03f;
        [SerializeField] private float traumaNoiseFrequency = 32f;

        [Header("Curves")]
        [SerializeField] private AnimationCurve rollCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve swayCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private Quaternion baseLocalRotation;
        private Vector3 baseLocalPosition;
        private float targetIntensity;
        private float smoothedIntensity;
        private float trauma;

        public float CurrentIntensity => smoothedIntensity;
        public SideEffectStage CurrentStage { get; private set; } = SideEffectStage.Stable;

        private void Awake()
        {
            baseLocalRotation = transform.localRotation;
            baseLocalPosition = transform.localPosition;
        }

        private void LateUpdate()
        {
            smoothedIntensity = Mathf.Lerp(smoothedIntensity, targetIntensity, 1f - Mathf.Exp(-intensitySmoothing * Time.deltaTime));

            float rollIntensity = rollCurve.Evaluate(smoothedIntensity);
            float swayIntensity = swayCurve.Evaluate(smoothedIntensity);

            float roll = Mathf.Sin(Time.time * rollFrequency * Mathf.PI * 2f) * maxRollDegrees * rollIntensity;
            float swayPitch = Mathf.Sin(Time.time * swayFrequency * 1.9f) * swayAmplitude.x * swayIntensity;
            float swayYaw = Mathf.Cos(Time.time * swayFrequency * 1.1f) * swayAmplitude.y * swayIntensity;

            trauma = Mathf.Max(0f, trauma - traumaDecayPerSecond * Time.deltaTime);
            float traumaFactor = trauma * trauma;
            float traumaX = (Mathf.PerlinNoise(Time.time * traumaNoiseFrequency, 0.11f) - 0.5f) * 2f;
            float traumaY = (Mathf.PerlinNoise(0.37f, Time.time * traumaNoiseFrequency) - 0.5f) * 2f;
            float traumaZ = (Mathf.PerlinNoise(Time.time * traumaNoiseFrequency, 0.73f) - 0.5f) * 2f;

            Quaternion traumaRotation = Quaternion.Euler(
                traumaX * traumaShakeDegrees * traumaFactor,
                traumaY * traumaShakeDegrees * traumaFactor,
                traumaZ * traumaShakeDegrees * traumaFactor
            );

            Vector3 traumaPositionOffset = new Vector3(traumaX, traumaY, 0f) * (traumaShakePosition * traumaFactor);
            Quaternion proceduralRotation = Quaternion.Euler(swayPitch, swayYaw, roll);

            transform.localRotation = baseLocalRotation * proceduralRotation * traumaRotation;
            transform.localPosition = baseLocalPosition + traumaPositionOffset;
        }

        public void SetProgression(float normalizedIntensity, SideEffectStage stage)
        {
            targetIntensity = Mathf.Clamp01(normalizedIntensity);
            CurrentStage = stage;
        }

        public void AddTrauma(float amount)
        {
            trauma = Mathf.Clamp01(trauma + Mathf.Abs(amount));
        }
    }
}
