using System;
using UnityEngine;
using Mindrift.Checkpoints;
using Mindrift.Effects;
using Mindrift.UI;

namespace Mindrift.Player
{
    public sealed class PlayerFallRespawn : MonoBehaviour
    {
        [Header("Fall Punishment")]
        [SerializeField] private float killHeight = -20f;
        [SerializeField] private Vector3 respawnPositionOffset = new Vector3(0f, 0.1f, 0f);

        [Header("References")]
        [SerializeField] private FirstPersonMotor firstPersonMotor;
        [SerializeField] private CheckpointManager checkpointManager;
        [SerializeField] private SideEffectUI sideEffectUI;
        [SerializeField] private CameraSideEffects cameraSideEffects;

        [Header("Debug")]
        [SerializeField] private bool logRespawns;

        public event Action Respawned;

        public float KillHeight => killHeight;

        private bool isRespawning;

        private void Awake()
        {
            if (firstPersonMotor == null)
            {
                firstPersonMotor = GetComponent<FirstPersonMotor>();
            }
        }

        private void Update()
        {
            if (isRespawning)
            {
                return;
            }

            if (transform.position.y < killHeight)
            {
                RespawnAtCheckpoint();
            }
        }

        [ContextMenu("Respawn At Last Checkpoint")]
        public void RespawnAtCheckpoint()
        {
            if (checkpointManager == null)
            {
                checkpointManager = FindFirstObjectByType<CheckpointManager>();
            }

            Transform spawnTransform = checkpointManager != null
                ? checkpointManager.GetCurrentRespawnPoint()
                : null;

            if (spawnTransform == null)
            {
                return;
            }

            isRespawning = true;

            Vector3 targetPosition = spawnTransform.position + respawnPositionOffset;
            Quaternion targetRotation = Quaternion.Euler(0f, spawnTransform.eulerAngles.y, 0f);

            if (firstPersonMotor != null)
            {
                firstPersonMotor.TeleportTo(targetPosition);
            }
            else
            {
                transform.position = targetPosition;
            }

            transform.rotation = targetRotation;

            if (cameraSideEffects != null)
            {
                cameraSideEffects.AddTrauma(0.4f);
            }

            if (sideEffectUI != null)
            {
                sideEffectUI.PlayRespawnFlash();
            }

            Respawned?.Invoke();

            if (logRespawns)
            {
                Debug.Log($"[MINDRIFT] Respawned at checkpoint {spawnTransform.name}.");
            }

            isRespawning = false;
        }

        public void SetCheckpointManager(CheckpointManager manager)
        {
            checkpointManager = manager;
        }
    }
}
