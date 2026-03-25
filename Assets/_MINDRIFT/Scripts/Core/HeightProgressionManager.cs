using System;
using UnityEngine;
using Mindrift.Effects;
using Mindrift.UI;
using Mindrift.World;

namespace Mindrift.Core
{
    public sealed class HeightProgressionManager : MonoBehaviour
    {
        [Header("Height Source")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private float minHeight = 0f;
        [SerializeField] private float maxHeight = 140f;
        [SerializeField] private AnimationCurve progressionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Stage Thresholds")]
        [SerializeField, Range(0f, 1f)] private float elevatedThreshold = 0.2f;
        [SerializeField, Range(0f, 1f)] private float distortedThreshold = 0.45f;
        [SerializeField, Range(0f, 1f)] private float overstimulatedThreshold = 0.75f;
        [SerializeField, Range(0f, 1f)] private float criticalThreshold = 0.92f;
        [SerializeField] private bool useCriticalStage = true;

        [Header("Runtime Targets")]
        [SerializeField] private PsychedelicVolumeController volumeController;
        [SerializeField] private PsychedelicCustomPassController customPassController;
        [SerializeField] private CameraSideEffects cameraSideEffects;
        [SerializeField] private AudioIntensityDriver audioIntensityDriver;
        [SerializeField] private SideEffectEventDirector sideEffectEventDirector;
        [SerializeField] private SideEffectUI sideEffectUI;
        [SerializeField] private FakePlatform[] fakePlatforms;

        [Header("Debug")]
        [SerializeField] private bool forceStageOverride;
        [SerializeField] private SideEffectStage forcedStage = SideEffectStage.Distorted;
        [SerializeField] private bool logStageChanges;

        public float CurrentHeight { get; private set; }
        public float RawProgression { get; private set; }
        public float Progression01 { get; private set; }
        public SideEffectStage CurrentStage { get; private set; } = SideEffectStage.Stable;

        public event Action<float, SideEffectStage> ProgressionUpdated;
        public event Action<SideEffectStage> StageChanged;

        private const float MinHeightRange = 1f;

        public float GetProgression()
        {
            return Progression01;
        }

        public SideEffectStage GetCurrentStage()
        {
            return CurrentStage;
        }

        private void Awake()
        {
            if (playerTransform == null)
            {
                Player.PlayerFallRespawn respawn = FindFirstObjectByType<Player.PlayerFallRespawn>();
                if (respawn != null)
                {
                    playerTransform = respawn.transform;
                }
            }
        }

        private void Update()
        {
            if (playerTransform == null)
            {
                return;
            }

            CurrentHeight = playerTransform.position.y;

            float range = Mathf.Max(MinHeightRange, maxHeight - minHeight);
            RawProgression = Mathf.Clamp01((CurrentHeight - minHeight) / range);
            Progression01 = Mathf.Clamp01(progressionCurve.Evaluate(RawProgression));

            SideEffectStage stage = forceStageOverride ? forcedStage : ResolveStage(Progression01);
            bool stageChanged = stage != CurrentStage;
            CurrentStage = stage;

            PushToTargets(Progression01, CurrentStage);

            if (stageChanged)
            {
                StageChanged?.Invoke(CurrentStage);
                if (logStageChanges)
                {
                    Debug.Log($"[MINDRIFT] Stage changed to {CurrentStage} at progression {Progression01:0.00}.");
                }
            }

            ProgressionUpdated?.Invoke(Progression01, CurrentStage);
        }

        private SideEffectStage ResolveStage(float progression)
        {
            if (useCriticalStage && progression >= criticalThreshold)
            {
                return SideEffectStage.Critical;
            }

            if (progression >= overstimulatedThreshold)
            {
                return SideEffectStage.Overstimulated;
            }

            if (progression >= distortedThreshold)
            {
                return SideEffectStage.Distorted;
            }

            if (progression >= elevatedThreshold)
            {
                return SideEffectStage.Elevated;
            }

            return SideEffectStage.Stable;
        }

        private void PushToTargets(float progression, SideEffectStage stage)
        {
            if (volumeController != null)
            {
                volumeController.ApplyProgression(progression, stage);
            }

            if (customPassController != null)
            {
                customPassController.ApplyProgression(progression, stage);
            }

            if (cameraSideEffects != null)
            {
                cameraSideEffects.SetProgression(progression, stage);
            }

            if (audioIntensityDriver != null)
            {
                audioIntensityDriver.SetIntensity(progression, stage);
            }

            if (sideEffectEventDirector != null)
            {
                sideEffectEventDirector.SetProgression(progression, stage);
            }

            if (sideEffectUI != null)
            {
                sideEffectUI.SetStage(stage, progression);
            }

            if (fakePlatforms == null)
            {
                return;
            }

            for (int i = 0; i < fakePlatforms.Length; i++)
            {
                FakePlatform platform = fakePlatforms[i];
                if (platform != null)
                {
                    platform.SetGlobalIntensity(progression);
                }
            }
        }
    }
}
