using System;
using UnityEngine;
using Mindrift.Core;
using Mindrift.UI;
using Mindrift.World;

namespace Mindrift.Effects
{
    public sealed class SideEffectEventDirector : MonoBehaviour
    {
        [Serializable]
        private sealed class StageEventWeights
        {
            public SideEffectStage stage = SideEffectStage.Stable;
            [Range(0f, 1f)] public float falseWarningWeight = 0.5f;
            [Range(0f, 1f)] public float platformUnrealityWeight = 0.25f;
            [Range(0f, 1f)] public float visualSurgeWeight = 0.25f;
        }

        [Header("References")]
        [SerializeField] private SideEffectUI sideEffectUI;
        [SerializeField] private FakePlatform[] deceptivePlatforms;
        [SerializeField] private CameraSideEffects cameraSideEffects;
        [SerializeField] private PsychedelicVolumeController volumeController;
        [SerializeField] private PsychedelicCustomPassController customPassController;
        [SerializeField] private AudioIntensityDriver audioIntensityDriver;

        [Header("Timing")]
        [SerializeField, Range(0f, 1f)] private float minimumEventProgression = 0.12f;
        [SerializeField] private float maxCooldown = 14f;
        [SerializeField] private float minCooldown = 6f;
        [SerializeField] private AnimationCurve cooldownByIntensity = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Event Weights")]
        [SerializeField] private StageEventWeights[] stageEventWeights =
        {
            new StageEventWeights
            {
                stage = SideEffectStage.Stable,
                falseWarningWeight = 0.75f,
                platformUnrealityWeight = 0.1f,
                visualSurgeWeight = 0.15f
            },
            new StageEventWeights
            {
                stage = SideEffectStage.Elevated,
                falseWarningWeight = 0.6f,
                platformUnrealityWeight = 0.2f,
                visualSurgeWeight = 0.2f
            },
            new StageEventWeights
            {
                stage = SideEffectStage.Distorted,
                falseWarningWeight = 0.45f,
                platformUnrealityWeight = 0.3f,
                visualSurgeWeight = 0.25f
            },
            new StageEventWeights
            {
                stage = SideEffectStage.Overstimulated,
                falseWarningWeight = 0.3f,
                platformUnrealityWeight = 0.35f,
                visualSurgeWeight = 0.35f
            },
            new StageEventWeights
            {
                stage = SideEffectStage.Critical,
                falseWarningWeight = 0.25f,
                platformUnrealityWeight = 0.35f,
                visualSurgeWeight = 0.4f
            }
        };

        [Header("Warning Text")]
        [SerializeField] private string[] deceptiveWarnings =
        {
            "YOU ARE FALLING",
            "WRONG DIRECTION",
            "TURN BACK NOW",
            "NEURAL FAILURE IMMINENT",
            "CHECKPOINT LOST",
            "VERTIGO SPIKE DETECTED"
        };
        [SerializeField] private Vector2 warningDurationRange = new Vector2(0.7f, 1.2f);

        [Header("Platform Event")]
        [SerializeField] private Vector2 platformUnrealDuration = new Vector2(1f, 2.4f);

        [Header("Visual Surge Event")]
        [SerializeField] private Vector2 traumaSurgeRange = new Vector2(0.2f, 0.6f);
        [SerializeField] private Vector2 visualSurgeRange = new Vector2(0.2f, 0.6f);

        private float progression;
        private float nextEventTime;

        public SideEffectStage CurrentStage { get; private set; } = SideEffectStage.Stable;

        private void Start()
        {
            nextEventTime = Time.time + UnityEngine.Random.Range(5f, 9f);
        }

        private void Update()
        {
            if (progression < minimumEventProgression || Time.time < nextEventTime)
            {
                return;
            }

            TriggerRandomEvent();
            ScheduleNextEvent();
        }

        public void SetProgression(float normalizedProgression, SideEffectStage stage)
        {
            progression = Mathf.Clamp01(normalizedProgression);
            CurrentStage = stage;
        }

        private void TriggerRandomEvent()
        {
            StageEventWeights weights = GetWeightsForStage(CurrentStage);

            float warningWeight = Mathf.Max(0f, weights.falseWarningWeight);
            float platformWeight = Mathf.Max(0f, weights.platformUnrealityWeight);
            float surgeWeight = Mathf.Max(0f, weights.visualSurgeWeight);
            float total = warningWeight + platformWeight + surgeWeight;
            if (total <= 0.001f)
            {
                return;
            }

            float roll = UnityEngine.Random.Range(0f, total);
            if (roll < warningWeight)
            {
                TriggerFalseWarning();
                return;
            }

            roll -= warningWeight;
            if (roll < platformWeight)
            {
                if (TriggerPlatformUnreality())
                {
                    return;
                }
            }

            TriggerVisualSurge();
        }

        private void TriggerFalseWarning()
        {
            if (sideEffectUI == null || deceptiveWarnings == null || deceptiveWarnings.Length == 0)
            {
                return;
            }

            int index = UnityEngine.Random.Range(0, deceptiveWarnings.Length);
            float duration = UnityEngine.Random.Range(warningDurationRange.x, warningDurationRange.y);
            sideEffectUI.ShowFalseWarning(deceptiveWarnings[index], duration);
        }

        private bool TriggerPlatformUnreality()
        {
            if (deceptivePlatforms == null || deceptivePlatforms.Length == 0)
            {
                return false;
            }

            FakePlatform platform = deceptivePlatforms[UnityEngine.Random.Range(0, deceptivePlatforms.Length)];
            if (platform == null)
            {
                return false;
            }

            float duration = UnityEngine.Random.Range(platformUnrealDuration.x, platformUnrealDuration.y);
            platform.ForceTemporaryUnreality(duration);
            return true;
        }

        private void TriggerVisualSurge()
        {
            float normalized = Mathf.Clamp01(progression);
            float trauma = Mathf.Lerp(traumaSurgeRange.x, traumaSurgeRange.y, normalized);
            float surge = Mathf.Lerp(visualSurgeRange.x, visualSurgeRange.y, normalized);

            if (cameraSideEffects != null)
            {
                cameraSideEffects.AddTrauma(trauma);
            }

            if (volumeController != null)
            {
                volumeController.TriggerVisualSurge(surge);
            }

            if (customPassController != null)
            {
                customPassController.TriggerVisualSurge(surge);
            }

            if (sideEffectUI != null)
            {
                sideEffectUI.PlaySurgeFlash(surge);
            }

            if (audioIntensityDriver != null)
            {
                float chance = Mathf.Lerp(0.1f, 0.5f, normalized);
                audioIntensityDriver.TryPlayGlitchStinger(chance);
            }
        }

        private void ScheduleNextEvent()
        {
            float intensity = Mathf.Clamp01(cooldownByIntensity.Evaluate(progression));
            float cooldown = Mathf.Lerp(maxCooldown, minCooldown, intensity);
            nextEventTime = Time.time + Mathf.Max(0.1f, cooldown);
        }

        private StageEventWeights GetWeightsForStage(SideEffectStage stage)
        {
            if (stageEventWeights == null || stageEventWeights.Length == 0)
            {
                return new StageEventWeights();
            }

            for (int i = 0; i < stageEventWeights.Length; i++)
            {
                StageEventWeights candidate = stageEventWeights[i];
                if (candidate != null && candidate.stage == stage)
                {
                    return candidate;
                }
            }

            return stageEventWeights[0];
        }
    }
}
