using System.Threading.Tasks;
using Mindrift.Auth;
using Mindrift.Core;
using Mindrift.Online.Models;
using UnityEngine;

namespace Mindrift.Online.Core
{
    public sealed class RunSubmissionHook : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RunSessionManager runSessionManager;
        [SerializeField] private HeightProgressionManager heightProgressionManager;

        [Header("Behavior")]
        [SerializeField] private bool submitOnRunCompleted = true;
        [SerializeField] private bool requireAuthenticatedSession = true;
        [SerializeField] private bool logSubmissionResult;

        private void Awake()
        {
            if (runSessionManager == null)
            {
                runSessionManager = FindFirstObjectByType<RunSessionManager>();
            }

            if (heightProgressionManager == null)
            {
                heightProgressionManager = FindFirstObjectByType<HeightProgressionManager>();
            }
        }

        private void OnEnable()
        {
            if (runSessionManager != null)
            {
                runSessionManager.RunCompleted += HandleRunCompleted;
            }
        }

        private void OnDisable()
        {
            if (runSessionManager != null)
            {
                runSessionManager.RunCompleted -= HandleRunCompleted;
            }
        }

        private void HandleRunCompleted(RunSessionManager.RunResult result)
        {
            if (!submitOnRunCompleted)
            {
                return;
            }

            _ = SubmitRunAsync(result);
        }

        public async Task SubmitRunAsync(RunSessionManager.RunResult result)
        {
            if (requireAuthenticatedSession)
            {
                AuthSessionData session = AuthRuntime.Service.CurrentSession;
                if (session == null || session.isGuest)
                {
                    if (logSubmissionResult)
                    {
                        Debug.Log("[MINDRIFT] Run submission skipped (guest session).");
                    }

                    return;
                }
            }

            float maxHeight = heightProgressionManager != null ? heightProgressionManager.CurrentHeight : 0f;
            int estimatedScore = EstimateRunScore(maxHeight, result.Checkpoints, result.Falls);

            MindriftRunSubmission payload = new MindriftRunSubmission
            {
                score = estimatedScore,
                max_height = maxHeight,
                duration_seconds = result.TimeSeconds,
                deaths = result.Falls,
                build_version = Application.version
            };

            ApiRequestResult<bool> submissionResult = await MindriftOnlineService.Instance.SubmitRunAsync(payload);
            if (!logSubmissionResult)
            {
                return;
            }

            if (submissionResult.Success)
            {
                Debug.Log("[MINDRIFT] Run submitted to backend.");
            }
            else
            {
                Debug.LogWarning($"[MINDRIFT] Run submission failed: {submissionResult.ErrorMessage}");
            }
        }

        private static int EstimateRunScore(float maxHeight, int checkpoints, int falls)
        {
            int baseScore = Mathf.RoundToInt(Mathf.Max(0f, maxHeight) * 10f);
            int checkpointBonus = Mathf.Max(0, checkpoints) * 250;
            int fallPenalty = Mathf.Max(0, falls) * 50;
            int completionBonus = 1000;
            return Mathf.Max(0, baseScore + checkpointBonus + completionBonus - fallPenalty);
        }
    }
}
