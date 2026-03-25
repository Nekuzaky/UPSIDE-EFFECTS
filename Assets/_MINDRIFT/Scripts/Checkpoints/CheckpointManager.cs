using System;
using System.Collections.Generic;
using UnityEngine;
using Mindrift.Player;
using Mindrift.UI;

namespace Mindrift.Checkpoints
{
    public sealed class CheckpointManager : MonoBehaviour
    {
        [Header("Checkpoint Setup")]
        [SerializeField] private bool autoCollectCheckpointsFromChildren = true;
        [SerializeField] private List<Checkpoint> checkpoints = new List<Checkpoint>();
        [SerializeField] private int defaultCheckpointIndex = 0;

        [Header("Runtime References")]
        [SerializeField] private PlayerFallRespawn playerFallRespawn;
        [SerializeField] private CheckpointUI checkpointUI;

        [Header("Debug")]
        [SerializeField] private bool logCheckpointChanges;

        private Checkpoint activeCheckpoint;

        public event Action<Checkpoint> CheckpointActivated;

        public int CurrentCheckpointIndex => activeCheckpoint != null ? activeCheckpoint.CheckpointIndex : -1;

        private void Awake()
        {
            if (autoCollectCheckpointsFromChildren)
            {
                RebuildCheckpointList();
            }

            SortCheckpointList();

            if (playerFallRespawn == null)
            {
                playerFallRespawn = FindFirstObjectByType<PlayerFallRespawn>();
            }

            if (playerFallRespawn != null)
            {
                playerFallRespawn.SetCheckpointManager(this);
            }

            if (checkpoints.Count > 0)
            {
                int clamped = Mathf.Clamp(defaultCheckpointIndex, 0, checkpoints.Count - 1);
                activeCheckpoint = checkpoints[clamped];
            }
        }

        [ContextMenu("Rebuild Checkpoint List")]
        public void RebuildCheckpointList()
        {
            checkpoints.Clear();
            GetComponentsInChildren(checkpoints);
            checkpoints.RemoveAll(c => c == null);
            SortCheckpointList();
        }

        public void ActivateCheckpoint(Checkpoint checkpoint)
        {
            if (checkpoint == null)
            {
                return;
            }

            if (activeCheckpoint == checkpoint)
            {
                return;
            }

            SetActiveCheckpoint(checkpoint, true);
        }

        public Transform GetCurrentRespawnPoint()
        {
            if (activeCheckpoint == null && checkpoints.Count > 0)
            {
                activeCheckpoint = checkpoints[0];
            }

            return activeCheckpoint != null ? activeCheckpoint.RespawnAnchor : null;
        }

        public Checkpoint GetActiveCheckpoint()
        {
            return activeCheckpoint;
        }

        public void ActivateDefaultCheckpoint(bool notifyUI = true)
        {
            if (checkpoints.Count == 0)
            {
                return;
            }

            int clamped = Mathf.Clamp(defaultCheckpointIndex, 0, checkpoints.Count - 1);
            Checkpoint checkpoint = checkpoints[clamped];
            if (checkpoint == null)
            {
                return;
            }

            bool shouldNotify = notifyUI || activeCheckpoint != checkpoint;
            SetActiveCheckpoint(checkpoint, shouldNotify);
        }

        private void SortCheckpointList()
        {
            checkpoints.Sort((a, b) =>
            {
                if (a == null && b == null)
                {
                    return 0;
                }

                if (a == null)
                {
                    return 1;
                }

                if (b == null)
                {
                    return -1;
                }

                return a.CheckpointIndex.CompareTo(b.CheckpointIndex);
            });
        }

        private void SetActiveCheckpoint(Checkpoint checkpoint, bool notify)
        {
            if (checkpoint == null)
            {
                return;
            }

            activeCheckpoint = checkpoint;

            if (!notify)
            {
                return;
            }

            CheckpointActivated?.Invoke(checkpoint);

            if (checkpointUI != null)
            {
                checkpointUI.ShowCheckpoint(checkpoint.CheckpointLabel);
            }

            if (logCheckpointChanges)
            {
                Debug.Log($"[MINDRIFT] Active checkpoint: {checkpoint.CheckpointLabel} ({checkpoint.CheckpointIndex}).");
            }
        }
    }
}
