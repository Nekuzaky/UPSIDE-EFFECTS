using UnityEngine;
using UnityEngine.UI;
using Mindrift.Core;

namespace Mindrift.UI
{
    public sealed class RunHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RunSessionManager runSessionManager;
        [SerializeField] private LivesSystem livesSystem;
        [SerializeField] private Component timerTextComponent;
        [SerializeField] private Component summaryTextComponent;
        [SerializeField] private CanvasGroup summaryCanvasGroup;

        [Header("Labels")]
        [SerializeField] private string timerPrefix = "RUN";
        [SerializeField] private string livesPrefix = "LIVES";
        [SerializeField] private string completedPrefix = "RUN COMPLETE";
        [SerializeField] private bool includeLivesInTimer = true;

        private void Awake()
        {
            if (runSessionManager == null)
            {
                runSessionManager = FindFirstObjectByType<RunSessionManager>();
            }

            if (livesSystem == null)
            {
                livesSystem = FindFirstObjectByType<LivesSystem>();
            }

            if (summaryCanvasGroup != null)
            {
                summaryCanvasGroup.alpha = 0f;
            }
        }

        private void OnEnable()
        {
            if (runSessionManager == null)
            {
                return;
            }

            runSessionManager.TimeUpdated += HandleTimeUpdated;
            runSessionManager.RunStarted += HandleRunStarted;
            runSessionManager.RunCompleted += HandleRunCompleted;
        }

        private void OnDisable()
        {
            if (runSessionManager == null)
            {
                return;
            }

            runSessionManager.TimeUpdated -= HandleTimeUpdated;
            runSessionManager.RunStarted -= HandleRunStarted;
            runSessionManager.RunCompleted -= HandleRunCompleted;
        }

        private void HandleRunStarted()
        {
            if (summaryCanvasGroup != null)
            {
                summaryCanvasGroup.alpha = 0f;
            }

            if (summaryTextComponent != null)
            {
                UITextUtility.SetText(summaryTextComponent, string.Empty);
            }
        }

        private void HandleTimeUpdated(float elapsed)
        {
            if (timerTextComponent == null)
            {
                return;
            }

            string timerText = $"{timerPrefix}: {RunSessionManager.FormatTime(elapsed)}";
            if (includeLivesInTimer && livesSystem != null)
            {
                timerText += $"  |  {livesPrefix}: {livesSystem.CurrentLives}/{livesSystem.MaxLives}";
            }

            UITextUtility.SetText(timerTextComponent, timerText);
        }

        private void HandleRunCompleted(RunSessionManager.RunResult result)
        {
            if (summaryTextComponent != null)
            {
                string livesLine = string.Empty;
                if (livesSystem != null)
                {
                    livesLine = $"LIVES LEFT: {livesSystem.CurrentLives}/{livesSystem.MaxLives}\n";
                }

                string summary = $"{completedPrefix}\n" +
                                 $"TIME: {RunSessionManager.FormatTime(result.TimeSeconds)}\n" +
                                 $"FALLS: {result.Falls}\n" +
                                 $"CHECKPOINTS: {result.Checkpoints}\n" +
                                 livesLine +
                                 "PRESS R TO RESTART";
                UITextUtility.SetText(summaryTextComponent, summary);
            }

            if (summaryCanvasGroup != null)
            {
                summaryCanvasGroup.alpha = 1f;
            }
        }
    }
}
