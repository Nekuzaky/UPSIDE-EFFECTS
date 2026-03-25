using System;
using UnityEngine;
using Mindrift.Checkpoints;
using Mindrift.Player;

namespace Mindrift.Core
{
    public sealed class LivesSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerFallRespawn playerFallRespawn;
        [SerializeField] private CheckpointManager checkpointManager;
        [SerializeField] private RunSessionManager runSessionManager;

        [Header("Lives")]
        [SerializeField, Min(1)] private int maxLives = 5;
        [SerializeField] private bool resetLivesOnRunStart = true;
        [SerializeField] private bool refillLivesAfterDepletion = true;

        [Header("Depletion Behavior")]
        [SerializeField] private bool returnToDefaultCheckpointOnDepletion = true;
        [SerializeField] private bool notifyCheckpointOnForcedReturn = true;
        [SerializeField] private bool showDeathScreenOnDepletion = true;

        [Header("Game Over")]
        [SerializeField] private bool pauseTimeDuringGameOver = true;
        [SerializeField] private bool disablePlayerControlDuringGameOver = true;
        [SerializeField] private FirstPersonMotor firstPersonMotor;
        [SerializeField] private FirstPersonLook firstPersonLook;

        [Header("Debug")]
        [SerializeField] private bool logEvents;

        private bool suppressNextRespawnConsume;
        private bool gameplayLockedBySystem;
        private float cachedTimeScale = 1f;

        public int MaxLives => Mathf.Max(1, maxLives);
        public int CurrentLives { get; private set; }
        public bool IsGameOver { get; private set; }

        public event Action<int, int> LivesChanged;
        public event Action LivesDepleted;
        public event Action<bool> GameOverStateChanged;

        private void Awake()
        {
            if (playerFallRespawn == null)
            {
                playerFallRespawn = FindFirstObjectByType<PlayerFallRespawn>();
            }

            if (checkpointManager == null)
            {
                checkpointManager = FindFirstObjectByType<CheckpointManager>();
            }

            if (runSessionManager == null)
            {
                runSessionManager = FindFirstObjectByType<RunSessionManager>();
            }

            if (firstPersonMotor == null && playerFallRespawn != null)
            {
                firstPersonMotor = playerFallRespawn.GetComponent<FirstPersonMotor>();
            }

            if (firstPersonLook == null && playerFallRespawn != null)
            {
                firstPersonLook = playerFallRespawn.GetComponent<FirstPersonLook>();
            }

            ResetLives(false);
        }

        private void OnEnable()
        {
            if (playerFallRespawn != null)
            {
                playerFallRespawn.Respawned += HandleRespawned;
            }

            if (runSessionManager != null)
            {
                runSessionManager.RunStarted += HandleRunStarted;
            }
        }

        private void OnDisable()
        {
            if (playerFallRespawn != null)
            {
                playerFallRespawn.Respawned -= HandleRespawned;
            }

            if (runSessionManager != null)
            {
                runSessionManager.RunStarted -= HandleRunStarted;
            }
        }

        [ContextMenu("Reset Lives")]
        public void ResetLives()
        {
            ResetLives(true);
        }

        private void HandleRunStarted()
        {
            if (IsGameOver)
            {
                ClearGameOverState();
            }

            if (!resetLivesOnRunStart)
            {
                return;
            }

            ResetLives(true);
        }

        private void HandleRespawned()
        {
            if (suppressNextRespawnConsume)
            {
                suppressNextRespawnConsume = false;
                return;
            }

            CurrentLives = Mathf.Max(0, CurrentLives - 1);
            LivesChanged?.Invoke(CurrentLives, MaxLives);

            if (logEvents)
            {
                Debug.Log($"[MINDRIFT] Lives: {CurrentLives}/{MaxLives}.");
            }

            if (CurrentLives > 0)
            {
                return;
            }

            LivesDepleted?.Invoke();

            if (logEvents)
            {
                Debug.Log("[MINDRIFT] Lives depleted.");
            }

            if (showDeathScreenOnDepletion)
            {
                EnterGameOverState();
                return;
            }

            ForceReturnToSpawnAndRefill();
        }

        public void ContinueFromGameOver()
        {
            if (!IsGameOver)
            {
                return;
            }

            ForceReturnToSpawnAndRefill();
            ClearGameOverState();
        }

        public void ClearGameOverState()
        {
            if (!IsGameOver)
            {
                return;
            }

            IsGameOver = false;
            ApplyGameplayLock(false);
            GameOverStateChanged?.Invoke(false);
        }

        private void EnterGameOverState()
        {
            if (IsGameOver)
            {
                return;
            }

            IsGameOver = true;
            ApplyGameplayLock(true);
            GameOverStateChanged?.Invoke(true);

            if (logEvents)
            {
                Debug.Log("[MINDRIFT] Game over state enabled.");
            }
        }

        private void ForceReturnToSpawnAndRefill()
        {
            if (returnToDefaultCheckpointOnDepletion && checkpointManager != null)
            {
                checkpointManager.ActivateDefaultCheckpoint(notifyCheckpointOnForcedReturn);
            }

            if (refillLivesAfterDepletion)
            {
                ResetLives(true);
            }

            if (playerFallRespawn != null)
            {
                suppressNextRespawnConsume = true;
                playerFallRespawn.RespawnAtCheckpoint();
            }
        }

        private void ResetLives(bool notify)
        {
            CurrentLives = MaxLives;
            if (notify)
            {
                LivesChanged?.Invoke(CurrentLives, MaxLives);
            }
        }

        private void ApplyGameplayLock(bool lockGameplay)
        {
            if (pauseTimeDuringGameOver)
            {
                if (lockGameplay)
                {
                    cachedTimeScale = Time.timeScale;
                    Time.timeScale = 0f;
                }
                else if (gameplayLockedBySystem)
                {
                    Time.timeScale = cachedTimeScale <= 0f ? 1f : cachedTimeScale;
                }
            }

            if (disablePlayerControlDuringGameOver)
            {
                if (firstPersonMotor == null && playerFallRespawn != null)
                {
                    firstPersonMotor = playerFallRespawn.GetComponent<FirstPersonMotor>();
                }

                if (firstPersonLook == null && playerFallRespawn != null)
                {
                    firstPersonLook = playerFallRespawn.GetComponent<FirstPersonLook>();
                }

                if (firstPersonMotor != null)
                {
                    firstPersonMotor.enabled = !lockGameplay;
                }

                if (firstPersonLook != null)
                {
                    firstPersonLook.enabled = !lockGameplay;
                    if (lockGameplay)
                    {
                        firstPersonLook.SetCursorLock(false);
                    }
                    else
                    {
                        firstPersonLook.SetCursorLock(true);
                    }
                }
            }

            gameplayLockedBySystem = lockGameplay;
        }
    }
}
