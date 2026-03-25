using UnityEngine;
using Mindrift.Core;

namespace Mindrift.Effects
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioIntensityDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AudioSource masterLoopSource;
        [SerializeField] private AudioLowPassFilter lowPassFilter;

        [Header("Loop Response")]
        [SerializeField] private float baseVolume = 0.35f;
        [SerializeField] private float maxVolume = 1f;
        [SerializeField] private AnimationCurve volumeCurve = AnimationCurve.EaseInOut(0f, 0.1f, 1f, 1f);
        [SerializeField] private float basePitch = 1f;
        [SerializeField] private float maxPitch = 1.15f;
        [SerializeField] private AnimationCurve pitchCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Low Pass")]
        [SerializeField] private bool driveLowPass = false;
        [SerializeField] private float lowPassMax = 22000f;
        [SerializeField] private float lowPassMin = 1800f;
        [SerializeField] private AnimationCurve lowPassCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Optional Stingers")]
        [SerializeField] private AudioClip[] glitchStingers;
        [SerializeField] private float stingerVolume = 0.65f;
        [SerializeField] private float minStingerCooldown = 5f;

        private float intensity;
        private float nextStingerTime;
        private float menuMusicVolumeScale = 1f;
        private float menuSfxVolumeScale = 1f;

        public float Intensity => intensity;
        public SideEffectStage CurrentStage { get; private set; } = SideEffectStage.Stable;

        private void Awake()
        {
            if (masterLoopSource == null)
            {
                masterLoopSource = GetComponent<AudioSource>();
            }

            if (lowPassFilter == null)
            {
                lowPassFilter = GetComponent<AudioLowPassFilter>();
            }
        }

        private void Update()
        {
            if (masterLoopSource != null)
            {
                float volumeT = Mathf.Clamp01(volumeCurve.Evaluate(intensity));
                float pitchT = Mathf.Clamp01(pitchCurve.Evaluate(intensity));
                masterLoopSource.volume = Mathf.Lerp(baseVolume, maxVolume, volumeT) * menuMusicVolumeScale;
                masterLoopSource.pitch = Mathf.Lerp(basePitch, maxPitch, pitchT);
            }

            if (!driveLowPass || lowPassFilter == null)
            {
                return;
            }

            float lowPassT = Mathf.Clamp01(lowPassCurve.Evaluate(intensity));
            lowPassFilter.cutoffFrequency = Mathf.Lerp(lowPassMax, lowPassMin, lowPassT);
        }

        public void SetIntensity(float normalizedIntensity, SideEffectStage stage)
        {
            intensity = Mathf.Clamp01(normalizedIntensity);
            CurrentStage = stage;
        }

        public void TryPlayGlitchStinger(float chance = 1f)
        {
            if (glitchStingers == null || glitchStingers.Length == 0 || masterLoopSource == null)
            {
                return;
            }

            if (Time.time < nextStingerTime || Random.value > Mathf.Clamp01(chance))
            {
                return;
            }

            int clipIndex = Random.Range(0, glitchStingers.Length);
            AudioClip clip = glitchStingers[clipIndex];
            if (clip == null)
            {
                return;
            }

            masterLoopSource.PlayOneShot(clip, stingerVolume * menuSfxVolumeScale);
            nextStingerTime = Time.time + minStingerCooldown;
        }

        public void ApplyMenuVolumeSettings(float musicVolume, float sfxVolume)
        {
            menuMusicVolumeScale = Mathf.Clamp01(musicVolume);
            menuSfxVolumeScale = Mathf.Clamp01(sfxVolume);
        }
    }
}
