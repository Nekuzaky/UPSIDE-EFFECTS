using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Mindrift.UI
{
    public sealed class CheckpointUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Component checkpointTextComponent;
        [SerializeField] private CanvasGroup checkpointCanvasGroup;

        [Header("Display")]
        [SerializeField] private string prefix = "CHECKPOINT";
        [SerializeField] private float visibleDuration = 1.35f;
        [SerializeField] private float fadeDuration = 0.4f;

        private Coroutine routine;

        private void Awake()
        {
            if (checkpointCanvasGroup != null)
            {
                checkpointCanvasGroup.alpha = 0f;
            }

            if (checkpointTextComponent != null)
            {
                UITextUtility.SetText(checkpointTextComponent, string.Empty);
            }
        }

        public void ShowCheckpoint(string checkpointLabel)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
            }

            routine = StartCoroutine(ShowRoutine(checkpointLabel));
        }

        private IEnumerator ShowRoutine(string checkpointLabel)
        {
            if (checkpointTextComponent == null || checkpointCanvasGroup == null)
            {
                yield break;
            }

            UITextUtility.SetText(checkpointTextComponent, $"{prefix}: {checkpointLabel}");
            checkpointCanvasGroup.alpha = 1f;

            float timer = 0f;
            while (timer < visibleDuration)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / fadeDuration);
                checkpointCanvasGroup.alpha = 1f - t;
                yield return null;
            }

            checkpointCanvasGroup.alpha = 0f;
            UITextUtility.SetText(checkpointTextComponent, string.Empty);
            routine = null;
        }
    }
}
