using System;
using UnityEngine;
using Mindrift.Checkpoints;
using Mindrift.Player;
using Mindrift.World;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Mindrift.Core
{
    public sealed class RunSessionManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerFallRespawn playerFallRespawn;
        [SerializeField] private CheckpointManager checkpointManager;
        [SerializeField] private GoalZone goalZone;

        [Header("Lifecycle")]
        [SerializeField] private bool autoStartOnPlay = true;
        [SerializeField] private bool allowRestartWithKey = true;
        [SerializeField] private KeyCode restartKey = KeyCode.R;

        [Header("Debug")]
        [SerializeField] private bool logRunEvents;

        public bool IsRunning { get; private set; }
        public bool IsCompleted { get; private set; }
        public float ElapsedTime { get; private set; }
        public int FallCount { get; private set; }
        public int CheckpointCount { get; private set; }

        public event Action<float> TimeUpdated;
        public event Action RunStarted;
        public event Action<RunResult> RunCompleted;

        [Serializable]
        public readonly struct RunResult
        {
            public readonly float TimeSeconds;
            public readonly int Falls;
            public readonly int Checkpoints;

            public RunResult(float timeSeconds, int falls, int checkpoints)
            {
                TimeSeconds = timeSeconds;
                Falls = falls;
                Checkpoints = checkpoints;
            }
        }

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

            if (goalZone == null)
            {
                goalZone = FindFirstObjectByType<GoalZone>();
            }
        }

        private void OnEnable()
        {
            Subscribe(true);
        }

        private void OnDisable()
        {
            Subscribe(false);
        }

        private void Start()
        {
            if (autoStartOnPlay)
            {
                StartRun();
            }
        }

        private void Update()
        {
            if (allowRestartWithKey && IsRestartPressed())
            {
                StartRun();
                return;
            }

            if (!IsRunning)
            {
                return;
            }

            ElapsedTime += Time.deltaTime;
            TimeUpdated?.Invoke(ElapsedTime);
        }

        private bool IsRestartPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && TryMapKeyCode(restartKey, out Key mappedKey))
            {
                var keyControl = Keyboard.current[mappedKey];
                if (keyControl != null && keyControl.wasPressedThisFrame)
                {
                    return true;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(restartKey);
#else
            return false;
#endif
        }

        private static bool TryMapKeyCode(KeyCode keyCode, out Key key)
        {
            switch (keyCode)
            {
                case KeyCode.R:
                    key = Key.R;
                    return true;
                case KeyCode.Space:
                    key = Key.Space;
                    return true;
                case KeyCode.Escape:
                    key = Key.Escape;
                    return true;
                case KeyCode.Tab:
                    key = Key.Tab;
                    return true;
                default:
                    key = Key.None;
                    return false;
            }
        }

        public void StartRun()
        {
            IsRunning = true;
            IsCompleted = false;
            ElapsedTime = 0f;
            FallCount = 0;
            CheckpointCount = 0;

            if (logRunEvents)
            {
                Debug.Log("[MINDRIFT] Run started.");
            }

            RunStarted?.Invoke();
            TimeUpdated?.Invoke(ElapsedTime);
        }

        public void CompleteRun()
        {
            if (!IsRunning || IsCompleted)
            {
                return;
            }

            IsRunning = false;
            IsCompleted = true;

            RunResult result = new RunResult(ElapsedTime, FallCount, CheckpointCount);
            RunCompleted?.Invoke(result);

            if (logRunEvents)
            {
                Debug.Log($"[MINDRIFT] Run completed in {FormatTime(ElapsedTime)} | Falls: {FallCount} | Checkpoints: {CheckpointCount}");
            }
        }

        public static string FormatTime(float timeSeconds)
        {
            int totalMilliseconds = Mathf.Max(0, Mathf.RoundToInt(timeSeconds * 1000f));
            int minutes = totalMilliseconds / 60000;
            int seconds = (totalMilliseconds / 1000) % 60;
            int millis = totalMilliseconds % 1000;
            return $"{minutes:00}:{seconds:00}.{millis:000}";
        }

        private void HandleRespawned()
        {
            if (!IsRunning)
            {
                return;
            }

            FallCount++;
        }

        private void HandleCheckpointActivated(Checkpoint _)
        {
            if (!IsRunning)
            {
                return;
            }

            CheckpointCount++;
        }

        private void HandleGoalReached()
        {
            CompleteRun();
        }

        private void Subscribe(bool subscribe)
        {
            if (playerFallRespawn != null)
            {
                if (subscribe)
                {
                    playerFallRespawn.Respawned += HandleRespawned;
                }
                else
                {
                    playerFallRespawn.Respawned -= HandleRespawned;
                }
            }

            if (checkpointManager != null)
            {
                if (subscribe)
                {
                    checkpointManager.CheckpointActivated += HandleCheckpointActivated;
                }
                else
                {
                    checkpointManager.CheckpointActivated -= HandleCheckpointActivated;
                }
            }

            if (goalZone != null)
            {
                if (subscribe)
                {
                    goalZone.GoalReached += HandleGoalReached;
                }
                else
                {
                    goalZone.GoalReached -= HandleGoalReached;
                }
            }
        }
    }
}
