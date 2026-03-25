using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Mindrift.Core;

namespace Mindrift.UI
{
    public sealed class SideEffectUI : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private Component sideEffectsTextComponent;
        [SerializeField] private Component warningTextComponent;
        [SerializeField] private Component statusTextComponent;

        [Header("Flash")]
        [SerializeField] private Image warningFlashImage;
        [SerializeField] private AnimationCurve flashCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField] private float respawnFlashDuration = 0.28f;

        [Header("Styling")]
        [SerializeField] private string sideEffectsPrefix = "SIDE EFFECTS";
        [SerializeField] private string neuralLoadPrefix = "NEURAL LOAD";

        private Coroutine warningRoutine;
        private Coroutine flashRoutine;

        private void Awake()
        {
            if (warningFlashImage != null)
            {
                Color color = warningFlashImage.color;
                color.a = 0f;
                warningFlashImage.color = color;
            }

            if (warningTextComponent != null)
            {
                UITextUtility.SetText(warningTextComponent, string.Empty);
            }
        }

        public void SetStage(SideEffectStage stage, float progression)
        {
            if (sideEffectsTextComponent != null)
            {
                UITextUtility.SetText(sideEffectsTextComponent, $"{sideEffectsPrefix}: {StageToText(stage)}");
            }

            if (statusTextComponent != null)
            {
                int load = Mathf.RoundToInt(progression * 100f);
                UITextUtility.SetText(statusTextComponent, $"{neuralLoadPrefix}: {load:000}%");
            }
        }

        public void ShowFalseWarning(string warningMessage, float duration = 1.15f)
        {
            if (warningRoutine != null)
            {
                StopCoroutine(warningRoutine);
            }

            warningRoutine = StartCoroutine(WarningRoutine(warningMessage, duration));
        }

        public void PlayRespawnFlash()
        {
            PlayFlash(new Color(1f, 0.08f, 0.18f, 0.42f), respawnFlashDuration);
        }

        public void PlaySurgeFlash(float intensity)
        {
            float alpha = Mathf.Lerp(0.12f, 0.45f, Mathf.Clamp01(intensity));
            float duration = Mathf.Lerp(0.18f, 0.42f, Mathf.Clamp01(intensity));
            PlayFlash(new Color(0.12f, 0.95f, 0.95f, alpha), duration);
        }

        private void PlayFlash(Color color, float duration)
        {
            if (warningFlashImage == null)
            {
                return;
            }

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            flashRoutine = StartCoroutine(FlashRoutine(color, duration));
        }

        private IEnumerator WarningRoutine(string warningMessage, float duration)
        {
            if (warningTextComponent == null)
            {
                yield break;
            }

            UITextUtility.SetText(warningTextComponent, warningMessage);
            PlayFlash(new Color(1f, 0.16f, 0.16f, 0.18f), Mathf.Min(0.2f, duration));

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            UITextUtility.SetText(warningTextComponent, string.Empty);
            warningRoutine = null;
        }

        private IEnumerator FlashRoutine(Color flashColor, float duration)
        {
            if (warningFlashImage == null)
            {
                yield break;
            }

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / duration);
                float curveValue = flashCurve.Evaluate(t);

                Color drawColor = flashColor;
                drawColor.a = flashColor.a * curveValue;
                warningFlashImage.color = drawColor;
                yield return null;
            }

            Color off = warningFlashImage.color;
            off.a = 0f;
            warningFlashImage.color = off;
            flashRoutine = null;
        }

        private static string StageToText(SideEffectStage stage)
        {
            return stage switch
            {
                SideEffectStage.Stable => "Stable",
                SideEffectStage.Elevated => "Elevated",
                SideEffectStage.Distorted => "Distorted",
                SideEffectStage.Overstimulated => "Overstimulated",
                SideEffectStage.Critical => "Critical",
                _ => "Unknown"
            };
        }
    }
}
